using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MSBuild.Sdk.SqlProj.Aspire;

/// <summary>
/// The lifecycle hook that waits for all dependencies to be "running" before starting resources. 
/// 
/// See <see cref="WaitForSqlProjectAnnotation"/> for configuring annotations that waits.
/// </summary>
/// <param name="executionContext"></param>
/// <param name="resourceNotificationService"></param>
public class WaitForSqlProjectLifecycleHook(
    DistributedApplicationExecutionContext executionContext,
    ResourceNotificationService resourceNotificationService) :
    IDistributedApplicationLifecycleHook,
    IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Before starting resources we will loop over all resources to find those with <see cref="WaitForSqlProjectAnnotation"/> annotations.
    /// </summary>
    /// <param name="appModel"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        // We don't need to execute any of this logic in publish mode
        if (executionContext.IsPublishMode)
        {
            return Task.CompletedTask;
        }

        var waitingResources = ScanForWaitOnAnnotations(resourceNotificationService, appModel);

        _ = Task.Run(() => LongRunningWaiterImplementationAsync(resourceNotificationService, waitingResources), cancellationToken);

        return Task.CompletedTask;
    }

    private async Task LongRunningWaiterImplementationAsync(
        ResourceNotificationService resourceNotificationService,
        ConcurrentDictionary<IResource, ConcurrentDictionary<WaitForSqlProjectAnnotation, TaskCompletionSource>> waitingResources)
    {
        var stoppingToken = _cts.Token;

        // Watch for global resource state changes
        await foreach (var resourceEvent in resourceNotificationService.WatchAsync(stoppingToken))
        {
            if (waitingResources.TryGetValue(resourceEvent.Resource, out var pendingAnnotations))
            {
                foreach (var (waitOn, tcs) in pendingAnnotations)
                {
                    if (resourceEvent.Snapshot.State?.Text == "Published")
                    {
                        pendingAnnotations.TryRemove(waitOn, out _);
                        tcs.TrySetResult();
                    }

                    if (resourceEvent.Snapshot.State?.Text == "FailedToStart")
                    {
                        pendingAnnotations.TryRemove(waitOn, out _);
                        tcs.TrySetException(new Exception("Resource failed to start"));
                    }
                }
            }
        }
    }

    private static ConcurrentDictionary<IResource, ConcurrentDictionary<WaitForSqlProjectAnnotation, TaskCompletionSource>> ScanForWaitOnAnnotations(ResourceNotificationService resourceNotificationService, DistributedApplicationModel appModel)
    {
        // The global list of resources being waited on
        var waitingResources = new ConcurrentDictionary<IResource, ConcurrentDictionary<WaitForSqlProjectAnnotation, TaskCompletionSource>>();

        // For each resource, add an environment callback that waits for dependencies to be running
        foreach (var r in appModel.Resources)
        {
            var resourcesToWaitOn = r.Annotations.OfType<WaitForSqlProjectAnnotation>().ToLookup(a => a.Resource);

            if (resourcesToWaitOn.Count == 0)
            {
                continue;
            }

            // Abuse the environment callback to wait for dependencies to be running

            r.Annotations.Add(new EnvironmentCallbackAnnotation(async context =>
            {
                var dependencies = new List<Task>();

                // Find connection strings and endpoint references and get the resource they point to
                foreach (var group in resourcesToWaitOn)
                {
                    var resource = group.Key;

                    // REVIEW: This logic does not handle cycles in the dependency graph (that would result in a deadlock)

                    // Don't wait for yourself
                    if (resource != r && resource is not null)
                    {
                        var pendingAnnotations = waitingResources.GetOrAdd(resource, _ => new());

                        foreach (var waitOn in group)
                        {
                            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                            async Task Wait()
                            {
                                context.Logger?.LogInformation("Waiting for {Resource}.", waitOn.Resource.Name);

                                await tcs.Task;

                                context.Logger?.LogInformation("Waiting for {Resource} completed.", waitOn.Resource.Name);
                            }

                            pendingAnnotations[waitOn] = tcs;

                            dependencies.Add(Wait());
                        }
                    }
                }

                await resourceNotificationService.PublishUpdateAsync(r, s => s with
                {
                    State = new("Waiting", KnownResourceStateStyles.Info)
                });

                await Task.WhenAll(dependencies).WaitAsync(context.CancellationToken);
            }));
        }

        return waitingResources;
    }

    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        return default;
    }
}
