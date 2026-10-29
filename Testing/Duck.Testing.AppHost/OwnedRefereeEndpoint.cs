using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duck.Testing.AppHost;

internal enum OwnedEndpointPhase
{
    Initial,
    Reconfiguring,
    Active,
    Suspended,
}

internal sealed record OwnedEndpointSnapshot(
    long Generation,
    OwnedEndpointPhase Phase,
    string? SourceContainerId,
    string? Uri);

/// <summary>RefereeDriverへ渡す接続先と、そのGame Controller世代を直列化して保持します。</summary>
internal sealed class OwnedRefereeEndpointState(string initialUri)
{
    private readonly object sync = new();
    private long generation;
    private OwnedEndpointPhase phase = OwnedEndpointPhase.Initial;
    private string? sourceContainerId;
    private string? uri;

    internal string GetUriForEnvironment()
    {
        lock (sync)
        {
            return phase switch
            {
                OwnedEndpointPhase.Initial => initialUri,
                OwnedEndpointPhase.Active when uri is not null => uri,
                _ => throw new InvalidOperationException("検証済みの Game Controller 接続先がありません。"),
            };
        }
    }

    internal OwnedEndpointSnapshot Snapshot()
    {
        lock (sync)
        {
            return new(generation, phase, sourceContainerId, uri);
        }
    }

    internal long BeginReconfiguration()
    {
        lock (sync)
        {
            if (phase == OwnedEndpointPhase.Reconfiguring)
            {
                throw new InvalidOperationException("Game Controller 接続先の再構成が進行中です。");
            }

            generation = checked(generation + 1);
            phase = OwnedEndpointPhase.Reconfiguring;
            sourceContainerId = null;
            uri = null;
            return generation;
        }
    }

    internal bool TryActivate(long expectedGeneration, string containerId, string endpointUri)
    {
        lock (sync)
        {
            if (phase != OwnedEndpointPhase.Reconfiguring || generation != expectedGeneration)
            {
                return false;
            }

            phase = OwnedEndpointPhase.Active;
            sourceContainerId = containerId;
            uri = endpointUri;
            return true;
        }
    }

    internal bool Suspend(long expectedGeneration)
    {
        lock (sync)
        {
            if (generation != expectedGeneration || phase == OwnedEndpointPhase.Suspended)
            {
                return false;
            }

            generation = checked(generation + 1);
            phase = OwnedEndpointPhase.Suspended;
            sourceContainerId = null;
            uri = null;
            return true;
        }
    }

    internal bool SuspendForSourceObservation(OwnedResourceObservation? current)
    {
        lock (sync)
        {
            if (phase != OwnedEndpointPhase.Active)
            {
                return false;
            }

            if (current is not null &&
                string.Equals(current.State, KnownResourceStates.Running, StringComparison.Ordinal) &&
                string.Equals(current.ContainerId, sourceContainerId, StringComparison.Ordinal))
            {
                return false;
            }

            generation = checked(generation + 1);
            phase = OwnedEndpointPhase.Suspended;
            sourceContainerId = null;
            uri = null;
            return true;
        }
    }

    internal bool IsActive(long expectedGeneration, string containerId)
    {
        lock (sync)
        {
            return phase == OwnedEndpointPhase.Active &&
                generation == expectedGeneration &&
                string.Equals(sourceContainerId, containerId, StringComparison.Ordinal);
        }
    }

    internal bool IsSuspended()
    {
        lock (sync)
        {
            return phase == OwnedEndpointPhase.Suspended;
        }
    }
}

internal sealed record OwnedResourceObservation(
    IResource Resource,
    string ResourceId,
    string State,
    string? ContainerId)
{
    internal bool IsRunningOrStarting =>
        string.Equals(State, KnownResourceStates.Running, StringComparison.Ordinal) ||
        string.Equals(State, "Starting", StringComparison.Ordinal);

    internal bool IsStopped =>
        string.Equals(State, KnownResourceStates.Exited, StringComparison.Ordinal) ||
        string.Equals(State, KnownResourceStates.Finished, StringComparison.Ordinal) ||
        string.Equals(State, KnownResourceStates.NotStarted, StringComparison.Ordinal);
}

