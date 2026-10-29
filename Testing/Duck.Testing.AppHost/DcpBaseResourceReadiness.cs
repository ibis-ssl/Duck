using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Duck.Testing.AppHost;

internal sealed record DcpBaseResourceReadinessSnapshot(
    string ResourceId,
    string ResourceName,
    string Role,
    string RunId,
    string State,
    string? ContainerId,
    long Generation,
    bool IdentityVerified,
    bool NetworkVerified,
    bool OwnershipVerified,
    string? ObservedNetworkName,
    bool IsReady,
    string Reason);

internal sealed record DcpBaseResourceIdentityCandidate(
    IResource Resource,
    string ResourceId,
    string ResourceName,
    string Role,
    string RunId,
    string StackId,
    string State,
    string? ContainerId,
    long Generation,
    bool IdentityVerified,
    bool NetworkVerified,
    bool OwnershipVerified,
    string? ObservedNetworkName,
    bool ResourceKeyAmbiguous);

internal sealed record DcpBaseResourceOwnershipProof(
    IResource Resource,
    string ResourceId,
    string ContainerId,
    long Generation,
    string EngineId,
    string? NetworkName);

/// <summary>DCP通知から現在候補の識別子を追跡します。この観測だけでは所有とサービス準備完了を確定しません。</summary>
internal sealed class DcpBaseResourceReadinessRegistry
{
    private sealed class Entry(IResource resource, string role, string runId, string stackId)
    {
        internal readonly IResource Resource = resource;
        internal readonly string Role = role;
        internal readonly string RunId = runId;
        internal readonly string StackId = stackId;
        internal string? ResourceId;
        internal string State = "NotStarted";
        internal string? ContainerId;
        internal long Generation;
        internal bool IsReady;
        internal bool IdentityVerified;
        internal bool NetworkVerified;
        internal bool OwnershipVerified;
        internal string? ObservedNetworkName;
        internal bool ResourceKeyAmbiguous;
        internal bool HasObservedFullContainerId;
        internal bool IsInitialPlaceholder;
        internal string? AmbiguousActiveResourceId;
        internal readonly HashSet<string> RetiredResourceIds = new(StringComparer.Ordinal);
        internal readonly HashSet<string> PendingResourceIds = new(StringComparer.Ordinal);
        internal string Reason = "No current DCP container identity has been observed.";
    }

    private readonly object sync = new();
    private readonly Dictionary<IResource, Entry> entries = new(ReferenceEqualityComparer.Instance);

