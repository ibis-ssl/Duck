using Duck.Testing.RefereeDriver;

namespace Tracker.Tests;

public sealed class RefereeDriverFixtureTests
{
    [Fact]
    public async Task DriveToActiveAsyncContinuesDirectlyFromInitialStop()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 1,
                Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 100),
            new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffBlue, 2,
                YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
                NextCommand: Referee.Types.Command.PrepareKickoffBlue),
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 3,
                YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 300));
        var preparation = new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffBlue, 2,
            YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
            NextCommand: Referee.Types.Command.PrepareKickoffBlue);
        var client = new LifecycleControlClient(KickoffContinuation(preparation, "READY_AUTO"));
        var fixture = new RefereeDriverFixture(source, client,
            new SequenceLifecycleObservationSource(KickoffObservation(1, preparation)));

        var active = await fixture.DriveToActiveAsync(CancellationToken.None);

        Assert.Equal(Referee.Types.Command.NormalStart, active.Command);
        Assert.Equal([GameControllerContinueAction.NextCommand, GameControllerContinueAction.NormalStart], client.Actions);
    }

    [Fact]
    public async Task DriveToActiveAsyncResumesFromHaltThenAdvancesFromStop()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Halt, 10,
                Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 100),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 11,
                Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 110),
            new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffBlue, 12,
                YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
                NextCommand: Referee.Types.Command.PrepareKickoffBlue),
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 13,
                YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 300));
        var preparation = new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffBlue, 12,
            YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
            NextCommand: Referee.Types.Command.PrepareKickoffBlue);
        var client = new LifecycleControlClient(KickoffContinuation(preparation, "READY_AUTO"));
        var fixture = new RefereeDriverFixture(source, client,
            new SequenceLifecycleObservationSource(KickoffObservation(1, preparation)));

        await fixture.DriveToActiveAsync(CancellationToken.None);

        Assert.Equal(
            ["ResumeFromHalt", "NextCommand", "NormalStart"],
            client.Actions.Select(action => action.ToString()));
    }

    [Fact]
    public async Task DriveToActiveAsyncUsesNormalStartAfterPreparationCommand()
    {
        var source = new FakeRefereeCommandSource(
            new RefereeCommandSnapshot(Referee.Types.Command.Halt, 20,
                Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 100),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 21,
                Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 110),
            new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffYellow, 22,
                YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
                NextCommand: Referee.Types.Command.PrepareKickoffYellow),
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 23,
                YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 300));
        var preparation = new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffYellow, 22,
            YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
            NextCommand: Referee.Types.Command.PrepareKickoffYellow);
        var client = new LifecycleControlClient(KickoffContinuation(preparation, "READY_AUTO"));
        var fixture = new RefereeDriverFixture(source, client,
            new SequenceLifecycleObservationSource(KickoffObservation(1, preparation)));

        await fixture.DriveToActiveAsync(CancellationToken.None);

        Assert.Equal(
            [GameControllerContinueAction.ResumeFromHalt, GameControllerContinueAction.NextCommand, GameControllerContinueAction.NormalStart],
            client.Actions);
    }

    [Fact]
    public async Task DriveToActiveAsyncWaitsForCurrentNormalStartReadyAutoAction()
    {
        var stop = new RefereeCommandSnapshot(Referee.Types.Command.Stop, 1,
            Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 100);
        var preparation = new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffBlue, 2,
            YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
            BlueScore: 0, YellowScore: 0, NextCommand: Referee.Types.Command.PrepareKickoffBlue);
        var active = preparation with
        {
            Command = Referee.Types.Command.NormalStart,
            CommandCounter = 3,
            PacketTimestamp = 300,
        };
        var source = new FakeRefereeCommandSource(stop, preparation, active);
        var observations = new SequenceLifecycleObservationSource(
            KickoffObservation(1, preparation with { PacketTimestamp = 201 }),
            KickoffObservation(2, preparation with { PacketTimestamp = 202 }));
        var client = new LifecycleControlClient(
            KickoffContinuation(preparation, "READY_MANUAL"),
            KickoffContinuation(preparation, "READY_AUTO"));
        var fixture = new RefereeDriverFixture(source, client, observations);

        var result = await fixture.DriveToActiveAsync(CancellationToken.None);

        Assert.Equal(Referee.Types.Command.NormalStart, result.Command);
        Assert.Equal(2, client.ReadCount);
        Assert.Equal([GameControllerContinueAction.NextCommand, GameControllerContinueAction.NormalStart],
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
            new RefereeCommandSnapshot(Referee.Types.Command.Halt, 40,
                Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 100),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 41,
                Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 110),
            new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffYellow, 42,
                YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
                NextCommand: Referee.Types.Command.PrepareKickoffYellow),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 43,
                YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 300));
        var preparation = new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffYellow, 42,
            YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
            NextCommand: Referee.Types.Command.PrepareKickoffYellow);
        var client = new LifecycleControlClient(KickoffContinuation(preparation, "READY_AUTO"));
        var fixture = new RefereeDriverFixture(source, client,
            new SequenceLifecycleObservationSource(KickoffObservation(1, preparation)));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.DriveToActiveAsync(CancellationToken.None));

        Assert.Equal(1, client.ReadCount);
        Assert.Contains("NORMAL_START", failure.Message, StringComparison.Ordinal);
        Assert.Equal(
            [GameControllerContinueAction.ResumeFromHalt, GameControllerContinueAction.NextCommand, GameControllerContinueAction.NormalStart],
            client.Actions);
    }

    [Fact]
    public async Task ContinueMatchUntilPostGameUsesFreshManualActionsAcrossBothNormalHalves()
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.NormalStart, 10, Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: 100);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Halt, 11, Referee.Types.Stage.NormalFirstHalf, 101,
                "Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(2, Referee.Types.Command.Stop, 12, Referee.Types.Stage.NormalFirstHalf, 102,
                "Stop", "Stop", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(3, Referee.Types.Command.Stop, 13, Referee.Types.Stage.NormalFirstHalf, 103,
                "Stop", "Stop", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(-1)),
            Observation(4, Referee.Types.Command.Halt, 14, Referee.Types.Stage.NormalHalfTime, 104,
                "Halt", "Halt", "NORMAL_HALF_TIME", TimeSpan.FromSeconds(5)),
            Observation(5, Referee.Types.Command.Halt, 15, Referee.Types.Stage.NormalHalfTime, 105,
                "Halt", "Halt", "NORMAL_HALF_TIME", TimeSpan.FromSeconds(-1)),
            Observation(6, Referee.Types.Command.Stop, 16, Referee.Types.Stage.NormalSecondHalfPre, 106,
                "Stop", "Stop", "NORMAL_SECOND_HALF_PRE", TimeSpan.FromSeconds(45)),
            Observation(7, Referee.Types.Command.NormalStart, 17, Referee.Types.Stage.NormalSecondHalf, 107,
                "NormalStart", "Running", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(-1)),
            Observation(8, Referee.Types.Command.Stop, 18, Referee.Types.Stage.NormalSecondHalf, 108,
                "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(-1)),
            Observation(9, Referee.Types.Command.Stop, 19, Referee.Types.Stage.PostGame, 109,
                "Stop", "Stop", "POST_GAME", null));
        var control = new LifecycleControlClient(
            ContinueState("Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25), "RESUME_FROM_HALT"),
            ContinueState("Stop", "Stop", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(-1), "NEXT_STAGE"),
            ContinueState("Halt", "Halt", "NORMAL_HALF_TIME", TimeSpan.FromSeconds(-1), "NEXT_STAGE"),
            ContinueState("NormalStart", "Running", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(-1), "STOP_GAME"),
            ContinueState("Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(-1), "END_GAME"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Referee.Types.Stage.PostGame, result.LastSnapshot.Stage);
        Assert.Equal(
            [GameControllerContinueAction.ResumeFromHalt, GameControllerContinueAction.NextStage,
                GameControllerContinueAction.NextStage, GameControllerContinueAction.StopGame,
                GameControllerContinueAction.EndGame],
            control.Actions);
    }

    [Fact]
    public async Task ContinueMatchUntilPostGameDoesNotRepeatResumeWhileSameHaltPersists()
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.NormalStart, 20, Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: 200);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Halt, 21, Referee.Types.Stage.NormalFirstHalf, 201,
                "Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(2, Referee.Types.Command.Halt, 21, Referee.Types.Stage.NormalFirstHalf, 202,
                "Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(24)));
        var control = new LifecycleControlClient(
            ContinueState("Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25), "RESUME_FROM_HALT"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromMilliseconds(75), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Equal([GameControllerContinueAction.ResumeFromHalt], control.Actions);
    }

    [Fact]
    public async Task ContinueMatchUntilPostGameFailsClosedWhenManualActionIsBlocked()
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.NormalStart, 30, Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: 300);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Halt, 31, Referee.Types.Stage.NormalFirstHalf, 301,
                "Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)));
        var control = new LifecycleControlClient(new GameControllerContinuationState(
            "Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25),
            [new("RESUME_FROM_HALT", "UNKNOWN", "DISABLED", HasContinuationIssues: true)]));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromMilliseconds(75), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Empty(control.Actions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ContinueMatchPersistsMaximumTimeWhenBudgetExpiresDuringControlOperation(bool blockRead)
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.NormalStart, 40, Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: 400);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Halt, 41, Referee.Types.Stage.NormalFirstHalf, 401,
                "Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)));
        var control = new BlockingContinuationControlClient(blockRead);
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Equal(Referee.Types.Command.Halt, result.LastSnapshot.Command);
        Assert.Equal(41u, result.LastSnapshot.CommandCounter);
    }

    [Fact]
    public async Task ContinueMatchPreservesCallerCancellationDuringControlRead()
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.NormalStart, 50, Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: 500);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Halt, 51, Referee.Types.Stage.NormalFirstHalf, 501,
                "Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)));
        var control = new BlockingContinuationControlClient(blockRead: true);
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);
        using var cancellation = new CancellationTokenSource();

        var continuation = fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromSeconds(5), cancellation.Token);
        await control.ControlReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => continuation);
    }

    private static RefereeLifecycleObservation Observation(
        long sequence,
        Referee.Types.Command command,
        uint counter,
        Referee.Types.Stage stage,
        ulong packetTimestamp,
        string apiCommand,
        string apiPhase,
        string apiStage,
        TimeSpan? stageTimeLeft) =>
        new(sequence, new(command, counter, Stage: stage, PacketTimestamp: packetTimestamp),
            apiCommand, apiPhase, apiStage, stageTimeLeft);

    private static RefereeLifecycleObservation KickoffObservation(
        long sequence,
        RefereeCommandSnapshot udp)
    {
        const string stage = "NORMAL_FIRST_HALF_PRE";
        var expectedTeam = udp.Command == Referee.Types.Command.PrepareKickoffBlue ? "BLUE" : "YELLOW";
        var api = new GameControllerMatchSnapshot(
            GameControllerCommandKind.Kickoff,
            GameControllerMatchPhase.Kickoff,
            stage,
            TimeSpan.FromSeconds(8),
            "KICKOFF",
            expectedTeam,
            udp.BlueScore,
            udp.YellowScore,
            CommandForTeam: expectedTeam,
            PhaseForTeam: expectedTeam);
        return new(sequence, udp, "Kickoff", "Kickoff", stage, TimeSpan.FromSeconds(8), api);
    }

    private static GameControllerContinuationState KickoffContinuation(
        RefereeCommandSnapshot udp,
        string actionState)
    {
        const string stage = "NORMAL_FIRST_HALF_PRE";
        var expectedTeam = udp.Command == Referee.Types.Command.PrepareKickoffBlue ? "BLUE" : "YELLOW";
        return new("Kickoff", "Kickoff", stage, TimeSpan.FromSeconds(8),
            [new("NORMAL_START", "UNKNOWN", actionState, HasContinuationIssues: false)],
            "KICKOFF", expectedTeam, udp.BlueScore, udp.YellowScore);
    }

    private static GameControllerContinuationState ContinueState(
        string command,
        string phase,
        string stage,
        TimeSpan? stageTimeLeft,
        string action) =>
        new(command, phase, stage, stageTimeLeft,
            [new(action, "UNKNOWN", "READY_MANUAL", HasContinuationIssues: false)]);

    private sealed class SequenceLifecycleObservationSource(params RefereeLifecycleObservation[] observations)
        : IRefereeLifecycleObservationSource
    {
        private readonly Queue<RefereeLifecycleObservation> remaining = new(observations);

        public async Task<RefereeLifecycleObservation> WaitForNextAsync(
            long previousRequestSequence,
            CancellationToken cancellationToken)
        {
            if (remaining.TryDequeue(out var observation) && observation.RequestSequence > previousRequestSequence)
                return observation;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The lifecycle observation wait should be canceled at maximum time.");
        }
    }

    private sealed class LifecycleControlClient(params GameControllerContinuationState[] states)
        : IGameControllerControlClient
    {
        private readonly Queue<GameControllerContinuationState> remaining = new(states);
        public List<GameControllerContinueAction> Actions { get; } = [];
        public int ReadCount { get; private set; }

        public Task<GameControllerContinuationState?> ReadContinuationStateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return Task.FromResult<GameControllerContinuationState?>(
                remaining.TryDequeue(out var state) ? state : null);
        }

        public Task SendContinueActionAsync(GameControllerContinueAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingContinuationControlClient(bool blockRead) : IGameControllerControlClient
    {
        public TaskCompletionSource ControlReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<GameControllerContinuationState?> ReadContinuationStateAsync(CancellationToken cancellationToken)
        {
            ControlReadEntered.TrySetResult();
            if (blockRead)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ContinueState("Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25),
                "RESUME_FROM_HALT");
        }

        public async Task SendContinueActionAsync(GameControllerContinueAction action, CancellationToken cancellationToken)
        {
            if (!blockRead)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
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

        public Task<GameControllerContinuationState?> ReadContinuationStateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<GameControllerContinuationState?>(null);
        }

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
