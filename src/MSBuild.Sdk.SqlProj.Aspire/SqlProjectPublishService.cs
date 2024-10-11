using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace MSBuild.Sdk.SqlProj.Aspire;

public class SqlProjectPublishService
{
    private readonly IDacpacDeployer _deployer;
    private readonly ResourceLoggerService _resourceLoggerService;
    private readonly ResourceNotificationService _resourceNotificationService;

    public SqlProjectPublishService(IDacpacDeployer deployer, ResourceLoggerService resourceLoggerService,
        ResourceNotificationService resourceNotificationService)
    {
        _deployer = deployer ?? throw new ArgumentNullException(nameof(deployer));
        _resourceLoggerService = resourceLoggerService ?? throw new ArgumentNullException(nameof(resourceLoggerService));
        _resourceNotificationService = resourceNotificationService ?? throw new ArgumentNullException(nameof(resourceNotificationService));
    }

    public async Task PublishSqlProject(SqlProjectResource sqlProject, SqlServerDatabaseResource target, CancellationToken cancellationToken)
    {
        var logger = _resourceLoggerService.GetLogger(sqlProject);

        var dacpacPath = sqlProject.GetDacpacPath();
        if (!File.Exists(dacpacPath))
        {
            logger.LogError("SQL Server Database project package not found at path {DacpacPath}.", dacpacPath);
            await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                state => state with { State = new ResourceStateSnapshot("Failed", KnownResourceStateStyles.Error) });
            return;
        }

        var connectionString = await target.ConnectionStringExpression.GetValueAsync(cancellationToken);
        if (connectionString == null)
        {
            logger.LogError("Failed to retrieve connection string for target database {TargetDatabaseResourceName}.", target.Name);
            await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                state => state with { State = new ResourceStateSnapshot("Failed", KnownResourceStateStyles.Error) });
            return;
        }

        await _resourceNotificationService.PublishUpdateAsync(sqlProject,
            state => state with { State = new ResourceStateSnapshot("Publishing", KnownResourceStateStyles.Info) });

        try
        {
            _deployer.Deploy(dacpacPath, connectionString, target.DatabaseName, logger, cancellationToken);

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
