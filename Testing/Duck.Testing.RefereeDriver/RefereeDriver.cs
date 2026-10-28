namespace Duck.Testing.RefereeDriver;

public sealed record RefereeCommandSnapshot(
    Referee.Types.Command Command,
    uint CommandCounter,
    string YellowTeamName = "");

public enum GameControllerContinueAction
{
    NextCommand,
    ForceStart,
    NormalStart,
}

public interface IRefereeCommandSource
{
    ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
        uint? previousCommandCounter,
        CancellationToken cancellationToken);
}

public sealed record RefereeStateLossEvent(RefereeCommandSnapshot Snapshot);

public sealed class RefereeStateLostException(RefereeCommandSnapshot snapshot)
    : InvalidOperationException($"The referee left an active command: {snapshot.Command} ({snapshot.CommandCounter}).")
{
    public RefereeStateLossEvent StateLoss { get; } = new(snapshot);
}

public interface IGameControllerControlClient
{
    Task SendContinueActionAsync(
        GameControllerContinueAction action,
        CancellationToken cancellationToken);
}

public sealed class RefereeDriverFixture
{
    private readonly IRefereeCommandSource commandSource;
    private readonly IGameControllerControlClient controlClient;

    public RefereeDriverFixture(
        IRefereeCommandSource commandSource,
        IGameControllerControlClient controlClient)
    {
        this.commandSource = commandSource;
        this.controlClient = controlClient;
    }

    public async Task DriveToActiveAsync(CancellationToken cancellationToken)
    {
        var initial = await commandSource.WaitForChangeAsync(null, cancellationToken);
        if (initial.Command != Referee.Types.Command.Halt)
        {
            throw new InvalidOperationException(
                $"Expected initial referee command HALT, but observed {initial.Command}.");
        }

        await controlClient.SendContinueActionAsync(
            GameControllerContinueAction.NextCommand,
            cancellationToken);

        var afterNext = await commandSource.WaitForChangeAsync(
            initial.CommandCounter,
            cancellationToken);
        if (IsActive(afterNext.Command))
        {
            return;
        }

        var continueAction = afterNext.Command switch
        {
            Referee.Types.Command.Halt or Referee.Types.Command.Stop =>
                GameControllerContinueAction.ForceStart,
            Referee.Types.Command.PrepareKickoffYellow or
            Referee.Types.Command.PrepareKickoffBlue or
            Referee.Types.Command.PreparePenaltyYellow or
            Referee.Types.Command.PreparePenaltyBlue =>
                GameControllerContinueAction.NormalStart,
            _ => throw new InvalidOperationException(
                $"Referee command {afterNext.Command} cannot be continued by this fixture."),
        };

        await controlClient.SendContinueActionAsync(continueAction, cancellationToken);

        var active = await commandSource.WaitForChangeAsync(
            afterNext.CommandCounter,
            cancellationToken);
        if (!IsActive(active.Command))
        {
            throw new InvalidOperationException(
                $"Game Controller did not reach an active referee command; observed {active.Command}.");
        }
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
}
