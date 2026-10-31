using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Shouldly;

[assembly: DoNotParallelize]

namespace MSBuild.Sdk.SqlProj.Deployment.IntegrationTests;

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
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(nameof(PublishingFixture.RunAsync))]
    [DataRow(nameof(PublishingFixture.BuildImageAsync))]
    [DataRow(nameof(PublishingFixture.AssertTableAsync))]
    public async Task PublishingFixture_CanceledCallerToken_Throws(string method)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cancellation.Cancel();
        Func<Task> operation = method switch
        {
            nameof(PublishingFixture.RunAsync) => () => _fixture.RunAsync("must-not-start", cancellation.Token),
            nameof(PublishingFixture.BuildImageAsync) => () => _fixture.BuildImageAsync(cancellation.Token),
            nameof(PublishingFixture.AssertTableAsync) => () => _fixture.AssertTableAsync("master", cancellation.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };

        await Should.ThrowAsync<OperationCanceledException>(operation);

        TestContext.CancellationToken.IsCancellationRequested.ShouldBeFalse();
    }

    [TestMethod]
    [Timeout(300_000, CooperativeCancellation = true)]
    public async Task PublishDatabase_CreatesTable()
    {
        var database = $"BuiltIn_{Guid.NewGuid():N}";
        await _fixture.RunAsync("dotnet", TestContext.CancellationToken, "publish", _fixture.ProjectPath, "-c", "Release", "/t:PublishDatabase", _fixture.ToolProperty,
            $"/p:TargetServerName={_fixture.SqlServer.Hostname}", $"/p:TargetPort={_fixture.SqlServer.GetMappedPublicPort(1433)}",
            $"/p:TargetDatabaseName={database}", "/p:TargetUser=sa", $"/p:TargetPassword={_fixture.Password}");
        await _fixture.AssertTableAsync(database, TestContext.CancellationToken);
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
        await _fixture.RunAsync(_fixture.SqlPackage, TestContext.CancellationToken, arguments.ToArray());
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
