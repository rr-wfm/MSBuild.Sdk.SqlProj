using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

namespace MSBuild.Sdk.SqlProj.Aspire;

public class PublishDataTierApplicationLifecycleHook : IDistributedApplicationLifecycleHook
{
    private readonly ResourceLoggerService _resourceLoggerService;
    private readonly ResourceNotificationService _resourceNotificationService;

    public PublishDataTierApplicationLifecycleHook(ResourceLoggerService resourceLoggerService,
        ResourceNotificationService resourceNotificationService, DistributedApplicationOptions options)
    {
        _resourceLoggerService = resourceLoggerService ?? throw new ArgumentNullException(nameof(resourceLoggerService));
        _resourceNotificationService = resourceNotificationService ?? throw new ArgumentNullException(nameof(resourceNotificationService));
    }

    public async Task AfterResourcesCreatedAsync(DistributedApplicationModel application, CancellationToken cancellationToken)
    {
        foreach (var dataTierApplication in application.Resources.OfType<DataTierApplicationResource>())
        {
            var logger = _resourceLoggerService.GetLogger(dataTierApplication);

            var dacpacPath = dataTierApplication.GetDacpacPath();
            if (!File.Exists(dacpacPath))
            {
                logger.LogError("Data-tier application package not found at path {DacpacPath}.", dacpacPath);
                await _resourceNotificationService.PublishUpdateAsync(dataTierApplication,
                    state => state with { State = new ResourceStateSnapshot("Failed", KnownResourceStateStyles.Error) });
                continue;
            }

            var targetDatabaseResourceName = dataTierApplication.Annotations.OfType<TargetDatabaseResourceAnnotation>().Single().TargetDatabaseResourceName;
            var targetDatabaseResource = application.Resources.OfType<SqlServerDatabaseResource>().Single(r => r.Name == targetDatabaseResourceName);
            var connectionString = await targetDatabaseResource.ConnectionStringExpression.GetValueAsync(cancellationToken);

            await _resourceNotificationService.PublishUpdateAsync(dataTierApplication,
                state => state with { State = new ResourceStateSnapshot("Publishing", KnownResourceStateStyles.Info) });

            try
            {
                var dacServices = new DacServices(connectionString);
                dacServices.Message += (sender, args) => logger.LogInformation(args.Message.ToString());

                var dacpacPackage = DacPackage.Load(dacpacPath, DacSchemaModelStorageType.Memory);
                dacServices.Deploy(dacpacPackage, targetDatabaseResource.Name, true, new DacDeployOptions(), cancellationToken);

                await _resourceNotificationService.PublishUpdateAsync(dataTierApplication,
                    state => state with { State = new ResourceStateSnapshot("Published", KnownResourceStateStyles.Success) });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish database project.");

                await _resourceNotificationService.PublishUpdateAsync(dataTierApplication,
                    state => state with { State = new ResourceStateSnapshot("Failed", KnownResourceStateStyles.Error) });
            }
        }
    }
}
