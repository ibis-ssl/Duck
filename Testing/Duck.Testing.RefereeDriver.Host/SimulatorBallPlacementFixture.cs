using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using Duck.Testing.RefereeDriver;

namespace Duck.Testing.RefereeDriver.Hosting;

internal sealed record BallPlacementFixtureEvent(
    string Status,
    uint BlueScore,
    uint YellowScore,
    uint CommandCounter,
    long ObservationSequence,
    string? SimulatorContainerId,
    string? RunId,
    string? Reason);

internal interface ISimulatorBallControlClient
{
    Task SendBallToCenterAsync(CancellationToken cancellationToken);
}

internal sealed class SimulatorBallControlClient : ISimulatorBallControlClient
{
    internal const string SimulatorResourceHost = "simulator";
    internal const int DefaultControlPort = 10300;

    internal static byte[] CreateBallToCenterCommand() => new SimulatorCommand
    {
        Control = new SimulatorControl
        {
            TeleportBall = new TeleportBall
            {
                X = 0,
                Y = 0,
                Vx = 0,
                Vy = 0,
            },
        },
    }.ToByteArray();

    public async Task SendBallToCenterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var addresses = await Dns.GetHostAddressesAsync(SimulatorResourceHost, cancellationToken).ConfigureAwait(false);
        var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
            ?? throw new InvalidOperationException($"Simulator resource '{SimulatorResourceHost}' did not resolve to IPv4.");
        using var client = new UdpClient(AddressFamily.InterNetwork);
        await client.SendAsync(CreateBallToCenterCommand(), new IPEndPoint(address, DefaultControlPort), cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Reuses the pinned Crane match fixture: after a fresh accepted score increment, put only the ball at the
/// official center through SSL simulation-control UDP 10300. Port 12345 is the separate Ibis robot input.
/// </summary>
internal sealed class SimulatorBallPlacementFixture(
    RefereeDriverFixture driver,
    IMatchControllerReadinessProbe readinessProbe,
    ISimulatorBallControlClient simulator,
    MatchParticipantResourceEvidence expectedSimulator,
    RefereeCommandSnapshot baselineSnapshot)
{
    private uint handledBlueScore = baselineSnapshot.BlueScore;
    private uint handledYellowScore = baselineSnapshot.YellowScore;
    private PendingPlacement? pending;
    private RefereeLifecycleObservation? previousObservation;
    private (uint Blue, uint Yellow)? pendingSecondHalfKickoff;
    private readonly HashSet<(uint Blue, uint Yellow, Referee.Types.Stage? Stage)> reportedNotSent = [];

    internal List<BallPlacementFixtureEvent> Events { get; } = [];
    internal bool HasUnconfirmedPlacement => pending is not null;

    internal async Task ObserveAsync(RefereeLifecycleObservation observation, CancellationToken cancellationToken)
    {
        var previous = previousObservation;
        previousObservation = observation;

        if (previous is not null && IsObservedSecondHalfKickoff(previous, observation))
            pendingSecondHalfKickoff = (observation.Udp.BlueScore, observation.Udp.YellowScore);

        var secondHalfEpisodeStillActive = pendingSecondHalfKickoff is { } kickoffScore &&
            observation.Udp.Stage == Referee.Types.Stage.NormalSecondHalfPre &&
            observation.Udp.BlueScore == kickoffScore.Blue && observation.Udp.YellowScore == kickoffScore.Yellow;
        if (pendingSecondHalfKickoff is not null && !secondHalfEpisodeStillActive)
            pendingSecondHalfKickoff = null;
        var isPendingSecondHalfKickoff = secondHalfEpisodeStillActive &&
            observation.Udp.NextCommand == Referee.Types.Command.PrepareKickoffBlue;

        if (pending is { } outstanding)
        {
            await ConfirmPlacementAsync(observation, outstanding, cancellationToken).ConfigureAwait(false);
            return;
        }

        var udp = observation.Udp;
        if (udp.BlueScore < handledBlueScore || udp.YellowScore < handledYellowScore)
        {
            handledBlueScore = udp.BlueScore;
            handledYellowScore = udp.YellowScore;
            return;
        }

        var hasScoreIncrease = udp.BlueScore > handledBlueScore || udp.YellowScore > handledYellowScore;
        if (!hasScoreIncrease && !isPendingSecondHalfKickoff)
            return;

        // Keep looking at the same score while the controller settles into its stopped kickoff context.
        var api = observation.ApiSnapshot;
        var action = await driver.ReadContinuationStateAsync(cancellationToken).ConfigureAwait(false);
        if (!IsEligiblePlacement(udp, api, action, handledBlueScore, handledYellowScore,
                isPendingSecondHalfKickoff, out var reason))
        {
            if (reportedNotSent.Add((udp.BlueScore, udp.YellowScore, udp.Stage)))
                Events.Add(new("not_sent", udp.BlueScore, udp.YellowScore, udp.CommandCounter,
                    observation.RequestSequence, expectedSimulator.ContainerId, expectedSimulator.RunId, reason));
            return;
        }

        if (!await IsCurrentSimulatorAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reportedNotSent.Add((udp.BlueScore, udp.YellowScore, udp.Stage)))
                Events.Add(new("not_sent", udp.BlueScore, udp.YellowScore, udp.CommandCounter,
                    observation.RequestSequence, expectedSimulator.ContainerId, expectedSimulator.RunId,
                    "Simulator identity changed or is no longer Running."));
            return;
        }

        handledBlueScore = udp.BlueScore;
        handledYellowScore = udp.YellowScore;
        pendingSecondHalfKickoff = null;
        await simulator.SendBallToCenterAsync(cancellationToken).ConfigureAwait(false);
        if (!await IsCurrentSimulatorAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Simulator identity changed while sending the official ball-placement fixture.");

        var sent = new BallPlacementFixtureEvent("sent_unconfirmed", udp.BlueScore, udp.YellowScore,
            udp.CommandCounter, observation.RequestSequence, expectedSimulator.ContainerId,
            expectedSimulator.RunId, "Sent ball-only TeleportBall(0,0) on simulator SSL control port 10300.");
        Events.Add(sent);
        pending = new(sent, observation.ApiSnapshot, observation.RequestSequence);
    }

    internal static bool IsEligibleGoalPlacement(
        RefereeCommandSnapshot udp,
        GameControllerMatchSnapshot? api,
        GameControllerContinuationState? continuation,
        uint previousBlueScore,
        uint previousYellowScore,
        out string reason) =>
        IsEligiblePlacement(udp, api, continuation, previousBlueScore, previousYellowScore,
            observedSecondHalfKickoff: false, out reason);

    internal static bool IsEligiblePlacement(
        RefereeCommandSnapshot udp,
        GameControllerMatchSnapshot? api,
        GameControllerContinuationState? continuation,
        uint previousBlueScore,
        uint previousYellowScore,
        bool observedSecondHalfKickoff,
        out string reason)
    {
        var blueIncreased = udp.BlueScore > previousBlueScore;
        var yellowIncreased = udp.YellowScore > previousYellowScore;
        var unchangedScore = udp.BlueScore == previousBlueScore && udp.YellowScore == previousYellowScore;
        if ((!blueIncreased && !yellowIncreased && !(observedSecondHalfKickoff && unchangedScore)) ||
            (blueIncreased && yellowIncreased) || udp.BlueScore < previousBlueScore || udp.YellowScore < previousYellowScore)
        {
            reason = "No accepted score increase or observed score-invariant second-half kickoff is present.";
            return false;
        }

        if (observedSecondHalfKickoff &&
            (udp.Stage != Referee.Types.Stage.NormalSecondHalfPre ||
             udp.NextCommand != Referee.Types.Command.PrepareKickoffBlue))
        {
            reason = "The score-invariant placement is not the observed normal second-half BLUE kickoff.";
            return false;
        }

        if (udp.Command is not (Referee.Types.Command.Stop or Referee.Types.Command.Halt) ||
            udp.NextCommand is not (Referee.Types.Command.PrepareKickoffBlue or Referee.Types.Command.PrepareKickoffYellow))
        {
            reason = "Score changed without a stopped referee state and next kickoff command.";
            return false;
        }

        var expectedTeam = udp.NextCommand == Referee.Types.Command.PrepareKickoffYellow ? "YELLOW" : "BLUE";
        if (api is null || api.Command != GameControllerCommandKind.Stop || api.Phase != GameControllerMatchPhase.Stop ||
            api.NextCommand != "KICKOFF" || api.NextCommandForTeam != expectedTeam ||
            !GameControllerStateMapping.TryParseStage(api.Stage, out var apiStage) || udp.Stage != apiStage ||
            (observedSecondHalfKickoff && api.Stage != "NORMAL_SECOND_HALF_PRE") ||
            api.BlueScore != udp.BlueScore || api.YellowScore != udp.YellowScore ||
            api.PlacementX is not { } x || api.PlacementY is not { } y || Math.Abs(x) > 0.001f || Math.Abs(y) > 0.001f)
        {
            reason = "Fresh UDP and Game Controller API do not agree on the stopped center-kickoff state.";
            return false;
        }

        if (continuation is null || continuation.Command != nameof(GameControllerCommandKind.Stop) ||
            continuation.Phase != nameof(GameControllerMatchPhase.Stop) ||
            continuation.Stage != api.Stage ||
            continuation.NextCommand != api.NextCommand || continuation.NextCommandForTeam != expectedTeam ||
            continuation.BlueScore != udp.BlueScore || continuation.YellowScore != udp.YellowScore ||
            continuation.PlacementX is not { } continuationX || continuation.PlacementY is not { } continuationY ||
            Math.Abs(continuationX - x) > 0.001f || Math.Abs(continuationY - y) > 0.001f)
        {
            reason = "A fresh control API read did not match the current UDP/API kickoff snapshot.";
            return false;
        }

        var next = continuation.ContinueActions.SingleOrDefault(action =>
            action.Type == "NEXT_COMMAND" && action.ForTeam == expectedTeam);
        if (next is null || next.State != "BLOCKED" ||
            next.ContinuationIssues is not { } issues ||
            !issues.Any(issue => issue.Contains("away from placement pos", StringComparison.OrdinalIgnoreCase)))
        {
            reason = "GC NEXT_COMMAND is not blocked specifically because the ball is away from placement position.";
            return false;
        }

        reason = observedSecondHalfKickoff
            ? "Observed NORMAL_HALF_TIME to NORMAL_SECOND_HALF_PRE transition; current BLUE kickoff is blocked by ball-placement distance."
            : "Accepted score increment; current STOP/KICKOFF state is blocked by ball-placement distance.";
        return true;
    }

    internal static bool IsObservedSecondHalfKickoff(
        RefereeLifecycleObservation previous,
        RefereeLifecycleObservation current) =>
        current.RequestSequence > previous.RequestSequence &&
        previous.Udp.PacketTimestamp is { } previousPacketTimestamp &&
        current.Udp.PacketTimestamp is { } currentPacketTimestamp &&
        currentPacketTimestamp > previousPacketTimestamp &&
        previous.ApiSnapshot is { Stage: "NORMAL_HALF_TIME" } previousApi &&
        current.ApiSnapshot is { Stage: "NORMAL_SECOND_HALF_PRE" } currentApi &&
        previous.Udp.Stage == Referee.Types.Stage.NormalHalfTime &&
        current.Udp.Stage == Referee.Types.Stage.NormalSecondHalfPre &&
        previous.Udp.BlueScore == current.Udp.BlueScore &&
        previous.Udp.YellowScore == current.Udp.YellowScore &&
        previousApi.BlueScore == previous.Udp.BlueScore &&
        previousApi.YellowScore == previous.Udp.YellowScore &&
        currentApi.BlueScore == current.Udp.BlueScore &&
        currentApi.YellowScore == current.Udp.YellowScore;

    private async Task ConfirmPlacementAsync(
        RefereeLifecycleObservation observation,
        PendingPlacement outstanding,
        CancellationToken cancellationToken)
    {
        if (observation.RequestSequence <= outstanding.RequestSequence ||
            observation.ApiSnapshot is not { } api ||
            api.BlueScore != outstanding.Event.BlueScore || api.YellowScore != outstanding.Event.YellowScore ||
            api.Stage != outstanding.ApiSnapshot?.Stage ||
            !GameControllerStateMapping.MatchesUdpCommand(api, observation.Udp.Command))
            return;

        var expectedTeam = outstanding.ApiSnapshot?.NextCommandForTeam;
        if (expectedTeam is not ("BLUE" or "YELLOW") ||
            observation.Udp.BlueScore != outstanding.Event.BlueScore ||
            observation.Udp.YellowScore != outstanding.Event.YellowScore ||
            !GameControllerStateMapping.TryParseStage(outstanding.ApiSnapshot?.Stage, out var expectedStage) ||
            observation.Udp.Stage != expectedStage)
            return;

        var stoppedPlacement = api.Command == GameControllerCommandKind.Stop &&
            api.Phase == GameControllerMatchPhase.Stop &&
            api.NextCommand == outstanding.ApiSnapshot?.NextCommand &&
            api.NextCommandForTeam == expectedTeam &&
            api.PlacementX is { } placementX && api.PlacementY is { } placementY &&
            Math.Abs(placementX) <= 0.001f && Math.Abs(placementY) <= 0.001f &&
            observation.Udp.Command is Referee.Types.Command.Stop or Referee.Types.Command.Halt &&
            observation.Udp.NextCommand == (expectedTeam == "YELLOW"
                ? Referee.Types.Command.PrepareKickoffYellow
                : Referee.Types.Command.PrepareKickoffBlue);

        var progressedKickoff = IsExpectedKickoffProgression(api, observation.Udp, expectedTeam);
        if (!stoppedPlacement && !progressedKickoff)
            return;

        if (!await IsCurrentSimulatorAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Simulator identity changed before the fresh placement confirmation.");

        var continuation = await driver.ReadContinuationStateAsync(cancellationToken).ConfigureAwait(false);
        if (continuation is null || continuation.Command != api.Command.ToString() ||
            continuation.Phase != api.Phase.ToString() ||
            continuation.NextCommand != api.NextCommand ||
            continuation.NextCommandForTeam != api.NextCommandForTeam ||
            continuation.Stage != api.Stage ||
            continuation.BlueScore != api.BlueScore || continuation.YellowScore != api.YellowScore ||
            (stoppedPlacement &&
                (continuation.PlacementX is not { } continuationX || continuation.PlacementY is not { } continuationY ||
                 Math.Abs(continuationX) > 0.001f || Math.Abs(continuationY) > 0.001f)))
            return;

        if (stoppedPlacement)
        {
            var next = continuation.ContinueActions.SingleOrDefault(action =>
                action.Type == "NEXT_COMMAND" && action.ForTeam == expectedTeam);
            if (next is not { HasContinuationIssues: false } ||
                next.State is not ("READY_AUTO" or "READY_MANUAL"))
                return;
        }

        if (!await IsCurrentSimulatorAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Simulator identity changed while confirming ball placement.");

        var index = Events.FindLastIndex(item => item.Status == "sent_unconfirmed" &&
            item.CommandCounter == outstanding.Event.CommandCounter);
        if (index >= 0)
            Events[index] = outstanding.Event with
            {
                Status = "confirmed",
                ObservationSequence = observation.RequestSequence,
                Reason = stoppedPlacement
                    ? outstanding.ApiSnapshot?.Stage == "NORMAL_SECOND_HALF_PRE"
                        ? "Observed NORMAL_HALF_TIME to NORMAL_SECOND_HALF_PRE transition; fresh matched observation reports BLUE kickoff NEXT_COMMAND ready after ball-only placement."
                        : "Fresh matched UDP/API observation reports the same kickoff NEXT_COMMAND ready after ball-only placement."
                    : "Fresh matched UDP/API observation progressed through the same scored kickoff after ball-only placement.",
            };
        pending = null;
    }

    private static bool IsExpectedKickoffProgression(
        GameControllerMatchSnapshot api,
        RefereeCommandSnapshot udp,
        string expectedTeam)
    {
        if (api.Command == GameControllerCommandKind.Kickoff &&
            api.Phase == GameControllerMatchPhase.Kickoff)
        {
            var expectedPrepareCommand = expectedTeam == "YELLOW"
                ? Referee.Types.Command.PrepareKickoffYellow
                : Referee.Types.Command.PrepareKickoffBlue;
            return api.CommandForTeam == expectedTeam && api.NextCommand == "NORMAL_START" &&
                udp.Command == expectedPrepareCommand && udp.NextCommand == Referee.Types.Command.NormalStart;
        }

        return api.Command == GameControllerCommandKind.NormalStart &&
            api.Phase == GameControllerMatchPhase.Kickoff &&
            api.PhaseForTeam == expectedTeam &&
            udp.Command == Referee.Types.Command.NormalStart;
    }

    private async Task<bool> IsCurrentSimulatorAsync(CancellationToken cancellationToken)
    {
        var evidence = await readinessProbe.ReadCurrentParticipantEvidenceAsync(cancellationToken).ConfigureAwait(false);
        var current = evidence?.Resources.SingleOrDefault(resource => resource.ResourceId == "simulator");
        var now = DateTimeOffset.UtcNow;
        return evidence is not null && now - evidence.ObservedAtUtc <= TimeSpan.FromSeconds(2) &&
            evidence.ObservedAtUtc <= now + TimeSpan.FromSeconds(1) &&
            IsFullContainerId(expectedSimulator.ContainerId) && current is not null && current.IsReadyAt(now) &&
            current.CurrentResourceId == expectedSimulator.CurrentResourceId &&
            current.ContainerId == expectedSimulator.ContainerId && current.RunId == expectedSimulator.RunId;
    }

    private static bool IsFullContainerId(string? containerId) => containerId is { Length: 64 } &&
        containerId.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private sealed record PendingPlacement(
        BallPlacementFixtureEvent Event,
        GameControllerMatchSnapshot? ApiSnapshot,
        long RequestSequence);
}
