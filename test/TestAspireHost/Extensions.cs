using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

public static class Extensions
{
    public static IResourceBuilder<DatabaseProjectResource> AddDatabaseProject<TProject>(this IDistributedApplicationBuilder builder, string name)
        where TProject : IProjectMetadata, new()
    {
        var resource = new DatabaseProjectResource(name);
        
        return builder.AddResource(resource)
                      .WithAnnotation(new TProject());
    }

    public static IProjectMetadata GetProjectMetadata(this DatabaseProjectResource resource)
    {
        return resource.Annotations.OfType<IProjectMetadata>().Single();
    }

    public static IResourceBuilder<SqlServerDatabaseResource> WithDatabaseProject(
        this IResourceBuilder<SqlServerDatabaseResource> builder, IResourceBuilder<DatabaseProjectResource> project)
    {
        builder.ApplicationBuilder.Services.TryAddLifecycleHook<DeployDatabaseProjectLifecycleHook>();
        builder.WithAnnotation(new DatabaseProjectAnnotation(project.Resource.Name), ResourceAnnotationMutationBehavior.Replace);
        return builder;
    }
}

public sealed class DatabaseProjectResource(string name) : Resource(name)
{
    public string GetDacpacPath()
    {
        var projectPath = this.GetProjectMetadata().ProjectPath;
        var projectDirectory = Path.GetDirectoryName(projectPath);
        if (projectDirectory == null)
        {
            throw new InvalidOperationException("Unable to determine parent directory of project path.");
        }

        var dacpacPath = Path.Combine(projectDirectory, "bin", "Debug", "netstandard2.0", Path.GetFileNameWithoutExtension(projectPath) + ".dacpac");
        if (!File.Exists(dacpacPath))
        {
            throw new InvalidOperationException($"Dacpac not found at '{dacpacPath}'.");
        }

        return dacpacPath;
    }
}

public record DatabaseProjectAnnotation(string DatabaseProjectResourceName) : IResourceAnnotation
{
}

public class DeployDatabaseProjectLifecycleHook : IDistributedApplicationLifecycleHook
{
    private readonly ResourceLoggerService _resourceLoggerService;

    public DeployDatabaseProjectLifecycleHook(ResourceLoggerService resourceLoggerService)
    {
        _resourceLoggerService = resourceLoggerService ?? throw new ArgumentNullException(nameof(resourceLoggerService));
    }

    public async Task AfterResourcesCreatedAsync(DistributedApplicationModel application, CancellationToken cancellationToken)
    {
        foreach (var database in application.Resources.OfType<SqlServerDatabaseResource>())
        {
            var connectionString = await database.ConnectionStringExpression.GetValueAsync(cancellationToken);
            foreach (var annotation in database.Annotations.OfType<DatabaseProjectAnnotation>())
            {
                var databaseProjectResource = application.Resources.OfType<DatabaseProjectResource>().Single(r => r.Name == annotation.DatabaseProjectResourceName);
                var logger = _resourceLoggerService.GetLogger(databaseProjectResource);

                var dacServices = new DacServices(connectionString);
                dacServices.Message += (sender, args) => logger.LogInformation(args.Message.ToString());

                var dacpacPackage = DacPackage.Load(databaseProjectResource.GetDacpacPath(), DacSchemaModelStorageType.Memory);
                dacServices.Deploy(dacpacPackage, database.Name, true, new DacDeployOptions(), cancellationToken);
            }
        }
    }
}
