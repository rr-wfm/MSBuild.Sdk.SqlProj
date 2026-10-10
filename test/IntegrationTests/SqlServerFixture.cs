using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.SqlServer.Dac.Model;
using Testcontainers.MsSql;

namespace MSBuild.Sdk.SqlProj.IntegrationTests;

internal sealed class SqlServerFixture : IAsyncDisposable
{
    public string Password { get; } = $"SqlProj!{Guid.NewGuid():N}";
    public INetwork Network { get; } = new NetworkBuilder().Build();
    public MsSqlContainer SqlServer { get; }
    public SqlServerVersion TargetPlatform { get; } = SqlServerVersion.Sql170;

    public SqlServerFixture()
    {
        SqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
            .WithPassword(Password)
            .WithNetwork(Network)
            .WithNetworkAliases("sqlserver")
            .Build();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await Network.CreateAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        await SqlServer.StartAsync(timeout.Token);
    }

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
