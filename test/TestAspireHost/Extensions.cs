using Aspire.Hosting.Lifecycle;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

public static class Extensions
{
    public static IResourceBuilder<SqlServerDatabaseResource> WithDatabaseProject(
        this IResourceBuilder<SqlServerDatabaseResource> builder, IResourceBuilder<ProjectResource> project)
    {
        builder.ApplicationBuilder.Services.TryAddLifecycleHook<DeployDatabaseProjectLifecycleHook>();
        builder.WithAnnotation(new DatabaseProjectAnnotation
        {
            Project = project
        }, ResourceAnnotationMutationBehavior.Replace);
        return builder;
    }
}

public class DatabaseProjectAnnotation : IResourceAnnotation
{
    public IResourceBuilder<ProjectResource> Project { get; set; }
}

public class DeployDatabaseProjectLifecycleHook : IDistributedApplicationLifecycleHook
{
    private readonly ILogger<DeployDatabaseProjectLifecycleHook> _logger;

    public DeployDatabaseProjectLifecycleHook(ILogger<DeployDatabaseProjectLifecycleHook> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task AfterResourcesCreatedAsync(DistributedApplicationModel application, CancellationToken cancellationToken)
    {
        foreach (var database in application.Resources.OfType<SqlServerDatabaseResource>())
        {
            var connectionString = await database.ConnectionStringExpression.GetValueAsync(cancellationToken);
            foreach (var annotation in database.Annotations.OfType<DatabaseProjectAnnotation>())
            {
                var dacServices = new DacServices(connectionString);
                dacServices.Message += (sender, args) => _logger.LogInformation(args.Message.ToString());

                var projectPath = annotation.Project.Resource.GetProjectMetadata().ProjectPath;
                var dacpacPath = Path.Combine(Path.GetDirectoryName(projectPath), "bin", "Debug", "netstandard2.0", Path.GetFileNameWithoutExtension(projectPath) + ".dacpac");
                var dacpacPackage = DacPackage.Load(dacpacPath, DacSchemaModelStorageType.Memory);
                dacServices.Deploy(dacpacPackage, database.Name, true, new DacDeployOptions(), cancellationToken);
            }
        }
    }
}
