using System.IO.Compression;
using System.Xml.Linq;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using MSBuild.Sdk.SqlProj.TestSupport;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.PackagedDeploymentIntegrationTests;

internal sealed class PackagedDeploymentFixture : IAsyncDisposable
{
    private readonly TestContext _context;
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("SqlProjPackaged_");
    private readonly string _diagnostics;
    private readonly IntegrationTestProcess _process;
    private bool _imageBuildAttempted;

    public PackagedDeploymentFixture(TestContext context)
    {
        _context = context;
        _diagnostics = Path.Combine(context.TestResultsDirectory!, "diagnostics", $"{context.TestName}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_diagnostics);
        _process = new IntegrationTestProcess(context, _directory.FullName, Server.Password,
            _diagnostics, Path.Combine(_directory.FullName, "packages"));
    }

    public SqlServerFixture Server { get; } = new();
    public string SqlPackage { get; } = Environment.GetEnvironmentVariable("SQLPACKAGE_PATH") ?? "sqlpackage";
    public string DatabaseName { get; } = $"Packaged_{Guid.NewGuid():N}";
    public string ImageName { get; } = $"sqlproj-packaged-test:{Guid.NewGuid():N}";
    public string ProjectPath => Path.Combine(_directory.FullName, "TestProjectWithSDKRef.csproj");
    public string DacpacPath { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var artifacts = Environment.GetEnvironmentVariable("SQLPROJ_TEST_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(artifacts))
        {
            throw new InvalidOperationException("Set SQLPROJ_TEST_ARTIFACTS to the prepared artifacts directory. See test/PackagedDeploymentIntegrationTests/README.md.");
        }
        artifacts = Path.GetFullPath(artifacts);
        var packages = Path.Combine(artifacts, "packages");
        var sdkVersion = ReadPackageVersion(packages, "MSBuild.Sdk.SqlProj");
        var dependencyVersion = ReadPackageVersion(packages, "TestProject");
        DacpacPath = Path.Combine(artifacts, "dacpac", "TestProjectWithSDKRef.dacpac");
        // Fail before starting SQL Server if artifacts do not target SQL Server 2025.
        foreach (var name in new[] { "TestProjectWithSDKRef.dacpac", "TestProject.dacpac" })
        {
            using var stream = File.OpenRead(Path.Combine(artifacts, "dacpac", name));
            AssertTargetPlatform(stream, name);
        }

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "MSBuild.Sdk.SqlProj.slnx")))
        {
            root = root.Parent;
        }
        root.ShouldNotBeNull("Run these tests from a repository checkout.");
        var source = Path.Combine(root.FullName, "test", "TestProjectWithSDKRef");
        foreach (var file in Directory.EnumerateFiles(source, "*.sql", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            {
                continue;
            }
            var destination = Path.Combine(_directory.FullName, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
        var project = XDocument.Load(Path.Combine(source, "TestProjectWithSDKRef.csproj"));
        project.Root!.SetAttributeValue("Sdk", $"MSBuild.Sdk.SqlProj/{sdkVersion}");
        var properties = project.Root.Element("PropertyGroup")!;
        properties.SetElementValue("DependencyVersion", dependencyVersion);
        properties.SetElementValue("SqlServerVersion", Server.TargetPlatform.ToString());
        project.Save(ProjectPath);

        // Only the supplied artifacts may satisfy these two package IDs. Use a private package
        // cache as well, so a previously restored SDK cannot hide a broken or missing package.
        new XDocument(new XElement("configuration",
            new XElement("packageSources", new XElement("clear"),
                new XElement("add", new XAttribute("key", "artifacts"), new XAttribute("value", packages)),
                new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", "https://api.nuget.org/v3/index.json"))),
            new XElement("packageSourceMapping", new XElement("clear"),
                new XElement("packageSource", new XAttribute("key", "artifacts"),
                    new XElement("package", new XAttribute("pattern", "MSBuild.Sdk.SqlProj")),
                    new XElement("package", new XAttribute("pattern", "TestProject"))),
                new XElement("packageSource", new XAttribute("key", "nuget.org"),
                    new XElement("package", new XAttribute("pattern", "*"))))))
            .Save(Path.Combine(_directory.FullName, "nuget.config"));
        await Server.InitializeAsync(_context.CancellationToken);
    }

    private string ReadPackageVersion(string directory, string id)
    {
        var versions = new List<string>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.nupkg", SearchOption.AllDirectories))
        {
            using var archive = ZipFile.OpenRead(path);
            using var manifest = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
            var document = XDocument.Load(manifest);
            var ns = document.Root!.Name.Namespace;
            var metadata = document.Root.Element(ns + "metadata")!;
            if (metadata.Element(ns + "id")!.Value != id)
            {
                continue;
            }
            versions.Add(metadata.Element(ns + "version")!.Value);
            if (id == "TestProject")
            {
                using var content = archive.GetEntry("tools/TestProject.dacpac")!.Open();
                using var buffer = new MemoryStream();
                content.CopyTo(buffer);
                buffer.Position = 0;
                AssertTargetPlatform(buffer, path);
            }
        }
        versions.Count.ShouldBe(1, $"Provide exactly one {id} package in {directory}.");
        return versions.Single();
    }

    private void AssertTargetPlatform(Stream stream, string name)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        using var model = archive.GetEntry("model.xml")!.Open();
        var provider = XDocument.Load(model).Root!.Attribute("DspName")!.Value;
        provider.ShouldBe($"Microsoft.Data.Tools.Schema.Sql.{Server.TargetPlatform}DatabaseSchemaProvider",
            $"{name} must target SQL Server 2025 (Sql170). Rebuild all deployment artifacts.");
    }

    public Task BuildProjectAsync() => RunAsync("dotnet", "build", ProjectPath, "-c", "Release",
        "/warnaserror:SQL71502", $"/bl:{Path.Combine(_diagnostics, "build.binlog")};ProjectImports=None");

    public async Task BuildImageAsync()
    {
        _imageBuildAttempted = true;
        await RunAsync("dotnet", "publish", ProjectPath, "-c", "Release", "/t:PublishContainer",
            "/warnaserror:SQL71502", $"/bl:{Path.Combine(_diagnostics, "publish-container.binlog")};ProjectImports=None",
            "/p:ContainerRepository=sqlproj-packaged-test", $"/p:ContainerImageTag={ImageName.Split(':')[1]}",
            // The SDK uses a shared temporary download filename; isolate simultaneous test runs.
            $"/p:TempStagingFolder={_directory.FullName}");
    }

    public async Task AssertDeploymentAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(Server.SqlServer.GetConnectionString()) { InitialCatalog = DatabaseName };
        await using var connection = new SqlConnection(connectionString.ConnectionString);
        await connection.OpenAsync(_context.CancellationToken);
        await using var command = connection.CreateCommand();
        // Check the project's table, the composite dependency, and the expanded post-deploy script.
        command.CommandText = "SELECT COUNT(*) FROM dbo.TestTable WHERE Column1 IN (N'FirstRow', N'SecondRow'); " +
            "SELECT COUNT(*) FROM dbo.MyTable WHERE (Column1 = N'FirstRow' AND Column2 = 1) OR (Column1 = N'SecondRow' AND Column2 = 2);";
        await using var reader = await command.ExecuteReaderAsync(_context.CancellationToken);
        (await reader.ReadAsync(_context.CancellationToken)).ShouldBeTrue();
        reader.GetInt32(0).ShouldBe(2);
        (await reader.NextResultAsync(_context.CancellationToken)).ShouldBeTrue();
        (await reader.ReadAsync(_context.CancellationToken)).ShouldBeTrue();
        reader.GetInt32(0).ShouldBe(2);
    }

    public Task RunAsync(string executable, params string[] arguments) => _process.RunAsync(executable, arguments, _context.CancellationToken);

    public Task CaptureLogsAsync(IContainer container, string name) => _process.CaptureLogsAsync(container, name);

    public async ValueTask DisposeAsync()
    {
        try
        {
            await CaptureLogsAsync(Server.SqlServer, "sqlserver.log");
            await Server.DisposeAsync();
        }
        finally
        {
            try
            {
                if (_imageBuildAttempted)
                {
                    try
                    {
                        await _process.RunAsync("docker", ["image", "rm", ImageName], CancellationToken.None);
                    }
                    catch (ShouldAssertException exception)
                    {
                        _context.WriteLine(exception.Message);
                    }
                }
            }
            finally
            {
                foreach (var file in Directory.EnumerateFiles(_diagnostics))
                {
                    _context.AddResultFile(file);
                }
                _directory.Delete(true);
            }
        }
    }
}
