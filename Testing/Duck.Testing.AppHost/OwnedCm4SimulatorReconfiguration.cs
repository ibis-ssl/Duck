using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Duck.Testing.AppHost;

internal interface IOwnedCm4SimulatorReconfigurationAdapter
{
    OwnedCm4TransportIdentityPair? GetCurrentOwnedPair();

    bool IsCurrentOwnedCrane();

    Task<ExecuteCommandResult> RestartSimulatorAsync(
        Func<ExecuteCommandContext, Task<ExecuteCommandResult>> restartCommand,
        ExecuteCommandContext commandContext,
        CancellationToken cancellationToken);

    Task<OwnedCm4TransportIdentityPair?> WaitForReplacementAsync(
        OwnedCm4TransportIdentityPair previous,
        CancellationToken cancellationToken);

    Task<OwnedCm4TransportIdentityPair?> WaitForCm4ReplacementAsync(
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
        ExecuteCommandContext commandContext,
        Func<ExecuteCommandContext, Task<ExecuteCommandResult>> restartCommand)
    {
        ArgumentNullException.ThrowIfNull(commandContext);
        ArgumentNullException.ThrowIfNull(restartCommand);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(commandContext.CancellationToken);
        deadline.CancelAfter(operationLimit);
        var token = deadline.Token;
        var entered = false;
        var replacementTargetAcknowledged = false;
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
            var restartContext = new ExecuteCommandContext
            {
                Services = commandContext.Services,
                ResourceName = commandContext.ResourceName,
                Logger = commandContext.Logger,
                Arguments = commandContext.Arguments,
                CancellationToken = token,
            };
            var restartResult = await adapter.RestartSimulatorAsync(
                restartCommand,
                restartContext,
                token).ConfigureAwait(false);
            if (!restartResult.Success)
            {
                return new(false,
                    restartResult.Message ?? "The DCP simulator restart command did not succeed; the old target remains disabled.",
                    restartResult.Canceled,
                    restartResult);
            }
            if (token.IsCancellationRequested)
            {
                controller.Invalidate();
                token.ThrowIfCancellationRequested();
            }

            var replacement = await adapter.WaitForReplacementAsync(previous, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (replacement is null || !SamePhysicalIdentity(replacement.Cm4, previous.Cm4) ||
                replacement.Simulator.Generation <= previous.Simulator.Generation ||
                replacement.Simulator.ContainerId == previous.Simulator.ContainerId ||
                !SamePair(replacement, adapter.GetCurrentOwnedPair()))
            {
                return Failed("A new owned simulator generation was not confirmed; no target was applied.");
            }

            token.ThrowIfCancellationRequested();
            var applied = await controller.ApplyCurrentTargetAsync(helperLaunchId, token).ConfigureAwait(false);
            if (!applied.Applied || applied.Unknown)
            {
                return Failed("The replacement target was not confirmed applied; no prior target was restored.");
            }

            replacementTargetAcknowledged = true;
            token.ThrowIfCancellationRequested();
            return new(true,
                "Remote target invalidation, DCP restart, and replacement target application were confirmed.",
                RestartCommandResult: restartResult);
        }
        catch (OperationCanceledException) when (commandContext.CancellationToken.IsCancellationRequested)
        {
            if (replacementTargetAcknowledged)
            {
                controller?.Invalidate();
            }
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

    /// <summary>CM4 再起動を無効化確認、所有世代交換、Simulator 再起動、最終適用の順で実行します。</summary>
    internal async Task<OwnedCm4SimulatorReconfigurationResult> ExecuteCm4RestartAsync(
        ExecuteCommandContext commandContext,
        Func<ExecuteCommandContext, Task<ExecuteCommandResult>> restartCm4Command,
        Func<ExecuteCommandContext, Task<ExecuteCommandResult>> restartSimulatorCommand,
        Func<ExecuteCommandContext, Task<ExecuteCommandResult>>? restartCraneCommand = null)
    {
        ArgumentNullException.ThrowIfNull(commandContext);
        ArgumentNullException.ThrowIfNull(restartCm4Command);
        ArgumentNullException.ThrowIfNull(restartSimulatorCommand);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(commandContext.CancellationToken);
        deadline.CancelAfter(operationLimit);
        var token = deadline.Token;
        var entered = false;
        var replacementTargetAcknowledged = false;
        try
        {
            await operationGate.WaitAsync(token).ConfigureAwait(false);
            entered = true;
            if (controller is null) return Failed("Docker context or expected Engine ID is not configured.");

            var previous = adapter.GetCurrentOwnedPair();
            if (previous is null || controller.Snapshot().Phase != Cm4TransportTargetPhase.Applied)
            {
                return Failed("The current owned pair and applied target are required before CM4 restart.");
            }
            if (restartCraneCommand is not null && !adapter.IsCurrentOwnedCrane())
            {
                return Failed("The current Crane ownership proof is unavailable; CM4 restart was not requested.");
            }

            var invalidation = await controller.InvalidateCurrentTargetAsync(helperLaunchId, token).ConfigureAwait(false);
            if (!invalidation.Invalidated || invalidation.Unknown ||
                controller.Snapshot().Phase != Cm4TransportTargetPhase.Invalidated)
            {
                return Failed("Remote target invalidation was not confirmed; CM4 restart was not requested.");
            }
            token.ThrowIfCancellationRequested();
            if (!SamePair(previous, adapter.GetCurrentOwnedPair()))
            {
                return Failed("The owned pair changed before CM4 restart; no restart was requested.");
            }

            var cm4Restart = await restartCm4Command(CreateRestartContext(commandContext, previous.Cm4.ResourceName, token))
                .ConfigureAwait(false);
            if (!cm4Restart.Success)
            {
                return new(false, cm4Restart.Message ?? "CM4 restart did not succeed; the route remains invalidated.",
                    cm4Restart.Canceled, cm4Restart);
            }
            token.ThrowIfCancellationRequested();

            var afterCm4 = await adapter.WaitForCm4ReplacementAsync(previous, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (afterCm4 is null || !SamePhysicalIdentity(afterCm4.Simulator, previous.Simulator) ||
                afterCm4.Cm4.Generation <= previous.Cm4.Generation ||
                afterCm4.Cm4.ContainerId == previous.Cm4.ContainerId ||
                !SamePair(afterCm4, adapter.GetCurrentOwnedPair()) ||
                !controller.IsInvalidatedFor(afterCm4))
            {
                return Failed("A reaped helper session and current owned CM4 replacement were not confirmed.");
            }

            token.ThrowIfCancellationRequested();
            if (!SamePair(afterCm4, adapter.GetCurrentOwnedPair()))
            {
                return Failed("The owned pair changed before Simulator restart; the route remains invalidated.");
            }
            var simulatorRestart = await restartSimulatorCommand(
                CreateRestartContext(commandContext, afterCm4.Simulator.ResourceName, token)).ConfigureAwait(false);
            if (!simulatorRestart.Success)
            {
                return new(false,
                    simulatorRestart.Message ?? "Simulator restart did not succeed; the route remains invalidated.",
                    simulatorRestart.Canceled, simulatorRestart);
            }
            token.ThrowIfCancellationRequested();

            var replacement = await adapter.WaitForReplacementAsync(afterCm4, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (replacement is null || !SamePhysicalIdentity(replacement.Cm4, afterCm4.Cm4) ||
                replacement.Simulator.Generation <= afterCm4.Simulator.Generation ||
                replacement.Simulator.ContainerId == afterCm4.Simulator.ContainerId ||
                !SamePair(replacement, adapter.GetCurrentOwnedPair()))
            {
                return Failed("A current owned Simulator replacement was not confirmed; no target was applied.");
            }

            var applied = await controller.ApplyCurrentTargetAsync(helperLaunchId, token).ConfigureAwait(false);
            if (!applied.Applied || applied.Unknown || !SamePair(replacement, adapter.GetCurrentOwnedPair()))
            {
                return Failed("The final target was not confirmed for the current owned pair.");
            }
            replacementTargetAcknowledged = true;

            if (restartCraneCommand is not null)
            {
                token.ThrowIfCancellationRequested();
                if (!SamePair(replacement, adapter.GetCurrentOwnedPair()) || !adapter.IsCurrentOwnedCrane())
                {
                    controller.Invalidate();
                    return Failed("The current owned Crane was not confirmed; Crane restart was not requested.");
                }

                var craneRestart = await restartCraneCommand(
                    CreateRestartContext(commandContext, "crane", token)).ConfigureAwait(false);
                if (!craneRestart.Success)
                {
                    return new(false,
                        craneRestart.Message ?? "Crane restart did not succeed after the CM4 and Simulator reconfiguration.",
                        craneRestart.Canceled, craneRestart);
                }
                token.ThrowIfCancellationRequested();
                if (!SamePair(replacement, adapter.GetCurrentOwnedPair()))
                {
                    controller.Invalidate();
                    return Failed("The owned CM4 or Simulator identity changed while Crane was restarting.");
                }
            }

            token.ThrowIfCancellationRequested();
            return new(true, "CM4 and Simulator restart completed with a confirmed current target.",
                RestartCommandResult: simulatorRestart);
        }
        catch (OperationCanceledException) when (commandContext.CancellationToken.IsCancellationRequested)
        {
            if (replacementTargetAcknowledged) controller?.Invalidate();
            return new(false, "Reconfiguration was canceled; remote state is not inferred.", Canceled: true);
        }
        catch (OperationCanceledException)
        {
            controller?.Invalidate();
            return Failed("The bounded reconfiguration operation expired; remote state is unknown.");
        }
        finally
        {
            if (entered) operationGate.Release();
        }
    }

    private static ExecuteCommandContext CreateRestartContext(
        ExecuteCommandContext commandContext,
        string resourceName,
        CancellationToken cancellationToken) => new()
    {
        Services = commandContext.Services,
        ResourceName = resourceName,
        Logger = commandContext.Logger,
        Arguments = commandContext.Arguments,
        CancellationToken = cancellationToken,
    };

    private static OwnedCm4SimulatorReconfigurationResult Failed(string message) => new(false, message);

    private static bool SamePair(
        OwnedCm4TransportIdentityPair? expected,
        OwnedCm4TransportIdentityPair? actual) => expected is not null && actual is not null &&
        expected.Cm4 == actual.Cm4 && expected.Simulator == actual.Simulator;

    private static bool SamePhysicalIdentity(OwnedCm4TransportIdentity current, OwnedCm4TransportIdentity expected) =>
        current.RunId == expected.RunId &&
        current.EngineId == expected.EngineId &&
        current.ResourceName == expected.ResourceName &&
        current.ResourceId == expected.ResourceId &&
        current.ContainerId == expected.ContainerId;
}

internal sealed record OwnedCm4SimulatorReconfigurationResult(
    bool Success,
    string Message,
    bool Canceled = false,
    ExecuteCommandResult? RestartCommandResult = null);

/// <summary>
/// 標準CM4 SimulatorのRestart命令に、遠隔宛先の無効化と再適用を接続します。
/// </summary>
internal sealed class OwnedCm4SimulatorRestartCommandHandler(
    OwnedCm4SimulatorReconfigurationCoordinator coordinator)
{
    private IResource? boundResource;
    private ResourceCommandAnnotation? originalCommand;
    private ResourceCommandAnnotation? guardedCommand;
    private IResource? boundCm4Resource;
    private ResourceCommandAnnotation? originalCm4Command;
    private ResourceCommandAnnotation? guardedCm4Command;
    private IResource? boundCraneResource;
    private ResourceCommandAnnotation? originalCraneCommand;
    private bool craneRestartRequired;

    /// <summary>
    /// 作成済み資源のRestart命令を一度だけ捕捉し、公開メタデータを保ったガードへ置き換えます。
    /// </summary>
    internal void Install(IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (boundResource is not null && !ReferenceEquals(boundResource, resource))
        {
            throw new InvalidOperationException("The CM4 restart guard is already bound to another resource.");
        }

        var restartCommands = resource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .Where(command => command.Name == KnownResourceCommands.RestartCommand)
            .ToArray();
        if (guardedCommand is not null && restartCommands.Length == 1 &&
            ReferenceEquals(restartCommands[0], guardedCommand))
        {
            return;
        }

        if (restartCommands.Length != 1)
        {
            throw new InvalidOperationException(
                "The standard CM4 simulator Restart command was not available after resource creation.");
        }

        var command = restartCommands[0];
#pragma warning disable CS0618 // The standard Restart command does not use the obsolete parameter.
        if (command.Parameter is not null || command.Progress is not null)
        {
            throw new InvalidOperationException(
                "The standard CM4 simulator Restart command has metadata that cannot be preserved by the public command API.");
        }
#pragma warning restore CS0618

        var guarded = new ResourceCommandAnnotation(
            command.Name,
            command.DisplayName,
            command.UpdateState,
            ExecuteAsync,
            command.DisplayDescription,
            command.Arguments,
            command.ConfirmationMessage,
            command.IconName,
            command.IconVariant,
            command.IsHighlighted,
            command.Visibility,
            command.ValidateArguments);

        if (!resource.Annotations.Remove(command))
        {
            throw new InvalidOperationException("The standard CM4 simulator Restart command could not be replaced.");
        }

        resource.Annotations.Add(guarded);
        boundResource = resource;
        originalCommand = command;
        guardedCommand = guarded;
    }

    /// <summary>標準CM4の再起動命令を、Simulatorの元命令と組み合わせるガードへ一度だけ置き換えます。</summary>
    internal void InstallCm4(IResource cm4Resource, IResource simulatorResource, IResource? craneResource = null)
    {
        ArgumentNullException.ThrowIfNull(cm4Resource);
        ArgumentNullException.ThrowIfNull(simulatorResource);
        if (craneResource is not null)
        {
            craneRestartRequired = true;
            CaptureCraneRestart(craneResource);
        }
        if (boundCm4Resource is not null && !ReferenceEquals(boundCm4Resource, cm4Resource))
        {
            throw new InvalidOperationException("The CM4 restart guard is already bound to another resource.");
        }
        if (boundCm4Resource is not null && guardedCm4Command is not null &&
            cm4Resource.Annotations.OfType<ResourceCommandAnnotation>().Any(command =>
                command.Name == KnownResourceCommands.RestartCommand && ReferenceEquals(command, guardedCm4Command)))
        {
            return;
        }

        if (boundResource is not null && !ReferenceEquals(boundResource, simulatorResource))
        {
            throw new InvalidOperationException("The Simulator restart guard is bound to another resource.");
        }
        var restartCommands = cm4Resource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .Where(command => command.Name == KnownResourceCommands.RestartCommand)
            .ToArray();
        if (restartCommands.Length == 0)
        {
            return;
        }
        if (restartCommands.Length != 1)
        {
            throw new InvalidOperationException("The standard CM4 Restart command was not available before startup.");
        }

        var simulatorRestart = originalCommand ?? simulatorResource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .SingleOrDefault(command => command.Name == KnownResourceCommands.RestartCommand);
        if (simulatorRestart is null)
        {
            throw new InvalidOperationException("The captured Simulator Restart command is unavailable.");
        }

        var command = restartCommands[0];
#pragma warning disable CS0618 // The standard Restart command does not use the obsolete parameter.
        if (command.Parameter is not null || command.Progress is not null)
        {
            throw new InvalidOperationException("The standard CM4 Restart command has metadata that cannot be preserved by the public command API.");
        }
#pragma warning restore CS0618

        var guarded = new ResourceCommandAnnotation(
            command.Name,
            command.DisplayName,
            command.UpdateState,
            context => ExecuteCm4Async(context, simulatorRestart.ExecuteCommand),
            command.DisplayDescription,
            command.Arguments,
            command.ConfirmationMessage,
            command.IconName,
            command.IconVariant,
            command.IsHighlighted,
            command.Visibility,
            command.ValidateArguments);
        if (!cm4Resource.Annotations.Remove(command))
        {
            throw new InvalidOperationException("The standard CM4 Restart command could not be replaced.");
        }

        cm4Resource.Annotations.Add(guarded);
        boundCm4Resource = cm4Resource;
        originalCm4Command = command;
        guardedCm4Command = guarded;
    }

    /// <summary>既存のCrane再起動処理を捕捉し、再構成成功後に同じ処理を直接呼び出せるようにします。</summary>
    internal void CaptureCraneRestart(IResource craneResource)
    {
        ArgumentNullException.ThrowIfNull(craneResource);
        if (boundCraneResource is not null && !ReferenceEquals(boundCraneResource, craneResource))
        {
            throw new InvalidOperationException("The Crane restart callback is already bound to another resource.");
        }

        var restartCommands = craneResource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .Where(command => command.Name == KnownResourceCommands.RestartCommand)
            .ToArray();
        if (restartCommands.Length == 0)
        {
            return;
        }
        if (restartCommands.Length != 1)
        {
            throw new InvalidOperationException("The standard Crane Restart command was not unique.");
        }

        if (originalCraneCommand is not null && !ReferenceEquals(originalCraneCommand, restartCommands[0]))
        {
            throw new InvalidOperationException("The captured Crane Restart command changed after installation.");
        }

        boundCraneResource = craneResource;
        originalCraneCommand = restartCommands[0];
    }

    /// <summary>
    /// 無効化確認後に捕捉した元のRestart処理を呼び、交換後の宛先適用までを実行します。
    /// </summary>
    internal async Task<ExecuteCommandResult> ExecuteAsync(ExecuteCommandContext context)
    {
        var original = originalCommand?.ExecuteCommand;
        if (original is null)
        {
            return new ExecuteCommandResult
            {
                Success = false,
                Message = "The standard CM4 simulator Restart command is not bound.",
            };
        }

        var result = await coordinator.ExecuteAsync(context, original).ConfigureAwait(false);
        if (result.Success && result.RestartCommandResult is not null)
        {
            return result.RestartCommandResult;
        }

        return new ExecuteCommandResult
        {
            Success = false,
            Message = result.Message,
            Canceled = result.Canceled,
        };
    }

    private async Task<ExecuteCommandResult> ExecuteCm4Async(
        ExecuteCommandContext context,
        Func<ExecuteCommandContext, Task<ExecuteCommandResult>> restartSimulatorCommand)
    {
        var restartCm4Command = originalCm4Command?.ExecuteCommand;
        if (restartCm4Command is null)
        {
            return new ExecuteCommandResult { Success = false, Message = "The standard CM4 Restart command is not bound." };
        }
        if (craneRestartRequired && originalCraneCommand?.ExecuteCommand is null)
        {
            return new ExecuteCommandResult { Success = false, Message = "The standard Crane Restart command is not bound." };
        }

        var result = await coordinator.ExecuteCm4RestartAsync(
            context,
            restartCm4Command,
            restartSimulatorCommand,
            originalCraneCommand?.ExecuteCommand).ConfigureAwait(false);
        if (result.Success && result.RestartCommandResult is not null) return result.RestartCommandResult;
        return new ExecuteCommandResult { Success = false, Message = result.Message, Canceled = result.Canceled };
    }
}

internal sealed class DcpOwnedCm4SimulatorReconfigurationAdapter(
    DcpBaseResourceReadinessRegistry registry,
    DcpBaseResourceOwnershipEventConsumer ownership,
    DcpBaseResourceReadinessEventConsumer events,
    ResourceNotificationService notifications,
    IResource simulator,
    IResource cm4,
    string? expectedEngineId,
    Func<OwnedCm4TransportServerSessionManager?>? getSessionManager = null,
    IResource? crane = null) : IOwnedCm4SimulatorReconfigurationAdapter
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

    public bool IsCurrentOwnedCrane()
    {
        var pair = GetCurrentOwnedPair();
        var candidate = crane is null
            ? null
            : registry.GetCandidates().SingleOrDefault(item => ReferenceEquals(item.Resource, crane));
        return pair is not null && candidate is not null && candidate.Role == "crane" &&
            candidate.RunId == pair.Cm4.RunId && IsOwnedRunning(candidate);
    }

    public async Task<ExecuteCommandResult> RestartSimulatorAsync(
        Func<ExecuteCommandContext, Task<ExecuteCommandResult>> restartCommand,
        ExecuteCommandContext commandContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await restartCommand(commandContext).ConfigureAwait(false);
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
            if (pair is not null && SamePhysicalIdentity(pair.Cm4, previous.Cm4) &&
                pair.Simulator.Generation > previous.Simulator.Generation &&
                pair.Simulator.ContainerId != previous.Simulator.ContainerId)
            {
                return pair;
            }
        }

        return null;
    }

    public async Task<OwnedCm4TransportIdentityPair?> WaitForCm4ReplacementAsync(
        OwnedCm4TransportIdentityPair previous,
        CancellationToken cancellationToken)
    {
        var sessionManager = getSessionManager?.Invoke();
        var currentCandidate = GetCurrentCm4ReplacementCandidate(previous);
        if (currentCandidate is not null)
        {
            if (sessionManager is null ||
                !await sessionManager.WaitForSessionStartedAsync(currentCandidate, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            await ownership.VerifyCurrentAsync(resourceId =>
                notifications.TryGetCurrentState(resourceId, out var state) ? state : null,
                cancellationToken).ConfigureAwait(false);
            var currentPair = GetCurrentOwnedPair();
            if (IsCm4ReplacementFor(previous, currentPair) &&
                IsCurrentWaitedCm4(currentCandidate, currentPair)) return currentPair;
            return null;
        }

        await foreach (var observation in notifications.WatchAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!ReferenceEquals(observation.Resource, cm4) ||
                !notifications.TryGetCurrentState(observation.ResourceId, out var current))
            {
                continue;
            }

            events.Observe(observation, current, "cm4-sim", resourceId =>
                notifications.TryGetCurrentState(resourceId, out var state) ? state : null);
            await ownership.VerifyCurrentAsync(resourceId =>
                notifications.TryGetCurrentState(resourceId, out var state) ? state : null,
                cancellationToken).ConfigureAwait(false);

            var candidate = GetCurrentCm4ReplacementCandidate(previous);
            if (candidate is null) continue;
            if (sessionManager is null ||
                !await sessionManager.WaitForSessionStartedAsync(candidate, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            await ownership.VerifyCurrentAsync(resourceId =>
                notifications.TryGetCurrentState(resourceId, out var state) ? state : null,
                cancellationToken).ConfigureAwait(false);
            var pair = GetCurrentOwnedPair();
            if (IsCm4ReplacementFor(previous, pair) && IsCurrentWaitedCm4(candidate, pair)) return pair;
            return null;
        }

        return null;
    }

    private DcpBaseResourceIdentityCandidate? GetCurrentCm4ReplacementCandidate(
        OwnedCm4TransportIdentityPair previous) => registry.GetCandidates().SingleOrDefault(candidate =>
        ReferenceEquals(candidate.Resource, cm4) && candidate.Role == "cm4-sim" &&
        candidate.State == "Running" && !candidate.ResourceKeyAmbiguous &&
        OwnedContainerInspection.IsFullId(candidate.ContainerId) &&
        candidate.Generation > previous.Cm4.Generation &&
        candidate.ContainerId != previous.Cm4.ContainerId);

    private bool IsCm4ReplacementFor(
        OwnedCm4TransportIdentityPair previous,
        OwnedCm4TransportIdentityPair? current) => current is not null &&
        SamePhysicalIdentity(current.Simulator, previous.Simulator) &&
        current.Cm4.Generation > previous.Cm4.Generation &&
        current.Cm4.ContainerId != previous.Cm4.ContainerId &&
        SamePair(current, GetCurrentOwnedPair());

    private static bool IsCurrentWaitedCm4(
        DcpBaseResourceIdentityCandidate waited,
        OwnedCm4TransportIdentityPair? current) => current is not null &&
        current.Cm4.RunId == waited.RunId &&
        current.Cm4.ResourceName == waited.ResourceName &&
        current.Cm4.ResourceId == waited.ResourceId &&
        current.Cm4.ContainerId == waited.ContainerId;

    private static bool SamePair(OwnedCm4TransportIdentityPair? expected, OwnedCm4TransportIdentityPair? actual) =>
        expected is not null && actual is not null && expected.Cm4 == actual.Cm4 && expected.Simulator == actual.Simulator;

    private bool IsOwnedRunning(DcpBaseResourceIdentityCandidate? candidate) =>
        candidate is not null && candidate.State == "Running" && !candidate.ResourceKeyAmbiguous &&
        !string.IsNullOrWhiteSpace(candidate.ResourceId) && OwnedContainerInspection.IsFullId(candidate.ContainerId) &&
        candidate.IdentityVerified && candidate.NetworkVerified && candidate.OwnershipVerified &&
        registry.AreOwnershipProofsCurrent([candidate]);

    private static bool SamePhysicalIdentity(OwnedCm4TransportIdentity current, OwnedCm4TransportIdentity expected) =>
        current.RunId == expected.RunId &&
        current.EngineId == expected.EngineId &&
        current.ResourceName == expected.ResourceName &&
        current.ResourceId == expected.ResourceId &&
        current.ContainerId == expected.ContainerId;
}