internal static class OwnedResourceObservationProjection
{
    private const string ContainerIdProperty = "container.id";

    internal static OwnedResourceObservation? TryProject(ResourceEvent observation)
    {
        if (string.IsNullOrWhiteSpace(observation.ResourceId))
        {
            return null;
        }

        var state = observation.Snapshot.State?.Text;
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        string? containerId = null;
        var properties = observation.Snapshot.Properties;
        if (!properties.IsDefaultOrEmpty)
        {
            foreach (var property in properties)
            {
                if (!string.Equals(property.Name, ContainerIdProperty, StringComparison.Ordinal))
                {
                    continue;
                }

                if (containerId is not null || property.Value is not string value || !OwnedContainerInspection.IsFullId(value))
                {
                    containerId = null;
                    break;
                }

                containerId = value;
            }
        }

        return new OwnedResourceObservation(observation.Resource, observation.ResourceId, state, containerId);
    }
}

internal interface IOwnedReconfigurationAdapter
{
    OwnedResourceObservation? GetCurrent(IResource resource);

    OwnedResourceObservation? GetCurrent(string resourceId);

    IAsyncEnumerable<OwnedResourceObservation> WatchAsync(CancellationToken cancellationToken);

    Task<bool> ExecuteLifecycleCommandAsync(
        IResource resource,
        string commandName,
        CancellationToken cancellationToken);

    Task<string> CaptureDockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

/// <summary>Aspire通知とresource commandを、再構成処理が使う狭い境界へ結び付けます。</summary>
internal sealed class AspireOwnedReconfigurationAdapter(
    ResourceNotificationService notifications,
    ResourceCommandService commands,
    IDockerCommandRunner dockerRunner,
    string dockerContext) : IOwnedReconfigurationAdapter
{
    public OwnedResourceObservation? GetCurrent(IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return notifications.TryGetCurrentState(resource.Name, out var current) &&
            ReferenceEquals(current.Resource, resource)
                ? OwnedResourceObservationProjection.TryProject(current)
                : null;
    }

    public OwnedResourceObservation? GetCurrent(string resourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        return notifications.TryGetCurrentState(resourceId, out var current)
            ? OwnedResourceObservationProjection.TryProject(current)
            : null;
    }

    public async IAsyncEnumerable<OwnedResourceObservation> WatchAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var observation in notifications.WatchAsync(cancellationToken).ConfigureAwait(false))
        {
            var projected = OwnedResourceObservationProjection.TryProject(observation);
            if (projected is not null)
            {
                yield return projected;
            }
        }
    }

    public async Task<bool> ExecuteLifecycleCommandAsync(
        IResource resource,
        string commandName,
        CancellationToken cancellationToken)
    {
        var result = await commands.ExecuteCommandAsync(resource, commandName, cancellationToken).ConfigureAwait(false);
        return result.Success;
    }

    public Task<string> CaptureDockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        dockerRunner.CaptureAsync(["--context", dockerContext, .. arguments], cancellationToken);
}

internal sealed record OwnedReconfigurationResult(
    bool Success,
    string Message,
    string? ErrorMessage = null,
    bool Canceled = false);

