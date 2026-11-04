namespace Duck.Testing.RefereeDriver;

public sealed record RefereeCommandSnapshot(
    Referee.Types.Command Command,
    uint CommandCounter,
    string YellowTeamName = "",
    Referee.Types.Stage? Stage = null,
    ulong? PacketTimestamp = null,
    uint BlueScore = 0,
    uint YellowScore = 0,
    Referee.Types.Command? NextCommand = null,
    float? DesignatedPositionX = null,
    float? DesignatedPositionY = null);

public enum GameControllerContinueAction
{
    ResumeFromHalt,
    NextCommand,
    ForceStart,
    NormalStart,
    StopGame,
    NextStage,
    EndGame,
}

public interface IRefereeCommandSource
{
    ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
        uint? previousCommandCounter,
        CancellationToken cancellationToken);
}

public sealed record RefereeLifecycleObservation(
    long RequestSequence,
    RefereeCommandSnapshot Udp,
    string ApiCommand,
    string ApiPhase,
    string? ApiStage,
    TimeSpan? ApiStageTimeLeft,
    GameControllerMatchSnapshot? ApiSnapshot = null);

/// <summary>Waits on the existing RefereeObservationWorker updates; it does not start another poller.</summary>
public interface IRefereeLifecycleObservationSource
{
    Task<RefereeLifecycleObservation> WaitForNextAsync(
        long previousRequestSequence,
        CancellationToken cancellationToken);
}

public sealed record GameControllerContinueActionStatus(
    string Type,
    string ForTeam,
    string State,
    bool HasContinuationIssues,
    IReadOnlyList<string>? ContinuationIssues = null);

public sealed record GameControllerContinuationState(
    string Command,
    string Phase,
    string? Stage,
    TimeSpan? StageTimeLeft,
    IReadOnlyList<GameControllerContinueActionStatus> ContinueActions,
    string? NextCommand = null,
    string? NextCommandForTeam = null,
    uint? BlueScore = null,
    uint? YellowScore = null,
    float? PlacementX = null,
    float? PlacementY = null);

public sealed record RefereeStateLossEvent(RefereeCommandSnapshot Snapshot)
{
    public const string ActiveCommandLostFailureReason = "REFEREE_STATE_LOSS";

    public string FailureReason => ActiveCommandLostFailureReason;

    public string ToFailureMessage() =>
        $"{FailureReason}: active referee command was lost during Yellow motion; " +
        $"observed {Snapshot.Command} at counter {Snapshot.CommandCounter}.";
}

public enum RefereeMatchTerminationKind
{
    PostGame,
    MaximumTimeReached,
}

public sealed record RefereeMatchTerminationResult(
    RefereeMatchTerminationKind Kind,
    RefereeCommandSnapshot LastSnapshot)
{
    public bool IsSuccess => Kind == RefereeMatchTerminationKind.PostGame;
}

public sealed class RefereeStateLostException(RefereeCommandSnapshot snapshot)
    : InvalidOperationException($"The referee left an active command: {snapshot.Command} ({snapshot.CommandCounter}).")
{
    public RefereeStateLossEvent StateLoss { get; } = new(snapshot);
}

public interface IGameControllerControlClient
{
    Task<bool?> ReadAutoContinueAsync(CancellationToken cancellationToken) =>
        Task.FromResult<bool?>(null);

    Task<bool> SetAutoContinueAsync(bool enabled, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    Task<GameControllerContinuationState?> ReadContinuationStateAsync(CancellationToken cancellationToken);

    Task SendContinueActionAsync(
        GameControllerContinueAction action,
        CancellationToken cancellationToken);
}

public sealed class RefereeDriverFixture
{
    private readonly IRefereeCommandSource commandSource;
    private readonly IGameControllerControlClient controlClient;
    private readonly IRefereeLifecycleObservationSource? lifecycleObservations;

    public RefereeDriverFixture(
        IRefereeCommandSource commandSource,
        IGameControllerControlClient controlClient,
        IRefereeLifecycleObservationSource? lifecycleObservations = null)
    {
        this.commandSource = commandSource;
        this.controlClient = controlClient;
        this.lifecycleObservations = lifecycleObservations;
    }

    public Task<bool?> ReadAutoContinueAsync(CancellationToken cancellationToken) =>
        controlClient.ReadAutoContinueAsync(cancellationToken);

