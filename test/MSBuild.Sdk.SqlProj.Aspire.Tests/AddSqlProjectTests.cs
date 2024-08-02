using Aspire.Hosting;
using Aspire.Hosting.Lifecycle;

namespace MSBuild.Sdk.SqlProj.Aspire.Tests;

public class AddSqlProjectTests
{
    [Fact]
    public void AddSqlProject_WithProjectMetadata()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddSqlProject<TestProject>("MySqlProject");
        
        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("MySqlProject", sqlProjectResource.Name);

        var dacpacPath = sqlProjectResource.GetDacpacPath();
        Assert.NotNull(dacpacPath);
        Assert.True(File.Exists(dacpacPath));
    }

    [Fact]
    public void AddSqlProject_WithExplicitPath()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddSqlProject("MySqlProject").FromDacpac("../../../../TestProject/TestProject.csproj");
        
        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("MySqlProject", sqlProjectResource.Name);

        var annotation = Assert.Single(sqlProjectResource.Annotations.OfType<DacpacMetadataAnnotation>());
        Assert.Equal(Path.Combine(appBuilder.AppHostDirectory, "../../../../TestProject/TestProject.csproj"), annotation.DacpacPath);

        var dacpacPath = sqlProjectResource.GetDacpacPath();
        Assert.NotNull(dacpacPath);
        Assert.True(File.Exists(dacpacPath));
    }

    [Fact]
    public void PublishTo_AddsAnnotation()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var targetDatabase = appBuilder.AddSqlServer("sql").AddDatabase("test");
        appBuilder.AddSqlProject<TestProject>("MySqlProject")
                  .PublishTo(targetDatabase);
        
        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        var annotation = Assert.Single(sqlProjectResource.Annotations.OfType<TargetDatabaseResourceAnnotation>());
        Assert.Equal("test", annotation.TargetDatabaseResourceName);
    }

    [Fact]
    public void PublishTo_AddsLifecycleHook()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var targetDatabase = appBuilder.AddSqlServer("sql").AddDatabase("test");
        appBuilder.AddSqlProject<TestProject>("MySqlProject")
                  .PublishTo(targetDatabase);
        
        // Act
        using var app = appBuilder.Build();

        // Assert
        var lifecycleHooks = app.Services.GetServices<IDistributedApplicationLifecycleHook>();
        Assert.Single(lifecycleHooks.OfType<PublishSqlProjectLifecycleHook>());
    }
}
