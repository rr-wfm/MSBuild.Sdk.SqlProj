using Microsoft.SqlTools.ServiceLayer.BatchParser;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using NSubstitute;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class ScriptParserTests
    {
        private const string TEST_PROJECT_PATH = "../../../../TestProjectWithPrePost";
        private readonly IVariableResolver _variableResolver = Substitute.For<IVariableResolver>();

        [TestMethod]
        public void CanParseFileWithoutIncludes()
        {
            // Arrange
            var collector = new ScriptParser($"{TEST_PROJECT_PATH}/Post-Deployment/Script.PostDeployment.sql", _variableResolver);

            // Act
            var result = collector.CollectFileNames();

            // Assert
            result.ShouldNotBeNull();
            result.Count().ShouldBe(0);
        }

        [TestMethod]
        public void CanParseFileWithIncludes()
        {
            // Arrange
            var collector = new ScriptParser($"{TEST_PROJECT_PATH}/Pre-Deployment/Script.PreDeployment.SimpleInclude.sql", _variableResolver);

            // Act
            var result = collector.CollectFileNames();

            // Assert
            result.ShouldNotBeNull();
            result.Count().ShouldBe(1);
        }

        [TestMethod]
        public void ParserFailsWhenIncludesDontExist()
        {
            // Arrange
            var collector = new ScriptParser($"{TEST_PROJECT_PATH}/Pre-Deployment/Script.PreDeployment.MissingScript.sql", _variableResolver);

            // Act / Assert
            Assert.ThrowsException<System.IO.FileNotFoundException>(() => collector.CollectFileNames());
        }

        [TestMethod]
        public void CanParseFileWithIncludesFromRelativePaths()
        {
            // Arrange
            var collector = new ScriptParser($"{TEST_PROJECT_PATH}/Pre-Deployment/Script.PreDeployment.RelativePathIncludes.sql", _variableResolver);

            // Act
            var result = collector.CollectFileNames();

            // Assert
            result.ShouldNotBeNull();
            result.Count().ShouldBe(2);
        }

        [TestMethod]
        public void CanParseFileWithNestedIncludes()
        {
            // Arrange
            var collector = new ScriptParser($"{TEST_PROJECT_PATH}/Pre-Deployment/Script.PreDeployment.NestedIncludes.sql", _variableResolver);

            // Act
            var result = collector.CollectFileNames();

            // Assert
            result.ShouldNotBeNull();
            result.Count().ShouldBe(3);
        }

        [TestMethod]
        public void CanParseFileWithNestedIncludesContainingRelativePaths()
        {
            // Arrange
            var collector = new ScriptParser($"{TEST_PROJECT_PATH}/Pre-Deployment/Script.PreDeployment.NestedRelativePaths.sql", _variableResolver);

            // Act
            var result = collector.CollectFileNames();

            // Assert
            result.ShouldNotBeNull();
            result.Count().ShouldBe(4);
        }

        [TestMethod]
        public void IgnoresDirectivesOtherThanColonR()
        {
            // Arrange
            var collector = new ScriptParser($"{TEST_PROJECT_PATH}/Pre-Deployment/Script.PreDeployment.IgnoredDirective.sql", _variableResolver);

            // Act
            var result = collector.CollectFileNames();

            // Assert
            result.ShouldNotBeNull();
            result.Count().ShouldBe(1);
        }

        [TestMethod]
        public void CanGenerateScriptWithoutIncludes()
        {
            // Arrange
            var collector = new ScriptParser($"{TEST_PROJECT_PATH}/Post-Deployment/Script.PostDeployment.Simple.sql", _variableResolver);

            // Act
            var result = collector.GenerateScript();

            // Assert
            result.ShouldNotBeNull();
            result.ShouldMatch(@"SELECT TOP 10 \* FROM MyTable[\r\n]*GO[\r\n]*");
        }

        [TestMethod]
        public void CanGenerateScriptWithIncludes()
        {
            // Arrange
            var collector = new ScriptParser($"{TEST_PROJECT_PATH}/Pre-Deployment/Script.PreDeployment.SimpleInclude.sql", _variableResolver);

            // Act
            var result = collector.GenerateScript();

            // Assert
            result.ShouldNotBeNull();
            result.ShouldMatch(@"PRINT N'Pre deploy'[\r\n]*PRINT N'Script1.sql'[\r\n]*GO[\r\n]*");
        }

        [TestMethod]
        public void CanGenerateScriptWithNestedIncludes()
        {
            // Arrange
            var collector = new ScriptParser($"{TEST_PROJECT_PATH}/Pre-Deployment/Script.PreDeployment.NestedIncludes.sql", _variableResolver);

            // Act
            var result = collector.GenerateScript();

            // Assert
            result.ShouldNotBeNull();
            result.ShouldMatch(@"PRINT N'Pre deploy'[\r\n]*PRINT N'Script1.sql'[\r\n]*PRINT N'Script3.sql'[\r\n]*PRINT N'Script2.sql'[\r\n]*GO[\r\n]*");
        }

        [TestMethod]
        public void CanGenerateScriptWithNestedIncludesContainingRelativePaths()
        {
            // Arrange
            var collector = new ScriptParser($"{TEST_PROJECT_PATH}/Pre-Deployment/Script.PreDeployment.NestedRelativePaths.sql", _variableResolver);

            // Act
            var result = collector.GenerateScript();

            // Assert
            result.ShouldNotBeNull();
            result.ShouldMatch(@"PRINT N'Pre deploy'[\r\n]*PRINT N'Script1.sql'[\r\n]*PRINT N'Script4.sql'[\r\n]*PRINT N'Script5.sql'[\r\n]*PRINT N'Script6.sql'[\r\n]*GO[\r\n]*");
        }
    }
}