    public Task<bool> SetAutoContinueAsync(bool enabled, CancellationToken cancellationToken) =>
        controlClient.SetAutoContinueAsync(enabled, cancellationToken);

    public Task<GameControllerContinuationState?> ReadContinuationStateAsync(CancellationToken cancellationToken) =>
        controlClient.ReadContinuationStateAsync(cancellationToken);

    public async Task<RefereeCommandSnapshot> DriveToActiveAsync(CancellationToken cancellationToken)
    {
        var initial = await commandSource.WaitForChangeAsync(null, cancellationToken);
        var stop = initial;
        if (initial.Command == Referee.Types.Command.Halt)
        {
            await controlClient.SendContinueActionAsync(
                GameControllerContinueAction.ResumeFromHalt,
                cancellationToken);

            stop = await commandSource.WaitForChangeAsync(
                initial.CommandCounter,
                cancellationToken);
            EnsureNewerSnapshot(initial, stop, "RESUME_FROM_HALT");
            if (stop.Command != Referee.Types.Command.Stop)
            {
                throw new InvalidOperationException(
                    $"Game Controller did not reach STOP after RESUME_FROM_HALT; observed {stop.Command}.");
            }
        }
        else if (initial.Command != Referee.Types.Command.Stop)
        {
            throw new InvalidOperationException(
                $"Expected initial referee command HALT or STOP, but observed {initial.Command}.");
        }

        await controlClient.SendContinueActionAsync(
            GameControllerContinueAction.NextCommand,
            cancellationToken);

        var preparation = await commandSource.WaitForChangeAsync(
            stop.CommandCounter,
            cancellationToken);
        EnsureNewerSnapshot(stop, preparation, "NEXT_COMMAND");
        if (!IsPreparation(preparation.Command))
        {
            throw new InvalidOperationException(
                $"Game Controller did not reach a PREPARE_KICKOFF command after NEXT_COMMAND; observed {preparation.Command}.");
        }

        await WaitForNormalStartReadinessAsync(preparation, cancellationToken).ConfigureAwait(false);

        await controlClient.SendContinueActionAsync(
            GameControllerContinueAction.NormalStart,
            cancellationToken);

        var active = await commandSource.WaitForChangeAsync(
            preparation.CommandCounter,
            cancellationToken);
        EnsureNewerSnapshot(preparation, active, "NORMAL_START");
        if (active.Command != Referee.Types.Command.NormalStart)
        {
            throw new InvalidOperationException(
                $"Game Controller did not reach NORMAL_START after NORMAL_START; observed {active.Command}.");
        }

        return active;
    }

    private async Task WaitForNormalStartReadinessAsync(
        RefereeCommandSnapshot preparation,
        CancellationToken cancellationToken)
    {
        if (lifecycleObservations is null)
            throw new InvalidOperationException(
                "A current Game Controller lifecycle observer is required before NORMAL_START.");

        long requestSequence = 0;
        while (true)
        {
            var observation = await lifecycleObservations.WaitForNextAsync(
                requestSequence, cancellationToken).ConfigureAwait(false);
            if (observation.RequestSequence <= requestSequence)
                throw new InvalidOperationException(
                    "Game Controller lifecycle observation sequence did not advance before NORMAL_START.");
            requestSequence = observation.RequestSequence;

            EnsurePreparationContext(preparation, observation.Udp);
            if (!IsAlignedLifecycleObservation(observation) ||
                !IsKickoffLifecycleContext(preparation, observation))
                continue;

            var current = await controlClient.ReadContinuationStateAsync(cancellationToken).ConfigureAwait(false);
            if (!IsCurrentKickoffContinuationContext(preparation, observation, current))
                continue;

            if (current!.ContinueActions.Any(action =>
                    string.Equals(action.Type, "NORMAL_START", StringComparison.Ordinal) &&
                    string.Equals(action.ForTeam, "UNKNOWN", StringComparison.Ordinal) &&
                    string.Equals(action.State, "READY_AUTO", StringComparison.Ordinal) &&
                    !action.HasContinuationIssues))
                return;
        }
    }