    internal DcpBaseResourceReadinessRegistry(
        IEnumerable<(IResource Resource, string Role, string RunId)> resources,
        string stackId = "")
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(stackId);
        foreach (var (resource, role, runId) in resources)
        {
            ArgumentNullException.ThrowIfNull(resource);
            ArgumentException.ThrowIfNullOrWhiteSpace(role);
            ArgumentException.ThrowIfNullOrWhiteSpace(runId);
            if (!entries.TryAdd(resource, new Entry(resource, role, runId, stackId)))
            {
                throw new InvalidOperationException($"DCP readiness resource '{resource.Name}' was registered more than once.");
            }
        }
    }

    internal void ObserveCurrent(RegisteredContainerRuntimeTarget target, string state)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        lock (sync)
        {
            var entry = FindEntry(target.RoleName);
            if (entry is null ||
                !OwnedContainerInspection.IsFullId(target.ContainerId) ||
                state is not ("Running" or "Starting") ||
                entry.RetiredResourceIds.Contains(target.ResourceId))
            {
                return;
            }

            if (entry.ResourceKeyAmbiguous)
            {
                if (!string.Equals(entry.ResourceId, target.ResourceId, StringComparison.Ordinal))
                {
                    entry.PendingResourceIds.Add(target.ResourceId);
                    return;
                }

                if (entry.AmbiguousActiveResourceId is not null)
                {
                    return;
                }

                entry.ResourceKeyAmbiguous = false;
                entry.PendingResourceIds.Clear();
            }

            if (entry.ResourceId is not null &&
                !string.Equals(entry.ResourceId, target.ResourceId, StringComparison.Ordinal))
            {
                if (IsTerminalState(entry.State))
                {
                    entry.RetiredResourceIds.Add(entry.ResourceId);
                }
                else if (entry.IsInitialPlaceholder && !entry.HasObservedFullContainerId)
                {
                    entry.RetiredResourceIds.Add(entry.ResourceId);
                    entry.ResourceId = target.ResourceId;
                    entry.State = state;
                    entry.ContainerId = target.ContainerId;
                    entry.IsInitialPlaceholder = false;
                    entry.HasObservedFullContainerId = true;
                    entry.IdentityVerified = false;
                    entry.NetworkVerified = false;
                    entry.OwnershipVerified = false;
                    entry.ObservedNetworkName = null;
                    entry.IsReady = false;
                    entry.Generation++;
                    entry.Reason = "The initial unresolved resource key was replaced by a runtime key with a full container identity; engine ownership and service readiness are unverified.";
                    return;
                }
                else
                {
                    entry.PendingResourceIds.Add(target.ResourceId);
                    entry.ResourceKeyAmbiguous = true;
                    entry.AmbiguousActiveResourceId = entry.ResourceId;
                    entry.ContainerId = null;
                    entry.IdentityVerified = false;
                    entry.NetworkVerified = false;
                    entry.OwnershipVerified = false;
                    entry.ObservedNetworkName = null;
                    entry.IsReady = false;
                    entry.State = "Unknown";
                    entry.Reason = "Multiple resource keys were observed before the previous key stopped; the current identity is ambiguous.";
                    entry.Generation++;
                    return;
                }
            }

            var identityChanged = entry.ResourceId != target.ResourceId || entry.ContainerId != target.ContainerId;
            if (identityChanged)
            {
                entry.Generation++;
                entry.IsReady = false;
                entry.IdentityVerified = false;
                entry.NetworkVerified = false;
                entry.OwnershipVerified = false;
                entry.ObservedNetworkName = null;
                entry.Reason = "A new current DCP identity candidate was observed; engine ownership and service readiness are unverified.";
            }

            entry.ResourceId = target.ResourceId;
            entry.ContainerId = target.ContainerId;
            entry.State = state;
            entry.HasObservedFullContainerId = true;
            entry.IsInitialPlaceholder = false;
        }
    }

    internal string? ObserveUnavailable(IResource resource, string resourceId, string state)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        lock (sync)
        {
            if (!entries.TryGetValue(resource, out var entry))
            {
                return null;
            }

            if (entry.RetiredResourceIds.Contains(resourceId))
            {
                return null;
            }

            if (entry.ResourceId is not null &&
                !string.Equals(entry.ResourceId, resourceId, StringComparison.Ordinal))
            {
                if (!IsTerminalState(entry.State))
                {
                    if (entry.IsInitialPlaceholder && !entry.HasObservedFullContainerId)
                    {
                        entry.RetiredResourceIds.Add(entry.ResourceId);
                        entry.ResourceId = resourceId;
                        entry.State = state;
                        entry.ContainerId = null;
                        entry.IsInitialPlaceholder = false;
                        entry.IsReady = false;
                        entry.IdentityVerified = false;
                        entry.NetworkVerified = false;
                        entry.OwnershipVerified = false;
                        entry.ObservedNetworkName = null;
                        entry.Generation++;
                        entry.Reason = "The initial unresolved resource key was replaced by a runtime resource key; awaiting its current full container identity.";
                        return null;
                    }

                    entry.PendingResourceIds.Add(resourceId);
                    entry.ResourceKeyAmbiguous = true;
                    entry.AmbiguousActiveResourceId = entry.ResourceId;
                    entry.ContainerId = null;
                    entry.IsReady = false;
                    entry.IdentityVerified = false;
                    entry.NetworkVerified = false;
                    entry.OwnershipVerified = false;
                    entry.ObservedNetworkName = null;
                    entry.State = "Unknown";
                    entry.Reason = "Multiple resource keys were observed before the previous key stopped; the current identity is ambiguous.";
                    entry.Generation++;
                    return null;
                }

                entry.RetiredResourceIds.Add(entry.ResourceId);
            }

            if (entry.ResourceId != resourceId || entry.State != state || entry.ContainerId is not null || entry.IsReady)
            {
                entry.Generation++;
            }
            if (entry.ResourceId is null && !entry.HasObservedFullContainerId && state == "Starting")
            {
                entry.IsInitialPlaceholder = true;
            }
            entry.ResourceId = resourceId;
            entry.State = state;
            entry.ContainerId = null;
            entry.IsReady = false;
            entry.IdentityVerified = false;
            entry.NetworkVerified = false;
            entry.OwnershipVerified = false;
            entry.ObservedNetworkName = null;
            entry.Reason = "No current full container identity was projected; engine ownership and service readiness are unverified.";

            if (IsTerminalState(state) && entry.ResourceKeyAmbiguous && entry.PendingResourceIds.Count == 1)
            {
                entry.RetiredResourceIds.Add(resourceId);
                var pendingResourceId = entry.PendingResourceIds.Single();
                entry.PendingResourceIds.Clear();
                entry.ResourceId = pendingResourceId;
                entry.State = "Unknown";
                entry.ResourceKeyAmbiguous = true;
                entry.AmbiguousActiveResourceId = null;
                entry.Reason = "A replacement resource key was identified after the prior key stopped; awaiting its current full container identity.";
                entry.Generation++;
                return pendingResourceId;
            }

            return null;
        }
    }

    internal DcpBaseResourceReadinessSnapshot? Get(IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        lock (sync)
        {
            return entries.TryGetValue(resource, out var entry)
                ? new DcpBaseResourceReadinessSnapshot(
                    entry.ResourceKeyAmbiguous ? string.Empty : entry.ResourceId ?? string.Empty,
                    entry.Resource.Name,
                    entry.Role,
                    entry.RunId,
                    entry.State,
                    entry.ContainerId,
                    entry.Generation,
                    entry.IdentityVerified,
                    entry.NetworkVerified,
                    entry.OwnershipVerified,
                    entry.ObservedNetworkName,
                    entry.IsReady,
                    entry.Reason)
                : null;
        }
    }

    internal IReadOnlyList<DcpBaseResourceIdentityCandidate> GetCandidates()
    {
        lock (sync)
        {
            return entries.Values.Select(entry => new DcpBaseResourceIdentityCandidate(
                entry.Resource,
                entry.ResourceKeyAmbiguous ? string.Empty : entry.ResourceId ?? string.Empty,
                entry.Resource.Name,
                entry.Role,
                entry.RunId,
                entry.StackId,
                entry.State,
                entry.ResourceKeyAmbiguous ? null : entry.ContainerId,
                entry.Generation,
                entry.IdentityVerified,
                entry.NetworkVerified,
                entry.OwnershipVerified,
                entry.ObservedNetworkName,
                entry.ResourceKeyAmbiguous)).ToArray();
        }
    }

    internal bool TryMarkIdentityVerified(
        IReadOnlyList<DcpBaseResourceOwnershipProof> proofs,
        bool sharedStandardDcpNetworkVerified)
    {
        ArgumentNullException.ThrowIfNull(proofs);
        lock (sync)
        {
            if (proofs.Count != entries.Count ||
                proofs.Select(proof => proof.Resource).Distinct(ReferenceEqualityComparer.Instance).Count() != entries.Count ||
                proofs.Select(proof => proof.EngineId).Distinct(StringComparer.Ordinal).Count() != 1 ||
                sharedStandardDcpNetworkVerified &&
                    (proofs.Any(proof => string.IsNullOrWhiteSpace(proof.NetworkName)) ||
                     proofs.Select(proof => proof.NetworkName).Distinct(StringComparer.Ordinal).Count() != 1))
            {
                return false;
            }

            foreach (var proof in proofs)
            {
                if (!entries.TryGetValue(proof.Resource, out var entry) ||
                    entry.ResourceKeyAmbiguous ||
                    entry.ResourceId != proof.ResourceId ||
                    entry.ContainerId != proof.ContainerId ||
                    entry.Generation != proof.Generation ||
                    entry.State != "Running" ||
                    !OwnedContainerInspection.IsFullId(entry.ContainerId))
                {
                    return false;
                }
            }

            foreach (var proof in proofs)
            {
                var entry = entries[proof.Resource];
                entry.IdentityVerified = true;
                entry.NetworkVerified = sharedStandardDcpNetworkVerified;
                entry.OwnershipVerified = sharedStandardDcpNetworkVerified;
                entry.ObservedNetworkName = proof.NetworkName;
                entry.Reason = sharedStandardDcpNetworkVerified
                    ? "The current DCP-owned container identity and common default-network membership were verified; service readiness remains unverified."
                    : "Docker Engine and container ownership labels were verified; common default-network membership and service readiness remain unverified.";
            }

            return true;
        }
    }

    private Entry? FindEntry(string roleName) =>
        entries.Values.SingleOrDefault(entry => entry.Resource.Name == roleName);

    internal static bool IsTerminalState(string state) => state is
        "Exited" or "Finished" or "Stopped" or "Failed" or "NotStarted";
}

