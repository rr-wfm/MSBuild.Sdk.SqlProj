using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Shouldly;

[assembly: DoNotParallelize]

namespace MSBuild.Sdk.SqlProj.PackagedDeploymentIntegrationTests;

[TestClass]
public sealed class PackagedDeploymentTests
{
    private PackagedDeploymentFixture _fixture = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _fixture = new PackagedDeploymentFixture(TestContext);
        await _fixture.InitializeAsync();
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        if (_fixture != null)
        {
            await _fixture.DisposeAsync();
        }
    }

    [TestMethod]
    [TestCategory("PackagedSqlPackage")]
    public async Task SqlPackage_DeploysSuppliedDacpac()
    {
        await _fixture.RunAsync(_fixture.SqlPackage,
            "/Action:Publish", $"/SourceFile:{_fixture.DacpacPath}",
            "/Properties:IncludeCompositeObjects=True",
            $"/TargetServerName:{_fixture.Server.SqlServer.Hostname},{_fixture.Server.SqlServer.GetMappedPublicPort(1433)}",
            "/TargetUser:sa", $"/TargetPassword:{_fixture.Server.Password}",
            $"/TargetDatabaseName:{_fixture.DatabaseName}", "/TargetEncryptConnection:False");
        await _fixture.AssertDeploymentAsync();
    }

    [TestMethod]
    [TestCategory("PackagedPublish")]
    public async Task PublishDatabase_DeploysUsingPackagedSdk()
    {
        await _fixture.BuildProjectAsync();
        // Keep credentials out of binary logs: the preceding build records package/target resolution.
        await _fixture.RunAsync("dotnet", "publish", _fixture.ProjectPath, "-c", "Release",
            "--no-restore", "/t:PublishDatabase", "/warnaserror:SQL71502",
            $"/p:TargetServerName={_fixture.Server.SqlServer.Hostname}",
            $"/p:TargetPort={_fixture.Server.SqlServer.GetMappedPublicPort(1433)}",
            $"/p:TargetDatabaseName={_fixture.DatabaseName}", "/p:TargetUser=sa",
            $"/p:TargetPassword={_fixture.Server.Password}");
        await _fixture.AssertDeploymentAsync();
    }

    [TestMethod]
    [TestCategory("PackagedContainer")]
    public async Task PublishContainer_DeploysUsingPackagedSdk()
    {
        await _fixture.BuildImageAsync();
        await using var container = new ContainerBuilder(_fixture.ImageName)
            .WithNetwork(_fixture.Server.Network)
            .WithCommand("/Properties:IncludeCompositeObjects=True", "/TargetServerName:sqlserver",
                "/TargetUser:sa", $"/TargetPassword:{_fixture.Server.Password}",
                $"/TargetDatabaseName:{_fixture.DatabaseName}", "/TargetEncryptConnection:False")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Successfully published database.",
                strategy => strategy.WithMode(WaitStrategyMode.OneShot)))
            .Build();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        try
        {
            await container.StartAsync(timeout.Token);
            (await container.GetExitCodeAsync(timeout.Token)).ShouldBe(0L);
            await _fixture.AssertDeploymentAsync();
        }
        finally
        {
            await _fixture.CaptureLogsAsync(container, "deployment-container.log");
        }
    }
}
