namespace Duck.Testing.RefereeDriver;

public sealed record RefereeCommandSnapshot(
    Referee.Types.Command Command,
    uint CommandCounter);

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

    private static bool IsActive(Referee.Types.Command command)
    {
        return command is Referee.Types.Command.NormalStart or Referee.Types.Command.ForceStart;
    }
}
