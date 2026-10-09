using System.IO.Compression;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.IntegrationTests;

[TestClass]
[TestCategory("Pack")]
public sealed class PackLayoutTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Pack_PlacesDacpacInToolsFolder()
    {
        var directory = Directory.CreateTempSubdirectory("SqlProjPack_");
        try
        {
            var projectPath = RepositoryWorkspace.PrepareTestProject(directory);

            await ProcessRunner.RunAsync(
                directory.FullName,
                "dotnet",
                ["pack", projectPath, "-c", "Release", RepositoryWorkspace.DacpacToolProperty],
                TestContext.WriteLine,
                TestContext.CancellationToken);

            var package = Directory.EnumerateFiles(directory.FullName, "*.nupkg", SearchOption.AllDirectories).SingleOrDefault();
            package.ShouldNotBeNull("Expected the pack output to produce a single NuGet package.");

            using var archive = ZipFile.OpenRead(package);
            var dacpacEntries = archive.Entries
                .Where(entry => entry.FullName.EndsWith(".dacpac", StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.FullName)
                .ToList();

            dacpacEntries.ShouldContain("tools/TestProject.dacpac");
            dacpacEntries.ShouldAllBe(name => name.StartsWith("tools/", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
