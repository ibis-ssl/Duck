using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Tracker.RuntimeHost;

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
    bool ResourceKeyAmbiguous,
    string? ObservedNetworkAddress = null);

internal sealed record DcpBaseResourceOwnershipProof(
    IResource Resource,
    string ResourceId,
    string ContainerId,
    long Generation,
    string EngineId,
    string? NetworkName,
    string? NetworkAddress = null);

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
        internal string? ObservedNetworkAddress;
        internal bool ResourceKeyAmbiguous;
        internal bool HasObservedFullContainerId;
        internal bool IsInitialPlaceholder;
        internal long VisionGeneration = -1;
        internal ResourceDiagnosticIdentity? VisionIdentity;
        internal long VisionReceiverEpoch;
        internal long VisionCount;
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
                    entry.ObservedNetworkAddress = null;
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
                    entry.ObservedNetworkAddress = null;
                    entry.IsReady = false;
                    entry.State = "Unknown";
                    entry.Reason = "Multiple resource keys were observed before the previous key stopped; the current identity is ambiguous.";
                    entry.Generation++;
                    return;
                }
            }

            var identityChanged = entry.ResourceId != target.ResourceId || entry.ContainerId != target.ContainerId;
            var stateChanged = !string.Equals(entry.State, state, StringComparison.Ordinal);
            if (identityChanged || stateChanged)
            {
                entry.Generation++;
                entry.IsReady = false;
                entry.IdentityVerified = false;
                entry.NetworkVerified = false;
                entry.OwnershipVerified = false;
                entry.ObservedNetworkName = null;
                entry.ObservedNetworkAddress = null;
                entry.Reason = stateChanged && !identityChanged
                    ? "The DCP state changed; prior engine ownership evidence is invalid and service readiness remains unverified."
                    : "A new current DCP identity candidate was observed; engine ownership and service readiness are unverified.";
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
                        entry.ObservedNetworkAddress = null;
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
                    entry.ObservedNetworkAddress = null;
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
            entry.ObservedNetworkAddress = null;
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
                entry.ResourceKeyAmbiguous,
                entry.ResourceKeyAmbiguous ? null : entry.ObservedNetworkAddress)).ToArray();
        }
    }

    internal bool TryMarkIdentityVerified(
        IReadOnlyList<DcpBaseResourceOwnershipProof> proofs,
        bool sharedStandardDcpNetworkVerified)
    {
        ArgumentNullException.ThrowIfNull(proofs);
        lock (sync)
        {
            if (proofs.Count == 0 ||
                proofs.Select(proof => proof.Resource).Distinct(ReferenceEqualityComparer.Instance).Count() != proofs.Count ||
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

            if (sharedStandardDcpNetworkVerified)
            {
                var incomingNetwork = proofs[0].NetworkName;
                if (entries.Values.Any(entry =>
                        entry.State == "Running" && entry.NetworkVerified && entry.OwnershipVerified &&
                        !proofs.Any(proof => ReferenceEquals(proof.Resource, entry.Resource)) &&
                        !string.Equals(entry.ObservedNetworkName, incomingNetwork, StringComparison.Ordinal)))
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
                entry.ObservedNetworkAddress = proof.NetworkAddress;
                entry.Reason = sharedStandardDcpNetworkVerified
                    ? "The current DCP-owned container identity and common default-network membership were verified; service readiness remains unverified."
                    : "Docker Engine and container ownership labels were verified; common default-network membership and service readiness remain unverified.";
            }

            return true;
        }
    }

    internal bool InvalidateOwnershipProof(DcpBaseResourceIdentityCandidate candidate, string reason)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (sync)
        {
            if (!entries.TryGetValue(candidate.Resource, out var entry) ||
                entry.ResourceId != candidate.ResourceId ||
                entry.ContainerId != candidate.ContainerId ||
                entry.Generation != candidate.Generation)
            {
                return false;
            }

            entry.Generation++;
            entry.IsReady = false;
            entry.IdentityVerified = false;
            entry.NetworkVerified = false;
            entry.OwnershipVerified = false;
            entry.ObservedNetworkName = null;
            entry.ObservedNetworkAddress = null;
            entry.Reason = reason;
            return true;
        }
    }

    internal bool AreOwnershipProofsCurrent(IReadOnlyList<DcpBaseResourceIdentityCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        lock (sync)
        {
            return candidates.Count > 0 &&
                candidates.All(candidate =>
                    entries.TryGetValue(candidate.Resource, out var entry) &&
                    !entry.ResourceKeyAmbiguous &&
                    entry.ResourceId == candidate.ResourceId &&
                    entry.ContainerId == candidate.ContainerId &&
                    entry.Generation == candidate.Generation &&
                    entry.State == "Running" &&
                    entry.IdentityVerified &&
                    entry.NetworkVerified &&
                    entry.OwnershipVerified);
        }
    }

    internal bool SetCraneServiceReadiness(
        DcpBaseResourceIdentityCandidate candidate,
        bool isReady,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (sync)
        {
            if (!entries.TryGetValue(candidate.Resource, out var entry) ||
                entry.Role != "crane" ||
                entry.ResourceKeyAmbiguous ||
                entry.ResourceId != candidate.ResourceId ||
                entry.ContainerId != candidate.ContainerId ||
                entry.Generation != candidate.Generation ||
                entry.State != "Running" ||
                !entry.IdentityVerified || !entry.NetworkVerified || !entry.OwnershipVerified ||
                !OwnedContainerInspection.IsFullId(entry.ContainerId))
            {
                return false;
            }

            entry.IsReady = isReady;
            entry.Reason = reason;
            return true;
        }
    }

    internal bool SetGameControllerServiceReadiness(
        DcpBaseResourceIdentityCandidate candidate,
        bool isReady,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (sync)
        {
            if (!entries.TryGetValue(candidate.Resource, out var entry) ||
                entry.Role != "game-controller" ||
                entry.ResourceKeyAmbiguous ||
                entry.ResourceId != candidate.ResourceId ||
                entry.ContainerId != candidate.ContainerId ||
                entry.Generation != candidate.Generation ||
                entry.State != "Running" ||
                !entry.IdentityVerified || !entry.NetworkVerified || !entry.OwnershipVerified ||
                !OwnedContainerInspection.IsFullId(entry.ContainerId))
            {
                return false;
            }

            entry.IsReady = isReady;
            entry.Reason = reason;
            return true;
        }
    }

    internal bool ObserveSimulatorVision(
        DcpBaseResourceIdentityCandidate candidate,
        ResourceDiagnosticIdentity receiverIdentity,
        long receiverEpoch,
        long successfullyDecodedCount)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(receiverIdentity);
        lock (sync)
        {
            if (!entries.TryGetValue(candidate.Resource, out var entry) ||
                entry.ResourceKeyAmbiguous ||
                entry.ResourceId != candidate.ResourceId ||
                entry.ContainerId != candidate.ContainerId ||
                entry.Generation != candidate.Generation ||
                entry.State != "Running" ||
                !entry.IdentityVerified || !entry.NetworkVerified || !entry.OwnershipVerified ||
                receiverEpoch <= 0 || successfullyDecodedCount < 0)
            {
                return false;
            }

            if (entry.VisionGeneration != entry.Generation ||
                entry.VisionIdentity != receiverIdentity ||
                entry.VisionReceiverEpoch != receiverEpoch)
            {
                entry.VisionGeneration = entry.Generation;
                entry.VisionIdentity = receiverIdentity;
                entry.VisionReceiverEpoch = receiverEpoch;
                entry.VisionCount = successfullyDecodedCount;
                entry.IsReady = false;
                entry.Reason = "A fresh vision diagnostic baseline was recorded for the current simulator and receiver generation; waiting for new decoded progress.";
                return false;
            }

            if (successfullyDecodedCount < entry.VisionCount)
            {
                entry.IsReady = false;
                entry.Reason = "The current receiver's decoded count regressed; simulator service readiness is unverified.";
                return false;
            }

            if (successfullyDecodedCount > entry.VisionCount)
            {
                entry.VisionCount = successfullyDecodedCount;
                entry.IsReady = true;
                entry.Reason = "Current simulator ownership and fresh vision decode progress from the current Duck handoff were verified.";
            }

            return entry.IsReady;
        }
    }

    internal void InvalidateSimulatorVision(DcpBaseResourceIdentityCandidate candidate, string reason)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (sync)
        {
            if (entries.TryGetValue(candidate.Resource, out var entry) &&
                entry.ResourceId == candidate.ResourceId &&
                entry.ContainerId == candidate.ContainerId &&
                entry.Generation == candidate.Generation)
            {
                entry.IsReady = false;
                entry.Reason = reason;
            }
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

/// <summary>DCP通知で所有が確認された通常CM4資源に前景の補助処理を結び付けます。</summary>
internal sealed class OwnedCm4TransportServerSessionManager(
    DcpBaseResourceReadinessRegistry registry,
    IDockerCliForegroundSessionRunner runner,
    IResource cm4Resource,
    string? dockerContextName,
    string? expectedEngineId,
    string runId,
    IOwnedCm4TransportTargetController? targetController = null)
{
    private const string HelperPath = "/opt/duck/cm4-transport-control";
    private readonly SemaphoreSlim reconcileGate = new(1, 1);
    private readonly object sync = new();
    private IDockerCliForegroundSession? session;
    private DcpBaseResourceIdentityCandidate? sessionIdentity;
    private DcpBaseResourceIdentityCandidate? reapedSessionIdentity;
    private bool sessionResourceStopped;
    private DcpBaseResourceIdentityCandidate? failedStartIdentity;
    private bool failedStartResourceStopped;

    /// <summary>前景クライアントの終了後、既存の所有確認を再開する通知です。</summary>
    internal event Action? SessionCompleted;

    /// <summary>DCP の停止通知を記録し、前景セッションの解放可否を追跡します。</summary>
    internal void ObserveResourceEvent(ResourceEvent observation, ResourceEvent current)
    {
        if (!ReferenceEquals(observation.Resource, cm4Resource) ||
            !ReferenceEquals(current.Resource, cm4Resource) ||
            observation.ResourceId != current.ResourceId)
        {
            return;
        }

        var observedTerminal = OwnedResourceObservationProjection.TryProject(observation);
        var currentTerminal = OwnedResourceObservationProjection.TryProject(current);
        if (observedTerminal is null || currentTerminal is null ||
            observedTerminal.ResourceId != currentTerminal.ResourceId ||
            observedTerminal.State != currentTerminal.State ||
            !DcpBaseResourceReadinessRegistry.IsTerminalState(currentTerminal.State) ||
            !OwnedContainerInspection.IsFullId(currentTerminal.ContainerId) ||
            observedTerminal.ContainerId != currentTerminal.ContainerId)
        {
            return;
        }

        lock (sync)
        {
            if (sessionIdentity?.ResourceId == currentTerminal.ResourceId &&
                sessionIdentity.ContainerId == currentTerminal.ContainerId)
            {
                sessionResourceStopped = true;
            }
            if (failedStartIdentity?.ResourceId == currentTerminal.ResourceId &&
                failedStartIdentity.ContainerId == currentTerminal.ContainerId)
            {
                failedStartResourceStopped = true;
            }
        }
    }

    /// <summary>現在の所有確認済み CM4 に対し、前景サーバーを一つだけ開始または回収します。</summary>
    internal async Task ReconcileAsync(
        Func<string, ResourceEvent?> getCurrentByResourceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(getCurrentByResourceId);
        await reconcileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var shouldApplyInitialTargetAfterStart = false;
            var applyInitialTarget = false;
            IDockerCliForegroundSession? completedSession = null;
            lock (sync)
            {
                if (session is not null && sessionResourceStopped && session.Completion.IsCompletedSuccessfully)
                {
                    completedSession = session;
                }
                if (failedStartIdentity is not null && failedStartResourceStopped)
                {
                    failedStartIdentity = null;
                    failedStartResourceStopped = false;
                }
            }

            if (completedSession is not null)
            {
                await completedSession.DisposeAsync().ConfigureAwait(false);
                lock (sync)
                {
                    if (ReferenceEquals(session, completedSession))
                    {
                        session = null;
                        reapedSessionIdentity = sessionIdentity;
                        sessionIdentity = null;
                        sessionResourceStopped = false;
                    }
                }
            }

            lock (sync)
            {
                if (session is not null || failedStartIdentity is not null ||
                    string.IsNullOrWhiteSpace(dockerContextName) ||
                    string.IsNullOrWhiteSpace(expectedEngineId))
                {
                    return;
                }

                var candidate = registry.GetCandidates().SingleOrDefault(item =>
                    ReferenceEquals(item.Resource, cm4Resource) && item.Role == "cm4-sim");
                if (!IsCurrentOwnedCandidate(candidate, getCurrentByResourceId) ||
                    candidate is null ||
                    !IPAddress.TryParse(candidate.ObservedNetworkAddress, out var address) ||
                    address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return;
                }

                if (targetController is not null)
                {
                    var targetPhase = targetController.Snapshot().Phase;
                    shouldApplyInitialTargetAfterStart = targetPhase == Cm4TransportTargetPhase.Initial;
                    if (targetPhase == Cm4TransportTargetPhase.Applied && reapedSessionIdentity is not null)
                    {
                        var previous = ToTransportIdentity(reapedSessionIdentity);
                        var replacement = ToTransportIdentity(candidate);
                        shouldApplyInitialTargetAfterStart = targetController.TryReinitializeAfterConfirmedCm4Replacement(previous, replacement);
                        if (!shouldApplyInitialTargetAfterStart) return;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                var arguments = new[]
                {
                    "--context", dockerContextName!, "exec", candidate.ContainerId!, HelperPath, "--server",
                    "--run-id", runId,
                    "--engine-id", expectedEngineId!,
                    "--cm4-resource-id", candidate.ResourceId,
                    "--cm4-container-id", candidate.ContainerId!,
                    "--cm4-generation", candidate.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--helper-launch-id", runId,
                    "--cm4-ip", address.ToString(),
                };

                try
                {
                    var startedSession = runner.StartForegroundSession(arguments);
                    session = startedSession;
                    sessionIdentity = candidate;
                    reapedSessionIdentity = null;
                    sessionResourceStopped = false;
                    applyInitialTarget = shouldApplyInitialTargetAfterStart;
                    startedSession.Completion.GetAwaiter().OnCompleted(() => SessionCompleted?.Invoke());
                }
                catch (Exception exception) when (exception is IOException or System.ComponentModel.Win32Exception)
                {
                    failedStartIdentity = candidate;
                    failedStartResourceStopped = false;
                }
            }

            if (applyInitialTarget && targetController is not null)
            {
                // 初回だけ現行所有世代を補助処理へ渡し、失敗時は制御器の Unknown を維持します。
                await targetController.ApplyCurrentTargetAsync(runId, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            reconcileGate.Release();
        }
    }

    /// <summary>停止通知とローカル実行終了の両方が確認済みの場合だけ、停止時にセッションを解放します。</summary>
    internal async Task ReapConfirmedStoppedSessionAsync(CancellationToken cancellationToken)
    {
        await reconcileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IDockerCliForegroundSession? completedSession;
            lock (sync)
            {
                completedSession = session is not null && sessionResourceStopped && session.Completion.IsCompletedSuccessfully
                    ? session
                    : null;
            }

            if (completedSession is null)
            {
                return;
            }

            await completedSession.DisposeAsync().ConfigureAwait(false);
            lock (sync)
            {
                if (ReferenceEquals(session, completedSession))
                {
                    session = null;
                    sessionIdentity = null;
                    sessionResourceStopped = false;
                }
            }
        }
        finally
        {
            reconcileGate.Release();
        }
    }

    /// <summary>通知で確認した資源の所有情報を、補助制御の既存同一性表現へ移します。</summary>
    private OwnedCm4TransportIdentity ToTransportIdentity(DcpBaseResourceIdentityCandidate candidate) => new(
        candidate.RunId,
        expectedEngineId!,
        candidate.ResourceName,
        candidate.ResourceId,
        candidate.ContainerId!,
        candidate.Generation);

    private bool IsCurrentOwnedCandidate(
        DcpBaseResourceIdentityCandidate? candidate,
        Func<string, ResourceEvent?> getCurrentByResourceId)
    {
        if (candidate is null || candidate.ResourceKeyAmbiguous || candidate.State != "Running" ||
            string.IsNullOrWhiteSpace(candidate.ResourceId) ||
            !OwnedContainerInspection.IsFullId(candidate.ContainerId) ||
            !candidate.IdentityVerified || !candidate.NetworkVerified || !candidate.OwnershipVerified ||
            !registry.AreOwnershipProofsCurrent([candidate]))
        {
            return false;
        }

        var current = getCurrentByResourceId(candidate.ResourceId);
        return current is not null &&
            ReferenceEquals(current.Resource, candidate.Resource) &&
            current.ResourceId == candidate.ResourceId &&
            current.Snapshot.State?.Text == "Running" &&
            RegisteredContainerEventProjection.TryProject(
                candidate.Resource,
                candidate.ResourceId,
                "cm4-sim",
                current,
                current,
                out var latest) &&
            latest is not null &&
            latest.ContainerId == candidate.ContainerId &&
            registry.Get(candidate.Resource)?.Generation == candidate.Generation;
    }
}

internal sealed class DcpBaseResourceReadinessObserver(
    ResourceNotificationService notifications,
    DcpBaseResourceReadinessEventConsumer consumer,
    DcpBaseResourceOwnershipEventConsumer ownershipConsumer,
    IReadOnlyDictionary<IResource, string> roles,
    OwnedCm4TransportServerSessionManager? cm4TransportSessions = null) : BackgroundService
{
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        if (cm4TransportSessions is not null)
        {
            await cm4TransportSessions.ReapConfirmedStoppedSessionAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var verificationRequests = new SemaphoreSlim(0, 1);
        using var verificationStopping = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        // 2 つの通知元からの同時要求は、容量 1 の再検証シグナルへまとめます。
        void RequestVerification()
        {
            try
            {
                verificationRequests.Release();
            }
            catch (SemaphoreFullException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        if (cm4TransportSessions is not null)
        {
            cm4TransportSessions.SessionCompleted += RequestVerification;
        }
        var verificationTask = VerifyRequestedOwnershipAsync(verificationRequests, verificationStopping.Token);
        try
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
                cm4TransportSessions?.ObserveResourceEvent(observation, current);
                RequestVerification();
            }
        }
        finally
        {
            if (cm4TransportSessions is not null)
            {
                cm4TransportSessions.SessionCompleted -= RequestVerification;
            }
            verificationStopping.Cancel();
            try
            {
                await verificationTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (verificationStopping.IsCancellationRequested)
            {
            }
        }
    }

    private async Task VerifyRequestedOwnershipAsync(SemaphoreSlim requests, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await requests.WaitAsync(cancellationToken).ConfigureAwait(false);
                ResourceEvent? GetCurrent(string resourceId) =>
                    notifications.TryGetCurrentState(resourceId, out var state) ? state : null;
                await ownershipConsumer.VerifyCurrentAsync(GetCurrent, cancellationToken).ConfigureAwait(false);
                if (cm4TransportSessions is not null)
                {
                    await cm4TransportSessions.ReconcileAsync(GetCurrent, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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
        IResource duckResource,
        EndpointReference duckVisionEndpoint,
        ContainerIdentityHandoffStateStore handoffStates,
        string expectedCraneTeamName,
        Func<EndpointReference, CancellationToken, Task<Uri>>? gameControllerControlEndpointResolver,
        Func<Uri, CancellationToken, Task<bool>>? gameControllerControlApiProbe,
        params (IResource Resource, string Role)[] resources)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dockerExecutable);
        ArgumentNullException.ThrowIfNull(duckResource);
        ArgumentNullException.ThrowIfNull(duckVisionEndpoint);
        ArgumentNullException.ThrowIfNull(handoffStates);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCraneTeamName);
        ArgumentNullException.ThrowIfNull(resources);
        if (resources.Length == 0)
        {
            throw new ArgumentException("At least one DCP container is required for readiness tracking.", nameof(resources));
        }

        var trackedResources = resources.Concat([(Resource: duckResource, Role: "duck")]).ToArray();
        var entries = trackedResources.Select(item => (item.Resource, item.Role, runId)).ToArray();
        var registry = new DcpBaseResourceReadinessRegistry(entries, stackId);
        var eventConsumer = new DcpBaseResourceReadinessEventConsumer(registry);
        var dockerRunner = new DockerCliCommandRunner(dockerExecutable);
        builder.Services.AddSingleton<IDockerStdioCommandRunner>(dockerRunner);
        builder.Services.AddSingleton<IDockerCliForegroundSessionRunner>(dockerRunner);
        var ownershipConsumer = new DcpBaseResourceOwnershipEventConsumer(
            registry,
            dockerRunner,
            dockerContextName,
            expectedEngineId);
        var cm4Resource = trackedResources
            .Where(item => item.Role == "cm4-sim")
            .Select(item => item.Resource)
            .SingleOrDefault();
        if (cm4Resource is not null)
        {
            builder.Services.AddSingleton<OwnedCm4TransportServerSessionManager>(services =>
                new OwnedCm4TransportServerSessionManager(
                    registry,
                    services.GetRequiredService<IDockerCliForegroundSessionRunner>(),
                    cm4Resource,
                    dockerContextName,
                    expectedEngineId,
                    runId,
                    services.GetService<IOwnedCm4TransportTargetController>()));
        }
        var roleMap = new Dictionary<IResource, string>(ReferenceEqualityComparer.Instance);
        foreach (var (resource, role) in trackedResources)
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
            roleMap,
            services.GetService<OwnedCm4TransportServerSessionManager>()));

        var visionHttpClient = new HttpClient(new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(2),
        })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        builder.Services.AddSingleton(visionHttpClient);

        foreach (var (resource, role) in trackedResources)
        {
            if (resource is not ContainerResource container)
            {
                throw new InvalidOperationException($"DCP readiness resource '{resource.Name}' is not a ContainerResource.");
            }

            if (role == "duck")
            {
                continue;
            }

            var checkName = $"dcp-{role}-service-ready";
            var visionProbe = role == "simulator"
                ? new DcpBaseSimulatorVisionReadinessProbe(
                    registry,
                    resource,
                    duckResource,
                    cancellationToken => ResolveVisionEndpointAsync(duckVisionEndpoint, cancellationToken),
                    handoffStates,
                    visionHttpClient)
                : null;
            var craneProbe = role == "crane"
                ? new DcpBaseCraneReadinessProbe(
                    registry, resource, dockerRunner, dockerContextName, expectedCraneTeamName)
                : null;
            var gameControllerProbe = role == "game-controller"
                ? new DcpBaseGameControllerReadinessProbe(
                    registry,
                    resource,
                    cancellationToken => ResolveGameControllerControlEndpointAsync(
                        container.GetEndpoint("game-controller-control"),
                        gameControllerControlEndpointResolver,
                        cancellationToken),
                    gameControllerControlApiProbe)
                : null;
            IHealthCheck healthCheck = new DcpBaseResourceReadinessHealthCheck(
                registry, resource, visionProbe, craneProbe, gameControllerProbe);
            builder.Services.AddHealthChecks().AddCheck(checkName, healthCheck);
            builder.CreateResourceBuilder(container).WithHealthCheck(checkName);
        }
    }

    private static async Task<Uri> ResolveVisionEndpointAsync(
        EndpointReference endpoint,
        CancellationToken cancellationToken)
    {
        var value = await endpoint.GetValueAsync(cancellationToken).ConfigureAwait(false);
        var endpointValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            throw new InvalidOperationException("The Duck vision endpoint has not been allocated.");
        }
        return new Uri(endpointValue, UriKind.Absolute);
    }

    private static async Task<Uri> ResolveControlEndpointAsync(
        EndpointReference endpoint,
        CancellationToken cancellationToken)
    {
        var value = await endpoint.GetValueAsync(cancellationToken).ConfigureAwait(false);
        var endpointValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            throw new InvalidOperationException("The Game Controller control endpoint has not been allocated.");
        }
        return new Uri(endpointValue, UriKind.Absolute);
    }

    private static Task<Uri> ResolveGameControllerControlEndpointAsync(
        EndpointReference endpoint,
        Func<EndpointReference, CancellationToken, Task<Uri>>? endpointResolver,
        CancellationToken cancellationToken) =>
        endpointResolver is null
            ? ResolveControlEndpointAsync(endpoint, cancellationToken)
            : endpointResolver(endpoint, cancellationToken);
}