/// <summary>所有確認済みの Game Controller と RefereeDriver だけを順序どおりに再構成します。</summary>
internal sealed class OwnedRefereeReconfigurationCoordinator(
    IOwnedReconfigurationAdapter adapter,
    IResource gameController,
    IResource refereeDriver,
    OwnedRefereeEndpointState endpointState,
    string runId,
    string expectedEngineId,
    TimeSpan timeout,
    ILogger? logger = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Task<bool>? lateStartCleanup;
    private const string GameControllerRole = "game-controller";
    private const string RefereeDriverRole = "referee-driver";

    internal async Task HandleUnexpectedSourceObservationAsync(OwnedResourceObservation observation)
    {
        var current = adapter.GetCurrent(gameController);
        if (!ReferenceEquals(observation.Resource, gameController) ||
            !string.Equals(observation.ResourceId, gameController.Name, StringComparison.Ordinal) ||
            current is null || current.ContainerId != observation.ContainerId || current.State != observation.State ||
            !endpointState.SuspendForSourceObservation(observation))
        {
            return;
        }

        var stopped = await BestEffortStopOwnedRefereeAsync().ConfigureAwait(false);
        if (!stopped)
        {
            lateStartCleanup = ObserveLateDependentAsync(Task.FromResult(true), runningWasObserved: true);
        }
    }

    /// <summary>現在の所有者を先に照合し、依存資源を停止してから送信先を更新します。</summary>
    internal async Task<OwnedReconfigurationResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return Failure("再構成処理は既に実行中です。");
        }

        if (lateStartCleanup is not null)
        {
            if (!lateStartCleanup.IsCompleted || !lateStartCleanup.IsCompletedSuccessfully || !lateStartCleanup.Result)
            {
                gate.Release();
                return Failure("前回の RefereeDriver 開始後確認が未完了または失敗しているため、再構成を受け付けません。");
            }

            lateStartCleanup = null;
        }

        using var operationDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationDeadline.CancelAfter(timeout);
        var operationToken = operationDeadline.Token;

        long generation = 0;
        var lifecycleStarted = false;
        var refereeWasRunning = false;
        Task<bool>? startCommandTask = null;
        var dependentRunningObserved = false;
        try
        {
            operationToken.ThrowIfCancellationRequested();
            var source = adapter.GetCurrent(gameController);
            var dependent = adapter.GetCurrent(refereeDriver);
            if (!IsCurrent(source, gameController) || !IsCurrent(dependent, refereeDriver) ||
                !string.Equals(source!.State, KnownResourceStates.Running, StringComparison.Ordinal) ||
                !(dependent!.IsRunningOrStarting || dependent.IsStopped) ||
                !OwnedContainerInspection.IsFullId(source!.ContainerId) ||
                !OwnedContainerInspection.IsFullId(dependent.ContainerId))
            {
                return Failure("現在登録されている完全 ID を確認できません。");
            }

            var initialEngine = await ReadEngineIdAsync(operationToken).ConfigureAwait(false);
            if (initialEngine is null)
            {
                return Failure("Docker Engine が期待値と一致しません。");
            }

            var sourceInspection = await InspectAsync(source.ContainerId!, operationToken).ConfigureAwait(false);
            var dependentInspection = await InspectAsync(dependent.ContainerId!, operationToken).ConfigureAwait(false);
            if (!OwnedContainerInspection.MatchesOwner(sourceInspection, source.ContainerId!, runId, GameControllerRole, requireRunning: true) ||
                !OwnedContainerInspection.MatchesOwner(dependentInspection, dependent.ContainerId!, runId, RefereeDriverRole, requireRunning: false) ||
                !StillCurrent(source, gameController) || !StillCurrent(dependent, refereeDriver))
            {
                return Failure("Game Controller または RefereeDriver の所有情報が一致しません。");
            }

            var engineAfterPreflight = await ReadEngineIdAsync(operationToken).ConfigureAwait(false);
            if (engineAfterPreflight is null || !string.Equals(engineAfterPreflight, initialEngine, StringComparison.Ordinal))
            {
                return Failure("所有確認の途中で Docker Engine が変わりました。");
            }
            if (!StillCurrent(source, gameController) || !StillCurrent(dependent, refereeDriver) ||
                !(dependent.IsRunningOrStarting || dependent.IsStopped) ||
                !await IsOwnedAsync(source.ContainerId!, GameControllerRole, requireRunning: true, operationToken).ConfigureAwait(false) ||
                !await IsOwnedAsync(dependent.ContainerId!, RefereeDriverRole, requireRunning: dependent.IsRunningOrStarting, operationToken).ConfigureAwait(false) ||
                !StillCurrent(source, gameController) || !StillCurrent(dependent, refereeDriver))
            {
                return Failure("ライフサイクル命令の直前に現在所有者を再確認できません。");
            }

            operationToken.ThrowIfCancellationRequested();
            generation = endpointState.BeginReconfiguration();
            lifecycleStarted = true;
            refereeWasRunning = dependent.IsRunningOrStarting;
            if (refereeWasRunning)
            {
                var stopSucceeded = await adapter.ExecuteLifecycleCommandAsync(
                    refereeDriver, KnownResourceCommands.StopCommand, operationToken).WaitAsync(operationToken).ConfigureAwait(false);
                if (!stopSucceeded || await WaitForStateAsync(
                        refereeDriver,
                        observation => observation.IsStopped,
                        operationToken).ConfigureAwait(false) is null)
                {
                    return await FailAfterMutationAsync(generation, "RefereeDriver を安全に停止できませんでした。").ConfigureAwait(false);
                }
            }

            operationToken.ThrowIfCancellationRequested();
            if (!StillCurrent(source, gameController) || !await IsOwnedAsync(
                    source.ContainerId!, GameControllerRole, requireRunning: true, operationToken).ConfigureAwait(false))
            {
                return await FailAfterMutationAsync(generation, "再起動前に Game Controller の世代または所有者が変わりました。").ConfigureAwait(false);
            }

            if (!await adapter.ExecuteLifecycleCommandAsync(
                    gameController, KnownResourceCommands.RestartCommand, operationToken).WaitAsync(operationToken).ConfigureAwait(false))
            {
                return await FailAfterMutationAsync(generation, "Game Controller の再起動命令に失敗しました。").ConfigureAwait(false);
            }

            var restartedSource = await WaitForStateAsync(
                gameController,
                observation => string.Equals(observation.State, KnownResourceStates.Running, StringComparison.Ordinal) &&
                    OwnedContainerInspection.IsFullId(observation.ContainerId) && observation.ContainerId != source.ContainerId,
                operationToken).ConfigureAwait(false);
            if (restartedSource is null)
            {
                return await FailAfterMutationAsync(generation, "新しい Game Controller 世代の Running を確認できません。").ConfigureAwait(false);
            }

            var engineAfterRestart = await ReadEngineIdAsync(operationToken).ConfigureAwait(false);
            if (engineAfterRestart is null || !string.Equals(engineAfterRestart, initialEngine, StringComparison.Ordinal))
            {
                return await FailAfterMutationAsync(generation, "再起動後に Docker Engine が変わりました。").ConfigureAwait(false);
            }

            var restartedInspection = await InspectAsync(restartedSource.ContainerId!, operationToken).ConfigureAwait(false);
            var engineAfterInspection = await ReadEngineIdAsync(operationToken).ConfigureAwait(false);
            if (!OwnedContainerInspection.MatchesOwner(restartedInspection, restartedSource.ContainerId!, runId, GameControllerRole, requireRunning: true) ||
                !OwnedContainerInspection.TryReadNetworkEndpoint(restartedInspection, out var endpoint) ||
                engineAfterInspection is null || !string.Equals(engineAfterInspection, initialEngine, StringComparison.Ordinal) ||
                !CurrentMatches(gameController, restartedSource))
            {
                return await FailAfterMutationAsync(generation, "再作成後の Game Controller 所有者またはネットワーク IPv4 を検証できません。").ConfigureAwait(false);
            }

            var uri = $"ws://{endpoint!.Address}:8082/api/control";
            if (!endpointState.TryActivate(generation, restartedSource.ContainerId!, uri))
            {
                return await FailAfterMutationAsync(generation, "Game Controller の新しい送信先を安全に公開できません。").ConfigureAwait(false);
            }

            operationToken.ThrowIfCancellationRequested();
            if (!endpointState.IsActive(generation, restartedSource.ContainerId!) || !CurrentMatches(gameController, restartedSource))
            {
                return await FailAfterMutationAsync(generation, "RefereeDriver の開始前に Game Controller 世代が変わりました。").ConfigureAwait(false);
            }
            var dependentBeforeStart = adapter.GetCurrent(refereeDriver);
            var dependentBeforeStartOwned = dependentBeforeStart is not null &&
                ReferenceEquals(dependentBeforeStart.Resource, refereeDriver) &&
                string.Equals(dependentBeforeStart.ResourceId, refereeDriver.Name, StringComparison.Ordinal) &&
                dependentBeforeStart.IsStopped &&
                (dependentBeforeStart.ContainerId is null ||
                 OwnedContainerInspection.IsFullId(dependentBeforeStart.ContainerId) &&
                 dependentBeforeStart.ContainerId == dependent.ContainerId &&
                 await IsOwnedAsync(
                     dependentBeforeStart.ContainerId!,
                     RefereeDriverRole,
                     requireRunning: dependentBeforeStart.IsRunningOrStarting,
                     operationToken).ConfigureAwait(false));
            if (!dependentBeforeStartOwned || !SameObservation(adapter.GetCurrent(refereeDriver), dependentBeforeStart!, refereeDriver))
            {
                return await FailAfterMutationAsync(generation, "RefereeDriver の所有者を再作成前に確認できません。").ConfigureAwait(false);
            }
            startCommandTask = adapter.ExecuteLifecycleCommandAsync(
                refereeDriver, KnownResourceCommands.StartCommand, operationToken);
            var startSucceeded = await startCommandTask.WaitAsync(operationToken).ConfigureAwait(false);
            if (!startSucceeded)
            {
                return await FailAfterMutationAsync(
                    generation,
                    "RefereeDriver の再作成命令に失敗しました。",
                    startCommandTask).ConfigureAwait(false);
            }
            operationToken.ThrowIfCancellationRequested();

            var restartedDependent = await WaitForStateAsync(
                refereeDriver,
                observation => string.Equals(observation.State, KnownResourceStates.Running, StringComparison.Ordinal) &&
                    OwnedContainerInspection.IsFullId(observation.ContainerId) &&
                    !string.Equals(observation.ContainerId, dependent.ContainerId, StringComparison.Ordinal),
                operationToken).ConfigureAwait(false);
            if (restartedDependent is null)
            {
                return await FailAfterMutationAsync(
                    generation,
                    "RefereeDriver の新しい Running 世代を確認できません。",
                    startCommandTask).ConfigureAwait(false);
            }
            dependentRunningObserved = true;
            var restartedDependentInspection = await InspectAsync(restartedDependent.ContainerId!, operationToken).ConfigureAwait(false);
            if (!OwnedContainerInspection.MatchesOwner(
                    restartedDependentInspection,
                    restartedDependent.ContainerId!,
                    runId,
                    RefereeDriverRole,
                    requireRunning: true) ||
                !OwnedContainerInspection.TryReadNetworkEndpoint(restartedDependentInspection, out var dependentEndpoint) ||
                !string.Equals(endpoint.NetworkName, dependentEndpoint!.NetworkName, StringComparison.Ordinal) ||
                !endpointState.IsActive(generation, restartedSource.ContainerId!) ||
                !CurrentMatches(gameController, restartedSource) ||
                !CurrentMatches(refereeDriver, restartedDependent))
            {
                return await FailAfterMutationAsync(
                    generation,
                    "RefereeDriver の新しい所有世代または Game Controller 世代を確認できません。",
                    startCommandTask,
                    dependentRunningObserved).ConfigureAwait(false);
            }

            return new OwnedReconfigurationResult(true, "新しい Game Controller の送信先を反映しました。");
        }
        catch (OperationCanceledException)
        {
            if (lifecycleStarted)
            {
                return await FailAfterMutationAsync(
                    generation,
                    "再構成は取り消され、安全状態へ移行しました。",
                    startCommandTask,
                    dependentRunningObserved,
                    canceled: cancellationToken.IsCancellationRequested).ConfigureAwait(false);
            }

            return new OwnedReconfigurationResult(
                false,
                cancellationToken.IsCancellationRequested ? "再構成は取り消されました。" : "再構成の期限を超えました。",
                cancellationToken.IsCancellationRequested ? "cancelled" : "timeout",
                Canceled: cancellationToken.IsCancellationRequested);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Owned Game Controller reconfiguration failed.");
            return lifecycleStarted
                ? await FailAfterMutationAsync(
                    generation,
                    "再構成に失敗し、安全状態へ移行しました。",
                    startCommandTask,
                    dependentRunningObserved).ConfigureAwait(false)
                : Failure("再構成前の所有確認に失敗しました。");
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string?> ReadEngineIdAsync(CancellationToken cancellationToken)
    {
        var output = await adapter.CaptureDockerAsync(["info", "--format", "{{.ID}}"], cancellationToken).ConfigureAwait(false);
        var engineId = output.Trim();
        return string.Equals(engineId, expectedEngineId, StringComparison.Ordinal) ? engineId : null;
    }

    private Task<string> InspectAsync(string containerId, CancellationToken cancellationToken) =>
        adapter.CaptureDockerAsync(["inspect", "--format", "{{json .}}", containerId], cancellationToken);

    private async Task<bool> IsOwnedAsync(string containerId, string role, bool requireRunning, CancellationToken cancellationToken)
    {
        var engineId = await ReadEngineIdAsync(cancellationToken).ConfigureAwait(false);
        if (engineId is null)
        {
            return false;
        }

        var json = await InspectAsync(containerId, cancellationToken).ConfigureAwait(false);
        return OwnedContainerInspection.MatchesOwner(json, containerId, runId, role, requireRunning);
    }

    private async Task<OwnedResourceObservation?> WaitForStateAsync(
        IResource resource,
        Func<OwnedResourceObservation, bool> predicate,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var current = adapter.GetCurrent(resource);
        if (IsCurrent(current, resource) && predicate(current!))
        {
            return current;
        }

        try
        {
            await foreach (var observation in adapter.WatchAsync(timeoutSource.Token).ConfigureAwait(false))
            {
                if (ReferenceEquals(observation.Resource, resource) &&
                    string.Equals(observation.ResourceId, resource.Name, StringComparison.Ordinal) &&
                    predicate(observation))
                {
                    var latest = adapter.GetCurrent(resource);
                    if (latest is not null && latest.ContainerId == observation.ContainerId && predicate(latest))
                    {
                        return latest;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        return null;
    }

    private bool StillCurrent(OwnedResourceObservation observation, IResource resource) => CurrentMatches(resource, observation);

    private bool CurrentMatches(IResource resource, OwnedResourceObservation expected)
    {
        var current = adapter.GetCurrent(resource);
        return IsCurrent(current, resource) && current!.ContainerId == expected.ContainerId && current.State == expected.State;
    }

    private static bool SameObservation(OwnedResourceObservation? current, OwnedResourceObservation expected, IResource resource) =>
        current is not null && ReferenceEquals(current.Resource, resource) &&
        string.Equals(current.ResourceId, resource.Name, StringComparison.Ordinal) &&
        current.ContainerId == expected.ContainerId && current.State == expected.State;

    private static bool IsCurrent(OwnedResourceObservation? observation, IResource resource) =>
        observation is not null && ReferenceEquals(observation.Resource, resource) &&
        string.Equals(observation.ResourceId, resource.Name, StringComparison.Ordinal) &&
        OwnedContainerInspection.IsFullId(observation.ContainerId);

    private async Task<OwnedReconfigurationResult> FailAfterMutationAsync(
        long generation,
        string message,
        Task<bool>? possibleLateStart = null,
        bool runningWasObserved = false,
        bool canceled = false)
    {
        endpointState.Suspend(generation);
        var dependentStopped = await BestEffortStopOwnedRefereeAsync().ConfigureAwait(false);
        if (possibleLateStart is not null)
        {
            lateStartCleanup = ObserveLateDependentAsync(possibleLateStart, runningWasObserved || dependentStopped);
        }
        return new OwnedReconfigurationResult(false, message, message, Canceled: canceled);
    }

    private async Task<bool> ObserveLateDependentAsync(Task<bool> possibleLateStart, bool runningWasObserved)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        try
        {
            await possibleLateStart.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "RefereeDriver start command finished with an uncertain result.");
        }

        var initial = adapter.GetCurrent(refereeDriver);
        if (runningWasObserved && initial is not null && initial.IsStopped)
        {
            return true;
        }

        var stopRequested = false;
        try
        {
            while (!timeoutSource.IsCancellationRequested)
            {
                var current = adapter.GetCurrent(refereeDriver);
                if (current is not null && ReferenceEquals(current.Resource, refereeDriver) &&
                    current.ResourceId == refereeDriver.Name && current.IsRunningOrStarting &&
                    OwnedContainerInspection.IsFullId(current.ContainerId))
                {
                    if (!await IsOwnedAsync(current.ContainerId!, RefereeDriverRole, requireRunning: true, timeoutSource.Token).ConfigureAwait(false) ||
                        !SameObservation(adapter.GetCurrent(refereeDriver), current, refereeDriver))
                    {
                        return false;
                    }

                    if (string.Equals(current.State, KnownResourceStates.Running, StringComparison.Ordinal) && !stopRequested)
                    {
                        if (!await adapter.ExecuteLifecycleCommandAsync(
                                refereeDriver,
                                KnownResourceCommands.StopCommand,
                                timeoutSource.Token).ConfigureAwait(false))
                        {
                            return false;
                        }

                        stopRequested = true;
                    }
                }
                else if (current is not null && current.IsStopped && (stopRequested || runningWasObserved))
                {
                    return true;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), timeoutSource.Token).ConfigureAwait(false);
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Could not observe a late RefereeDriver start after cancellation.");
            return false;
        }
    }

    private async Task<bool> BestEffortStopOwnedRefereeAsync()
    {
        try
        {
            var current = adapter.GetCurrent(refereeDriver);
            if (current is null || !ReferenceEquals(current.Resource, refereeDriver) ||
                !string.Equals(current.ResourceId, refereeDriver.Name, StringComparison.Ordinal))
            {
                return false;
            }

            if (current.IsStopped)
            {
                return true;
            }

            if (string.Equals(current.State, KnownResourceStates.Stopping, StringComparison.Ordinal))
            {
                return await WaitForStateAsync(refereeDriver, observation => observation.IsStopped, CancellationToken.None)
                    .ConfigureAwait(false) is not null;
            }

            if (!current.IsRunningOrStarting || !OwnedContainerInspection.IsFullId(current.ContainerId) ||
                !await IsOwnedAsync(current.ContainerId!, RefereeDriverRole, requireRunning: true, CancellationToken.None).ConfigureAwait(false) ||
                !StillCurrent(current, refereeDriver))
            {
                return false;
            }

            return await adapter.ExecuteLifecycleCommandAsync(
                    refereeDriver, KnownResourceCommands.StopCommand, CancellationToken.None).ConfigureAwait(false) &&
                await WaitForStateAsync(refereeDriver, observation => observation.IsStopped, CancellationToken.None)
                    .ConfigureAwait(false) is not null;
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Could not stop the verified RefereeDriver after reconfiguration failure.");
            return false;
        }
    }

    private static OwnedReconfigurationResult Failure(string message) => new(false, message, message);
}

/// <summary>有効な送信先の Game Controller 世代が変わった通知を監視します。</summary>
internal sealed class OwnedSourceGenerationMonitor(
    IOwnedReconfigurationAdapter adapter,
    OwnedRefereeReconfigurationCoordinator coordinator,
    IResource gameController,
    ILogger<OwnedSourceGenerationMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var observation in adapter.WatchAsync(stoppingToken).ConfigureAwait(false))
            {
                if (ReferenceEquals(observation.Resource, gameController) &&
                    string.Equals(observation.ResourceId, gameController.Name, StringComparison.Ordinal))
                {
                    await coordinator.HandleUnexpectedSourceObservationAsync(observation).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Game Controller generation monitoring stopped unexpectedly.");
            throw;
        }
    }
}

internal sealed record OwnedContainerNetworkEndpoint(string NetworkName, IPAddress Address);

internal static class OwnedContainerInspection
{
    internal static bool IsFullId(string? value)
    {
        if (value is null || value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!((character >= '0' && character <= '9') ||
                  (character >= 'a' && character <= 'f') ||
                  (character >= 'A' && character <= 'F')))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool MatchesOwner(
        string json,
        string expectedContainerId,
        string runId,
        string role,
        bool requireRunning)
    {
        if (!IsFullId(expectedContainerId) || string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("Id", out var id) || id.ValueKind != JsonValueKind.String ||
                !string.Equals(id.GetString(), expectedContainerId, StringComparison.Ordinal) ||
                !root.TryGetProperty("State", out var state) || state.ValueKind != JsonValueKind.Object ||
                !state.TryGetProperty("Status", out var status) || status.ValueKind != JsonValueKind.String ||
                (requireRunning && !string.Equals(status.GetString(), "running", StringComparison.Ordinal)) ||
                !root.TryGetProperty("Config", out var config) || config.ValueKind != JsonValueKind.Object ||
                !config.TryGetProperty("Labels", out var labels) || labels.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return HasLabel(labels, ContainerIdentityExecHandoff.RunIdLabel, runId) &&
                HasLabel(labels, ContainerIdentityExecHandoff.RoleLabel, role);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool TryReadNetworkEndpoint(string json, out OwnedContainerNetworkEndpoint? endpoint)
    {
        endpoint = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("HostConfig", out var hostConfig) || hostConfig.ValueKind != JsonValueKind.Object ||
                !hostConfig.TryGetProperty("NetworkMode", out var networkMode) || networkMode.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(networkMode.GetString()) ||
                !root.TryGetProperty("NetworkSettings", out var networkSettings) || networkSettings.ValueKind != JsonValueKind.Object ||
                !networkSettings.TryGetProperty("Networks", out var networks) || networks.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var entries = networks.EnumerateObject().ToArray();
            if (entries.Length != 1 ||
                !string.Equals(networkMode.GetString(), entries[0].Name, StringComparison.Ordinal) ||
                entries[0].Value.ValueKind != JsonValueKind.Object ||
                !entries[0].Value.TryGetProperty("IPAddress", out var addressElement) ||
                addressElement.ValueKind != JsonValueKind.String ||
                !IPAddress.TryParse(addressElement.GetString(), out var address) ||
                address.AddressFamily != AddressFamily.InterNetwork ||
                IPAddress.IsLoopback(address) ||
                address.Equals(IPAddress.Any) ||
                address.Equals(IPAddress.Broadcast) ||
                address.GetAddressBytes()[0] == 0 ||
                address.GetAddressBytes()[0] >= 224 ||
                address.GetAddressBytes()[0] == 169 && address.GetAddressBytes()[1] == 254)
            {
                return false;
            }

            endpoint = new OwnedContainerNetworkEndpoint(entries[0].Name, address);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasLabel(JsonElement labels, string name, string expectedValue) =>
        labels.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expectedValue, StringComparison.Ordinal);
}
