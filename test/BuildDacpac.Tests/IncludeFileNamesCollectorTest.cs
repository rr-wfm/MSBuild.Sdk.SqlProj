using Shouldly;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace MSBuild.Sdk.SqlProj.BuildDacpac.Tests
{
    /// <summary>
    /// Contains tests for the <see cref="IncludeFileNamesCollector" /> class.
    /// </summary>
    [TestClass]
    public class IncludeFileNamesCollectorTest
    {
        /// <summary>
        /// Tests that it can succesfully parse a file that doesn't have any includes. 
        /// </summary>
        [TestMethod]
        public void CanParseFileWithoutIncludes()
        {
            // Arrange
            var inputFile = "../../../../TestProjectWithPrePost/Post-Deployment/Script.PostDeployment.sql";
            var sqlCmdVariableResolver = new SqlCmdVariableResolver(new Dictionary<string, string>(), "dummy");
            var instance = new IncludeFileNamesCollector(inputFile, sqlCmdVariableResolver);

            // Act
            var result = instance.CollectIncludedFilenames();
            var parseErrors = instance.GetParseErrors();

            // Assert
            result.Any().ShouldBeFalse();
            parseErrors.Any().ShouldBeFalse();
        }

        /// <summary>
        /// Tests that it can succesfully parse a file that has included files.
        /// </summary>
        [TestMethod]
        public void CanParseFileWithIncludes()
        {
            // Arrange
            var inputFile = "../../../../TestProjectWithPrePostIncludes/Post-Deployment/Script.PostDeployment.sql";
            var sqlCmdVariableResolver = new SqlCmdVariableResolver(new Dictionary<string, string>(), "dummy");
            var instance = new IncludeFileNamesCollector(inputFile, sqlCmdVariableResolver);

            // Act
            var result = instance.CollectIncludedFilenames();
            var parseErrors = instance.GetParseErrors();

            // Assert
            result.Any().ShouldBeTrue();
            parseErrors.Any().ShouldBeFalse();

            var includedFiles = result.ToList();
            includedFiles[0].EndsWith("SCRIPT1.SQL").ShouldBeTrue();   // Full paths are returned in uppercase
            includedFiles[1].EndsWith("SCRIPT2.SQL").ShouldBeTrue();
        }
    }
}