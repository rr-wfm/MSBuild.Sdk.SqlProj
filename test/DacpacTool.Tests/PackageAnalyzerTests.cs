using System.IO;
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
            var packageAnalyzer = new PackageAnalyzer(_console);

            // Act
            packageAnalyzer.Analyze(path);
            
            // Assert
            testConsole.Lines.Count.ShouldBe(11);

            testConsole.Lines.ShouldContain($"Analyzing package '{path.FullName}'");
            testConsole.Lines.ShouldContain($"[dbo].[sp_GetData](1,47): Warning SRD0006 : Rules : Avoid using SELECT *.");
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{path.FullName}'");
        }

        private static FileInfo BuildSimpleModel()
        {
            var packagePath = new TestModelBuilder()
                .AddTable("TestTable", ("Column1", "nvarchar(100)"))
                .AddStoredProcedure("sp_GetData", "SELECT * FROM dbo.TestTable")
            .SaveAsPackage();

            return new FileInfo(packagePath);
        }
    }
}
