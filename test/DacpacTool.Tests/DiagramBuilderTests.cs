using System.IO;
using System.Text;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MSBuild.Sdk.SqlProj.DacpacTool.Diagram;
using Shouldly;

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

            var diagramBuilder = new ErDiagramBuilder(_console);

            // Act
            var diagram = diagramBuilder.BuildErDiagram(result.model, packageName, result.fileInfo);
            
            // Assert
            testConsole.Lines.Count.ShouldBe(1);

            testConsole.Lines.ShouldContain($"Generating ER diagram MyPackage_erdiagram.md");

            var diagramText = File.ReadAllText(diagram, Encoding.UTF8);

            diagramText.ShouldContain("erDiagram");

            diagramText.ShouldContain("    Column1 nvarchar(100) PK");
        }

        private static (FileInfo fileInfo, TSqlModel model) BuildSimpleModel()
        {
            var tmodel = new TestModelBuilder()
                .AddTable("TestTable", ("Column1", "nvarchar(100) PRIMARY KEY"))
                .AddStoredProcedure("sp_GetData", "SELECT * FROM dbo.TestTable", "proc1.sql");

            var model = tmodel.Build();
            var packagePath = tmodel.SaveAsPackage();

            return (new FileInfo(packagePath), model);
        }
    }
}