internal sealed class DcpBaseResourceReadinessEventConsumer(DcpBaseResourceReadinessRegistry registry)
{
    internal void Observe(
        ResourceEvent observation,
        ResourceEvent current,
        string role,
        Func<string, ResourceEvent?> getCurrentByResourceId)
    {
        if (!ReferenceEquals(observation.Resource, current.Resource) ||
            observation.ResourceId != current.ResourceId)
        {
            return;
        }

        var state = current.Snapshot.State?.Text;
        if (string.IsNullOrWhiteSpace(state))
        {
            registry.ObserveUnavailable(current.Resource, current.ResourceId, "Unknown");
            return;
        }

        if (DcpBaseResourceReadinessRegistry.IsTerminalState(state))
        {
            var observedTerminal = OwnedResourceObservationProjection.TryProject(observation);
            var currentTerminal = OwnedResourceObservationProjection.TryProject(current);
            if (observedTerminal is null || currentTerminal is null ||
                observedTerminal.ResourceId != currentTerminal.ResourceId ||
                observedTerminal.State != currentTerminal.State ||
                observedTerminal.ContainerId != currentTerminal.ContainerId)
            {
                return;
            }

            var pendingId = registry.ObserveUnavailable(current.Resource, current.ResourceId, state);
            if (pendingId is not null)
            {
                var pending = getCurrentByResourceId(pendingId);
                if (pending is not null && pending.ResourceId == pendingId)
                {
                    Observe(pending, pending, role, getCurrentByResourceId);
                }
            }
            return;
        }

        if (!RegisteredContainerEventProjection.TryProject(
                current.Resource,
                observation.ResourceId,
                role,
                observation,
                current,
                out var target) || target is null)
        {
            registry.ObserveUnavailable(current.Resource, observation.ResourceId, state);
            return;
        }

        registry.ObserveCurrent(target, state);
    }
}

