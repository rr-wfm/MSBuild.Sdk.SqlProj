using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

namespace MSBuild.Sdk.SqlProj.Aspire;

public class PublishSqlProjectLifecycleHook : IDistributedApplicationLifecycleHook
{
    private readonly IDacpacDeployer _deployer;
    private readonly ResourceLoggerService _resourceLoggerService;
    private readonly ResourceNotificationService _resourceNotificationService;

    public PublishSqlProjectLifecycleHook(IDacpacDeployer deployer, ResourceLoggerService resourceLoggerService,
        ResourceNotificationService resourceNotificationService)
    {
        _deployer = deployer ?? throw new ArgumentNullException(nameof(deployer));
        _resourceLoggerService = resourceLoggerService ?? throw new ArgumentNullException(nameof(resourceLoggerService));
        _resourceNotificationService = resourceNotificationService ?? throw new ArgumentNullException(nameof(resourceNotificationService));
    }

    public async Task AfterResourcesCreatedAsync(DistributedApplicationModel application, CancellationToken cancellationToken)
    {
        foreach (var sqlProject in application.Resources.OfType<SqlProjectResource>())
        {
            var logger = _resourceLoggerService.GetLogger(sqlProject);

            var dacpacPath = sqlProject.GetDacpacPath();
            if (!File.Exists(dacpacPath))
            {
                logger.LogError("SQL Server Database project package not found at path {DacpacPath}.", dacpacPath);
                await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                    state => state with { State = new ResourceStateSnapshot("Failed", KnownResourceStateStyles.Error) });
                continue;
            }

            var targetDatabaseResourceName = sqlProject.Annotations.OfType<TargetDatabaseResourceAnnotation>().Single().TargetDatabaseResourceName;
            var targetDatabaseResource = application.Resources.OfType<SqlServerDatabaseResource>().Single(r => r.Name == targetDatabaseResourceName);
            var connectionString = await targetDatabaseResource.ConnectionStringExpression.GetValueAsync(cancellationToken);
            if (connectionString == null)
            {
                logger.LogError("Failed to retrieve connection string for target database {TargetDatabaseResourceName}.", targetDatabaseResourceName);
                await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                    state => state with { State = new ResourceStateSnapshot("Failed", KnownResourceStateStyles.Error) });
                continue;
            }

            await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                state => state with { State = new ResourceStateSnapshot("Publishing", KnownResourceStateStyles.Info) });

            try
            {
                _deployer.Deploy(dacpacPath, connectionString, targetDatabaseResource.DatabaseName, logger, cancellationToken);

                await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                    state => state with { State = new ResourceStateSnapshot("Published", KnownResourceStateStyles.Success) });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish database project.");

                await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                    state => state with { State = new ResourceStateSnapshot("Failed", KnownResourceStateStyles.Error) });
            }
        }
    }
}