/// <summary>現在所有されているCraneへ既存のROS診断を実行し、同じ世代へ準備状態を記録します。</summary>
internal sealed class DcpBaseCraneReadinessProbe(
    DcpBaseResourceReadinessRegistry registry,
    IResource crane,
    IDockerStdioCommandRunner runner,
    string? dockerContextName,
    string expectedTeamName)
{
    private static readonly TimeSpan ProbeBudget = TimeSpan.FromSeconds(12);
    private const int MaximumOutputCharacters = 16 * 1024;

    internal async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        var candidate = GetCurrentCandidate();
        if (cancellationToken.IsCancellationRequested)
        {
            if (candidate is not null)
            {
                registry.SetCraneServiceReadiness(candidate, false, "The Crane readiness check was canceled; service readiness is unverified.");
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        if (!IsOwnedRunning(candidate))
        {
            return HealthCheckResult.Unhealthy("Crane has no current owned running container identity.");
        }

        if (string.IsNullOrWhiteSpace(dockerContextName))
        {
            registry.SetCraneServiceReadiness(candidate!, false, "The Docker context is not configured for the Crane readiness probe.");
            return HealthCheckResult.Unhealthy("The Docker context is not configured for the Crane readiness probe.");
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ProbeBudget);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await runner.CaptureBoundedAsync(
                ["--context", dockerContextName, .. DockerServiceReadiness.CreateCraneReadinessProbeArguments(
                    candidate!.ContainerId!, expectedTeamName)],
                MaximumOutputCharacters,
                ProbeBudget,
                budget.Token).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                registry.SetCraneServiceReadiness(candidate, false, "The Crane readiness check was canceled; service readiness is unverified.");
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (budget.IsCancellationRequested || timer.Elapsed >= ProbeBudget)
            {
                registry.SetCraneServiceReadiness(candidate, false, "The bounded Crane readiness probe expired.");
                return HealthCheckResult.Unhealthy("The bounded Crane readiness probe expired.");
            }

            var outcome = CraneProbeDiagnostics.Evaluate(
                result.ExitCode, result.StandardOutput, result.StandardError, timer.Elapsed);
            if (!outcome.Ready)
            {
                registry.SetCraneServiceReadiness(candidate, false, "The current Crane ROS diagnostic did not confirm the coordinator and expected team.");
                return HealthCheckResult.Unhealthy("The current Crane ROS diagnostic did not confirm the coordinator and expected team.");
            }

            var current = GetCurrentCandidate();
            if (current is null || !SameIdentity(candidate, current) || !IsOwnedRunning(current))
            {
                return HealthCheckResult.Unhealthy("The Crane identity or generation changed while its ROS diagnostic was running.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                registry.SetCraneServiceReadiness(candidate, false, "The Crane readiness check was canceled; service readiness is unverified.");
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (budget.IsCancellationRequested || timer.Elapsed >= ProbeBudget)
            {
                registry.SetCraneServiceReadiness(candidate, false, "The bounded Crane readiness probe expired.");
                return HealthCheckResult.Unhealthy("The bounded Crane readiness probe expired.");
            }
            if (!registry.SetCraneServiceReadiness(candidate, true, "The current owned Crane container passed its ROS coordinator and team diagnostic."))
            {
                return HealthCheckResult.Unhealthy("The Crane identity or generation changed while its ROS diagnostic was running.");
            }
            if (cancellationToken.IsCancellationRequested)
            {
                registry.SetCraneServiceReadiness(candidate, false, "The Crane readiness check was canceled; service readiness is unverified.");
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (budget.IsCancellationRequested || timer.Elapsed >= ProbeBudget)
            {
                registry.SetCraneServiceReadiness(candidate, false, "The bounded Crane readiness probe expired.");
                return HealthCheckResult.Unhealthy("The bounded Crane readiness probe expired.");
            }

            return HealthCheckResult.Healthy("The current owned Crane container passed its ROS coordinator and team diagnostic.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            registry.SetCraneServiceReadiness(candidate!, false, "The Crane readiness check was canceled; service readiness is unverified.");
            throw;
        }
        catch (OperationCanceledException)
        {
            registry.SetCraneServiceReadiness(candidate!, false, "The bounded Crane readiness probe expired.");
            return HealthCheckResult.Unhealthy("The bounded Crane readiness probe expired.");
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or InvalidOperationException)
        {
            registry.SetCraneServiceReadiness(candidate!, false, "The current Crane ROS diagnostic could not be completed safely.");
            return HealthCheckResult.Unhealthy("The current Crane ROS diagnostic could not be completed safely.");
        }
    }

    private DcpBaseResourceIdentityCandidate? GetCurrentCandidate() => registry.GetCandidates()
        .SingleOrDefault(candidate => ReferenceEquals(candidate.Resource, crane));

    private bool IsOwnedRunning(DcpBaseResourceIdentityCandidate? candidate) => candidate is not null &&
        candidate.Role == "crane" && candidate.State == "Running" && !candidate.ResourceKeyAmbiguous &&
        !string.IsNullOrWhiteSpace(candidate.ResourceId) && OwnedContainerInspection.IsFullId(candidate.ContainerId) &&
        candidate.IdentityVerified && candidate.NetworkVerified && candidate.OwnershipVerified &&
        registry.AreOwnershipProofsCurrent([candidate]);

    private static bool SameIdentity(
        DcpBaseResourceIdentityCandidate expected,
        DcpBaseResourceIdentityCandidate actual) =>
        ReferenceEquals(expected.Resource, actual.Resource) && expected.ResourceId == actual.ResourceId &&
        expected.ContainerId == actual.ContainerId && expected.Generation == actual.Generation &&
        expected.RunId == actual.RunId && expected.Role == actual.Role;
}

internal sealed class DcpBaseResourceReadinessHealthCheck(
    DcpBaseResourceReadinessRegistry registry,
    IResource resource,
    DcpBaseSimulatorVisionReadinessProbe? simulatorVisionProbe = null,
    DcpBaseCraneReadinessProbe? craneReadinessProbe = null,
    DcpBaseGameControllerReadinessProbe? gameControllerReadinessProbe = null) : IHealthCheck
{
    internal DcpBaseCraneReadinessProbe? CraneReadinessProbe => craneReadinessProbe;
    internal DcpBaseGameControllerReadinessProbe? GameControllerReadinessProbe => gameControllerReadinessProbe;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (simulatorVisionProbe is not null)
        {
            return await simulatorVisionProbe.CheckAsync(cancellationToken).ConfigureAwait(false);
        }
        if (craneReadinessProbe is not null)
        {
            return await craneReadinessProbe.CheckAsync(cancellationToken).ConfigureAwait(false);
        }
        if (gameControllerReadinessProbe is not null)
        {
            return await gameControllerReadinessProbe.CheckAsync(cancellationToken).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = registry.Get(resource);
        if (snapshot is null || !snapshot.IsReady)
        {
            var reason = snapshot?.Reason ?? "The resource is not registered with the DCP readiness observer.";
            return HealthCheckResult.Unhealthy(reason);
        }

        return HealthCheckResult.Healthy(snapshot.Reason);
    }
}

internal sealed class DcpBaseGameControllerReadinessProbe(
    DcpBaseResourceReadinessRegistry registry,
    IResource gameController,
    Func<CancellationToken, Task<Uri>> resolveControlEndpoint,
    Func<Uri, CancellationToken, Task<bool>>? controlApiProbe = null)
{
    private static readonly TimeSpan ProbeBudget = TimeSpan.FromSeconds(2);

    internal async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        var candidate = CurrentCandidate();
        if (cancellationToken.IsCancellationRequested)
        {
            ClearReadiness(candidate, "The Game Controller control API check was canceled.");
            cancellationToken.ThrowIfCancellationRequested();
        }
        if (!IsOwnedRunning(candidate))
        {
            ClearReadiness(candidate, "The Game Controller has no current owned running container identity.");
            return HealthCheckResult.Unhealthy("The Game Controller has no current owned running container identity.");
        }

        registry.SetGameControllerServiceReadiness(
            candidate!, false, "The current Game Controller control API has not been checked.");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ProbeBudget);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var endpoint = await resolveControlEndpoint(budget.Token).ConfigureAwait(false);
            var controlUri = CreateControlApiUri(endpoint);
            if (cancellationToken.IsCancellationRequested)
            {
                ClearReadiness(candidate, "The Game Controller control API check was canceled.");
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (budget.IsCancellationRequested || timer.Elapsed >= ProbeBudget)
            {
                ClearReadiness(candidate, "The bounded Game Controller control API check expired.");
                return HealthCheckResult.Unhealthy("The bounded Game Controller control API check expired.");
            }

            var dispatchCandidate = CurrentCandidate();
            if (!SameCurrent(candidate!, dispatchCandidate) || !IsOwnedRunning(dispatchCandidate))
            {
                ClearReadiness(candidate, "The Game Controller identity changed before its control API was checked.");
                return HealthCheckResult.Unhealthy("The Game Controller identity changed before its control API was checked.");
            }

            var reachable = controlApiProbe is not null
                ? await controlApiProbe(controlUri, budget.Token).ConfigureAwait(false)
                : await ConnectControlApiAsync(controlUri, budget.Token).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                ClearReadiness(candidate, "The Game Controller control API check was canceled.");
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (budget.IsCancellationRequested || timer.Elapsed >= ProbeBudget)
            {
                ClearReadiness(candidate, "The bounded Game Controller control API check expired.");
                return HealthCheckResult.Unhealthy("The bounded Game Controller control API check expired.");
            }
            if (!reachable)
            {
                ClearReadiness(candidate, "The current Game Controller control API did not accept a WebSocket connection.");
                return HealthCheckResult.Unhealthy("The current Game Controller control API did not accept a WebSocket connection.");
            }

            var current = CurrentCandidate();
            if (!SameCurrent(candidate!, current) || !registry.AreOwnershipProofsCurrent([current!]))
            {
                ClearReadiness(candidate, "The Game Controller identity changed while its control API was checked.");
                return HealthCheckResult.Unhealthy("The Game Controller identity changed while its control API was checked.");
            }
            if (cancellationToken.IsCancellationRequested)
            {
                ClearReadiness(candidate, "The Game Controller control API check was canceled.");
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (budget.IsCancellationRequested || timer.Elapsed >= ProbeBudget)
            {
                ClearReadiness(candidate, "The bounded Game Controller control API check expired.");
                return HealthCheckResult.Unhealthy("The bounded Game Controller control API check expired.");
            }
            if (!registry.SetGameControllerServiceReadiness(
                    candidate!, true, "The current owned Game Controller control API accepted a WebSocket connection."))
            {
                return HealthCheckResult.Unhealthy("The Game Controller identity or ownership changed before readiness was applied.");
            }
            if (cancellationToken.IsCancellationRequested)
            {
                ClearReadiness(candidate, "The Game Controller control API check was canceled.");
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (budget.IsCancellationRequested || timer.Elapsed >= ProbeBudget)
            {
                ClearReadiness(candidate, "The bounded Game Controller control API check expired.");
                return HealthCheckResult.Unhealthy("The bounded Game Controller control API check expired.");
            }
            var final = CurrentCandidate();
            if (!SameCurrent(candidate!, final) || !registry.AreOwnershipProofsCurrent([final!]))
            {
                ClearReadiness(candidate, "The Game Controller identity changed before readiness was returned.");
                return HealthCheckResult.Unhealthy("The Game Controller identity changed before readiness was returned.");
            }

            return HealthCheckResult.Healthy("The current owned Game Controller control API accepted a WebSocket connection.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ClearReadiness(candidate, "The Game Controller control API check was canceled.");
            throw;
        }
        catch (OperationCanceledException)
        {
            ClearReadiness(candidate, "The bounded Game Controller control API check expired.");
            return HealthCheckResult.Unhealthy("The bounded Game Controller control API check expired.");
        }
        catch (Exception exception) when (exception is WebSocketException or IOException or InvalidOperationException or UriFormatException)
        {
            ClearReadiness(candidate, "The current Game Controller control API endpoint could not be reached safely.");
            return HealthCheckResult.Unhealthy("The current Game Controller control API endpoint could not be reached safely.");
        }
    }

    internal static Uri CreateControlApiUri(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || !endpoint.IsLoopback || endpoint.Port is < 1 or > 65535 ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("The Game Controller control endpoint must be an allocated loopback HTTP endpoint.");
        }

        return new UriBuilder(endpoint)
        {
            Scheme = endpoint.Scheme == "https" ? "wss" : "ws",
            Path = "/api/control",
            Query = string.Empty,
            Fragment = string.Empty,
        }.Uri;
    }

    private static async Task<bool> ConnectControlApiAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
        return socket.State == WebSocketState.Open;
    }

    private DcpBaseResourceIdentityCandidate? CurrentCandidate() => registry.GetCandidates()
        .SingleOrDefault(candidate => ReferenceEquals(candidate.Resource, gameController));

    private bool IsOwnedRunning(DcpBaseResourceIdentityCandidate? candidate) => candidate is not null &&
        candidate.Role == "game-controller" && candidate.State == "Running" && !candidate.ResourceKeyAmbiguous &&
        !string.IsNullOrWhiteSpace(candidate.ResourceId) && OwnedContainerInspection.IsFullId(candidate.ContainerId) &&
        candidate.IdentityVerified && candidate.NetworkVerified && candidate.OwnershipVerified &&
        registry.AreOwnershipProofsCurrent([candidate]);

    private void ClearReadiness(DcpBaseResourceIdentityCandidate? candidate, string reason)
    {
        if (candidate is not null)
        {
            registry.SetGameControllerServiceReadiness(candidate, false, reason);
        }
    }

    private static bool SameCurrent(
        DcpBaseResourceIdentityCandidate expected,
        DcpBaseResourceIdentityCandidate? current) =>
        current is not null && ReferenceEquals(expected.Resource, current.Resource) &&
        expected.ResourceId == current.ResourceId && expected.ContainerId == current.ContainerId &&
        expected.Generation == current.Generation && current.State == "Running" &&
        current.IdentityVerified && current.NetworkVerified && current.OwnershipVerified;
}

internal sealed class DcpBaseSimulatorVisionReadinessProbe(
    DcpBaseResourceReadinessRegistry registry,
    IResource simulator,
    IResource duck,
    Func<CancellationToken, Task<Uri>> resolveDuckVisionEndpoint,
    ContainerIdentityHandoffStateStore handoffStates,
    HttpClient httpClient)
{
    private const int MaximumResponseBytes = 16 * 1024;

    internal async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        var simulatorCandidate = CurrentCandidate(simulator);
        if (cancellationToken.IsCancellationRequested)
        {
            Invalidate(simulatorCandidate, "The Duck vision diagnostic check was canceled; simulator service readiness is unverified.");
            cancellationToken.ThrowIfCancellationRequested();
        }
        var duckCandidate = CurrentCandidate(duck);
        var handoffIdentity = handoffStates.GetCurrent(duck);
        if (!IsOwnedRunning(simulatorCandidate) ||
            !IsOwnedRunning(duckCandidate) ||
            handoffIdentity is null ||
            handoffIdentity.RunId != duckCandidate!.RunId ||
            handoffIdentity.ResourceId != duckCandidate.ResourceId ||
            handoffIdentity.ContainerId != duckCandidate.ContainerId)
        {
            Invalidate(simulatorCandidate, "The simulator or Duck has no current owned container identity, or the Duck handoff is not current.");
            return HealthCheckResult.Unhealthy("The simulator or Duck has no current owned container identity, or the Duck handoff is not current.");
        }

        using var requestBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestBudget.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            var requestStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            var endpointUri = await resolveDuckVisionEndpoint(requestBudget.Token).ConfigureAwait(false);
            var requestUri = new Uri(endpointUri, "/health/vision");
            using var response = await httpClient.GetAsync(
                requestUri, HttpCompletionOption.ResponseHeadersRead, requestBudget.Token).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK ||
                response.Content.Headers.ContentLength is > MaximumResponseBytes)
            {
                return Reject(simulatorCandidate, "The current Duck vision diagnostic endpoint is unavailable or returned an oversized response.");
            }

            await using var body = await response.Content.ReadAsStreamAsync(requestBudget.Token).ConfigureAwait(false);
            var buffer = new byte[MaximumResponseBytes + 1];
            var total = 0;
            while (total < buffer.Length)
            {
                var read = await body.ReadAsync(buffer.AsMemory(total), requestBudget.Token).ConfigureAwait(false);
                if (read == 0) break;
                total += read;
            }
            if (total > MaximumResponseBytes)
            {
                return Reject(simulatorCandidate, "The current Duck vision diagnostic response exceeded the size limit.");
            }

            var health = JsonSerializer.Deserialize<RuntimeVisionHealthResponse>(buffer.AsSpan(0, total));
            var diagnostics = health?.Diagnostics;
            if (health is null || diagnostics is null ||
                !health.IsReady || !diagnostics.IsReady ||
                diagnostics.Identity != handoffIdentity ||
                diagnostics.InputId != "vision" ||
                diagnostics.ReceiverEpoch <= 0 ||
                diagnostics.SuccessfullyDecodedCount <= 0 ||
                diagnostics.DecodeStatus != DiagnosticDecodeStatus.Succeeded ||
                diagnostics.DecodeAgeUpperBound is not { } age ||
                age < TimeSpan.Zero || age > ServiceReceiveDiagnostics.FreshnessWindow)
            {
                return Reject(simulatorCandidate, "The current Duck vision diagnostic does not prove fresh decoded progress for its current identity handoff.");
            }

            var currentSimulator = CurrentCandidate(simulator);
            var currentDuck = CurrentCandidate(duck);
            if (!SameCurrent(simulatorCandidate, currentSimulator) ||
                !SameCurrent(duckCandidate, currentDuck) ||
                handoffStates.GetCurrent(duck) != handoffIdentity)
            {
                return Reject(simulatorCandidate, "The simulator or Duck identity changed while the vision diagnostic was being read.");
            }

            if (age + System.Diagnostics.Stopwatch.GetElapsedTime(requestStartedAt) > ServiceReceiveDiagnostics.FreshnessWindow)
            {
                return Reject(simulatorCandidate, "The current Duck vision diagnostic exceeded the freshness window while the response was being read.");
            }

            if (!registry.ObserveSimulatorVision(
                    simulatorCandidate!, handoffIdentity, diagnostics.ReceiverEpoch,
                    diagnostics.SuccessfullyDecodedCount))
            {
                var snapshot = registry.Get(simulator);
                return HealthCheckResult.Unhealthy(snapshot?.Reason ?? "Fresh vision progress for the current simulator generation has not been observed.");
            }

            return HealthCheckResult.Healthy(registry.Get(simulator)?.Reason ?? "Current simulator vision readiness was verified.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Reject(simulatorCandidate, "The Duck vision diagnostic request exceeded its bounded check time.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Invalidate(simulatorCandidate, "The Duck vision diagnostic check was canceled; simulator service readiness is unverified.");
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or UriFormatException or InvalidOperationException)
        {
            return Reject(simulatorCandidate, "The current Duck vision diagnostic could not be read safely.");
        }
    }

    private DcpBaseResourceIdentityCandidate? CurrentCandidate(IResource resource) =>
        registry.GetCandidates().SingleOrDefault(candidate => ReferenceEquals(candidate.Resource, resource));

    private static bool IsOwnedRunning(DcpBaseResourceIdentityCandidate? candidate) =>
        candidate is not null &&
        !candidate.ResourceKeyAmbiguous &&
        !string.IsNullOrWhiteSpace(candidate.ResourceId) &&
        OwnedContainerInspection.IsFullId(candidate.ContainerId) &&
        candidate.State == "Running" &&
        candidate.IdentityVerified &&
        candidate.NetworkVerified &&
        candidate.OwnershipVerified;

    private static bool SameCurrent(
        DcpBaseResourceIdentityCandidate? expected,
        DcpBaseResourceIdentityCandidate? current) =>
        expected is not null && current is not null &&
        ReferenceEquals(expected.Resource, current.Resource) &&
        expected.ResourceId == current.ResourceId &&
        expected.ContainerId == current.ContainerId &&
        expected.Generation == current.Generation &&
        current.State == "Running" &&
        current.IdentityVerified && current.NetworkVerified && current.OwnershipVerified;

    private HealthCheckResult Reject(DcpBaseResourceIdentityCandidate? candidate, string reason)
    {
        Invalidate(candidate, reason);
        return HealthCheckResult.Unhealthy(reason);
    }

    private void Invalidate(DcpBaseResourceIdentityCandidate? candidate, string reason)
    {
        if (candidate is not null)
        {
            registry.InvalidateSimulatorVision(candidate, reason);
        }
    }
}
