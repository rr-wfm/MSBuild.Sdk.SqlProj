using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MSBuild.Sdk.SqlProj.Aspire;

namespace Aspire.Hosting;

public static class SqlProjectBuilderExtensions
{
    /// <summary>
    /// Adds a SQL Server Database Project resource to the application based on a referenced MSBuild.Sdk.SqlProj project.
    /// </summary>
    /// <typeparam name="TProject">Type that represents the project that produces the .dacpac file.</typeparam>
    /// <param name="builder">An <see cref="IDistributedApplicationBuilder"/> instance to add the SQL Server Database project to.</param>
    /// <param name="name">Name of the resource.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> AddSqlProject<TProject>(this IDistributedApplicationBuilder builder, string name)
        where TProject : IProjectMetadata, new()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        var resource = new SqlProjectResource(name);
        
        return builder.AddResource(resource)
                      .WithAnnotation(new TProject());
    }

    /// <summary>
    /// Adds a SQL Server Database Project resource to the application.
    /// </summary>
    /// <param name="builder">An <see cref="IDistributedApplicationBuilder"/> instance to add the SQL Server Database project to.</param>
    /// <param name="name">Name of the resource.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> AddSqlProject(this IDistributedApplicationBuilder builder, string name)
    {
        var resource = new SqlProjectResource(name);
        
        return builder.AddResource(resource);
    }

    /// <summary>
    /// Specifies the path to the .dacpac file.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project.</param>
    /// <param name="dacpacPath">Path to the .dacpac file.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> FromDacpac(this IResourceBuilder<SqlProjectResource> builder, string dacpacPath)
    {
        if (!Path.IsPathRooted(dacpacPath))
        {
            dacpacPath = Path.Combine(builder.ApplicationBuilder.AppHostDirectory, dacpacPath);
        }

        return builder.WithAnnotation(new DacpacMetadataAnnotation(dacpacPath));
    }

    /// <summary>
    /// Publishes the SQL Server Database project to the target <see cref="SqlServerDatabaseResource"/>.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the SQL Server Database project to publish.</param>
    /// <param name="target">An <see cref="IResourceBuilder{T}"/> representing the target <see cref="SqlServerDatabaseResource"/> to publish the SQL Server Database project to.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SqlProjectResource> PublishTo(
        this IResourceBuilder<SqlProjectResource> builder, IResourceBuilder<SqlServerDatabaseResource> target)
    {
        builder.ApplicationBuilder.Services.TryAddSingleton<IDacpacDeployer, DacpacDeployer>();
        builder.ApplicationBuilder.Services.TryAddLifecycleHook<PublishSqlProjectLifecycleHook>();
        builder.WithAnnotation(new TargetDatabaseResourceAnnotation(target.Resource.Name), ResourceAnnotationMutationBehavior.Replace);
        return builder;
    }
}
