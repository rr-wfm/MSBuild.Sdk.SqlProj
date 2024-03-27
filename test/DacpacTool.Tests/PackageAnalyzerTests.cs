using System.IO;
using System.Linq;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class PackageAnalyzerTests
    {
        private readonly IConsole _console = new TestConsole();

        [TestMethod]
        public void RunsAnalyzer()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, null);

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo);
            
            // Assert
            testConsole.Lines.Count.ShouldBe(15);

            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.ShouldContain($"proc1.sql(1,47): Warning SRD0006 : SqlServer.Rules : Avoid using SELECT *.");
            testConsole.Lines.ShouldContain($"proc1.sql(1,47): Warning SML005 : Smells : Avoid use of 'Select *'");
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithSupressions()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "-SqlServer.Rules.SRD0006;-Smells.SML005;-SqlServer.Rules.SRD999;+!SqlServer.Rules.SRN0002;");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo);

            // Assert
            testConsole.Lines.Count.ShouldBe(13);

            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.Any(l => l.Contains("SRD0006")).ShouldBeFalse();
            testConsole.Lines.Any(l => l.Contains("SML0005")).ShouldBeFalse();
            testConsole.Lines.Any(l => l.Contains("Error SRN0002")).ShouldBeTrue();
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithWildcardSupressions()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "-SqlServer.Rules.SRD*");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo);

            // Assert
            testConsole.Lines.Count.ShouldBe(13);

            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.Any(l => l.Contains("SRD")).ShouldBeFalse();
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithWarningsAsErrors()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "+!SqlServer.Rules.SRD0006");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo);

            // Assert
            testConsole.Lines.Count.ShouldBe(15);

            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.ShouldContain($"proc1.sql(1,47): Error SRD0006 : SqlServer.Rules : Avoid using SELECT *.");
            testConsole.Lines.Count(l => l.Contains("Error ")).ShouldBe(1);
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithWarningsAsErrorsUsingWildcard()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "+!SqlServer.Rules.SRD*");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo);

            // Assert
            testConsole.Lines.Count.ShouldBe(15);

            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.ShouldContain($"proc1.sql(1,47): Error SRD0006 : SqlServer.Rules : Avoid using SELECT *.");
            testConsole.Lines.ShouldContain($"-1(1,1): Error SRD0002 : SqlServer.Rules : Table does not have a primary key.");
            testConsole.Lines.Count(l => l.Contains("): Error ")).ShouldBe(2);
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        private static (FileInfo fileInfo, TSqlModel model) BuildSimpleModel()
        {
            var tmodel = new TestModelBuilder()
                .AddTable("TestTable", ("Column1", "nvarchar(100)"))
                .AddStoredProcedure("sp_GetData", "SELECT * FROM dbo.TestTable", "proc1.sql");
                
            var model = tmodel.Build();
            var packagePath = tmodel.SaveAsPackage();

            return (new FileInfo(packagePath), model);
        }
    }
}
