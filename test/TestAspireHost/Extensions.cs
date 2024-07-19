using Aspire.Hosting.Lifecycle;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

public static class Extensions
{
    public static IResourceBuilder<DatabaseProjectResource> AddDatabaseProject<TProject>(this IDistributedApplicationBuilder builder, string name)
        where TProject : IProjectMetadata, new()
    {
        MSBuildLocator.RegisterInstance(MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(
            instance => instance.Version).First());

        var resource = new DatabaseProjectResource(name);
        
        return builder.AddResource(resource)
                      .WithAnnotation(new TProject());
    }

    public static IProjectMetadata GetProjectMetadata(this DatabaseProjectResource resource)
    {
        return resource.Annotations.OfType<IProjectMetadata>().Single();
    }

    public static IResourceBuilder<DatabaseProjectResource> PublishTo(
        this IResourceBuilder<DatabaseProjectResource> builder, IResourceBuilder<SqlServerDatabaseResource> project)
    {
        builder.ApplicationBuilder.Services.TryAddLifecycleHook<DeployDatabaseProjectLifecycleHook>();
        builder.WithAnnotation(new TargetDatabaseResourceAnnotation(project.Resource.Name), ResourceAnnotationMutationBehavior.Replace);
        return builder;
    }
}

public sealed class DatabaseProjectResource(string name) : Resource(name)
{
    public string GetDacpacPath()
    {
        var projectPath = this.GetProjectMetadata().ProjectPath;
        var project = new Project(projectPath);
        return project.GetPropertyValue("TargetPath");
    }
}

public record TargetDatabaseResourceAnnotation(string TargetDatabaseResourceName) : IResourceAnnotation
{
}

public class DeployDatabaseProjectLifecycleHook : IDistributedApplicationLifecycleHook
{
    private readonly ResourceLoggerService _resourceLoggerService;
    private readonly ResourceNotificationService _resourceNotificationService;

    public DeployDatabaseProjectLifecycleHook(ResourceLoggerService resourceLoggerService, ResourceNotificationService resourceNotificationService)
    {
        _resourceLoggerService = resourceLoggerService ?? throw new ArgumentNullException(nameof(resourceLoggerService));
        _resourceNotificationService = resourceNotificationService ?? throw new ArgumentNullException(nameof(resourceNotificationService));
    }

    public async Task AfterResourcesCreatedAsync(DistributedApplicationModel application, CancellationToken cancellationToken)
    {
        foreach (var databaseProject in application.Resources.OfType<DatabaseProjectResource>())
        {
            var targetDatabaseResourceName = databaseProject.Annotations.OfType<TargetDatabaseResourceAnnotation>().Single().TargetDatabaseResourceName;
            var targetDatabaseResource = application.Resources.OfType<SqlServerDatabaseResource>().Single(r => r.Name == targetDatabaseResourceName);
            var connectionString = await targetDatabaseResource.ConnectionStringExpression.GetValueAsync(cancellationToken);

            var logger = _resourceLoggerService.GetLogger(databaseProject);

            await _resourceNotificationService.PublishUpdateAsync(databaseProject,
                state => state with { State = new ResourceStateSnapshot("Deploying", KnownResourceStateStyles.Info) });

            try
            {
                var dacServices = new DacServices(connectionString);
                dacServices.Message += (sender, args) => logger.LogInformation(args.Message.ToString());

                var dacpacPackage = DacPackage.Load(databaseProject.GetDacpacPath(), DacSchemaModelStorageType.Memory);
                dacServices.Deploy(dacpacPackage, targetDatabaseResource.Name, true, new DacDeployOptions(), cancellationToken);

                await _resourceNotificationService.PublishUpdateAsync(databaseProject,
                    state => state with { State = new ResourceStateSnapshot("Deployed", KnownResourceStateStyles.Success) });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to deploy database project.");

                await _resourceNotificationService.PublishUpdateAsync(databaseProject,
                    state => state with { State = new ResourceStateSnapshot("Failed", KnownResourceStateStyles.Error) });
            }
        }
    }
}