internal sealed class DcpBaseResourceReadinessObserver(
    ResourceNotificationService notifications,
    DcpBaseResourceReadinessEventConsumer consumer,
    DcpBaseResourceOwnershipEventConsumer ownershipConsumer,
    IReadOnlyDictionary<IResource, string> roles) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var observation in notifications.WatchAsync(stoppingToken).ConfigureAwait(false))
        {
            if (!roles.TryGetValue(observation.Resource, out var role) ||
                !notifications.TryGetCurrentState(observation.ResourceId, out var current))
            {
                continue;
            }

            consumer.Observe(observation, current, role, resourceId =>
                notifications.TryGetCurrentState(resourceId, out var state) ? state : null);
            await ownershipConsumer.VerifyCurrentAsync(
                resourceId => notifications.TryGetCurrentState(resourceId, out var state) ? state : null,
                stoppingToken).ConfigureAwait(false);
        }
    }
}

internal static class DcpBaseResourceReadinessRegistration
{
    internal static void Add(
        IDistributedApplicationBuilder builder,
        string runId,
        string stackId,
        string? dockerContextName,
        string? expectedEngineId,
        string dockerExecutable,
        params (IResource Resource, string Role)[] resources)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dockerExecutable);
        ArgumentNullException.ThrowIfNull(resources);
        if (resources.Length == 0)
        {
            throw new ArgumentException("At least one DCP container is required for readiness tracking.", nameof(resources));
        }

        var entries = resources.Select(item => (item.Resource, item.Role, runId)).ToArray();
        var registry = new DcpBaseResourceReadinessRegistry(entries, stackId);
        var eventConsumer = new DcpBaseResourceReadinessEventConsumer(registry);
        var ownershipConsumer = new DcpBaseResourceOwnershipEventConsumer(
            registry,
            new DockerCliCommandRunner(dockerExecutable),
            dockerContextName,
            expectedEngineId);
        var roleMap = new Dictionary<IResource, string>(ReferenceEqualityComparer.Instance);
        foreach (var (resource, role) in resources)
        {
            roleMap.Add(resource, role);
        }
        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton(eventConsumer);
        builder.Services.AddSingleton(ownershipConsumer);
        builder.Services.AddSingleton<IHostedService>(services => new DcpBaseResourceReadinessObserver(
            services.GetRequiredService<ResourceNotificationService>(),
            eventConsumer,
            ownershipConsumer,
            roleMap));

        foreach (var (resource, role) in resources)
        {
            var checkName = $"dcp-{role}-service-ready";
            builder.Services.AddHealthChecks().AddCheck(
                checkName,
                new DcpBaseResourceReadinessHealthCheck(registry, resource));
            if (resource is not ContainerResource container)
            {
                throw new InvalidOperationException($"DCP readiness resource '{resource.Name}' is not a ContainerResource.");
            }
            builder.CreateResourceBuilder(container).WithHealthCheck(checkName);
        }
    }
}

internal sealed class DcpBaseResourceReadinessHealthCheck(
    DcpBaseResourceReadinessRegistry registry,
    IResource resource) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = registry.Get(resource);
        if (snapshot is null || !snapshot.IsReady)
        {
            var reason = snapshot?.Reason ?? "The resource is not registered with the DCP readiness observer.";
            return Task.FromResult(HealthCheckResult.Unhealthy(reason));
        }

        return Task.FromResult(HealthCheckResult.Healthy(snapshot.Reason));
    }
}
