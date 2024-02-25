using System.IO;
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
            var path = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, null);

            // Act
            packageAnalyzer.Analyze(path.Item2, path.Item1);
            
            // Assert
            testConsole.Lines.Count.ShouldBe(11);

            testConsole.Lines.ShouldContain($"Analyzing package '{path.Item1.FullName}'");
            testConsole.Lines.ShouldContain($"proc1.sql(1,47): Warning SRD0006 : SqlServer.Rules : Avoid using SELECT *.");
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{path.Item1.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithSupressions()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var path = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "-SqlServer.Rules.SRD0006;-SqlServer.Rules.SRD9999;");

            // Act
            packageAnalyzer.Analyze(path.Item2, path.Item1);

            // Assert
            testConsole.Lines.Count.ShouldBe(10);

            testConsole.Lines.ShouldContain($"Analyzing package '{path.Item1.FullName}'");
            testConsole.Lines.ShouldNotContain($"Warning SRD0006 : SqlServer.Rules");
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{path.Item1.FullName}'");
        }

        private static (FileInfo, TSqlModel) BuildSimpleModel()
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
