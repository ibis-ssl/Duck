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
                NextCommand: Referee.Types.Command.NormalStart),
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 3,
                YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 300));
        var preparation = new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffBlue, 2,
            YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
            NextCommand: Referee.Types.Command.NormalStart);
        var client = new LifecycleControlClient(KickoffContinuation(preparation, "READY_AUTO"));
        var fixture = new RefereeDriverFixture(source, client,
            new SequenceLifecycleObservationSource(KickoffObservation(1, preparation)));

        var active = await fixture.DriveToActiveAsync(CancellationToken.None);

        Assert.Equal(Referee.Types.Command.NormalStart, active.Command);
        Assert.Equal([GameControllerContinueAction.NextCommand, GameControllerContinueAction.NormalStart], client.Actions);
    }

    [Fact]
    public async Task DriveToActiveAsyncObservesOfficialBallPlacementBeforePreparation()
    {
        var stop = new RefereeCommandSnapshot(Referee.Types.Command.Stop, 1,
            Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 100);
        var placement = new RefereeCommandSnapshot(Referee.Types.Command.BallPlacementYellow, 2,
            Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
            DesignatedPositionX: 0.25f, DesignatedPositionY: -0.5f);
        var preparation = new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffBlue, 3,
            YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 300,
            NextCommand: Referee.Types.Command.NormalStart);
        var active = new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 4,
            YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 400);
        var placementApi = new GameControllerMatchSnapshot(GameControllerCommandKind.BallPlacement,
            GameControllerMatchPhase.BallPlacement, "NORMAL_FIRST_HALF_PRE",
            CommandForTeam: "YELLOW", PhaseForTeam: "YELLOW", PlacementX: 0.25f, PlacementY: -0.5f);
        var placementObservation = new RefereeLifecycleObservation(1, placement,
            "BallPlacement", "BallPlacement", "NORMAL_FIRST_HALF_PRE", null, placementApi);
        var fixture = new RefereeDriverFixture(
            new FakeRefereeCommandSource(stop, placement, preparation, active),
            new LifecycleControlClient(KickoffContinuation(preparation, "READY_AUTO")),
            new SequenceLifecycleObservationSource(placementObservation, KickoffObservation(2, preparation)));
        var observed = new List<RefereeCommandSnapshot>();

        var result = await fixture.DriveToActiveAsync(CancellationToken.None,
            observeLifecycle: (observation, _) =>
            {
                observed.Add(observation.Udp);
                return Task.CompletedTask;
            });

        Assert.Equal(Referee.Types.Command.NormalStart, result.Command);
        Assert.Equal([placement], observed);
    }

    [Fact]
    public async Task DriveToActiveAsyncRejectsBallPlacementWithoutMatchedControllerState()
    {
        var stop = new RefereeCommandSnapshot(Referee.Types.Command.Stop, 1,
            Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 100);
        var placement = new RefereeCommandSnapshot(Referee.Types.Command.BallPlacementYellow, 2,
            Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200);
        var mismatchedApi = new GameControllerMatchSnapshot(GameControllerCommandKind.BallPlacement,
            GameControllerMatchPhase.BallPlacement, "NORMAL_FIRST_HALF_PRE",
            CommandForTeam: "BLUE", PhaseForTeam: "BLUE");
        var observation = new RefereeLifecycleObservation(1, placement,
            "BallPlacement", "BallPlacement", "NORMAL_FIRST_HALF_PRE", null, mismatchedApi);
        var control = new LifecycleControlClient();
        var fixture = new RefereeDriverFixture(
            new FakeRefereeCommandSource(stop, placement), control,
            new SequenceLifecycleObservationSource(observation));
        var callbackInvoked = false;

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.DriveToActiveAsync(CancellationToken.None,
                observeLifecycle: (_, _) =>
                {
                    callbackInvoked = true;
                    return Task.CompletedTask;
                }));

        Assert.Contains("Could not match official", failure.Message, StringComparison.Ordinal);
        Assert.False(callbackInvoked);
        Assert.Equal([GameControllerContinueAction.NextCommand], control.Actions);
    }

    [Theory]
    [InlineData("FreeKick", "YELLOW")]
    [InlineData("Running", "UNKNOWN")]
    public async Task WaitForDirectFreeAfterStopAsyncObservesOfficialPlacementBeforeDirectFree(
        string apiPhaseName,
        string apiPhaseTeam)
    {
        var stop = new RefereeCommandSnapshot(Referee.Types.Command.Stop, 4,
            Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: 400,
            NextCommand: Referee.Types.Command.DirectFreeYellow);
        var placement = new RefereeCommandSnapshot(Referee.Types.Command.BallPlacementYellow, 5,
            Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: 500);
        var active = new RefereeCommandSnapshot(Referee.Types.Command.DirectFreeYellow, 6,
            Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: 600);
        var placementApi = new GameControllerMatchSnapshot(GameControllerCommandKind.BallPlacement,
            GameControllerMatchPhase.BallPlacement, "NORMAL_FIRST_HALF", NextCommand: "DIRECT",
            NextCommandForTeam: "YELLOW", CommandForTeam: "YELLOW", PhaseForTeam: "YELLOW");
        var directApi = new GameControllerMatchSnapshot(GameControllerCommandKind.Direct,
            Enum.Parse<GameControllerMatchPhase>(apiPhaseName), "NORMAL_FIRST_HALF",
            CommandForTeam: "YELLOW", PhaseForTeam: apiPhaseTeam);
        var placementObservation = new RefereeLifecycleObservation(1, placement, "BallPlacement", "BallPlacement",
            "NORMAL_FIRST_HALF", null, placementApi);
        var directObservation = new RefereeLifecycleObservation(2, active, "Direct", apiPhaseName,
            "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(5), directApi);
        var control = new LifecycleControlClient(new GameControllerContinuationState(
            "Direct", apiPhaseName, "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(5), [], null, null, 0, 0));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control,
            new SequenceLifecycleObservationSource(placementObservation, directObservation));
        var observedCommands = new List<Referee.Types.Command>();

        var result = await fixture.WaitForDirectFreeAfterStopAsync(stop, CancellationToken.None,
            (observation, _) =>
            {
                observedCommands.Add(observation.Udp.Command);
                return Task.CompletedTask;
            });

        Assert.Equal(Referee.Types.Command.DirectFreeYellow, result.Command);
        Assert.Equal(
            [Referee.Types.Command.BallPlacementYellow, Referee.Types.Command.DirectFreeYellow],
            observedCommands);
        Assert.Empty(control.Actions);
    }

    [Fact]
    public async Task WaitForMotionWhileActiveAsyncAcceptsDirectFreeCommand()
    {
        var directFree = new RefereeCommandSnapshot(Referee.Types.Command.DirectFreeYellow, 5,
            Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: 500);

        var result = await RefereeDriverFixture.WaitForMotionWhileActiveAsync(
            new BlockingRefereeCommandSource(), directFree, TimeSpan.FromSeconds(1),
            _ => Task.FromResult("motion observed"), CancellationToken.None);

        Assert.Equal("motion observed", result);
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
                NextCommand: Referee.Types.Command.NormalStart),
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 13,
                YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 300));
        var preparation = new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffBlue, 12,
            YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
            NextCommand: Referee.Types.Command.NormalStart);
        var client = new LifecycleControlClient(KickoffContinuation(preparation, "READY_AUTO"));
        var fixture = new RefereeDriverFixture(source, client,
            new SequenceLifecycleObservationSource(KickoffObservation(1, preparation)));

        RefereeCommandSnapshot? stoppedAtCallback = null;
        await fixture.DriveToActiveAsync(CancellationToken.None, (stop, _) =>
        {
            stoppedAtCallback = stop;
            Assert.Equal([GameControllerContinueAction.ResumeFromHalt], client.Actions);
            return Task.CompletedTask;
        });

        Assert.Equal(11u, stoppedAtCallback?.CommandCounter);
        Assert.Equal(Referee.Types.Command.Stop, stoppedAtCallback?.Command);
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
                NextCommand: Referee.Types.Command.NormalStart),
            new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 23,
                YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 300));
        var preparation = new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffYellow, 22,
            YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
            NextCommand: Referee.Types.Command.NormalStart);
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
            BlueScore: 0, YellowScore: 0, NextCommand: Referee.Types.Command.NormalStart);
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
                NextCommand: Referee.Types.Command.NormalStart),
            new RefereeCommandSnapshot(Referee.Types.Command.Stop, 43,
                YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 300));
        var preparation = new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffYellow, 42,
            YellowTeamName: "ibis", Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 200,
            NextCommand: Referee.Types.Command.NormalStart);
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
    public async Task ContinueMatchAcceptsFreshSameStageStopAfterResumeFromHalt()
    {
        var stage = Referee.Types.Stage.NormalFirstHalf;
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.NormalStart, 30, Stage: stage, PacketTimestamp: 300);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Halt, 31, stage, 301,
                "Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(2, Referee.Types.Command.Stop, 32, stage, 302,
                "Stop", "Stop", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)));
        var control = new LifecycleControlClient(
            ContinueState("Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25), "RESUME_FROM_HALT"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromMilliseconds(75), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Equal(Referee.Types.Command.Stop, result.LastSnapshot.Command);
        Assert.Equal(32u, result.LastSnapshot.CommandCounter);
        Assert.Equal([GameControllerContinueAction.ResumeFromHalt], control.Actions);
    }

    [Fact]
    public async Task ContinueMatchContinuesUntilMaximumAfterFirstInMatchStop()
    {
        var stage = Referee.Types.Stage.NormalFirstHalf;
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Stop, 31, stage, 301,
                "Stop", "Stop", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(2, Referee.Types.Command.BallPlacementYellow, 32, stage, 302,
                "BallPlacement", "BallPlacement", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(3, Referee.Types.Command.PrepareKickoffBlue, 33, stage, 303,
                "Kickoff", "Kickoff", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(8)),
            Observation(4, Referee.Types.Command.NormalStart, 34, stage, 304,
                "NormalStart", "Kickoff", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(8)));
        var control = new LifecycleControlClient();
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            new(Referee.Types.Command.NormalStart, 30, Stage: stage, PacketTimestamp: 300),
            TimeSpan.FromMilliseconds(75), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.False(result.IsSuccess);
        Assert.Equal(Referee.Types.Command.NormalStart, result.LastSnapshot.Command);
        Assert.Equal(34u, result.LastSnapshot.CommandCounter);
        Assert.Empty(control.Actions);
    }

    [Fact]
    public async Task ContinueMatchContinuesAfterRunningResumesAndStopsAgain()
    {
        var stage = Referee.Types.Stage.NormalFirstHalf;
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Stop, 31, stage, 301,
                "Stop", "Stop", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(2, Referee.Types.Command.BallPlacementYellow, 32, stage, 302,
                "BallPlacement", "BallPlacement", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(3, Referee.Types.Command.DirectFreeYellow, 33, stage, 303,
                "Direct", "FreeKick", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(4, Referee.Types.Command.DirectFreeYellow, 34, stage, 304,
                "Direct", "Running", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(5, Referee.Types.Command.Stop, 35, stage, 305,
                "Stop", "Stop", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)));
        var control = new LifecycleControlClient();
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            new(Referee.Types.Command.NormalStart, 30, Stage: stage, PacketTimestamp: 300),
            TimeSpan.FromMilliseconds(250), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Equal(Referee.Types.Command.Stop, result.LastSnapshot.Command);
        Assert.Equal(35u, result.LastSnapshot.CommandCounter);
        Assert.Empty(control.Actions);
    }

    [Fact]
    public async Task ContinueMatchAcceptsFreshSameStageBallPlacementBlueAfterResumeFromHalt()
    {
        var stage = Referee.Types.Stage.NormalFirstHalf;
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.NormalStart, 30, Stage: stage, PacketTimestamp: 300);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Halt, 31, stage, 301,
                "Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(2, Referee.Types.Command.BallPlacementBlue, 32, stage, 302,
                "BallPlacement", "BallPlacement", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)));
        var control = new LifecycleControlClient(
            ContinueState("Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25), "RESUME_FROM_HALT"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromMilliseconds(75), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Equal(Referee.Types.Command.BallPlacementBlue, result.LastSnapshot.Command);
        Assert.Equal(32u, result.LastSnapshot.CommandCounter);
        Assert.Equal([GameControllerContinueAction.ResumeFromHalt], control.Actions);
    }

    [Fact]
    public async Task ContinueMatchRejectsUnrelatedFreshStateAfterResumeFromHalt()
    {
        var stage = Referee.Types.Stage.NormalFirstHalf;
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.NormalStart, 30, Stage: stage, PacketTimestamp: 300);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Halt, 31, stage, 301,
                "Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(2, Referee.Types.Command.BallPlacementYellow, 32, stage, 302,
                "BallPlacement", "BallPlacement", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)));
        var control = new LifecycleControlClient(
            ContinueState("Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25), "RESUME_FROM_HALT"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.ContinueMatchUntilPostGameAsync(initial, TimeSpan.FromSeconds(1), CancellationToken.None));

        Assert.Contains("did not produce its required fresh state", failure.Message, StringComparison.Ordinal);
        Assert.Equal([GameControllerContinueAction.ResumeFromHalt], control.Actions);
    }

    [Fact]
    public async Task ContinueMatchRejectsBallPlacementStateWithoutCommandCounterAdvanceAfterResumeFromHalt()
    {
        var stage = Referee.Types.Stage.NormalFirstHalf;
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.NormalStart, 30, Stage: stage, PacketTimestamp: 300);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Halt, 31, stage, 301,
                "Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)),
            Observation(2, Referee.Types.Command.BallPlacementBlue, 31, stage, 302,
                "BallPlacement", "BallPlacement", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25)));
        var control = new LifecycleControlClient(
            ContinueState("Halt", "Halt", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(25), "RESUME_FROM_HALT"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.ContinueMatchUntilPostGameAsync(initial, TimeSpan.FromSeconds(1), CancellationToken.None));

        Assert.Contains("did not produce its required fresh state", failure.Message, StringComparison.Ordinal);
        Assert.Equal([GameControllerContinueAction.ResumeFromHalt], control.Actions);
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
    public async Task ContinueMatchIssuesReadyManualForceStartForStoppedNextCommandEvenWhileClockHasTimeLeft()
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 50, Stage: Referee.Types.Stage.NormalSecondHalf, PacketTimestamp: 500);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Stop, 51, Referee.Types.Stage.NormalSecondHalf, 501,
                "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
                Referee.Types.Command.ForceStart, ForceStartApiSnapshot()),
            Observation(2, Referee.Types.Command.Stop, 51, Referee.Types.Stage.NormalSecondHalf, 502,
                "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
                Referee.Types.Command.ForceStart, ForceStartApiSnapshot()),
            Observation(3, Referee.Types.Command.ForceStart, 52, Referee.Types.Stage.NormalSecondHalf, 503,
                "ForceStart", "Running", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17)));
        var control = new LifecycleControlClient(
            new GameControllerContinuationState("Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
                [new("FORCE_START", "UNKNOWN", "READY_MANUAL", HasContinuationIssues: false)],
                "FORCE_START", "UNKNOWN"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromMilliseconds(75), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Equal([GameControllerContinueAction.ForceStart], control.Actions);
    }

    [Fact]
    public async Task ContinueMatchForceStartProgressesThroughEndGameToPostGame()
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 50, Stage: Referee.Types.Stage.NormalSecondHalf, PacketTimestamp: 500);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Stop, 51, Referee.Types.Stage.NormalSecondHalf, 501,
                "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
                Referee.Types.Command.ForceStart, ForceStartApiSnapshot()),
            Observation(2, Referee.Types.Command.ForceStart, 52, Referee.Types.Stage.NormalSecondHalf, 502,
                "ForceStart", "Running", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17)),
            Observation(3, Referee.Types.Command.Stop, 53, Referee.Types.Stage.NormalSecondHalf, 503,
                "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.Zero),
            Observation(4, Referee.Types.Command.Stop, 54, Referee.Types.Stage.PostGame, 504,
                "Stop", "PostGame", "POST_GAME", TimeSpan.Zero));
        var control = new LifecycleControlClient(
            new GameControllerContinuationState("Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
                [new("FORCE_START", "UNKNOWN", "READY_MANUAL", HasContinuationIssues: false)],
                "FORCE_START", "UNKNOWN"),
            ContinueState("Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.Zero, "END_GAME"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Referee.Types.Stage.PostGame, result.LastSnapshot.Stage);
        Assert.Equal([GameControllerContinueAction.ForceStart, GameControllerContinueAction.EndGame], control.Actions);
    }

    [Fact]
    public async Task ContinueMatchFailsWhenForceStartProducesAnUnexpectedRefereeEffect()
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 50, Stage: Referee.Types.Stage.NormalSecondHalf, PacketTimestamp: 500);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Stop, 51, Referee.Types.Stage.NormalSecondHalf, 501,
                "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
                Referee.Types.Command.ForceStart, ForceStartApiSnapshot()),
            Observation(2, Referee.Types.Command.NormalStart, 52, Referee.Types.Stage.NormalSecondHalf, 502,
                "NormalStart", "Running", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17)));
        var control = new LifecycleControlClient(new GameControllerContinuationState(
            "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
            [new("FORCE_START", "UNKNOWN", "READY_MANUAL", HasContinuationIssues: false)],
            "FORCE_START", "UNKNOWN"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.ContinueMatchUntilPostGameAsync(initial, TimeSpan.FromSeconds(1), CancellationToken.None));

        Assert.Contains("did not produce its required fresh state", failure.Message, StringComparison.Ordinal);
        Assert.Equal([GameControllerContinueAction.ForceStart], control.Actions);
    }

    [Fact]
    public async Task ContinueMatchRejectsRegressingForceStartEffectAfterManualAction()
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 50, Stage: Referee.Types.Stage.NormalSecondHalf, PacketTimestamp: 500);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Stop, 51, Referee.Types.Stage.NormalSecondHalf, 501,
                "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
                Referee.Types.Command.ForceStart, ForceStartApiSnapshot()),
            Observation(2, Referee.Types.Command.ForceStart, 50, Referee.Types.Stage.NormalSecondHalf, 500,
                "ForceStart", "Running", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17)));
        var control = new LifecycleControlClient(new GameControllerContinuationState(
            "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
            [new("FORCE_START", "UNKNOWN", "READY_MANUAL", HasContinuationIssues: false)],
            "FORCE_START", "UNKNOWN"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.ContinueMatchUntilPostGameAsync(initial, TimeSpan.FromSeconds(1), CancellationToken.None));

        Assert.Contains("regressed in counter or packet timestamp", failure.Message, StringComparison.Ordinal);
        Assert.Equal([GameControllerContinueAction.ForceStart], control.Actions);
    }

    [Fact]
    public async Task ContinueMatchRejectsForceStartEffectForDifferentStage()
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 50, Stage: Referee.Types.Stage.NormalSecondHalf, PacketTimestamp: 500);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Stop, 51, Referee.Types.Stage.NormalSecondHalf, 501,
                "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
                Referee.Types.Command.ForceStart, ForceStartApiSnapshot()),
            Observation(2, Referee.Types.Command.ForceStart, 52, Referee.Types.Stage.NormalFirstHalf, 502,
                "ForceStart", "Running", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(17)));
        var control = new LifecycleControlClient(new GameControllerContinuationState(
            "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
            [new("FORCE_START", "UNKNOWN", "READY_MANUAL", HasContinuationIssues: false)],
            "FORCE_START", "UNKNOWN"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.ContinueMatchUntilPostGameAsync(initial, TimeSpan.FromSeconds(1), CancellationToken.None));

        Assert.Contains("did not produce its required fresh state", failure.Message, StringComparison.Ordinal);
        Assert.Equal([GameControllerContinueAction.ForceStart], control.Actions);
    }

    [Theory]
    [InlineData(null, "UNKNOWN")]
    [InlineData("NORMAL_START", "UNKNOWN")]
    [InlineData("FORCE_START", "YELLOW")]
    [InlineData("FORCE_START", null)]
    public async Task ContinueMatchWaitsWhenForceStartDoesNotMatchLatestApiContext(
        string? apiNextCommand,
        string? apiNextCommandForTeam)
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 50, Stage: Referee.Types.Stage.NormalSecondHalf, PacketTimestamp: 500);
        var observation = Observation(1, Referee.Types.Command.Stop, 51, Referee.Types.Stage.NormalSecondHalf, 501,
            "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
            Referee.Types.Command.ForceStart,
            ForceStartApiSnapshot(apiNextCommand, apiNextCommandForTeam));
        var control = new LifecycleControlClient(new GameControllerContinuationState(
            "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
            [new("FORCE_START", "UNKNOWN", "READY_MANUAL", HasContinuationIssues: false)],
            "FORCE_START", "UNKNOWN"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control,
            new SequenceLifecycleObservationSource(observation));

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromMilliseconds(75), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Empty(control.Actions);
    }

    [Theory]
    [InlineData("NORMAL_START", "UNKNOWN")]
    [InlineData(null, "UNKNOWN")]
    [InlineData("FORCE_START", null)]
    [InlineData("FORCE_START", "YELLOW")]
    public async Task ContinueMatchWaitsWhenPreSendForceStartReadDisagreesWithObservedApi(
        string? continuationNextCommand,
        string? continuationNextCommandForTeam)
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 50, Stage: Referee.Types.Stage.NormalSecondHalf, PacketTimestamp: 500);
        var observation = Observation(1, Referee.Types.Command.Stop, 51, Referee.Types.Stage.NormalSecondHalf, 501,
            "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
            Referee.Types.Command.ForceStart, ForceStartApiSnapshot());
        var control = new LifecycleControlClient(new GameControllerContinuationState(
            "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
            [new("FORCE_START", "UNKNOWN", "READY_MANUAL", HasContinuationIssues: false)],
            continuationNextCommand, continuationNextCommandForTeam));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control,
            new SequenceLifecycleObservationSource(observation));

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromMilliseconds(75), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Empty(control.Actions);
    }

    [Theory]
    [InlineData("Stop", "Stop", "NORMAL_FIRST_HALF")]
    [InlineData("Halt", "Halt", "NORMAL_SECOND_HALF")]
    public async Task ContinueMatchWaitsWhenPreSendForceStartReadHasStaleLifecycleContext(
        string contextCommand,
        string contextPhase,
        string contextStage)
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 50, Stage: Referee.Types.Stage.NormalSecondHalf, PacketTimestamp: 500);
        var observation = Observation(1, Referee.Types.Command.Stop, 51, Referee.Types.Stage.NormalSecondHalf, 501,
            "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
            Referee.Types.Command.ForceStart, ForceStartApiSnapshot());
        var control = new LifecycleControlClient(new GameControllerContinuationState(
            contextCommand, contextPhase, contextStage, TimeSpan.FromSeconds(17),
            [new("FORCE_START", "UNKNOWN", "READY_MANUAL", HasContinuationIssues: false)],
            "FORCE_START", "UNKNOWN"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control,
            new SequenceLifecycleObservationSource(observation));

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromMilliseconds(75), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Empty(control.Actions);
    }

    [Theory]
    [InlineData("BLOCKED", false)]
    [InlineData("READY_AUTO", false)]
    [InlineData("READY_MANUAL", true)]
    public async Task ContinueMatchWaitsWhenForceStartIsNotUnblockedManual(
        string actionState,
        bool hasIssues)
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 50, Stage: Referee.Types.Stage.NormalSecondHalf, PacketTimestamp: 500);
        var observation = Observation(1, Referee.Types.Command.Stop, 51, Referee.Types.Stage.NormalSecondHalf, 501,
            "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
            Referee.Types.Command.ForceStart, ForceStartApiSnapshot());
        var control = new LifecycleControlClient(new GameControllerContinuationState(
            "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
            [new("FORCE_START", "UNKNOWN", actionState, hasIssues)],
            "FORCE_START", "UNKNOWN"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control,
            new SequenceLifecycleObservationSource(observation));

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromMilliseconds(75), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Empty(control.Actions);
    }

    [Fact]
    public async Task ContinueMatchWaitsWhenForceStartApiSnapshotIsUnavailable()
    {
        var initial = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 50, Stage: Referee.Types.Stage.NormalSecondHalf, PacketTimestamp: 500);
        var observation = Observation(1, Referee.Types.Command.Stop, 51, Referee.Types.Stage.NormalSecondHalf, 501,
            "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17), Referee.Types.Command.ForceStart);
        var control = new LifecycleControlClient(new GameControllerContinuationState(
            "Stop", "Stop", "NORMAL_SECOND_HALF", TimeSpan.FromSeconds(17),
            [new("FORCE_START", "UNKNOWN", "READY_MANUAL", HasContinuationIssues: false)],
            "FORCE_START", "UNKNOWN"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control,
            new SequenceLifecycleObservationSource(observation));

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            initial, TimeSpan.FromMilliseconds(75), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Empty(control.Actions);
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

    [Fact]
    public async Task ContinueMatchUsesOfferedNextStageAtRoundedZeroAndDoesNotChooseLaterKickoff()
    {
        var stopped = new RefereeCommandSnapshot(
            Referee.Types.Command.Stop, 80, Stage: Referee.Types.Stage.NormalFirstHalf,
            PacketTimestamp: 800);
        var observations = new SequenceLifecycleObservationSource(
            Observation(1, Referee.Types.Command.Stop, 80, Referee.Types.Stage.NormalFirstHalf, 800,
                "Stop", "Stop", "NORMAL_FIRST_HALF", TimeSpan.Zero,
                nextCommand: Referee.Types.Command.ForceStart));
        var control = new LifecycleControlClient(new GameControllerContinuationState(
            "Stop", "Stop", "NORMAL_FIRST_HALF", TimeSpan.Zero,
            [new("NEXT_STAGE", "UNKNOWN", "READY_MANUAL", HasContinuationIssues: false),
             new("NEXT_COMMAND", "UNKNOWN", "READY_AUTO", HasContinuationIssues: false)],
            NextCommand: "FORCE_START", NextCommandForTeam: "UNKNOWN"));
        var fixture = new RefereeDriverFixture(new FakeRefereeCommandSource(), control, observations);

        var result = await fixture.ContinueMatchUntilPostGameAsync(
            new(Referee.Types.Command.NormalStart, 79, Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: 790),
            TimeSpan.FromMilliseconds(40), CancellationToken.None);

        Assert.Equal(RefereeMatchTerminationKind.MaximumTimeReached, result.Kind);
        Assert.Equal([GameControllerContinueAction.NextStage], control.Actions);
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
        TimeSpan? stageTimeLeft,
        Referee.Types.Command? nextCommand = null,
        GameControllerMatchSnapshot? apiSnapshot = null) =>
        new(sequence, new(command, counter, Stage: stage, PacketTimestamp: packetTimestamp, NextCommand: nextCommand),
            apiCommand, apiPhase, apiStage, stageTimeLeft, apiSnapshot);

    private static GameControllerMatchSnapshot ForceStartApiSnapshot(
        string? nextCommand = "FORCE_START",
        string? nextCommandForTeam = "UNKNOWN") =>
        new(GameControllerCommandKind.Stop, GameControllerMatchPhase.Stop, "NORMAL_SECOND_HALF",
            TimeSpan.FromSeconds(17), nextCommand, nextCommandForTeam);

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

    private sealed class BlockingRefereeCommandSource : IRefereeCommandSource
    {
        public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
            uint? previousCommandCounter,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The referee wait should be canceled after motion completes.");
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
