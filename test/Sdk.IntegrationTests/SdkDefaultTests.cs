using System.IO.Compression;
using System.Xml.Linq;
using MSBuild.Sdk.SqlProj.TestSupport;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.Sdk.IntegrationTests;

[TestClass]
[TestCategory("Build")]
public sealed class SdkDefaultTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Build_WithoutSqlServerVersion_TargetsSqlServer2025()
    {
        var directory = Directory.CreateTempSubdirectory("SqlProjDefault_");
        try
        {
            var projectPath = RepositoryWorkspace.PrepareTestProject(directory);
            var project = XDocument.Load(projectPath);
            // Omit the property entirely so the build exercises the default in Sdk.props.
            project.Descendants("SqlServerVersion").Remove();
            project.Save(projectPath);

            await ProcessRunner.RunAsync(
                directory.FullName,
                "dotnet",
                ["build", projectPath, "-c", "Release", RepositoryWorkspace.DacpacToolProperty],
                TestContext.WriteLine,
                TestContext.CancellationToken);

            var dacpacPath = Path.Combine(directory.FullName, "bin", "Release", "net10.0", "TestProject.dacpac");
            using var dacpac = ZipFile.OpenRead(dacpacPath);
            var modelEntry = dacpac.GetEntry("model.xml");
            modelEntry.ShouldNotBeNull("Expected the built DACPAC to contain model.xml.");
            using var modelStream = modelEntry.Open();
            var model = XDocument.Load(modelStream);
            // Keep this explicit so a regression in the SDK default fails the test.
            var provider = (string?)model.Root!.Attribute("DspName");
            provider.ShouldBe("Microsoft.Data.Tools.Schema.Sql.Sql170DatabaseSchemaProvider");
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
