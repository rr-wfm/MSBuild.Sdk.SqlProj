using Aspire.Hosting;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace MSBuild.Sdk.SqlProj.Aspire.Tests;

public class PublishSqlProjectLifecycleHookTests
{
    [Fact]
    public async Task AfterResourcesCreatedAsync_PublishesDacpacToTargetDatabase()
    {
        // Arrange
        var dacDeployerMock = Substitute.For<IDacpacDeployer>();
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Services.AddSingleton(dacDeployerMock);
        
        var targetDatabase = appBuilder.AddSqlServer("sql")
                                       .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 1433)) // Simulated endpoint
                                       .AddDatabase("test");
        appBuilder.AddSqlProject<TestProject>("MySqlProject")
                  .PublishTo(targetDatabase);
        
        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var lifecycleHook = Assert.Single(app.Services.GetServices<IDistributedApplicationLifecycleHook>().OfType<PublishSqlProjectLifecycleHook>());
        await lifecycleHook.AfterResourcesCreatedAsync(appModel, CancellationToken.None);
        
        // Assert
        var expectedPath = Path.GetFullPath(Path.Combine(appBuilder.AppHostDirectory, "../../../../TestProject/bin/Debug/netstandard2.0/TestProject.dacpac"));
        dacDeployerMock.Received().Deploy(Arg.Is(expectedPath), Arg.Any<string>(), Arg.Is("test"), Arg.Any<ILogger>(), Arg.Any<CancellationToken>());
    }
}
