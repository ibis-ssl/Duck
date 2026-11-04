using Duck.Testing.RefereeDriver;
using Duck.Testing.RefereeDriver.Hosting;

namespace Tracker.Tests;

public sealed class SimulatorBallPlacementFixtureTests
{
    [Fact]
    public async Task SendsBallOnlyOnceAndRecordsFreshReadyConfirmationForTheSameSimulator()
    {
        var now = DateTimeOffset.UtcNow;
        var simulatorIdentity = new MatchParticipantResourceEvidence(
            "simulator", "Running", "Healthy", "simulator", new string('a', 64), "run-a");
        var readiness = new CurrentSimulatorReadinessProbe(new(now,
            [simulatorIdentity]));
        var control = new SequenceControlClient(BlockedKickoff(), ReadyKickoff());
        var sender = new RecordingSimulatorControlClient();
        var driver = new RefereeDriverFixture(new EmptyRefereeSource(), control);
        var fixture = new SimulatorBallPlacementFixture(driver, readiness, sender, simulatorIdentity,
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 9,
                Stage: Referee.Types.Stage.NormalFirstHalf, BlueScore: 0, YellowScore: 0));
        var udp = ScoreSnapshot();
        var api = CurrentApiSnapshot();

        await fixture.ObserveAsync(new(10, udp, "Stop", "Stop", "NORMAL_FIRST_HALF", null, api),
            CancellationToken.None);
        Assert.Equal(1, sender.SendCalls);
        Assert.Equal("sent_unconfirmed", Assert.Single(fixture.Events).Status);

        await fixture.ObserveAsync(new(11, udp, "Stop", "Stop", "NORMAL_FIRST_HALF", null, api),
            CancellationToken.None);
        Assert.Equal(1, sender.SendCalls);
        Assert.Equal("confirmed", Assert.Single(fixture.Events).Status);

