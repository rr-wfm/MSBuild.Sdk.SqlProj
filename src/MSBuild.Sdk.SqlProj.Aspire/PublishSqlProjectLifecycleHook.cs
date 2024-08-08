using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace MSBuild.Sdk.SqlProj.Aspire;

public sealed class PublishSqlProjectLifecycleHook 
    : IDistributedApplicationLifecycleHook, IAsyncDisposable
{
    private readonly IDacpacDeployer _deployer;
    private readonly ResourceLoggerService _resourceLoggerService;
    private readonly ResourceNotificationService _resourceNotificationService;
    private readonly CancellationTokenSource _tokenSource = new();

    public PublishSqlProjectLifecycleHook(
        IDacpacDeployer deployer, 
        ResourceLoggerService resourceLoggerService,
        ResourceNotificationService resourceNotificationService)
    {
        _deployer = deployer ?? throw new ArgumentNullException(nameof(deployer));
        _resourceLoggerService = resourceLoggerService ?? throw new ArgumentNullException(nameof(resourceLoggerService));
        _resourceNotificationService = resourceNotificationService ?? throw new ArgumentNullException(nameof(resourceNotificationService));
    }

    public Task BeforeStartAsync(DistributedApplicationModel application, CancellationToken cancellationToken = default)
    {
        foreach (var resource in application.Resources.OfType<SqlProjectResource>())
        {
            StartDacpacDeployment(resource, application, _tokenSource.Token);
        }

        return Task.CompletedTask;
    }

    private void StartDacpacDeployment(SqlProjectResource sqlProject, DistributedApplicationModel application, CancellationToken cancellationToken)
    {
        var logger = _resourceLoggerService.GetLogger(sqlProject);

        _ = Task.Run(async () =>
        {
            int retryCount = 10;
            int delayInMillisecodns = 1000;

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    var dacpacPath = sqlProject.GetDacpacPath();
                    if (!File.Exists(dacpacPath))
                    {
                        logger.LogError("SQL Server Database project package not found at path {DacpacPath}.", dacpacPath);

                        await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                            state => state with { State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error) });
                        break;
                    }

                    var targetDatabaseResourceName = sqlProject.Annotations.OfType<TargetDatabaseResourceAnnotation>().Single().TargetDatabaseResourceName;
                    var targetDatabaseResource = application.Resources.OfType<SqlServerDatabaseResource>().Single(r => r.Name == targetDatabaseResourceName);
                    var targetServerResource = targetDatabaseResource.Parent;
                    var connectionString = await targetDatabaseResource.ConnectionStringExpression.GetValueAsync(cancellationToken);

                    var serverReadyAnnotation = await CheckServerReady(sqlProject, logger, targetDatabaseResourceName, targetServerResource, connectionString);

                    await Publish(sqlProject, logger, dacpacPath, targetDatabaseResource, connectionString, serverReadyAnnotation, cancellationToken);
                }
                catch
                {
                    if (i == retryCount - 1)
                    {
                        await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                            state => state with { State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error) });
                    }
                    else
                    {
                        await Task.Delay(delayInMillisecodns);
                        delayInMillisecodns *= 2; // Exponential backoff
                    }
                }
            }
        }, cancellationToken);
    }

    private async Task<ServerReadyAnnotation> CheckServerReady(SqlProjectResource sqlProject, ILogger logger, string targetDatabaseResourceName, SqlServerServerResource targetServerResource, string? connectionString)
    {
        if (connectionString == null)
        {
            logger.LogError($"Failed to retrieve connection string for target database {targetDatabaseResourceName}.", targetDatabaseResourceName);

            throw new InvalidOperationException($"Failed to retrieve connection string for target database {targetDatabaseResourceName}.");
        }

        if (!sqlProject.TryGetLastAnnotation(out ServerReadyAnnotation? serverReadyAnnotation))
        {
            sqlProject.Annotations.Add(serverReadyAnnotation = new ServerReadyAnnotation());
        }

        if (!serverReadyAnnotation.Connected)
        {
            await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                state => state with { State = new ResourceStateSnapshot($"Checking server readiness", KnownResourceStateStyles.Info) });

            try
            {
                var serverConnectionString = await targetServerResource.ConnectionStringExpression.GetValueAsync(CancellationToken.None);

                using var conn = new SqlConnection(serverConnectionString);
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                await cmd.ExecuteScalarAsync();

                await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                    state => state with { State = new ResourceStateSnapshot($"Server ready", KnownResourceStateStyles.Info) });

                serverReadyAnnotation.Connected = true;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is SocketException || ex is SqlException)
            {
                serverReadyAnnotation.Attempt++;
                logger.LogWarning(ex.Message, "Transient error, probably due to endpoints or SQL Server not ready yet. Backing off to retry.");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to contact server");

                await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                    state => state with { State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error) });

                throw;
            }
        }

        return serverReadyAnnotation;
    }

    private async Task Publish(SqlProjectResource sqlProject, ILogger logger, string dacpacPath, SqlServerDatabaseResource targetDatabaseResource, string? connectionString, ServerReadyAnnotation serverReadyAnnotation, CancellationToken cancellationToken)
    {
        try
        {
            if (!serverReadyAnnotation.Published && serverReadyAnnotation.Connected)
            {
                await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                    state => state with { State = new ResourceStateSnapshot("Publishing", KnownResourceStateStyles.Info) });

                _deployer.Deploy(dacpacPath, connectionString, targetDatabaseResource.DatabaseName, logger, cancellationToken);

                await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                    state => state with { State = new ResourceStateSnapshot("Published", KnownResourceStateStyles.Success) });

                serverReadyAnnotation.Published = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish database project.");

            await _resourceNotificationService.PublishUpdateAsync(sqlProject,
                state => state with { State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error) });
        }
    }


    public ValueTask DisposeAsync()
    {
        _tokenSource.Cancel();
        return default;
    }
}
