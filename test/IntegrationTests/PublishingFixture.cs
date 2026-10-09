using System.Xml.Linq;
using DotNet.Testcontainers.Networks;
using Microsoft.Data.SqlClient;
using Shouldly;
using Testcontainers.MsSql;

namespace MSBuild.Sdk.SqlProj.IntegrationTests;

internal sealed class PublishingFixture : IAsyncDisposable
{
    private readonly TestContext _context;
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("SqlProjPublishing_");
    private readonly SqlServerFixture _server = new();
    private bool _imageBuildAttempted;
    private bool _disposed;

    public PublishingFixture(TestContext context)
    {
        _context = context;
    }

    public string WorkspaceDirectory => _directory.FullName;
    public MsSqlContainer SqlServer => _server.SqlServer;
    public INetwork Network => _server.Network;
    public string Password => _server.Password;
    public string ImageName { get; } = $"sqlproj-publishing-test:{Guid.NewGuid():N}";
    public string ProjectPath => Path.Combine(_directory.FullName, "TestProject.csproj");
    public string DacpacPath => Path.Combine(_directory.FullName, "bin", "Release", "net10.0", "TestProject.dacpac");
    public string ToolProperty { get; private set; } = "";
    public string SqlPackage { get; } = Environment.GetEnvironmentVariable("SQLPACKAGE_PATH") ?? "sqlpackage";

    public async Task InitializeAsync()
    {
        // Fail with a useful prerequisite error before starting SQL Server.
        await RunAsync(SqlPackage, "/Version");
        ToolProperty = RepositoryWorkspace.DacpacToolProperty;

        // Reuse the repository's stub project and SQL without changing its tracked files.
        RepositoryWorkspace.PrepareTestProject(_directory, _server.TargetPlatform.ToString());

        await RunAsync("dotnet", "build", ProjectPath, "-c", "Release", ToolProperty);

        await _server.InitializeAsync(_context.CancellationToken);
    }

    public string CreateProfile(string database, bool insideContainer = false)
    {
        XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
        var properties = new XElement(ns + "PropertyGroup",
            new XElement(ns + "ProfileVersionNumber", "1"),
            new XElement(ns + "TargetDatabaseName", database),
            new XElement(ns + "TargetConnectionString"),
            new XElement(ns + "BlockOnPossibleDataLoss", "True"),
            new XElement(ns + "DropObjectsNotInSource", "False"));
        var profile = new XDocument(new XElement(ns + "Project", properties));
        var connection = new SqlConnectionStringBuilder(SqlServer.GetConnectionString())
        {
            Password = "",
            InitialCatalog = "",
            TrustServerCertificate = true,
        };
        if (insideContainer)
        {
            connection.DataSource = "sqlserver,1433";
        }
        properties.Element(ns + "TargetConnectionString")!.Value = connection.ConnectionString;
        var path = Path.Combine(_directory.FullName, database + ".publish.xml");
        profile.Save(path);
        return path;
    }

    public async Task BuildImageAsync()
    {
        _imageBuildAttempted = true;
        await RunAsync("dotnet", "publish", ProjectPath, "-c", "Release", "/t:PublishContainer", ToolProperty,
            "/p:ContainerRepository=sqlproj-publishing-test", $"/p:ContainerImageTag={ImageName.Split(':')[1]}");
    }

    public async Task AssertTableAsync(string database)
    {
        var connectionString = new SqlConnectionStringBuilder(SqlServer.GetConnectionString()) { InitialCatalog = database };
        await using var connection = new SqlConnection(connectionString.ConnectionString);
        await connection.OpenAsync(_context.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'MyTable' ORDER BY ORDINAL_POSITION";
        await using var reader = await command.ExecuteReaderAsync(_context.CancellationToken);
        (await reader.ReadAsync(_context.CancellationToken)).ShouldBeTrue();
        reader.GetString(0).ShouldBe("Column1");
        reader.GetString(1).ShouldBe("nvarchar");
        (await reader.ReadAsync(_context.CancellationToken)).ShouldBeTrue();
        reader.GetString(0).ShouldBe("Column2");
        reader.GetString(1).ShouldBe("int");
        (await reader.ReadAsync(_context.CancellationToken)).ShouldBeFalse();
    }

    public async Task AssertTableAsync(string database, CancellationToken cancellationToken)
    {
        var connectionString = new SqlConnectionStringBuilder(SqlServer.GetConnectionString()) { InitialCatalog = database };
        await using var connection = new SqlConnection(connectionString.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'MyTable' ORDER BY ORDINAL_POSITION";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        (await reader.ReadAsync(cancellationToken)).ShouldBeTrue();
        reader.GetString(0).ShouldBe("Column1");
        reader.GetString(1).ShouldBe("nvarchar");
        (await reader.ReadAsync(cancellationToken)).ShouldBeTrue();
        reader.GetString(0).ShouldBe("Column2");
        reader.GetString(1).ShouldBe("int");
        (await reader.ReadAsync(cancellationToken)).ShouldBeFalse();
    }

    public Task RunAsync(string executable, params string[] arguments)
    {
        return RunProcessAsync(executable, arguments, _context.CancellationToken);
    }

    private Task RunProcessAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        return ProcessRunner.RunAsync(
            _directory.FullName,
            executable,
            arguments,
            _context.WriteLine,
            cancellationToken,
            output => output.Replace(Password, "[REDACTED]", StringComparison.Ordinal));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        try
        {
            await _server.DisposeAsync();
        }
        finally
        {
            try
            {
                if (_imageBuildAttempted)
                {
                    // A failed image build might not have created a tag to remove.
                    try
                    {
                        await RunProcessAsync("docker", ["image", "rm", ImageName], CancellationToken.None);
                    }
                    catch (ShouldAssertException exception)
                    {
                        _context.WriteLine(exception.Message);
                    }
                }
            }
            finally
            {
                _directory.Delete(true);
            }
        }
    }
}