    private static void EnsurePreparationContext(
        RefereeCommandSnapshot preparation,
        RefereeCommandSnapshot current)
    {
        if (current.Command != preparation.Command || current.CommandCounter != preparation.CommandCounter ||
            current.Stage != preparation.Stage || current.YellowTeamName != preparation.YellowTeamName ||
            current.BlueScore != preparation.BlueScore || current.YellowScore != preparation.YellowScore ||
            current.NextCommand != preparation.NextCommand ||
            preparation.PacketTimestamp is { } previousTimestamp &&
                current.PacketTimestamp is { } currentTimestamp && currentTimestamp < previousTimestamp)
            throw new InvalidOperationException(
                "The referee preparation context changed before the Game Controller offered NORMAL_START.");
    }

    private static bool IsKickoffLifecycleContext(
        RefereeCommandSnapshot preparation,
        RefereeLifecycleObservation observation)
    {
        if (observation.ApiSnapshot is not { } api ||
            preparation.Command is not (Referee.Types.Command.PrepareKickoffBlue or Referee.Types.Command.PrepareKickoffYellow))
            return false;

        if (preparation.Stage is not (Referee.Types.Stage.NormalFirstHalfPre or Referee.Types.Stage.NormalSecondHalfPre))
            return false;

        var expectedTeam = preparation.Command == Referee.Types.Command.PrepareKickoffBlue ? "BLUE" : "YELLOW";
        var expectedUdpNextCommand = preparation.Command == Referee.Types.Command.PrepareKickoffBlue
            ? Referee.Types.Command.PrepareKickoffBlue
            : Referee.Types.Command.PrepareKickoffYellow;
        return api.Command == GameControllerCommandKind.Kickoff &&
            api.Phase == GameControllerMatchPhase.Kickoff &&
            api.Stage == observation.ApiStage &&
            observation.Udp.NextCommand == expectedUdpNextCommand &&
            api.NextCommand == "KICKOFF" && api.NextCommandForTeam == expectedTeam &&
            api.CommandForTeam == expectedTeam && api.PhaseForTeam == expectedTeam &&
            api.BlueScore == preparation.BlueScore && api.YellowScore == preparation.YellowScore;
    }

    private static bool IsCurrentKickoffContinuationContext(
        RefereeCommandSnapshot preparation,
        RefereeLifecycleObservation observation,
        GameControllerContinuationState? context)
    {
        if (context is null || !IsCurrentContinuationContext(context, observation) ||
            !IsKickoffLifecycleContext(preparation, observation))
            return false;

        var expectedTeam = preparation.Command == Referee.Types.Command.PrepareKickoffBlue ? "BLUE" : "YELLOW";
        return context.NextCommand == "KICKOFF" && context.NextCommandForTeam == expectedTeam &&
            context.BlueScore == preparation.BlueScore && context.YellowScore == preparation.YellowScore;
    }

    public static async Task<T> WaitForMotionWhileActiveAsync<T>(
        IRefereeCommandSource commandSource,
        RefereeCommandSnapshot activeSnapshot,
        TimeSpan motionTimeout,
        Func<CancellationToken, Task<T>> waitForMotionAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandSource);
        ArgumentNullException.ThrowIfNull(activeSnapshot);
        ArgumentNullException.ThrowIfNull(waitForMotionAsync);
        if (motionTimeout <= TimeSpan.Zero || motionTimeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(motionTimeout));
        }

        if (!IsActive(activeSnapshot.Command))
        {
            throw new ArgumentException("The initial referee snapshot must be active.", nameof(activeSnapshot));
        }

        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var motionCancellation = CancellationTokenSource.CreateLinkedTokenSource(waitCancellation.Token);
        motionCancellation.CancelAfter(motionTimeout);
        var refereeLossTask = WaitForStateLossAsync(commandSource, activeSnapshot, waitCancellation.Token);
        var motionTask = RunMotionWaitAsync(waitForMotionAsync, motionCancellation.Token);

