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

    public Task DriveToActiveAsync(CancellationToken cancellationToken)
    {
        _ = commandSource;
        _ = controlClient;
        throw new NotImplementedException();
    }
}