        await fixture.ObserveAsync(new(12, udp, "Stop", "Stop", "NORMAL_FIRST_HALF", null, api),
            CancellationToken.None);
        Assert.Equal(1, sender.SendCalls);
        Assert.Equal("confirmed", Assert.Single(fixture.Events).Status);
    }

    [Fact]
    public async Task ConfirmsPlacementWhenFreshMatchedObservationAlreadyAdvancedToExpectedKickoff()
    {
        var now = DateTimeOffset.UtcNow;
        var simulatorIdentity = new MatchParticipantResourceEvidence(
            "simulator", "Running", "Healthy", "simulator", new string('a', 64), "run-a");
        var readiness = new CurrentSimulatorReadinessProbe(new(now, [simulatorIdentity]));
        var sender = new RecordingSimulatorControlClient();
        var driver = new RefereeDriverFixture(new EmptyRefereeSource(), new SequenceControlClient(
            BlockedKickoff(),
            new("Kickoff", "Kickoff", "NORMAL_FIRST_HALF", null, [], "NORMAL_START", null, 1, 0, 0, 0)));
        var fixture = new SimulatorBallPlacementFixture(driver, readiness, sender, simulatorIdentity,
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 9,
                Stage: Referee.Types.Stage.NormalFirstHalf, BlueScore: 0, YellowScore: 0));

        await fixture.ObserveAsync(new(10, ScoreSnapshot(), "Stop", "Stop", "NORMAL_FIRST_HALF", null,
            CurrentApiSnapshot()), CancellationToken.None);
        var kickoffUdp = new RefereeCommandSnapshot(
            Referee.Types.Command.PrepareKickoffYellow, 12, Stage: Referee.Types.Stage.NormalFirstHalf,
            PacketTimestamp: 120, BlueScore: 1, YellowScore: 0,
            NextCommand: Referee.Types.Command.NormalStart);
        var kickoffApi = new GameControllerMatchSnapshot(
            GameControllerCommandKind.Kickoff, GameControllerMatchPhase.Kickoff, "NORMAL_FIRST_HALF",
            NextCommand: "NORMAL_START", BlueScore: 1, YellowScore: 0, CommandForTeam: "YELLOW");

        await fixture.ObserveAsync(new(11, kickoffUdp, "Kickoff", "Kickoff", "NORMAL_FIRST_HALF", null,
            kickoffApi), CancellationToken.None);

        Assert.Equal(1, sender.SendCalls);
        Assert.Equal("confirmed", Assert.Single(fixture.Events).Status);
        Assert.False(fixture.HasUnconfirmedPlacement);
    }

    [Fact]
    public async Task ConfirmsPlacementWhenFreshMatchedObservationAdvancedToNormalStartForExpectedTeam()
    {
        var now = DateTimeOffset.UtcNow;
        var simulatorIdentity = new MatchParticipantResourceEvidence(
            "simulator", "Running", "Healthy", "simulator", new string('a', 64), "run-a");
        var readiness = new CurrentSimulatorReadinessProbe(new(now, [simulatorIdentity]));
        var sender = new RecordingSimulatorControlClient();
        var driver = new RefereeDriverFixture(new EmptyRefereeSource(), new SequenceControlClient(
            BlockedKickoff(),
            new("NormalStart", "Kickoff", "NORMAL_FIRST_HALF", null, [], "STOP", null, 1, 0)));
        var fixture = new SimulatorBallPlacementFixture(driver, readiness, sender, simulatorIdentity,
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 9,
                Stage: Referee.Types.Stage.NormalFirstHalf, BlueScore: 0, YellowScore: 0));
        var normalStartUdp = new RefereeCommandSnapshot(
            Referee.Types.Command.NormalStart, 12, Stage: Referee.Types.Stage.NormalFirstHalf,
            PacketTimestamp: 120, BlueScore: 1, YellowScore: 0);
        var normalStartApi = new GameControllerMatchSnapshot(
            GameControllerCommandKind.NormalStart, GameControllerMatchPhase.Kickoff, "NORMAL_FIRST_HALF",
            NextCommand: "STOP", BlueScore: 1, YellowScore: 0,
            CommandForTeam: "UNKNOWN", PhaseForTeam: "YELLOW");

        await fixture.ObserveAsync(new(10, ScoreSnapshot(), "Stop", "Stop", "NORMAL_FIRST_HALF", null,
            CurrentApiSnapshot()), CancellationToken.None);
        await fixture.ObserveAsync(new(11, normalStartUdp, "NormalStart", "Kickoff", "NORMAL_FIRST_HALF", null,
            normalStartApi), CancellationToken.None);

        Assert.Equal("confirmed", Assert.Single(fixture.Events).Status);
        Assert.False(fixture.HasUnconfirmedPlacement);
    }

    [Fact]
    public async Task DoesNotConfirmNormalStartWhenKickoffTeamProvenanceIsMissing()
    {
        var now = DateTimeOffset.UtcNow;
        var simulatorIdentity = new MatchParticipantResourceEvidence(
            "simulator", "Running", "Healthy", "simulator", new string('a', 64), "run-a");
        var readiness = new CurrentSimulatorReadinessProbe(new(now, [simulatorIdentity]));
        var sender = new RecordingSimulatorControlClient();
        var driver = new RefereeDriverFixture(new EmptyRefereeSource(), new SequenceControlClient(
            BlockedKickoff(),
            new("NormalStart", "Kickoff", "NORMAL_FIRST_HALF", null, [], "STOP", null, 1, 0)));
        var fixture = new SimulatorBallPlacementFixture(driver, readiness, sender, simulatorIdentity,
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 9,
                Stage: Referee.Types.Stage.NormalFirstHalf, BlueScore: 0, YellowScore: 0));
        var normalStartUdp = new RefereeCommandSnapshot(
            Referee.Types.Command.NormalStart, 12, Stage: Referee.Types.Stage.NormalFirstHalf,
            PacketTimestamp: 120, BlueScore: 1, YellowScore: 0);
        var normalStartApi = new GameControllerMatchSnapshot(
            GameControllerCommandKind.NormalStart, GameControllerMatchPhase.Kickoff, "NORMAL_FIRST_HALF",
            NextCommand: "STOP", BlueScore: 1, YellowScore: 0,
            CommandForTeam: "UNKNOWN", PhaseForTeam: "UNKNOWN");

        await fixture.ObserveAsync(new(10, ScoreSnapshot(), "Stop", "Stop", "NORMAL_FIRST_HALF", null,
            CurrentApiSnapshot()), CancellationToken.None);
        await fixture.ObserveAsync(new(11, normalStartUdp, "NormalStart", "Kickoff", "NORMAL_FIRST_HALF", null,
            normalStartApi), CancellationToken.None);

        Assert.Equal("sent_unconfirmed", Assert.Single(fixture.Events).Status);
        Assert.True(fixture.HasUnconfirmedPlacement);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task DoesNotSendToAReplacementSimulatorContainerOrRun(bool keepContainer, bool keepRun)
    {
        var expected = new MatchParticipantResourceEvidence(
            "simulator", "Running", "Healthy", "simulator", new string('a', 64), "run-a");
        var current = expected with
        {
            ContainerId = keepContainer ? expected.ContainerId : new string('b', 64),
            RunId = keepRun ? expected.RunId : "run-b",
        };
        var readiness = new CurrentSimulatorReadinessProbe(new(DateTimeOffset.UtcNow, [current]));
        var sender = new RecordingSimulatorControlClient();
        var driver = new RefereeDriverFixture(new EmptyRefereeSource(), new SequenceControlClient(BlockedKickoff()));
        var fixture = new SimulatorBallPlacementFixture(driver, readiness, sender, expected,
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 9,
                Stage: Referee.Types.Stage.NormalFirstHalf, BlueScore: 0, YellowScore: 0));

        await fixture.ObserveAsync(new(10, ScoreSnapshot(), "Stop", "Stop", "NORMAL_FIRST_HALF", null,
            CurrentApiSnapshot()), CancellationToken.None);

        Assert.Equal(0, sender.SendCalls);
        Assert.Equal("not_sent", Assert.Single(fixture.Events).Status);
        Assert.Contains("identity changed", Assert.Single(fixture.Events).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyRequestsBallPlacementForAnAcceptedScoreAndDistanceBlockedCenterKickoff()
    {
        var udp = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop,
            11,
            Stage: Referee.Types.Stage.NormalFirstHalf,
            PacketTimestamp: 110,
            BlueScore: 1,
            YellowScore: 0,
            NextCommand: Referee.Types.Command.PrepareKickoffYellow);
        var api = new GameControllerMatchSnapshot(
            GameControllerCommandKind.Stop,
            GameControllerMatchPhase.Stop,
            "NORMAL_FIRST_HALF",
            NextCommand: "KICKOFF",
            NextCommandForTeam: "YELLOW",
            BlueScore: 1,
            YellowScore: 0,
            PlacementX: 0,
            PlacementY: 0);
        var continuation = new GameControllerContinuationState(
            "Stop", "Stop", "NORMAL_FIRST_HALF", null,
            [new("NEXT_COMMAND", "YELLOW", "BLOCKED", true,
                ["Ball is 3.72 m (>1.00 m) away from placement pos"])],
            "KICKOFF", "YELLOW", 1, 0, 0, 0);

        Assert.True(SimulatorBallPlacementFixture.IsEligibleGoalPlacement(
            udp, api, continuation, previousBlueScore: 0, previousYellowScore: 0, out _));
    }

    [Fact]
    public void RejectsPlacementWhenTheBlockedReasonOrOfficialStateDoesNotMatch()
    {
        var udp = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 11, Stage: Referee.Types.Stage.NormalFirstHalf,
            PacketTimestamp: 110, BlueScore: 1, YellowScore: 0,
            NextCommand: Referee.Types.Command.PrepareKickoffYellow);
        var api = new GameControllerMatchSnapshot(
            GameControllerCommandKind.Stop, GameControllerMatchPhase.Stop,
            "NORMAL_FIRST_HALF", NextCommand: "KICKOFF", NextCommandForTeam: "YELLOW",
            BlueScore: 1, YellowScore: 0, PlacementX: 0, PlacementY: 0);

        var wrongIssue = new GameControllerContinuationState(
            "Stop", "Stop", "NORMAL_FIRST_HALF", null,
            [new("NEXT_COMMAND", "YELLOW", "BLOCKED", true, ["A different action is blocked."])],
            "KICKOFF", "YELLOW", 1, 0, 0, 0);
        Assert.False(SimulatorBallPlacementFixture.IsEligibleGoalPlacement(
            udp, api, wrongIssue, 0, 0, out _));

        var wrongPosition = api with { PlacementX = 0.5f };
        var distanceBlock = wrongIssue with
        {
            ContinueActions = [new("NEXT_COMMAND", "YELLOW", "BLOCKED", true,
                ["Ball is away from placement pos"])],
        };
        Assert.False(SimulatorBallPlacementFixture.IsEligibleGoalPlacement(
            udp, wrongPosition, distanceBlock, 0, 0, out _));

        Assert.False(SimulatorBallPlacementFixture.IsEligibleGoalPlacement(
            udp with { Stage = Referee.Types.Stage.NormalSecondHalf }, api, distanceBlock, 0, 0, out _));

        Assert.False(SimulatorBallPlacementFixture.IsEligibleGoalPlacement(
            udp with { Command = Referee.Types.Command.NormalStart }, api,
            distanceBlock, 0, 0, out _));
        Assert.False(SimulatorBallPlacementFixture.IsEligibleGoalPlacement(
            udp, api, distanceBlock, 1, 0, out _));
    }

    [Fact]
    public void SimulatorControlCommandTeleportsOnlyTheBallToCenterOnOfficialControlPort()
    {
        var command = SimulatorCommand.Parser.ParseFrom(SimulatorBallControlClient.CreateBallToCenterCommand());

        Assert.Equal(SimulatorBallControlClient.DefaultControlPort, 10300);
        Assert.Equal("simulator", SimulatorBallControlClient.SimulatorResourceHost);
        Assert.NotNull(command.Control);
        Assert.NotNull(command.Control.TeleportBall);
        Assert.Equal(0, command.Control.TeleportBall.X);
        Assert.Equal(0, command.Control.TeleportBall.Y);
        Assert.Equal(0, command.Control.TeleportBall.Vx);
        Assert.Equal(0, command.Control.TeleportBall.Vy);
        Assert.Empty(command.Control.TeleportRobot);
    }

    private static RefereeCommandSnapshot ScoreSnapshot() => new(
        Referee.Types.Command.Stop, 11, Stage: Referee.Types.Stage.NormalFirstHalf,
        PacketTimestamp: 110, BlueScore: 1, YellowScore: 0,
        NextCommand: Referee.Types.Command.PrepareKickoffYellow);

    private static GameControllerMatchSnapshot CurrentApiSnapshot() => new(
        GameControllerCommandKind.Stop, GameControllerMatchPhase.Stop, "NORMAL_FIRST_HALF",
        NextCommand: "KICKOFF", NextCommandForTeam: "YELLOW", BlueScore: 1,
        YellowScore: 0, PlacementX: 0, PlacementY: 0);

    private static GameControllerContinuationState BlockedKickoff() => new(
        "Stop", "Stop", "NORMAL_FIRST_HALF", null,
        [new("NEXT_COMMAND", "YELLOW", "BLOCKED", true,
            ["Ball is 3.72 m (>1.00 m) away from placement pos"])],
        "KICKOFF", "YELLOW", 1, 0, 0, 0);

    private static GameControllerContinuationState ReadyKickoff() => new(
        "Stop", "Stop", "NORMAL_FIRST_HALF", null,
        [new("NEXT_COMMAND", "YELLOW", "READY_AUTO", false, [])],
        "KICKOFF", "YELLOW", 1, 0, 0, 0);

    private sealed class CurrentSimulatorReadinessProbe(MatchParticipantStatusEvidence evidence)
        : IMatchControllerReadinessProbe
    {
        public Task<MatchControllerReadiness> WaitForInitialReadinessAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MatchControllerReadiness> ReadFinalReadinessAsync(
            RefereeCommandSnapshot finalSnapshot, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MatchParticipantStatusEvidence?> ReadCurrentParticipantEvidenceAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<MatchParticipantStatusEvidence?>(evidence);
        }
    }

    private sealed class RecordingSimulatorControlClient : ISimulatorBallControlClient
    {
        public int SendCalls { get; private set; }

        public Task SendBallToCenterAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceControlClient(params GameControllerContinuationState?[] states)
        : IGameControllerControlClient
    {
        private int index;

        public Task<GameControllerContinuationState?> ReadContinuationStateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(index < states.Length ? states[index++] : states[^1]);
        }

        public Task<bool?> ReadAutoContinueAsync(CancellationToken cancellationToken) => Task.FromResult<bool?>(true);
        public Task<bool> SetAutoContinueAsync(bool enabled, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task SendContinueActionAsync(GameControllerContinueAction action, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class EmptyRefereeSource : IRefereeCommandSource
    {
        public ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
            uint? previousCommandCounter, CancellationToken cancellationToken) =>
            ValueTask.FromException<RefereeCommandSnapshot>(new NotSupportedException());
    }
}
