using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Build.Locator;
using MSBuild.Sdk.SqlProj.Aspire;

namespace Aspire.Hosting;

public static class DataTierApplicationBuilderExtensions
{
    public static IResourceBuilder<DataTierApplicationResource> AddDataTierApplication<TProject>(this IDistributedApplicationBuilder builder, string name)
        where TProject : IProjectMetadata, new()
    {
        MSBuildLocator.RegisterInstance(MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(
            instance => instance.Version).First());

        var resource = new DataTierApplicationResource(name);
        
        return builder.AddResource(resource)
                      .WithAnnotation(new TProject());
    }

    public static IResourceBuilder<DataTierApplicationResource> PublishTo(
        this IResourceBuilder<DataTierApplicationResource> builder, IResourceBuilder<SqlServerDatabaseResource> project)
    {
        builder.ApplicationBuilder.Services.TryAddLifecycleHook<PublishDataTierApplicationLifecycleHook>();
        builder.WithAnnotation(new TargetDatabaseResourceAnnotation(project.Resource.Name), ResourceAnnotationMutationBehavior.Replace);
        return builder;
    }
}
