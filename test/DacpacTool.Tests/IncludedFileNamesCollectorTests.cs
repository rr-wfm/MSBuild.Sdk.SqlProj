using Microsoft.SqlTools.ServiceLayer.BatchParser;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class IncludedFileNamesCollectorTests
    {
        private readonly IVariableResolver _variableResolver = Substitute.For<IVariableResolver>();

        [TestMethod]
        public void CanParseFileWithoutIncludes()
        {
            // Arrange
            var collector = new IncludedFileNamesCollector("../../../../TestProjectWithPrePost/Post-Deployment/Script.PostDeployment.sql", _variableResolver);

            // Act
            var result = collector.CollectFileNames();

            // Assert
            result.ShouldNotBeNull();
            result.Length.ShouldBe(0);
        }

        [TestMethod]
        public void CanParseFileWithIncludes()
        {
            // Arrange
            var collector = new IncludedFileNamesCollector("../../../../TestProjectWithPrePost/Pre-Deployment/Script.PreDeployment.sql", _variableResolver);

            // Act
            var result = collector.CollectFileNames();

            // Assert
            result.ShouldNotBeNull();
            result.Length.ShouldBe(1);
        }
    }
}
