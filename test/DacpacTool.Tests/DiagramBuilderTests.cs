using System;
using System.IO;
using System.Text;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MSBuild.Sdk.SqlProj.DacpacTool.Diagram;
using Shouldly;
using MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class DiagramBuilderTests
    {
        private readonly IConsole _console = new TestConsole();

        [TestMethod]
        public void CreatesDiagram()
        {
            // Arrange
            var packageName = "MyPackage";
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();

            var diagramBuilder = new MermaidDiagramBuilder(_console);

            // Act
            var diagram = diagramBuilder.BuildErDiagram(result.model, packageName);
            
            // Assert
            Path.GetDirectoryName(diagram).ShouldBe(Environment.CurrentDirectory);

            testConsole.Lines.Count.ShouldBe(1);

            testConsole.Lines.ShouldContain($"Generating ER diagram MyPackage_erdiagram.md");

            var diagramText = File.ReadAllText(diagram, Encoding.UTF8);

            diagramText.ShouldContain("erDiagram");

            diagramText.ShouldNotContain("sp_GetData");

            diagramText.ShouldContain("  \"dbo.TestTable\" {");
            diagramText.ShouldContain("    Column1 nvarchar(100) PK");
            diagramText.ShouldContain("    Computed computed(NULL)");
        }

        [TestMethod]
        public void CreatesFilteredDiagram()
        {
            var packageName = "MyPackage";
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildRelationalModel();
            var configPath = WriteConfigFile("{\"tables\":[\"dbo.OrderHeader\",\"Customer\"]}");
            var diagramBuilder = new MermaidDiagramBuilder(_console);

            try
            {
                var diagram = diagramBuilder.BuildErDiagram(result.model, packageName, new FileInfo(configPath));
                var diagramText = File.ReadAllText(diagram!, Encoding.UTF8);

                diagramText.ShouldContain("  \"dbo.Customer\" {");
                diagramText.ShouldContain("  \"dbo.OrderHeader\" {");
                diagramText.ShouldContain("  \"dbo.OrderHeader\" }o--|| \"dbo.Customer\" : FK_OrderHeader_Customer");
                diagramText.ShouldNotContain("  \"dbo.AuditLog\" {");
            }
            finally
            {
                File.Delete(configPath);
            }
        }

        [TestMethod]
        public void CreatesEmptyDiagramWhenConfigHasNoTables()
        {
            var packageName = "MyPackage";
            var result = BuildRelationalModel();
            var configPath = WriteConfigFile("{\"tables\":[]}");
            var diagramBuilder = new MermaidDiagramBuilder(_console);

            try
            {
                var diagram = diagramBuilder.BuildErDiagram(result.model, packageName, new FileInfo(configPath));
                var diagramText = File.ReadAllText(diagram!, Encoding.UTF8);

                diagramText.ShouldContain("erDiagram");
                diagramText.ShouldNotContain("dbo.Customer");
                diagramText.ShouldNotContain("dbo.OrderHeader");
                diagramText.ShouldNotContain("dbo.AuditLog");
            }
            finally
            {
                File.Delete(configPath);
            }
        }

        [TestMethod]
        public void CreatesMultipleFilteredDiagrams()
        {
            var packageName = "MyPackage";
            var result = BuildMultiSchemaModel();
            var salesConfigPath = WriteConfigFile("{\"schemas\":[\"sales\"],\"outputFileName\":\"sales.md\"}");
            var hrConfigPath = WriteConfigFile("{\"tables\":[\"dbo.Employee\"],\"outputFileName\":\"hr.md\"}");
            var diagramBuilder = new MermaidDiagramBuilder(_console);

            try
            {
                var diagrams = diagramBuilder.BuildErDiagrams(
                    result.model,
                    packageName,
                    new[] { new FileInfo(salesConfigPath), new FileInfo(hrConfigPath) });

                diagrams.Count.ShouldBe(2);
                Path.GetFileName(diagrams[0]).ShouldBe("sales.md");
                Path.GetFileName(diagrams[1]).ShouldBe("hr.md");

                var salesDiagram = File.ReadAllText(diagrams[0], Encoding.UTF8);
                var hrDiagram = File.ReadAllText(diagrams[1], Encoding.UTF8);

                salesDiagram.ShouldContain("sales.Invoice");
                salesDiagram.ShouldNotContain("dbo.Employee");
                hrDiagram.ShouldContain("dbo.Employee");
                hrDiagram.ShouldNotContain("sales.Invoice");
            }
            finally
            {
                File.Delete(salesConfigPath);
                File.Delete(hrConfigPath);
            }
        }

        [TestMethod]
        public void ThrowsForUnknownConfigProperty()
        {
            var packageName = "MyPackage";
            var result = BuildRelationalModel();
            var configPath = WriteConfigFile("{\"unknown\":true}");
            var diagramBuilder = new MermaidDiagramBuilder(_console);

            try
            {
                var exception = Should.Throw<System.Text.Json.JsonException>(() =>
                    diagramBuilder.BuildErDiagram(result.model, packageName, new FileInfo(configPath)));

                exception.Message.ShouldContain("unsupported property 'unknown'");
            }
            finally
            {
                File.Delete(configPath);
            }
        }

        [TestMethod]
        public void ThrowsForDuplicateSchemaEntries()
        {
            var packageName = "MyPackage";
            var result = BuildRelationalModel();
            var configPath = WriteConfigFile("{\"schemas\":[\"sales\",\"sales\"]}");
            var diagramBuilder = new MermaidDiagramBuilder(_console);

            try
            {
                var exception = Should.Throw<System.Text.Json.JsonException>(() =>
                    diagramBuilder.BuildErDiagram(result.model, packageName, new FileInfo(configPath)));

                exception.Message.ShouldContain("must not contain duplicate values");
            }
            finally
            {
                File.Delete(configPath);
            }
        }

        [TestMethod]
        public void ThrowsForEmptyOutputFileName()
        {
            var packageName = "MyPackage";
            var result = BuildRelationalModel();
            var configPath = WriteConfigFile("{\"outputFileName\":\"\"}");
            var diagramBuilder = new MermaidDiagramBuilder(_console);

            try
            {
                var exception = Should.Throw<System.Text.Json.JsonException>(() =>
                    diagramBuilder.BuildErDiagram(result.model, packageName, new FileInfo(configPath)));

                exception.Message.ShouldContain("must be a non-empty string");
            }
            finally
            {
                File.Delete(configPath);
            }
        }

        private static (FileInfo fileInfo, TSqlModel model) BuildSimpleModel()
        {
            var tmodel = new TestModelBuilder()
                .AddTable("TestTable", ("Column1", "nvarchar(100) PRIMARY KEY"), ("Computed", "AS 2 * 2"))
                .AddStoredProcedure("sp_GetData", "SELECT * FROM dbo.TestTable", "proc1.sql");

            var model = tmodel.Build();
            var packagePath = tmodel.SaveAsPackage();

            return (new FileInfo(packagePath), model);
        }

        private static (FileInfo fileInfo, TSqlModel model) BuildRelationalModel()
        {
            var tmodel = new TestModelBuilder()
                .AddObjects("CREATE TABLE [dbo].[Customer] ([CustomerId] int NOT NULL PRIMARY KEY);")
                .AddObjects("CREATE TABLE [dbo].[OrderHeader] ([OrderHeaderId] int NOT NULL PRIMARY KEY, [CustomerId] int NOT NULL, CONSTRAINT [FK_OrderHeader_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customer]([CustomerId]));")
                .AddObjects("CREATE TABLE [dbo].[AuditLog] ([AuditLogId] int NOT NULL PRIMARY KEY);");

            var model = tmodel.Build();
            var packagePath = tmodel.SaveAsPackage();

            return (new FileInfo(packagePath), model);
        }

        private static (FileInfo fileInfo, TSqlModel model) BuildMultiSchemaModel()
        {
            var tmodel = new TestModelBuilder()
                .AddObjects("CREATE SCHEMA [sales];")
                .AddObjects("CREATE TABLE [sales].[Invoice] ([InvoiceId] int NOT NULL PRIMARY KEY);")
                .AddObjects("CREATE TABLE [dbo].[Employee] ([EmployeeId] int NOT NULL PRIMARY KEY);");

            var model = tmodel.Build();
            var packagePath = tmodel.SaveAsPackage();

            return (new FileInfo(packagePath), model);
        }

        private static string WriteConfigFile(string contents)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
            File.WriteAllText(path, contents, Encoding.UTF8);
            return path;
        }
    }
}
