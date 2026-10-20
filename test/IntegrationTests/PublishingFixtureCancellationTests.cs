using Shouldly;

namespace MSBuild.Sdk.SqlProj.IntegrationTests;

[TestClass]
[TestCategory("Cancellation")]
public sealed class PublishingFixtureCancellationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(120_000, CooperativeCancellation = true)]
    public async Task RunAsync_CallerCancellation_StopsRunningProcess()
    {
        await using var fixture = new PublishingFixture(TestContext);
        var source = Path.Combine(fixture.WorkspaceDirectory, "WaitForCancellation.cs");
        var ready = Path.Combine(fixture.WorkspaceDirectory, "process-ready.txt");
        await File.WriteAllTextAsync(source, """
            #:property TargetFramework=net10.0
            #:property ImportDirectoryBuildProps=false
            #:property ImportDirectoryBuildTargets=false
            await File.WriteAllTextAsync(args[0], "ready");
            await Task.Delay(TimeSpan.FromSeconds(30));
            """, TestContext.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var run = fixture.RunAsync("dotnet", cancellation.Token, "run", "--file", source, "--disable-build-servers", "--", ready);

        try
        {
            while (!File.Exists(ready) && !run.IsCompleted)
            {
                await Task.Delay(50, TestContext.CancellationToken);
            }
            File.Exists(ready).ShouldBeTrue("The child process must be running before cancellation.");
        }
        finally
        {
            await cancellation.CancelAsync();
            var exception = await Should.ThrowAsync<OperationCanceledException>(() => run);
            exception.CancellationToken.ShouldBe(cancellation.Token);
        }
    }
}
