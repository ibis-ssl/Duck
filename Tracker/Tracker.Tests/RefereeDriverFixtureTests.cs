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
    public async Task DriveToActiveAsyncResumesFromHaltThenAdvancesFromStop()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Halt, 10),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 11),
            new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffBlue, 12),
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 13));
        var client = new RecordingGameControllerControlClient();
        var fixture = new RefereeDriverFixture(source, client);

        await fixture.DriveToActiveAsync(CancellationToken.None);

        Assert.Equal(
            ["ResumeFromHalt", "NextCommand", "NormalStart"],
            client.Actions.Select(action => action.ToString()));
    }

    [Fact]
    public async Task DriveToActiveAsyncUsesNormalStartAfterPreparationCommand()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Halt, 20),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 21),
            new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffBlue, 22),
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 23));
        var client = new RecordingGameControllerControlClient();
        var fixture = new RefereeDriverFixture(source, client);

        await fixture.DriveToActiveAsync(CancellationToken.None);

        Assert.Equal(
            [GameControllerContinueAction.ResumeFromHalt, GameControllerContinueAction.NextCommand, GameControllerContinueAction.NormalStart],
            client.Actions);
    }

    [Fact]
    public async Task DriveToActiveAsyncRejectsForceStartWithoutObservedPreparation()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Halt, 30),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 31),
            new RefereeCommandSnapshot(Referee.Types.Command.ForceStart, 32));
        var client = new RecordingGameControllerControlClient();
        var fixture = new RefereeDriverFixture(source, client);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.DriveToActiveAsync(CancellationToken.None));

        Assert.Contains("PREPARE_KICKOFF", failure.Message, StringComparison.Ordinal);
        Assert.Equal(
            [GameControllerContinueAction.ResumeFromHalt, GameControllerContinueAction.NextCommand],
            client.Actions);
    }

    [Fact]
    public async Task DriveToActiveAsyncRejectsRegressingRefereeCounter()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Halt, 30),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 29));
        var client = new RecordingGameControllerControlClient();
        var fixture = new RefereeDriverFixture(source, client);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.DriveToActiveAsync(CancellationToken.None));

        Assert.Contains("was not newer", failure.Message, StringComparison.Ordinal);
        Assert.Equal([GameControllerContinueAction.ResumeFromHalt], client.Actions);
    }

    [Fact]
    public async Task DriveToActiveAsyncFailsWhenSecondContinueActionDoesNotReachActiveCommand()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Halt, 40),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 41),
            new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffYellow, 42),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 43));
        var client = new RecordingGameControllerControlClient();
        var fixture = new RefereeDriverFixture(source, client);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.DriveToActiveAsync(CancellationToken.None));

        Assert.Equal(
            [GameControllerContinueAction.ResumeFromHalt, GameControllerContinueAction.NextCommand, GameControllerContinueAction.NormalStart],
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
