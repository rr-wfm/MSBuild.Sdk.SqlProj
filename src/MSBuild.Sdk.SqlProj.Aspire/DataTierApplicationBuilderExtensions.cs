using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Build.Locator;
using MSBuild.Sdk.SqlProj.Aspire;

namespace Aspire.Hosting;

public static class DataTierApplicationBuilderExtensions
{
    /// <summary>
    /// Adds a data-tier application resource to the application based on a referenced MSBuild.Sdk.SqlProj project.
    /// </summary>
    /// <typeparam name="TProject">Type that represents the project that produces the data-tier application package (.dacpac file).</typeparam>
    /// <param name="builder">An <see cref="IDistributedApplicationBuilder"/> instance to add the data-tier application to.</param>
    /// <param name="name">Name of the resource.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<DataTierApplicationResource> AddDataTierApplication<TProject>(this IDistributedApplicationBuilder builder, string name)
        where TProject : IProjectMetadata, new()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterInstance(MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(
                instance => instance.Version).First());
        }

        var resource = new DataTierApplicationResource(name);
        
        return builder.AddResource(resource)
                      .WithAnnotation(new TProject());
    }

    /// <summary>
    /// Adds a data-tier application resource to the application based on a data-tier application package file (.dacpac).
    /// </summary>
    /// <param name="builder">An <see cref="IDistributedApplicationBuilder"/> instance to add the data-tier application to.</param>
    /// <param name="name">Name of the resource.</param>
    /// <param name="dacpacPath">Path to a data-tier application package file (.dacpac).</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<DataTierApplicationResource> AddDataTierApplication(this IDistributedApplicationBuilder builder, string name, string dacpacPath)
    {
        var resource = new DataTierApplicationResource(name);

        if (!Path.IsPathRooted(dacpacPath))
        {
            dacpacPath = Path.Combine(builder.AppHostDirectory, dacpacPath);
        }
        
        return builder.AddResource(resource)
                      .WithAnnotation(new DacpacMetadataAnnotation(dacpacPath));
    }

    /// <summary>
    /// Publishes the data-tier application to the target <see cref="SqlServerDatabaseResource"/>.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the data-tier application to publish.</param>
    /// <param name="target">An <see cref="IResourceBuilder{T}"/> representing the target <see cref="SqlServerDatabaseResource"/> to publish the data-tier application to.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<DataTierApplicationResource> PublishTo(
        this IResourceBuilder<DataTierApplicationResource> builder, IResourceBuilder<SqlServerDatabaseResource> target)
    {
        builder.ApplicationBuilder.Services.TryAddLifecycleHook<PublishDataTierApplicationLifecycleHook>();
        builder.WithAnnotation(new TargetDatabaseResourceAnnotation(target.Resource.Name), ResourceAnnotationMutationBehavior.Replace);
        return builder;
    }
}
