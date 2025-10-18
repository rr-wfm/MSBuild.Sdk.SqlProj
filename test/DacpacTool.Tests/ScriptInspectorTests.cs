using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests;

[TestClass]
public class ScriptInspectorTests
{
    [TestMethod]
    public void GetIncludedFiles_PredeploymentScript()
    {
        // Arrange
        var packageInspector = new ScriptInspector();

        // Act
        packageInspector.AddPreDeploymentScript(
            new FileInfo("../../../../TestProjectWithPrePost/Pre-Deployment/Script.PreDeployment.SimpleInclude.sql"));

        // Assert
        var expectedFile = new FileInfo("../../../../TestProjectWithPrePost/Pre-Deployment/Script1.sql");
        packageInspector.IncludedFiles.Count().ShouldBe(1);
        packageInspector.IncludedFiles.First().ShouldBe(expectedFile.FullName);
    }

    [TestMethod]
    public void GetIncludedFiles_NoIncludes()
    {
        // Arrange
        var packageInspector = new ScriptInspector();

        // Act
        packageInspector.AddPreDeploymentScript(
            new FileInfo("../../../../TestProjectWithPrePost/Pre-Deployment/Script1.sql"));

        // Assert
        packageInspector.IncludedFiles.Count().ShouldBe(0);
    }

    [TestMethod]
    public void GetIncludedFiles_PostdeploymentScript()
    {
        // Arrange
        var packageInspector = new ScriptInspector();

        // Act
        packageInspector.AddPostDeploymentScript(
            new FileInfo("../../../../TestProjectWithPrePost/Post-Deployment/Script.PostDeployment.SimpleInclude.sql"));

        // Assert
        var expectedFile = new FileInfo("../../../../TestProjectWithPrePost/Post-Deployment/Script1.sql");
        packageInspector.IncludedFiles.Count().ShouldBe(1);
        packageInspector.IncludedFiles.First().ShouldBe(expectedFile.FullName);
    }

    [TestMethod]
    public void GetIncludedFiles_MultipleScripts()
    {
        // Arrange
        var packageInspector = new ScriptInspector();

        // Act
        packageInspector.AddPreDeploymentScript(
            new FileInfo("../../../../TestProjectWithPrePost/Pre-Deployment/Script.PreDeployment.SimpleInclude.sql"));

        packageInspector.AddPostDeploymentScript(
            new FileInfo("../../../../TestProjectWithPrePost/Post-Deployment/Script.PostDeployment.SimpleInclude.sql"));

        // Assert
        var expectedFile1 = new FileInfo("../../../../TestProjectWithPrePost/Pre-Deployment/Script1.sql");
        var expectedFile2 = new FileInfo("../../../../TestProjectWithPrePost/Post-Deployment/Script1.sql");
        packageInspector.IncludedFiles.Count().ShouldBe(2);
        packageInspector.IncludedFiles.First().ShouldBe(expectedFile1.FullName);
        packageInspector.IncludedFiles.Last().ShouldBe(expectedFile2.FullName);
    }
}
