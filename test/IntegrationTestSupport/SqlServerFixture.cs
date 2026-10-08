using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.SqlServer.Dac.Model;
using Testcontainers.MsSql;

namespace MSBuild.Sdk.SqlProj.TestSupport;

/// <summary>
/// Owns a SQL Server container and its private network for use by any integration test project.
/// </summary>
internal sealed class SqlServerFixture : IAsyncDisposable
{
    public string Password { get; } = $"SqlProj!{Guid.NewGuid():N}";
    public INetwork Network { get; } = new NetworkBuilder().Build();
    public MsSqlContainer SqlServer { get; }
    public SqlServerVersion TargetPlatform { get; }

    /// <summary>
    /// Uses the caller's default version unless SQLPROJ_TEST_SQLSERVER_VERSION selects 2022 or 2025;
    /// SQLPROJ_TEST_SQLSERVER_IMAGE optionally overrides the container image.
    /// </summary>
    public SqlServerFixture(string defaultVersion = "2022")
    {
        var version = Environment.GetEnvironmentVariable("SQLPROJ_TEST_SQLSERVER_VERSION");
        if (string.IsNullOrWhiteSpace(version))
        {
            version = defaultVersion;
        }

        TargetPlatform = version switch
        {
            "2022" => SqlServerVersion.Sql160,
            "2025" => SqlServerVersion.Sql170,
            _ => throw new ArgumentException("SQLPROJ_TEST_SQLSERVER_VERSION must be 2022 or 2025."),
        };

        var image = Environment.GetEnvironmentVariable("SQLPROJ_TEST_SQLSERVER_IMAGE");
        if (string.IsNullOrWhiteSpace(image))
        {
            image = $"mcr.microsoft.com/mssql/server:{version}-latest";
        }

        SqlServer = new MsSqlBuilder(image)
            .WithPassword(Password)
            .WithNetwork(Network)
            .WithNetworkAliases("sqlserver")
            .Build();
    }

    /// <summary>
    /// Creates the private network and starts SQL Server with a five-minute startup timeout.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await Network.CreateAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        await SqlServer.StartAsync(timeout.Token);
    }

    /// <summary>
    /// Disposes the SQL Server container and attempts network cleanup even if container cleanup fails.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await SqlServer.DisposeAsync();
        }
        finally
        {
            await Network.DisposeAsync();
        }
    }
}
