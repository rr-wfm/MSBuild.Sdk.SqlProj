using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Shouldly;

[assembly: DoNotParallelize]

namespace MSBuild.Sdk.SqlProj.IntegrationTests;

[TestClass]
[TestCategory("Publishing")]
public sealed class PublishingTests
{
    private static PublishingFixture _fixture = null!;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static async Task InitializeAsync(TestContext context)
    {
        _fixture = new PublishingFixture(context);
        try
        {
            await _fixture.InitializeAsync();
        }
        catch
        {
            await _fixture.DisposeAsync();
            throw;
        }
    }

    [ClassCleanup]
    public static async Task CleanupAsync()
    {
        await _fixture.DisposeAsync();
    }

    [TestMethod]
    [Timeout(300_000, CooperativeCancellation = true)]
    public async Task PublishDatabase_CreatesTable()
    {
        var database = $"BuiltIn_{Guid.NewGuid():N}";
        await _fixture.RunAsync("dotnet", "publish", _fixture.ProjectPath, "-c", "Release", "/t:PublishDatabase", _fixture.ToolProperty,
            $"/p:TargetServerName={_fixture.SqlServer.Hostname}", $"/p:TargetPort={_fixture.SqlServer.GetMappedPublicPort(1433)}",
await _fixture.RunAsync(TestContext.CancellationToken, "dotnet", "publish", _fixture.ProjectPath, "-c", "Release", "/t:PublishDatabase", _fixture.ToolProperty,
$"/p:TargetServerName={_fixture.SqlServer.Hostname}", $"/p:TargetPort={_fixture.SqlServer.GetMappedPublicPort(1433)}",
$"/p:TargetDatabaseName={database}", "/p:TargetUser=sa", $"/p:TargetPassword={_fixture.Password}");
        await _fixture.AssertTableAsync(database);
    }

    [TestMethod]
    [Timeout(300_000, CooperativeCancellation = true)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SqlPackage_ProfileCreatesTable(bool overrideDatabase)
    {
        var database = $"Profile_{Guid.NewGuid():N}";
        var profile = _fixture.CreateProfile(database);
        var original = await File.ReadAllTextAsync(profile, TestContext.CancellationToken);
        var arguments = new List<string> { "/Action:Publish", $"/SourceFile:{_fixture.DacpacPath}", $"/Profile:{profile}", $"/TargetPassword:{_fixture.Password}" };
        if (overrideDatabase)
        {
            database += "_Override";
            arguments.Add($"/TargetDatabaseName:{database}");
        }
        await _fixture.RunAsync(TestContext.CancellationToken, _fixture.SqlPackage, arguments.ToArray());
        await _fixture.AssertTableAsync(database, TestContext.CancellationToken);
        (await File.ReadAllTextAsync(profile, TestContext.CancellationToken)).ShouldBe(original, "Publishing must not modify the profile.");
    }

    [TestMethod]
    [Timeout(300_000, CooperativeCancellation = true)]
    public async Task PublishContainer_MountedProfileCreatesTable()
    {
        await _fixture.BuildImageAsync(TestContext.CancellationToken);
        var database = $"Container_{Guid.NewGuid():N}";
        var profile = _fixture.CreateProfile(database, insideContainer: true);
        await using var container = new ContainerBuilder(_fixture.ImageName)
            .WithNetwork(_fixture.Network)
            // Verify that a profile can be supplied at runtime through a read-only bind mount.
            .WithBindMount(profile, "/profiles/Development.publish.xml", AccessMode.ReadOnly)
            .WithCommand("/Profile:/profiles/Development.publish.xml", $"/TargetPassword:{_fixture.Password}")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Successfully published database.",
                strategy => strategy.WithMode(WaitStrategyMode.OneShot)))
            .Build();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        await container.StartAsync(timeout.Token);
        (await container.GetExitCodeAsync(timeout.Token)).ShouldBe(0L);
        await _fixture.AssertTableAsync(database, TestContext.CancellationToken);
    }
}
