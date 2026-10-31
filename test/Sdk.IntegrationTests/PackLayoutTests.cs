using MSBuild.Sdk.SqlProj.TestSupport;
using NuGet.Packaging;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.Sdk.IntegrationTests;

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

            using var stream = File.OpenRead(package);
            using var reader = new PackageArchiveReader(stream);
            var dacpacEntries = reader.GetFiles()
                .Where(path => path.EndsWith(".dacpac", StringComparison.OrdinalIgnoreCase))
                .ToList();
            dacpacEntries.ShouldContain("tools/TestProject.dacpac");
            dacpacEntries.ShouldAllBe(path => path.StartsWith("tools/", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
