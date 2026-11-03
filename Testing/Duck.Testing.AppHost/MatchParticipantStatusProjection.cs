using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;

namespace Duck.Testing.AppHost;

internal sealed record MatchParticipantResourceStatus(
    string ResourceId,
    string? State,
    string? HealthStatus)
{
    public bool IsReady => string.Equals(State, "Running", StringComparison.Ordinal) &&
        !string.Equals(HealthStatus, "Unhealthy", StringComparison.OrdinalIgnoreCase);
}

internal sealed record MatchParticipantStatusSnapshot(
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<MatchParticipantResourceStatus> Resources)
{
    public bool IsReady => Resources.Count > 0 && Resources.All(resource => resource.IsReady);
}

internal sealed class MatchParticipantResourceIdMap
{
    private readonly object gate = new();
    private readonly Dictionary<IResource, string?> resourceIds = new(ReferenceEqualityComparer.Instance);

    internal MatchParticipantResourceIdMap(IEnumerable<IResource> resources)
    {
        foreach (var resource in resources)
            resourceIds.Add(resource, null);
    }

    internal bool Observe(ResourceEvent observation)
    {
        lock (gate)
        {
            if (!resourceIds.ContainsKey(observation.Resource))
                return false;
            resourceIds[observation.Resource] = observation.ResourceId;
            return true;
        }
    }

    internal string? GetCurrentId(IResource resource)
    {
        lock (gate)
            return resourceIds.TryGetValue(resource, out var resourceId) ? resourceId : null;
    }
}

/// <summary>Match controllerが共有結果mountから現在のAppHost参加資源状態を読めるよう投影する。</summary>
internal sealed class MatchParticipantStatusProjection(
    ResourceNotificationService notifications,
    IReadOnlyList<IResource> resources,
    string path,
    TimeProvider clock) : BackgroundService, IHostedLifecycleService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MatchParticipantResourceIdMap resourceIds = new(resources);
    private int observerStarted;

    public Task StartingAsync(CancellationToken cancellationToken) => StartObserverAsync(cancellationToken);
    public override Task StartAsync(CancellationToken cancellationToken) => StartObserverAsync(cancellationToken);
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private Task StartObserverAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref observerStarted, 1, 0) != 0)
            return Task.CompletedTask;
        try
        {
            return base.StartAsync(cancellationToken);
        }
        catch
        {
            Volatile.Write(ref observerStarted, 0);
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException(
            "Match participant status path must have a parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";
        var observer = ObserveResourceIdsAsync(stoppingToken);
        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();
            if (observer.IsCompleted)
                await observer.ConfigureAwait(false);
            var statuses = resources.Select(resource =>
            {
                var resourceId = resourceIds.GetCurrentId(resource);
                ResourceEvent? currentState = null;
                var found = resourceId is not null && notifications.TryGetCurrentState(resourceId, out currentState);
                var snapshot = found && currentState is not null && ReferenceEquals(currentState.Resource, resource)
                    ? currentState.Snapshot
                    : null;
                return new MatchParticipantResourceStatus(
                    resource.Name,
                    snapshot?.State?.Text,
                    snapshot?.HealthStatus?.ToString());
            }).ToArray();
            var content = JsonSerializer.Serialize(
                new MatchParticipantStatusSnapshot(clock.GetUtcNow(), statuses), JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, content, stoppingToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
            await Task.Delay(TimeSpan.FromMilliseconds(250), clock, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ObserveResourceIdsAsync(CancellationToken stoppingToken)
    {
        await foreach (var observation in notifications.WatchAsync(stoppingToken).ConfigureAwait(false))
            resourceIds.Observe(observation);
    }
}
