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
    public SqlServerVersion TargetPlatform { get; } = SqlServerVersion.Sql170;

    /// <summary>
    /// Uses SQL Server 2025.
    /// </summary>
    public SqlServerFixture()
    {
        SqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
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
