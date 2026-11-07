using System.Net;
using System.Net.Sockets;
using System.Globalization;
using Google.Protobuf;
using System.Text.Json;
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
    Task SendBallToPositionAsync(float x, float y, CancellationToken cancellationToken);
    Task SendInitialOwnHalfSetupAsync(bool blueTeamOnPositiveHalf, CancellationToken cancellationToken);
}

internal sealed class SimulatorBallControlClient : ISimulatorBallControlClient
{
    internal const string SimulatorResourceHost = "simulator";
    internal const int DefaultControlPort = 10300;

    internal static byte[] CreateBallPlacementCommand(float x, float y)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(x), "Ball placement coordinates must be finite.");
        return new SimulatorCommand
        {
            Control = new SimulatorControl
            {
                TeleportBall = new TeleportBall
                {
                    X = x,
                    Y = y,
                    Vx = 0,
                    Vy = 0,
                },
            },
        }.ToByteArray();
    }

    internal static byte[] CreateInitialOwnHalfSetupCommand(bool blueTeamOnPositiveHalf)
    {
        var control = new SimulatorControl
        {
            TeleportBall = new TeleportBall { X = 0, Y = 0, Z = 0, Vx = 0, Vy = 0, Vz = 0 },
        };
        var yellow = new (uint Id, float X, float Y)[]
        {
            (0, 4.2f, 0), (1, 1.2f, -2), (2, 1.2f, -1), (3, 1.2f, 0), (4, 1.2f, 1),
            (5, 1.2f, 2), (6, 2.6f, -2), (7, 2.6f, -1), (8, 2.6f, 0), (9, 2.6f, 1), (10, 2.6f, 2),
        };
        var yellowPositiveHalf = !blueTeamOnPositiveHalf;
        var yellowSign = yellowPositiveHalf ? 1f : -1f;
        var blueSign = -yellowSign;
        var yellowOrientation = yellowPositiveHalf ? MathF.PI : 0f;
        var blueOrientation = blueSign > 0 ? MathF.PI : 0f;
        foreach (var (id, x, y) in yellow)
            control.TeleportRobot.Add(CreateStoppedRobot(id, Team.Yellow, yellowSign * x, y, yellowOrientation));
        foreach (var (id, x, y) in yellow)
            control.TeleportRobot.Add(CreateStoppedRobot(id, Team.Blue, blueSign * x, y, blueOrientation));
        return new SimulatorCommand { Control = control }.ToByteArray();
    }

    private static TeleportRobot CreateStoppedRobot(uint id, Team team, float x, float y, float orientation) =>
        new()
        {
            Id = new RobotId { Id = id, Team = team },
            X = x,
            Y = y,
            Orientation = orientation,
            VX = 0,
            VY = 0,
            VAngular = 0,
            ByForce = false,
        };

    public async Task SendBallToPositionAsync(float x, float y, CancellationToken cancellationToken)
    {
        await SendCommandAsync(CreateBallPlacementCommand(x, y), cancellationToken).ConfigureAwait(false);
    }

    public async Task SendInitialOwnHalfSetupAsync(bool blueTeamOnPositiveHalf, CancellationToken cancellationToken)
    {
        await SendCommandAsync(CreateInitialOwnHalfSetupCommand(blueTeamOnPositiveHalf), cancellationToken).ConfigureAwait(false);
    }

    private static async Task SendCommandAsync(byte[] payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var addresses = await Dns.GetHostAddressesAsync(SimulatorResourceHost, cancellationToken).ConfigureAwait(false);
        var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
            ?? throw new InvalidOperationException($"Simulator resource '{SimulatorResourceHost}' did not resolve to IPv4.");
        using var client = new UdpClient(AddressFamily.InterNetwork);
        await client.SendAsync(payload, new IPEndPoint(address, DefaultControlPort), cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Reuses SSL simulation-control UDP 10300 for one initial own-half lineup and later ball-only
/// official placement. Port 12345 is the separate Ibis robot input.
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
    private readonly HashSet<(uint Counter, Referee.Types.Command Command)> handledOfficialPlacements = [];
    private readonly HashSet<(uint Counter, Referee.Types.Command NextCommand)> handledPenaltySetups = [];

    private readonly HashSet<string> diagnosticContexts = [];

    private void TraceFixture(string point, RefereeLifecycleObservation observation, object? detail = null)
    {
        var udp = observation.Udp;
        var api = observation.ApiSnapshot;
        var key = $"{point}:{udp.CommandCounter}:{api?.Command}:{api?.Phase}:{api?.Stage}:{udp.BlueScore}:{udp.YellowScore}:{api?.NextCommand}:{api?.NextCommandForTeam}:{api?.PlacementX}:{api?.PlacementY}:{pending?.Event.CommandCounter}";
        if (!diagnosticContexts.Add(key))
            return;
        try
        {
            Console.WriteLine("MATCH_BALL_FIXTURE " + JsonSerializer.Serialize(new
            {
                Point = point,
                ObservedAtUtc = DateTimeOffset.UtcNow,
                observation.RequestSequence,
                Command = udp.Command.ToString(),
                udp.CommandCounter,
                udp.PacketTimestamp,
                Stage = udp.Stage?.ToString(),
                udp.BlueScore,
                udp.YellowScore,
                NextCommand = udp.NextCommand?.ToString(),
                udp.DesignatedPositionX,
                udp.DesignatedPositionY,
                Api = api,
                Pending = pending?.Event,
                Detail = detail,
            }));
        }
        catch (Exception exception) when (exception is IOException or JsonException or ArgumentException or NotSupportedException)
        {
            // Diagnostic output must not change placement eligibility or stop the match.
        }
    }

    internal List<BallPlacementFixtureEvent> Events { get; } = [];
    internal bool HasUnconfirmedPlacement => pending is not null;

    internal async Task SendInitialOwnHalfSetupAsync(CancellationToken cancellationToken)
    {
        if (!await IsCurrentSimulatorAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Simulator identity changed before the initial own-half setup.");

        var blueTeamOnPositiveHalf = baselineSnapshot.BlueTeamOnPositiveHalf
            ?? throw new InvalidOperationException(
                "The initial referee STOP packet did not declare which half belongs to the blue team.");

        await simulator.SendInitialOwnHalfSetupAsync(blueTeamOnPositiveHalf, cancellationToken).ConfigureAwait(false);

        if (!await IsCurrentSimulatorAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Simulator identity changed while sending the initial own-half setup.");

        Events.Add(new("initial_setup_sent_unconfirmed", baselineSnapshot.BlueScore, baselineSnapshot.YellowScore,
            baselineSnapshot.CommandCounter, 0, expectedSimulator.ContainerId, expectedSimulator.RunId,
            $"Sent one centered stationary ball and 22 own-half robot teleports using blueTeamOnPositiveHalf={blueTeamOnPositiveHalf}; simulator application is not confirmed by UDP send."));
    }

    internal async Task ObserveAsync(RefereeLifecycleObservation observation, CancellationToken cancellationToken)
    {
        var previous = previousObservation;
        previousObservation = observation;
        TraceFixture("observation", observation);

        if (previous is not null && IsObservedSecondHalfKickoff(previous, observation))
            pendingSecondHalfKickoff = (observation.Udp.BlueScore, observation.Udp.YellowScore);

        var secondHalfEpisodeStillActive = pendingSecondHalfKickoff is { } kickoffScore &&
            observation.Udp.Stage == Referee.Types.Stage.NormalSecondHalfPre &&
            observation.Udp.BlueScore == kickoffScore.Blue && observation.Udp.YellowScore == kickoffScore.Yellow;
        if (pendingSecondHalfKickoff is not null && !secondHalfEpisodeStillActive)
            pendingSecondHalfKickoff = null;
        var isPendingSecondHalfKickoff = secondHalfEpisodeStillActive &&
            observation.Udp.NextCommand == Referee.Types.Command.PrepareKickoffBlue;

        PendingPlacement? superseded = null;
        if (pending is { } outstanding)
        {
            TraceFixture("pending_confirmation_enter", observation);
            await ConfirmPlacementAsync(observation, outstanding, cancellationToken).ConfigureAwait(false);
            TraceFixture("pending_confirmation_return", observation);
            if (pending is null || !IsNewOfficialPlacementEpisode(observation, outstanding))
                return;

            // GC may fail/cancel a placement and select a different restart. A later official
            // request must still pass the full UDP/API/control and simulator-identity checks below.
            // Do not pretend that the earlier placement succeeded.
            superseded = outstanding;
        }

        var udp = observation.Udp;
        if (udp.BlueScore < handledBlueScore || udp.YellowScore < handledYellowScore)
        {
            handledBlueScore = udp.BlueScore;
            handledYellowScore = udp.YellowScore;
            return;
        }

        var hasScoreIncrease = udp.BlueScore > handledBlueScore || udp.YellowScore > handledYellowScore;
        var isOfficialPlacement = TryGetPlacementTeam(udp.Command, out var placementTeam);
        var isPenaltySetup = TryGetPenaltyTeam(udp.NextCommand, out var penaltyTeam) &&
            udp.Command == Referee.Types.Command.Stop &&
            udp.Stage is Referee.Types.Stage.NormalFirstHalf or Referee.Types.Stage.NormalSecondHalf;
        if (!hasScoreIncrease && !isPendingSecondHalfKickoff && !isOfficialPlacement && !isPenaltySetup)
            return;
        if (isOfficialPlacement && handledOfficialPlacements.Contains((udp.CommandCounter, udp.Command)))
            return;
        if (isPenaltySetup && handledPenaltySetups.Contains((udp.CommandCounter, udp.NextCommand!.Value)))
            return;

        // Keep looking at the same score while the controller settles into its stopped restart context.
        var api = observation.ApiSnapshot;
        var action = await driver.ReadContinuationStateAsync(cancellationToken).ConfigureAwait(false);
        float placementX;
        float placementY;
        string reason;
        string? restartTeam = null;
        var eligible = isOfficialPlacement
            ? IsEligibleOfficialPlacement(udp, api, action, placementTeam!, out placementX, out placementY,
                out restartTeam, out reason)
            : isPenaltySetup
                ? IsEligiblePenaltySetup(udp, api, action, out placementX, out placementY,
                    out penaltyTeam, out reason)
                : IsEligiblePlacement(udp, api, action, handledBlueScore, handledYellowScore,
                    isPendingSecondHalfKickoff, out placementX, out placementY, out reason);
        if (!eligible)
        {
            TraceFixture("not_sent", observation, reason);
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
        if (isOfficialPlacement)
            handledOfficialPlacements.Add((udp.CommandCounter, udp.Command));
        if (isPenaltySetup)
            handledPenaltySetups.Add((udp.CommandCounter, udp.NextCommand!.Value));
        TraceFixture("placement_send_enter", observation, new { X = placementX, Y = placementY });
        await simulator.SendBallToPositionAsync(placementX, placementY, cancellationToken).ConfigureAwait(false);
        TraceFixture("placement_send_return", observation, new { X = placementX, Y = placementY });
        if (!await IsCurrentSimulatorAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Simulator identity changed while sending the official ball-placement fixture.");

        if (superseded is not null)
        {
            var index = Events.FindLastIndex(item => item.Status == "sent_unconfirmed" &&
                item.CommandCounter == superseded.Event.CommandCounter);
            if (index >= 0)
                Events[index] = superseded.Event with
                {
                    Status = "superseded",
                    ObservationSequence = observation.RequestSequence,
                    Reason = "A newer fresh matched official placement replaced the earlier unconfirmed episode; earlier placement success is not established.",
                };
            TraceFixture("placement_superseded", observation);
        }

        var sent = new BallPlacementFixtureEvent("sent_unconfirmed", udp.BlueScore, udp.YellowScore,
            udp.CommandCounter, observation.RequestSequence, expectedSimulator.ContainerId,
            expectedSimulator.RunId,
            $"Sent ball-only TeleportBall({placementX.ToString(CultureInfo.InvariantCulture)},{placementY.ToString(CultureInfo.InvariantCulture)}) on simulator SSL control port 10300.");
        Events.Add(sent);
        pending = new(sent, observation.ApiSnapshot, observation.RequestSequence, placementX, placementY,
            isOfficialPlacement ? placementTeam : null,
            isPenaltySetup ? penaltyTeam : restartTeam, udp.PacketTimestamp, isPenaltySetup);
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
        out string reason) =>
        IsEligiblePlacement(udp, api, continuation, previousBlueScore, previousYellowScore,
            observedSecondHalfKickoff, out _, out _, out reason);

    internal static bool IsEligiblePlacement(
        RefereeCommandSnapshot udp,
        GameControllerMatchSnapshot? api,
        GameControllerContinuationState? continuation,
        uint previousBlueScore,
        uint previousYellowScore,
        bool observedSecondHalfKickoff,
        out float placementX,
        out float placementY,
        out string reason)
    {
        placementX = 0;
        placementY = 0;
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

    internal static bool IsEligibleOfficialPlacement(
        RefereeCommandSnapshot udp,
        GameControllerMatchSnapshot? api,
        GameControllerContinuationState? continuation,
        string expectedTeam,
        out float placementX,
        out float placementY,
        out string? expectedRestartTeam,
        out string reason)
    {
        placementX = 0;
        placementY = 0;
        expectedRestartTeam = null;
        if (!TryGetPlacementTeam(udp.Command, out var udpTeam) || udpTeam != expectedTeam ||
            api is null || api.Command != GameControllerCommandKind.BallPlacement ||
            api.Phase != GameControllerMatchPhase.BallPlacement || api.CommandForTeam != expectedTeam ||
            api.PhaseForTeam != expectedTeam ||
            !GameControllerStateMapping.MatchesUdpCommand(api, udp) ||
            !GameControllerStateMapping.TryParseStage(api.Stage, out var apiStage) || udp.Stage != apiStage ||
            api.BlueScore != udp.BlueScore || api.YellowScore != udp.YellowScore ||
            !((api.NextCommand == "DIRECT" && api.NextCommandForTeam is "BLUE" or "YELLOW" &&
                udp.NextCommand == (api.NextCommandForTeam == "BLUE"
                    ? Referee.Types.Command.DirectFreeBlue : Referee.Types.Command.DirectFreeYellow)) ||
              (api.NextCommand == "FORCE_START" && api.NextCommandForTeam == "UNKNOWN" &&
                udp.NextCommand == Referee.Types.Command.ForceStart)) ||
            udp.DesignatedPositionX is not { } udpX || udp.DesignatedPositionY is not { } udpY ||
            api.PlacementX is not { } apiX || api.PlacementY is not { } apiY ||
            !float.IsFinite(udpX) || !float.IsFinite(udpY) || !float.IsFinite(apiX) || !float.IsFinite(apiY) ||
            Math.Abs(apiX - udpX / 1000f) > 0.001f || Math.Abs(apiY - udpY / 1000f) > 0.001f)
        {
            reason = "Fresh UDP and Game Controller API do not agree on the official team and designated ball-placement position.";
            return false;
        }

        if (continuation is null || continuation.Command != nameof(GameControllerCommandKind.BallPlacement) ||
            continuation.Phase != nameof(GameControllerMatchPhase.BallPlacement) ||
            continuation.Stage != api.Stage || continuation.NextCommand != api.NextCommand ||
            continuation.NextCommandForTeam != api.NextCommandForTeam ||
            continuation.BlueScore != udp.BlueScore || continuation.YellowScore != udp.YellowScore ||
            continuation.PlacementX is not { } continuationX || continuation.PlacementY is not { } continuationY ||
            Math.Abs(continuationX - apiX) > 0.001f || Math.Abs(continuationY - apiY) > 0.001f)
        {
            reason = "A fresh control API read did not match the current UDP/API designated placement.";
            return false;
        }

        placementX = apiX;
        placementY = apiY;
        expectedRestartTeam = api.NextCommandForTeam;
        reason = $"Fresh official {expectedTeam} BALL_PLACEMENT designated position matches the current GC state.";
        return true;
    }

    internal static bool IsEligiblePenaltySetup(
        RefereeCommandSnapshot udp,
        GameControllerMatchSnapshot? api,
        GameControllerContinuationState? continuation,
        out float placementX,
        out float placementY,
        out string? penaltyTeam,
        out string reason)
    {
        placementX = 0;
        placementY = 0;
        penaltyTeam = null;
        if (udp.Command != Referee.Types.Command.Stop ||
            udp.Stage is not (Referee.Types.Stage.NormalFirstHalf or Referee.Types.Stage.NormalSecondHalf) ||
            !TryGetPenaltyTeam(udp.NextCommand, out penaltyTeam) ||
            api is null || api.Command != GameControllerCommandKind.Stop || api.Phase != GameControllerMatchPhase.Stop ||
            !GameControllerStateMapping.MatchesUdpCommand(api, udp) ||
            !GameControllerStateMapping.TryParseStage(api.Stage, out var apiStage) || apiStage != udp.Stage ||
            api.CommandForTeam is not (null or "UNKNOWN") || api.PhaseForTeam is not (null or "UNKNOWN") ||
            api.NextCommand != "PENALTY" || api.NextCommandForTeam != penaltyTeam ||
            api.BlueScore != udp.BlueScore || api.YellowScore != udp.YellowScore ||
            udp.DesignatedPositionX is not { } udpX || udp.DesignatedPositionY is not { } udpY ||
            api.PlacementX is not { } apiX || api.PlacementY is not { } apiY ||
            !float.IsFinite(udpX) || !float.IsFinite(udpY) || !float.IsFinite(apiX) || !float.IsFinite(apiY) ||
            Math.Abs(apiX - udpX / 1000f) > 0.001f || Math.Abs(apiY - udpY / 1000f) > 0.001f)
        {
            reason = "Fresh UDP and Game Controller API do not agree on the stopped penalty team and official target.";
            return false;
        }

        if (continuation is null || continuation.Command != nameof(GameControllerCommandKind.Stop) ||
            continuation.Phase != nameof(GameControllerMatchPhase.Stop) || continuation.Stage != api.Stage ||
            continuation.NextCommand != "PENALTY" || continuation.NextCommandForTeam != penaltyTeam ||
            continuation.BlueScore != udp.BlueScore || continuation.YellowScore != udp.YellowScore ||
            continuation.PlacementX is not { } continuationX || continuation.PlacementY is not { } continuationY ||
            Math.Abs(continuationX - apiX) > 0.001f || Math.Abs(continuationY - apiY) > 0.001f)
        {
            reason = "A fresh control API read did not match the current UDP/API penalty target.";
            return false;
        }

        var expectedPenaltyTeam = penaltyTeam;
        var next = continuation.ContinueActions.SingleOrDefault(action =>
            action.Type == "NEXT_COMMAND" && action.ForTeam == expectedPenaltyTeam);
        if (next is not { State: "BLOCKED", HasContinuationIssues: true } ||
            next.ContinuationIssues is not { } issues ||
            !issues.Any(issue => issue.Contains("away from placement pos", StringComparison.OrdinalIgnoreCase)))
        {
            reason = "GC NEXT_COMMAND is not blocked because the ball is away from the official penalty target.";
            return false;
        }

        placementX = apiX;
        placementY = apiY;
        reason = $"Fresh STOP/NEXT_PENALTY/{penaltyTeam} target matches UDP millimeters and GC API meters; NEXT_COMMAND is distance-blocked.";
        return true;
    }

    private static bool TryGetPenaltyTeam(Referee.Types.Command? command, out string? team)
    {
        team = command switch
        {
            Referee.Types.Command.PreparePenaltyBlue => "BLUE",
            Referee.Types.Command.PreparePenaltyYellow => "YELLOW",
            _ => null,
        };
        return team is not null;
    }
    private static bool TryGetPlacementTeam(Referee.Types.Command command, out string? team)
    {
        team = command switch
        {
            Referee.Types.Command.BallPlacementBlue => "BLUE",
            Referee.Types.Command.BallPlacementYellow => "YELLOW",
            _ => null,
        };
        return team is not null;
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

    private static bool IsNewOfficialPlacementEpisode(
        RefereeLifecycleObservation observation, PendingPlacement outstanding) =>
        outstanding.PlacementTeam is not null && !outstanding.IsPenaltySetup &&
        observation.RequestSequence > outstanding.RequestSequence &&
        observation.Udp.CommandCounter > outstanding.Event.CommandCounter &&
        outstanding.PacketTimestamp is { } before &&
        observation.Udp.PacketTimestamp is { } after && after > before &&
        TryGetPlacementTeam(observation.Udp.Command, out _) &&
        GameControllerStateMapping.TryParseStage(outstanding.ApiSnapshot?.Stage, out var expectedStage) &&
        observation.Udp.Stage == expectedStage &&
        observation.ApiSnapshot?.Stage == outstanding.ApiSnapshot?.Stage &&
        observation.Udp.BlueScore == outstanding.Event.BlueScore &&
        observation.Udp.YellowScore == outstanding.Event.YellowScore;

    private async Task ConfirmPlacementAsync(
        RefereeLifecycleObservation observation,
        PendingPlacement outstanding,
        CancellationToken cancellationToken)
    {
        var advancesKickoffStage = observation.ApiSnapshot is { } currentApi &&
            IsExpectedKickoffStageAdvance(outstanding, currentApi, observation.Udp);
        if (observation.RequestSequence <= outstanding.RequestSequence ||
            observation.ApiSnapshot is not { } api ||
            api.BlueScore != outstanding.Event.BlueScore || api.YellowScore != outstanding.Event.YellowScore ||
            (api.Stage != outstanding.ApiSnapshot?.Stage && !advancesKickoffStage) ||
            !GameControllerStateMapping.MatchesUdpCommand(api, observation.Udp))
        {
            TraceFixture("confirmation_context_mismatch", observation);
            return;
        }

        var expectedTeam = outstanding.RestartTeam ?? outstanding.ApiSnapshot?.NextCommandForTeam;
        var neutralOfficialRestart = outstanding.PlacementTeam is not null && !outstanding.IsPenaltySetup &&
            outstanding.ApiSnapshot?.NextCommand == "FORCE_START" && expectedTeam == "UNKNOWN";
        if ((expectedTeam is not ("BLUE" or "YELLOW") && !neutralOfficialRestart) ||
            observation.Udp.BlueScore != outstanding.Event.BlueScore ||
            observation.Udp.YellowScore != outstanding.Event.YellowScore ||
            !GameControllerStateMapping.TryParseStage(outstanding.ApiSnapshot?.Stage, out var expectedStage) ||
            (observation.Udp.Stage != expectedStage && !advancesKickoffStage))
        {
            TraceFixture("confirmation_team_stage_mismatch", observation);
            return;
        }

        var legacyKickoffPlacement = outstanding.RestartTeam is null;
        var stoppedPlacement = legacyKickoffPlacement && api.Command == GameControllerCommandKind.Stop &&
            api.Phase == GameControllerMatchPhase.Stop &&
            api.NextCommand == outstanding.ApiSnapshot?.NextCommand &&
            api.NextCommandForTeam == expectedTeam &&
            api.PlacementX is { } placementX && api.PlacementY is { } placementY &&
            Math.Abs(placementX) <= 0.001f && Math.Abs(placementY) <= 0.001f &&
            observation.Udp.Command is Referee.Types.Command.Stop or Referee.Types.Command.Halt &&
            observation.Udp.NextCommand == (expectedTeam == "YELLOW"
                ? Referee.Types.Command.PrepareKickoffYellow
                : Referee.Types.Command.PrepareKickoffBlue);

        var stoppedPenaltyPlacement = outstanding.IsPenaltySetup &&
            outstanding.PacketTimestamp is { } penaltyPriorTimestamp &&
            observation.Udp.PacketTimestamp is { } penaltyNewTimestamp &&
            penaltyNewTimestamp > penaltyPriorTimestamp &&
            api.Command == GameControllerCommandKind.Stop && api.Phase == GameControllerMatchPhase.Stop &&
            api.NextCommand == "PENALTY" && api.NextCommandForTeam == expectedTeam &&
            api.PlacementX is { } penaltyX && api.PlacementY is { } penaltyY &&
            Math.Abs(penaltyX - outstanding.PlacementX) <= 0.001f &&
            Math.Abs(penaltyY - outstanding.PlacementY) <= 0.001f &&
            observation.Udp.DesignatedPositionX is { } currentPenaltyTargetX &&
            observation.Udp.DesignatedPositionY is { } currentPenaltyTargetY &&
            float.IsFinite(currentPenaltyTargetX) && float.IsFinite(currentPenaltyTargetY) &&
            Math.Abs(currentPenaltyTargetX / 1000f - outstanding.PlacementX) <= 0.001f &&
            Math.Abs(currentPenaltyTargetY / 1000f - outstanding.PlacementY) <= 0.001f &&
            observation.Udp.Command == Referee.Types.Command.Stop &&
            observation.Udp.NextCommand == (expectedTeam == "BLUE"
                ? Referee.Types.Command.PreparePenaltyBlue
                : Referee.Types.Command.PreparePenaltyYellow);

        var progressedPenaltyPlacement = outstanding.IsPenaltySetup &&
            outstanding.Event.CommandCounter < observation.Udp.CommandCounter &&
            outstanding.PacketTimestamp is { } priorPenaltyTimestamp &&
            observation.Udp.PacketTimestamp is { } newPenaltyTimestamp &&
            newPenaltyTimestamp > priorPenaltyTimestamp &&
            api.Phase == GameControllerMatchPhase.Penalty && api.PhaseForTeam == expectedTeam &&
            ((api.Command == GameControllerCommandKind.Penalty && api.CommandForTeam == expectedTeam &&
                observation.Udp.Command == (expectedTeam == "BLUE"
                    ? Referee.Types.Command.PreparePenaltyBlue
                    : Referee.Types.Command.PreparePenaltyYellow)) ||
             (api.Command == GameControllerCommandKind.NormalStart &&
                api.CommandForTeam is null or "UNKNOWN" &&
                observation.Udp.Command == Referee.Types.Command.NormalStart));

        var progressedKickoff = legacyKickoffPlacement &&
            outstanding.Event.CommandCounter < observation.Udp.CommandCounter &&
            outstanding.PacketTimestamp is { } kickoffPriorTimestamp &&
            observation.Udp.PacketTimestamp is { } kickoffNewTimestamp &&
            kickoffNewTimestamp > kickoffPriorTimestamp &&
            IsExpectedKickoffProgression(api, observation.Udp, expectedTeam);
        var progressedOfficialPlacement = !outstanding.IsPenaltySetup && outstanding.RestartTeam is { } restartTeam &&
            outstanding.Event.CommandCounter < observation.Udp.CommandCounter &&
            outstanding.PacketTimestamp is { } priorTimestamp && observation.Udp.PacketTimestamp is { } newTimestamp &&
            newTimestamp > priorTimestamp && IsExpectedOfficialPlacementProgression(
                api, observation.Udp, restartTeam, neutralOfficialRestart);
        if (!stoppedPlacement && !stoppedPenaltyPlacement && !progressedKickoff &&
            !progressedOfficialPlacement && !progressedPenaltyPlacement)
        {
            TraceFixture("confirmation_wait_progression", observation);
            return;
        }

        TraceFixture("confirmation_identity_enter", observation);
        if (!await IsCurrentSimulatorAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Simulator identity changed before the fresh placement confirmation.");
        TraceFixture("confirmation_identity_return", observation);
        TraceFixture("confirmation_control_enter", observation);
        var continuation = await driver.ReadContinuationStateAsync(cancellationToken).ConfigureAwait(false);
        TraceFixture("confirmation_control_return", observation, continuation);
        if (continuation is null || continuation.Command != api.Command.ToString() ||
            continuation.Phase != api.Phase.ToString() ||
            continuation.NextCommand != api.NextCommand ||
            continuation.NextCommandForTeam != api.NextCommandForTeam ||
            continuation.Stage != api.Stage ||
            continuation.BlueScore != api.BlueScore || continuation.YellowScore != api.YellowScore ||
            (stoppedPlacement &&
                (continuation.PlacementX is not { } continuationX || continuation.PlacementY is not { } continuationY ||
                 Math.Abs(continuationX) > 0.001f || Math.Abs(continuationY) > 0.001f)) ||
            (stoppedPenaltyPlacement &&
                (continuation.PlacementX is not { } penaltyContinuationX ||
                 continuation.PlacementY is not { } penaltyContinuationY ||
                 Math.Abs(penaltyContinuationX - outstanding.PlacementX) > 0.001f ||
                 Math.Abs(penaltyContinuationY - outstanding.PlacementY) > 0.001f)))
        {
            TraceFixture("confirmation_control_mismatch", observation, continuation);
            return;
        }

        if (stoppedPlacement || stoppedPenaltyPlacement)
        {
            var next = continuation.ContinueActions.SingleOrDefault(action =>
                action.Type == "NEXT_COMMAND" && action.ForTeam == expectedTeam);
            var ready = stoppedPenaltyPlacement
                ? next is { State: "READY_AUTO", HasContinuationIssues: false }
                : next is { HasContinuationIssues: false } && next.State is "READY_AUTO" or "READY_MANUAL";
            if (!ready)
            {
                TraceFixture("confirmation_not_ready", observation, continuation);
                return;
            }
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
                    : stoppedPenaltyPlacement
                        ? "Fresh matched STOP/penalty observation reports the same-team NEXT_COMMAND ready (READY_AUTO) after ball-only placement."
                    : progressedPenaltyPlacement
                        ? "Fresh matched UDP/API observation advanced through the captured team's penalty restart after ball-only designated-position placement."
                    : progressedOfficialPlacement
                        ? neutralOfficialRestart
                            ? "Fresh matched UDP/API observation advanced from official BALL_PLACEMENT to the captured GC-selected neutral restart after ball-only designated-position placement."
                            : "Fresh matched UDP/API observation advanced from the same-team BALL_PLACEMENT to DIRECT/FREE_KICK after ball-only designated-position placement."
                        : "Fresh matched UDP/API observation progressed through the same scored kickoff after ball-only placement.",
            };
        TraceFixture("confirmed", observation);
        pending = null;
    }

    private static bool IsExpectedKickoffStageAdvance(
        PendingPlacement outstanding,
        GameControllerMatchSnapshot api,
        RefereeCommandSnapshot udp)
    {
        if (outstanding.RestartTeam is not null || outstanding.IsPenaltySetup ||
            outstanding.ApiSnapshot?.NextCommandForTeam is not ("BLUE" or "YELLOW") ||
            api.Command != GameControllerCommandKind.NormalStart ||
            api.CommandForTeam is not (null or "UNKNOWN") ||
            api.Phase != GameControllerMatchPhase.Kickoff ||
            api.PhaseForTeam != outstanding.ApiSnapshot.NextCommandForTeam ||
            udp.Command != Referee.Types.Command.NormalStart ||
            udp.CommandCounter <= outstanding.Event.CommandCounter ||
            outstanding.PacketTimestamp is not { } before ||
            udp.PacketTimestamp is not { } after || after <= before)
            return false;

        return (outstanding.ApiSnapshot.Stage, api.Stage, udp.Stage) is
            ("NORMAL_FIRST_HALF_PRE", "NORMAL_FIRST_HALF", Referee.Types.Stage.NormalFirstHalf) or
            ("NORMAL_SECOND_HALF_PRE", "NORMAL_SECOND_HALF", Referee.Types.Stage.NormalSecondHalf);
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
            var normalStartNext = api.NextCommand == "NORMAL_START" &&
                udp.NextCommand == Referee.Types.Command.NormalStart;
            var capturedKickoffNext = api.NextCommand == "KICKOFF" &&
                api.NextCommandForTeam == expectedTeam && api.PhaseForTeam == expectedTeam &&
                udp.NextCommand == expectedPrepareCommand;
            return api.CommandForTeam == expectedTeam && udp.Command == expectedPrepareCommand &&
                (normalStartNext || capturedKickoffNext);
        }

        return api.Command == GameControllerCommandKind.NormalStart &&
            api.Phase == GameControllerMatchPhase.Kickoff &&
            api.PhaseForTeam == expectedTeam &&
            udp.Command == Referee.Types.Command.NormalStart;
    }

    private static bool IsExpectedOfficialPlacementProgression(
        GameControllerMatchSnapshot api,
        RefereeCommandSnapshot udp,
        string expectedTeam,
        bool neutralRestart)
    {
        if (neutralRestart)
            return api.Command == GameControllerCommandKind.ForceStart &&
                api.Phase == GameControllerMatchPhase.Running &&
                api.CommandForTeam is null or "UNKNOWN" && api.PhaseForTeam is null or "UNKNOWN" &&
                udp.Command == Referee.Types.Command.ForceStart &&
                api.BlueScore == udp.BlueScore && api.YellowScore == udp.YellowScore &&
                GameControllerStateMapping.TryParseStage(api.Stage, out var neutralStage) && udp.Stage == neutralStage;

        var expectedUdp = expectedTeam == "YELLOW"
            ? Referee.Types.Command.DirectFreeYellow
            : Referee.Types.Command.DirectFreeBlue;
        return api.Command == GameControllerCommandKind.Direct &&
            api.Phase is GameControllerMatchPhase.FreeKick or GameControllerMatchPhase.Running &&
            api.CommandForTeam == expectedTeam && udp.Command == expectedUdp &&
            api.BlueScore == udp.BlueScore && api.YellowScore == udp.YellowScore &&
            GameControllerStateMapping.TryParseStage(api.Stage, out var apiStage) && udp.Stage == apiStage;
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
        long RequestSequence,
        float PlacementX,
        float PlacementY,
        string? PlacementTeam,
        string? RestartTeam,
        ulong? PacketTimestamp,
        bool IsPenaltySetup);
}
