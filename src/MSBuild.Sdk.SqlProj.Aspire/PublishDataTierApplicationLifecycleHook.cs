using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

namespace MSBuild.Sdk.SqlProj.Aspire;

public class PublishDataTierApplicationLifecycleHook : IDistributedApplicationLifecycleHook
{
    private readonly ResourceLoggerService _resourceLoggerService;
    private readonly ResourceNotificationService _resourceNotificationService;

    public PublishDataTierApplicationLifecycleHook(ResourceLoggerService resourceLoggerService, ResourceNotificationService resourceNotificationService)
    {
        _resourceLoggerService = resourceLoggerService ?? throw new ArgumentNullException(nameof(resourceLoggerService));
        _resourceNotificationService = resourceNotificationService ?? throw new ArgumentNullException(nameof(resourceNotificationService));
    }

    public async Task AfterResourcesCreatedAsync(DistributedApplicationModel application, CancellationToken cancellationToken)
    {
        foreach (var dataTierApplication in application.Resources.OfType<DataTierApplicationResource>())
        {
            var targetDatabaseResourceName = dataTierApplication.Annotations.OfType<TargetDatabaseResourceAnnotation>().Single().TargetDatabaseResourceName;
            var targetDatabaseResource = application.Resources.OfType<SqlServerDatabaseResource>().Single(r => r.Name == targetDatabaseResourceName);
            var connectionString = await targetDatabaseResource.ConnectionStringExpression.GetValueAsync(cancellationToken);

            var logger = _resourceLoggerService.GetLogger(dataTierApplication);

            await _resourceNotificationService.PublishUpdateAsync(dataTierApplication,
                state => state with { State = new ResourceStateSnapshot("Publishing", KnownResourceStateStyles.Info) });

            try
            {
                var dacServices = new DacServices(connectionString);
                dacServices.Message += (sender, args) => logger.LogInformation(args.Message.ToString());

                var dacpacPackage = DacPackage.Load(dataTierApplication.GetDacpacPath(), DacSchemaModelStorageType.Memory);
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