        try
        {
            var completed = await Task.WhenAny(motionTask, refereeLossTask);
            // Preserve a loss that was already observed when both waits finish concurrently.
            if (completed == refereeLossTask || refereeLossTask.IsCompleted)
            {
                var lostSnapshot = await refereeLossTask;
                throw new RefereeStateLostException(lostSnapshot);
            }

            return await motionTask;
        }
        finally
        {
            waitCancellation.Cancel();
            await ObserveCompletionAsync(motionTask);
            await ObserveCompletionAsync(refereeLossTask);
        }
    }

    public static async Task<RefereeMatchTerminationResult> WaitForMatchTerminationAsync(
        IRefereeCommandSource commandSource,
        RefereeCommandSnapshot latestSnapshot,
        TimeSpan maximumDuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandSource);
        ArgumentNullException.ThrowIfNull(latestSnapshot);
        if (maximumDuration <= TimeSpan.Zero || maximumDuration == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        }

        var lastSnapshot = latestSnapshot;
        if (lastSnapshot.Stage == Referee.Types.Stage.PostGame)
        {
            return new(RefereeMatchTerminationKind.PostGame, lastSnapshot);
        }

        using var maximumDurationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        maximumDurationCancellation.CancelAfter(maximumDuration);

        while (true)
        {
            RefereeCommandSnapshot next;
            try
            {
                next = await commandSource.WaitForChangeAsync(
                    lastSnapshot.CommandCounter,
                    maximumDurationCancellation.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new(RefereeMatchTerminationKind.MaximumTimeReached, lastSnapshot);
            }

            EnsureNewerSnapshot(lastSnapshot, next, "match termination");

            lastSnapshot = next;
            if (next.Stage == Referee.Types.Stage.PostGame)
            {
                return new(RefereeMatchTerminationKind.PostGame, next);
            }
        }
    }

    /// <summary>
    /// Continues only the READY_MANUAL actions required by a normal two-half match, using updates from the
    /// existing referee observation worker and the match's existing maximum-duration budget.
    /// </summary>
    public async Task<RefereeMatchTerminationResult> ContinueMatchUntilPostGameAsync(
        RefereeCommandSnapshot latestSnapshot,
        TimeSpan maximumDuration,
        CancellationToken cancellationToken,
        Func<RefereeLifecycleObservation, CancellationToken, Task>? onObservation = null)
    {
        ArgumentNullException.ThrowIfNull(latestSnapshot);
        if (lifecycleObservations is null)
            throw new InvalidOperationException("A current Game Controller lifecycle observer is required to continue a match.");
        if (maximumDuration <= TimeSpan.Zero || maximumDuration == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));

        var lastSnapshot = latestSnapshot;
        if (lastSnapshot.Stage == Referee.Types.Stage.PostGame)
            return new(RefereeMatchTerminationKind.PostGame, lastSnapshot);

        using var maximumDurationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        maximumDurationCancellation.CancelAfter(maximumDuration);
        var attemptedActions = new HashSet<string>(StringComparer.Ordinal);
        long requestSequence = 0;
        (GameControllerContinueAction Action, RefereeCommandSnapshot Before, string? Stage)? pendingAction = null;

        try
        {
            while (true)
            {
                var observation = await lifecycleObservations.WaitForNextAsync(
                    requestSequence, maximumDurationCancellation.Token).ConfigureAwait(false);

            if (observation.RequestSequence <= requestSequence)
                throw new InvalidOperationException("Game Controller lifecycle observation sequence did not advance.");
            requestSequence = observation.RequestSequence;
            EnsureNonRegressingSnapshot(lastSnapshot, observation.Udp);
            if (onObservation is not null)
                await onObservation(observation, maximumDurationCancellation.Token).ConfigureAwait(false);
            if (pendingAction is { } pending)
            {
                if (IsExpectedContinueActionEffect(pending.Action, pending.Before, observation.Udp))
                {
                    pendingAction = null;
                }
                else if (HasNewerRefereeState(pending.Before, observation.Udp))
                {
                    throw new InvalidOperationException(
                        $"Game Controller action {pending.Action} did not produce its required fresh state; " +
                        $"observed {observation.Udp.Command}/{observation.Udp.Stage} after {pending.Before.Command}/{pending.Stage}.");
                }
                else
                {
                    lastSnapshot = observation.Udp;
                    continue;
                }
            }

            if (observation.Udp.Stage == Referee.Types.Stage.PostGame)
            {
                EnsureNewerSnapshot(lastSnapshot, observation.Udp, "POST_GAME");
                return new(RefereeMatchTerminationKind.PostGame, observation.Udp);
            }

            lastSnapshot = observation.Udp;

            if (!IsAlignedLifecycleObservation(observation))
                continue;

            var candidate = SelectNormalMatchContinueAction(observation);
            if (candidate is null)
                continue;

            var actionContext = await controlClient.ReadContinuationStateAsync(
                maximumDurationCancellation.Token).ConfigureAwait(false);
            if (!IsCurrentContinuationContext(actionContext, observation))
                continue;

            candidate = SelectNormalMatchContinueAction(
                observation with { ApiStageTimeLeft = actionContext!.StageTimeLeft });
            if (candidate is null)
                continue;

            var actionName = ToGameControllerActionName(candidate.Value);
            if (!actionContext!.ContinueActions.Any(action =>
                    string.Equals(action.Type, actionName, StringComparison.Ordinal) &&
                    string.Equals(action.ForTeam, "UNKNOWN", StringComparison.Ordinal) &&
                    string.Equals(action.State, "READY_MANUAL", StringComparison.Ordinal) &&
                    !action.HasContinuationIssues))
                continue;

            var attemptKey = $"{candidate}:{observation.Udp.CommandCounter}:{observation.ApiStage}";
            if (!attemptedActions.Add(attemptKey))
                continue;

            await controlClient.SendContinueActionAsync(candidate.Value,
                maximumDurationCancellation.Token).ConfigureAwait(false);
                pendingAction = (candidate.Value, observation.Udp, observation.ApiStage);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(RefereeMatchTerminationKind.MaximumTimeReached, lastSnapshot);
        }
    }

    private static bool IsAlignedLifecycleObservation(RefereeLifecycleObservation observation)
    {
        if (!GameControllerStateMapping.TryParseStage(observation.ApiStage, out var apiStage) ||
            apiStage != observation.Udp.Stage ||
            !Enum.TryParse<GameControllerCommandKind>(observation.ApiCommand, out var apiCommand) ||
            !Enum.TryParse<GameControllerMatchPhase>(observation.ApiPhase, out var apiPhase))
            return false;

        return GameControllerStateMapping.MatchesUdpCommand(
            new(apiCommand, apiPhase), observation.Udp.Command);
    }

    private static bool IsCurrentContinuationContext(
        GameControllerContinuationState? context,
        RefereeLifecycleObservation observation)
    {
        if (context is null || context.Stage != observation.ApiStage ||
            context.Command != observation.ApiCommand || context.Phase != observation.ApiPhase)
            return false;

        return Enum.TryParse<GameControllerCommandKind>(context.Command, out var command) &&
            Enum.TryParse<GameControllerMatchPhase>(context.Phase, out var phase) &&
            GameControllerStateMapping.MatchesUdpCommand(new(command, phase), observation.Udp.Command);
    }

    private static GameControllerContinueAction? SelectNormalMatchContinueAction(
        RefereeLifecycleObservation observation)
    {
        var udp = observation.Udp;
        var stage = udp.Stage;

        // The pinned controller exposes NEXT_STAGE, not RESUME_FROM_HALT, during the
        // paused half-time stage. Wait for its clock to expire before advancing.
        if (stage == Referee.Types.Stage.NormalHalfTime)
        {
            return udp.Command is Referee.Types.Command.Stop or Referee.Types.Command.Halt &&
                observation.ApiStageTimeLeft is { } halfTimeRemaining && halfTimeRemaining < TimeSpan.Zero
                    ? GameControllerContinueAction.NextStage
                    : null;
        }

        if (udp.Command == Referee.Types.Command.Halt && observation.ApiPhase == nameof(GameControllerMatchPhase.Halt))
            return GameControllerContinueAction.ResumeFromHalt;

        if (observation.ApiStageTimeLeft is not { } remaining || remaining >= TimeSpan.Zero)
            return null;

        var stopped = udp.Command is Referee.Types.Command.Stop or Referee.Types.Command.Halt;
        if (stage is Referee.Types.Stage.NormalFirstHalf or Referee.Types.Stage.NormalSecondHalf &&
            observation.ApiPhase == nameof(GameControllerMatchPhase.Running))
            return GameControllerContinueAction.StopGame;

        if (!stopped)
            return null;

        return stage switch
        {
            Referee.Types.Stage.NormalFirstHalf => GameControllerContinueAction.NextStage,
            Referee.Types.Stage.NormalSecondHalf => GameControllerContinueAction.EndGame,
            _ => null,
        };
    }

    private static string ToGameControllerActionName(GameControllerContinueAction action) => action switch
    {
        GameControllerContinueAction.ResumeFromHalt => "RESUME_FROM_HALT",
        GameControllerContinueAction.StopGame => "STOP_GAME",
        GameControllerContinueAction.NextStage => "NEXT_STAGE",
        GameControllerContinueAction.EndGame => "END_GAME",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    private static void EnsureNonRegressingSnapshot(
        RefereeCommandSnapshot previous,
        RefereeCommandSnapshot current)
    {
        var timestampRegressed = previous.PacketTimestamp is { } previousTimestamp &&
            current.PacketTimestamp is { } currentTimestamp && currentTimestamp < previousTimestamp;
        if (current.CommandCounter < previous.CommandCounter || timestampRegressed)
            throw new InvalidOperationException("Current referee lifecycle observation regressed in counter or packet timestamp.");
    }

    private static bool HasNewerRefereeState(
        RefereeCommandSnapshot previous,
        RefereeCommandSnapshot current) =>
        current.Command != previous.Command ||
        current.Stage != previous.Stage;

    private static bool IsExpectedContinueActionEffect(
        GameControllerContinueAction action,
        RefereeCommandSnapshot previous,
        RefereeCommandSnapshot current)
    {
        try { EnsureNewerSnapshot(previous, current, action.ToString()); }
        catch (InvalidOperationException) { return false; }
        return action switch
        {
            GameControllerContinueAction.ResumeFromHalt =>
                current.Command == Referee.Types.Command.Stop && current.Stage == previous.Stage,
            GameControllerContinueAction.StopGame =>
                current.Command is Referee.Types.Command.Stop or Referee.Types.Command.Halt && current.Stage == previous.Stage,
            GameControllerContinueAction.NextStage => previous.Stage switch
            {
                Referee.Types.Stage.NormalFirstHalf => current.Stage == Referee.Types.Stage.NormalHalfTime,
                Referee.Types.Stage.NormalHalfTime => current.Stage == Referee.Types.Stage.NormalSecondHalfPre,
                _ => false,
            },
            GameControllerContinueAction.EndGame => current.Stage == Referee.Types.Stage.PostGame,
            _ => false,
        };
    }

    private static async Task<T> RunMotionWaitAsync<T>(
        Func<CancellationToken, Task<T>> waitForMotionAsync,
        CancellationToken cancellationToken)
    {
        return await waitForMotionAsync(cancellationToken);
    }

    private static async Task<RefereeCommandSnapshot> WaitForStateLossAsync(
        IRefereeCommandSource commandSource,
        RefereeCommandSnapshot activeSnapshot,
        CancellationToken cancellationToken)
    {
        var previousCounter = activeSnapshot.CommandCounter;
        while (true)
        {
            var snapshot = await commandSource.WaitForChangeAsync(previousCounter, cancellationToken);
            previousCounter = snapshot.CommandCounter;
            if (!IsActive(snapshot.Command))
            {
                return snapshot;
            }
        }
    }

    private static async Task ObserveCompletionAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // The winning task determines the result; always observe the cancelled peer task.
        }
    }

    private static bool IsActive(Referee.Types.Command command)
    {
        return command is Referee.Types.Command.NormalStart or Referee.Types.Command.ForceStart;
    }

    private static bool IsPreparation(Referee.Types.Command command) =>
        command is Referee.Types.Command.PrepareKickoffYellow or
            Referee.Types.Command.PrepareKickoffBlue;

    private static void EnsureNewerSnapshot(
        RefereeCommandSnapshot previous,
        RefereeCommandSnapshot current,
        string transition)
    {
        var newer = previous.PacketTimestamp is { } previousTimestamp &&
            current.PacketTimestamp is { } currentTimestamp
                ? currentTimestamp > previousTimestamp
                : current.CommandCounter > previous.CommandCounter;
        if (!newer)
        {
            throw new InvalidOperationException(
                $"Referee evidence for {transition} was not newer; observed timestamp " +
                $"{current.PacketTimestamp?.ToString() ?? "unavailable"}, counter {current.CommandCounter} after " +
                $"timestamp {previous.PacketTimestamp?.ToString() ?? "unavailable"}, counter {previous.CommandCounter}.");
        }
    }
}
