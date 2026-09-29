using Duck.Testing.RefereeDriver;

namespace Tracker.Tests;

public sealed class RefereeDriverFixtureTests
{
    [Fact]
    public async Task DriveToActiveAsyncRejectsNonHaltInitialCommand()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 1));
        var client = new RecordingGameControllerControlClient();
        var fixture = new RefereeDriverFixture(source, client);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.DriveToActiveAsync(CancellationToken.None));

        Assert.Empty(client.Actions);
    }

    [Fact]
    public async Task DriveToActiveAsyncUsesForceStartAfterStop()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Halt, 10),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 11),
            new RefereeCommandSnapshot(Referee.Types.Command.ForceStart, 12));
        var client = new RecordingGameControllerControlClient();
        var fixture = new RefereeDriverFixture(source, client);

        await fixture.DriveToActiveAsync(CancellationToken.None);

        Assert.Equal(
            [GameControllerContinueAction.NextCommand, GameControllerContinueAction.ForceStart],
            client.Actions);
    }

    [Fact]
    public async Task DriveToActiveAsyncUsesNormalStartAfterPreparationCommand()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Halt, 20),
            new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffBlue, 21),
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 22));
        var client = new RecordingGameControllerControlClient();
        var fixture = new RefereeDriverFixture(source, client);

        await fixture.DriveToActiveAsync(CancellationToken.None);

        Assert.Equal(
            [GameControllerContinueAction.NextCommand, GameControllerContinueAction.NormalStart],
            client.Actions);
    }

    [Fact]
    public async Task DriveToActiveAsyncCompletesWhenNextCommandIsAlreadyActive()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Halt, 30),
            new RefereeCommandSnapshot(Referee.Types.Command.ForceStart, 31));
        var client = new RecordingGameControllerControlClient();
        var fixture = new RefereeDriverFixture(source, client);

        await fixture.DriveToActiveAsync(CancellationToken.None);

        Assert.Equal([GameControllerContinueAction.NextCommand], client.Actions);
    }

    [Fact]
    public async Task DriveToActiveAsyncFailsWhenSecondContinueActionDoesNotReachActiveCommand()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Halt, 40),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 41),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 42));
        var client = new RecordingGameControllerControlClient();
        var fixture = new RefereeDriverFixture(source, client);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.DriveToActiveAsync(CancellationToken.None));

        Assert.Equal(
            [GameControllerContinueAction.NextCommand, GameControllerContinueAction.ForceStart],
            client.Actions);
    }

    private sealed class FakeRefereeCommandSource(params RefereeCommandSnapshot[] commands)
        : IRefereeCommandSource
    {
        private readonly Queue<RefereeCommandSnapshot> commands = new(commands);

        public ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
            uint? previousCommandCounter,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            while (commands.Count > 0)
            {
                var command = commands.Dequeue();
                if (previousCommandCounter is null || command.CommandCounter != previousCommandCounter)
                {
                    return ValueTask.FromResult(command);
                }
            }

            throw new InvalidOperationException("No referee command remains in the fixture.");
        }
    }

    private sealed class RecordingGameControllerControlClient : IGameControllerControlClient
    {
        public List<GameControllerContinueAction> Actions { get; } = [];

        public Task SendContinueActionAsync(
            GameControllerContinueAction action,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }
}

