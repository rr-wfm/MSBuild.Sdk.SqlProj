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
    public SqlServerVersion TargetPlatform { get; }

    public SqlServerFixture()
    {
        var version = Environment.GetEnvironmentVariable("SQLPROJ_TEST_SQLSERVER_VERSION");
        if (string.IsNullOrWhiteSpace(version))
        {
            version = "2022";
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
