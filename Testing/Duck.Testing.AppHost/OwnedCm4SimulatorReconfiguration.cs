using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Duck.Testing.AppHost;

internal interface IOwnedCm4SimulatorReconfigurationAdapter
{
    OwnedCm4TransportIdentityPair? GetCurrentOwnedPair();

    Task<bool> RestartSimulatorAsync(CancellationToken cancellationToken);

    Task<OwnedCm4TransportIdentityPair?> WaitForReplacementAsync(
        OwnedCm4TransportIdentityPair previous,
        CancellationToken cancellationToken);
}

/// <summary>既存の所有確認、無効化、DCP再作成、再適用を順に実行します。</summary>
internal sealed class OwnedCm4SimulatorReconfigurationCoordinator(
    IOwnedCm4SimulatorReconfigurationAdapter adapter,
    IOwnedCm4TransportTargetController? controller,
    string helperLaunchId,
    TimeSpan? operationBudget = null)
{
    private static readonly TimeSpan DefaultOperationLimit = TimeSpan.FromSeconds(45);
    private readonly TimeSpan operationLimit = operationBudget ?? DefaultOperationLimit;
    private readonly SemaphoreSlim operationGate = new(1, 1);

    internal async Task<OwnedCm4SimulatorReconfigurationResult> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(operationLimit);
        var token = deadline.Token;
        var entered = false;
        try
        {
            await operationGate.WaitAsync(token).ConfigureAwait(false);
            entered = true;
            if (controller is null)
            {
                return Failed("Docker context or expected Engine ID is not configured.");
            }

            var previous = adapter.GetCurrentOwnedPair();
            if (previous is null)
            {
                return Failed("Current owned CM4 and simulator identities are unavailable.");
            }

            var snapshot = controller.Snapshot();
            if (snapshot.Phase == Cm4TransportTargetPhase.Initial)
            {
                var initialApply = await controller.ApplyCurrentTargetAsync(helperLaunchId, token).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                {
                    controller.Invalidate();
                    token.ThrowIfCancellationRequested();
                }
                if (!initialApply.Applied || initialApply.Unknown)
                {
                    return Failed("The current target is not confirmed applied; simulator restart was not requested.");
                }
            }
            else if (snapshot.Phase != Cm4TransportTargetPhase.Applied)
            {
                return Failed("The prior remote target state is not known to be applied; no retry or restart was requested.");
            }

            if (!SamePair(previous, adapter.GetCurrentOwnedPair()))
            {
                controller.Invalidate();
                return Failed("The owned identities changed before remote invalidation.");
            }

            var invalidation = await controller.InvalidateCurrentTargetAsync(helperLaunchId, token).ConfigureAwait(false);
            if (invalidation.Invalidated && !invalidation.Unknown)
            {
                token.ThrowIfCancellationRequested();
            }
            if (!invalidation.Invalidated || invalidation.Unknown ||
                controller.Snapshot().Phase != Cm4TransportTargetPhase.Invalidated)
            {
                return Failed("Remote target invalidation was not confirmed; simulator restart was not requested.");
            }

            if (!SamePair(previous, adapter.GetCurrentOwnedPair()))
            {
                controller.Invalidate();
                return Failed("The owned identities changed after invalidation; the old target remains disabled.");
            }

            token.ThrowIfCancellationRequested();
            if (!await adapter.RestartSimulatorAsync(token).ConfigureAwait(false))
            {
                return Failed("The DCP simulator restart command did not succeed; the old target remains disabled.");
            }
            if (token.IsCancellationRequested)
            {
                controller.Invalidate();
                token.ThrowIfCancellationRequested();
            }

            var replacement = await adapter.WaitForReplacementAsync(previous, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (replacement is null || replacement.Cm4 != previous.Cm4 ||
                replacement.Simulator.Generation <= previous.Simulator.Generation ||
                replacement.Simulator.ContainerId == previous.Simulator.ContainerId ||
                !SamePair(replacement, adapter.GetCurrentOwnedPair()))
            {
                return Failed("A new owned simulator generation was not confirmed; no target was applied.");
            }

            token.ThrowIfCancellationRequested();
            var applied = await controller.ApplyCurrentTargetAsync(helperLaunchId, token).ConfigureAwait(false);
            if (applied.Applied && !applied.Unknown && token.IsCancellationRequested)
            {
                controller.Invalidate();
                token.ThrowIfCancellationRequested();
            }
            if (!applied.Applied || applied.Unknown)
            {
                return Failed("The replacement target was not confirmed applied; no prior target was restored.");
            }

            token.ThrowIfCancellationRequested();
            return new(true, "Remote target invalidation, DCP restart, and replacement target application were confirmed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(false, "Reconfiguration was canceled; remote state is not inferred.", Canceled: true);
        }
        catch (OperationCanceledException)
        {
            controller?.Invalidate();
            return Failed("The bounded reconfiguration operation expired; remote state is unknown.");
        }
        finally
        {
            if (entered)
            {
                operationGate.Release();
            }
        }
    }

    private static OwnedCm4SimulatorReconfigurationResult Failed(string message) => new(false, message);

    private static bool SamePair(
        OwnedCm4TransportIdentityPair? expected,
        OwnedCm4TransportIdentityPair? actual) => expected is not null && actual is not null &&
        expected.Cm4 == actual.Cm4 && expected.Simulator == actual.Simulator;
}

internal sealed record OwnedCm4SimulatorReconfigurationResult(
    bool Success,
    string Message,
    bool Canceled = false);

internal sealed class DcpOwnedCm4SimulatorReconfigurationAdapter(
    DcpBaseResourceReadinessRegistry registry,
    DcpBaseResourceOwnershipEventConsumer ownership,
    DcpBaseResourceReadinessEventConsumer events,
    ResourceNotificationService notifications,
    ResourceCommandService commands,
    IResource simulator,
    IResource cm4,
    string? expectedEngineId) : IOwnedCm4SimulatorReconfigurationAdapter
{
    public OwnedCm4TransportIdentityPair? GetCurrentOwnedPair()
    {
        if (string.IsNullOrWhiteSpace(expectedEngineId))
        {
            return null;
        }

        var candidates = registry.GetCandidates();
        var simulatorCandidate = candidates.SingleOrDefault(candidate => ReferenceEquals(candidate.Resource, simulator));
        var cm4Candidate = candidates.SingleOrDefault(candidate => ReferenceEquals(candidate.Resource, cm4));
        if (!IsOwnedRunning(simulatorCandidate) || !IsOwnedRunning(cm4Candidate))
        {
            return null;
        }

        return new OwnedCm4TransportIdentityPair(
            ToIdentity(cm4Candidate!),
            ToIdentity(simulatorCandidate!));

        OwnedCm4TransportIdentity ToIdentity(DcpBaseResourceIdentityCandidate candidate) => new(
            candidate.RunId,
            expectedEngineId,
            candidate.ResourceName,
            candidate.ResourceId,
            candidate.ContainerId!,
            candidate.Generation);
    }

    public async Task<bool> RestartSimulatorAsync(CancellationToken cancellationToken)
    {
        var result = await commands.ExecuteCommandAsync(
            simulator,
            KnownResourceCommands.RestartCommand,
            cancellationToken).ConfigureAwait(false);
        return result.Success;
    }

    public async Task<OwnedCm4TransportIdentityPair?> WaitForReplacementAsync(
        OwnedCm4TransportIdentityPair previous,
        CancellationToken cancellationToken)
    {
        await foreach (var observation in notifications.WatchAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!ReferenceEquals(observation.Resource, simulator) ||
                !notifications.TryGetCurrentState(observation.ResourceId, out var current))
            {
                continue;
            }

            events.Observe(observation, current, "simulator", resourceId =>
                notifications.TryGetCurrentState(resourceId, out var state) ? state : null);
            await ownership.VerifyCurrentAsync(resourceId =>
                notifications.TryGetCurrentState(resourceId, out var state) ? state : null,
                cancellationToken).ConfigureAwait(false);

            var pair = GetCurrentOwnedPair();
            if (pair is not null && pair.Cm4 == previous.Cm4 &&
                pair.Simulator.Generation > previous.Simulator.Generation &&
                pair.Simulator.ContainerId != previous.Simulator.ContainerId)
            {
                return pair;
            }
        }

        return null;
    }

    private bool IsOwnedRunning(DcpBaseResourceIdentityCandidate? candidate) =>
        candidate is not null && candidate.State == "Running" && !candidate.ResourceKeyAmbiguous &&
        !string.IsNullOrWhiteSpace(candidate.ResourceId) && OwnedContainerInspection.IsFullId(candidate.ContainerId) &&
        candidate.IdentityVerified && candidate.NetworkVerified && candidate.OwnershipVerified &&
        registry.AreOwnershipProofsCurrent([candidate]);
}
