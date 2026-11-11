using System.Text.Json;
using Duck.Testing.RefereeDriver;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Duck.Testing.RefereeDriver.Hosting;

internal sealed record MatchControllerReadiness(
    IReadOnlyList<string> ExpectedParticipantResources,
    bool ParticipantResourcesReady,
    bool GameControllerApiAndRefereeReady,
    bool VisionReady,
    string? FailureReason = null,
    MatchParticipantStatusEvidence? ParticipantEvidence = null)
{
    public bool IsComplete => ParticipantResourcesReady && GameControllerApiAndRefereeReady && VisionReady;
}

internal sealed record MatchParticipantStatusEvidence(
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<MatchParticipantResourceEvidence> Resources);

internal sealed record MatchParticipantResourceEvidence(
    string ResourceId,
    string? State,
    string? HealthStatus,
    string? CurrentResourceId = null,
    string? ContainerId = null,
    string? RunId = null,
    bool ServiceEvidenceReady = false,
    DateTimeOffset? ServiceEvidenceObservedAtUtc = null,
    long? RecorderArchiveBytes = null,
    DateTimeOffset? RecorderArchiveLastWriteUtc = null,
    string? ServiceEvidenceReason = null)
{
    public bool IsReady
        => IsReadyAt(DateTimeOffset.UtcNow);

    internal bool IsReadyAt(DateTimeOffset now)
    {
        if (!string.Equals(State, "Running", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(CurrentResourceId) ||
            string.IsNullOrWhiteSpace(RunId) ||
            !IsFullContainerId(ContainerId) ||
            string.Equals(HealthStatus, "Unhealthy", StringComparison.OrdinalIgnoreCase))
            return false;

        if (ResourceId is "tigers-blue" or "autoref-tigers")
            return ServiceEvidenceReady && ServiceEvidenceObservedAtUtc is { } serviceObserved &&
                now - serviceObserved <= TimeSpan.FromMinutes(2) && serviceObserved <= now + TimeSpan.FromSeconds(1);

        if (ResourceId == "ssl-log-recorder")
            return ServiceEvidenceReady && ServiceEvidenceObservedAtUtc is { } recorderObserved &&
                now - recorderObserved <= TimeSpan.FromSeconds(15) &&
                recorderObserved <= now + TimeSpan.FromSeconds(1) &&
                RecorderArchiveBytes is > 0 &&
                RecorderArchiveLastWriteUtc is { } lastWrite &&
                now - lastWrite <= TimeSpan.FromSeconds(15) &&
                lastWrite <= now + TimeSpan.FromSeconds(1);

        return string.Equals(HealthStatus, "Healthy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFullContainerId(string? value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
}

internal interface IMatchControllerReadinessProbe
{
    TimeSpan ReadinessTimeout { get; }
    Task<MatchControllerReadiness> WaitForInitialReadinessAsync(CancellationToken cancellationToken);
    Task<MatchControllerReadiness> ReadFinalReadinessAsync(
        RefereeCommandSnapshot finalSnapshot, CancellationToken cancellationToken);
    Task<MatchParticipantStatusEvidence?> ReadCurrentParticipantEvidenceAsync(CancellationToken cancellationToken);
}

internal sealed class MatchControllerReadinessProbe(
    RefereeApiMatcher matcher,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    TimeProvider clock) : IMatchControllerReadinessProbe
{
    private readonly Uri visionHealthUri = ReadRequiredUri(configuration["MatchController:VisionHealthUri"]);
    private readonly string[] participantResources = ReadParticipantResources(configuration["MatchController:ParticipantResources"]);
    private readonly string participantStatusPath = ReadParticipantStatusPath(configuration["MatchController:ParticipantStatusPath"]);
    private readonly TimeSpan readinessTimeout = ReadTimeout(configuration["MatchController:ReadinessTimeout"]);
    public TimeSpan ReadinessTimeout => readinessTimeout;

    public async Task<MatchControllerReadiness> WaitForInitialReadinessAsync(CancellationToken cancellationToken)
    {
        var participantEvidence = await WaitForParticipantEvidenceAsync(cancellationToken).ConfigureAwait(false);
        var visionWasReady = await WaitForVisionAsync(cancellationToken).ConfigureAwait(false);
        // Vision can take time to become healthy. Re-read the observation produced by
        // RefereeObservationWorker afterwards so start is gated on current API/referee input.
        var match = await matcher.WaitForCurrentAsync(readinessTimeout, clock, cancellationToken)
            .ConfigureAwait(false);
        var visionReady = visionWasReady && await IsVisionReadyAsync(cancellationToken).ConfigureAwait(false);
        participantEvidence = await ReadParticipantEvidenceAsync(cancellationToken).ConfigureAwait(false);
        // Revalidate the matcher after both probes at the actual start boundary.
        match = matcher.GetCurrent();
        var participantsReady = IsParticipantEvidenceReady(participantEvidence, participantResources, clock.GetUtcNow());
        var reason = match.IsReady && visionReady && participantsReady
            ? null
            : $"apiReferee={match.Reason}, visionReady={visionReady}, participantResourcesReady={participantsReady}";
        return new(participantResources, participantsReady, match.IsReady, visionReady, reason, participantEvidence);
    }

    public async Task<MatchControllerReadiness> ReadFinalReadinessAsync(
        RefereeCommandSnapshot finalSnapshot, CancellationToken cancellationToken)
    {
        bool MatchesFinalSnapshot(RefereeApiMatchResult current) =>
            current.Udp?.Command.CommandCounter == finalSnapshot.CommandCounter &&
            current.Udp.Command.Stage == finalSnapshot.Stage;

        // RefereeObservationWorker owns API reads; consume its latest observation instead
        // of starting a competing request that could supersede it.
        var match = await matcher.WaitForCurrentAsync(readinessTimeout, clock,
                MatchesFinalSnapshot,
                cancellationToken)
            .ConfigureAwait(false);
        var visionReady = await IsVisionReadyAsync(cancellationToken).ConfigureAwait(false);
        var participantEvidence = await ReadParticipantEvidenceAsync(cancellationToken).ConfigureAwait(false);
        // Recheck the matcher after HTTP so the persisted result uses current input evidence.
        match = matcher.GetCurrent();
        if (!MatchesFinalSnapshot(match))
            match = match with { IsReady = false, Reason = "FinalRefereeSnapshotChanged" };
        var participantsReady = IsParticipantEvidenceReady(participantEvidence, participantResources, clock.GetUtcNow());
        var reason = match.IsReady && visionReady && participantsReady
            ? null
            : $"apiReferee={match.Reason}, visionReady={visionReady}, participantResourcesReady={participantsReady}";
        return new(participantResources, participantsReady, match.IsReady, visionReady, reason, participantEvidence);
    }

    public Task<MatchParticipantStatusEvidence?> ReadCurrentParticipantEvidenceAsync(CancellationToken cancellationToken) =>
        ReadParticipantEvidenceAsync(cancellationToken);

    private async Task<MatchParticipantStatusEvidence?> ReadParticipantEvidenceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(participantStatusPath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<MatchParticipantStatusEvidence>(json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return null;
        }
    }

    private async Task<MatchParticipantStatusEvidence?> WaitForParticipantEvidenceAsync(CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(readinessTimeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        MatchParticipantStatusEvidence? latest = null;
        try
        {
            while (true)
            {
                latest = await ReadParticipantEvidenceAsync(linked.Token).ConfigureAwait(false);
                if (IsParticipantEvidenceReady(latest, participantResources, clock.GetUtcNow()))
                    return latest;
                await Task.Delay(TimeSpan.FromMilliseconds(250), clock, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (
            deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return latest;
        }
    }

    internal static bool IsParticipantEvidenceReady(
        MatchParticipantStatusEvidence? evidence,
        IReadOnlyList<string> expectedResources,
        DateTimeOffset now)
    {
        if (evidence is null || now - evidence.ObservedAtUtc > TimeSpan.FromSeconds(2) ||
            evidence.ObservedAtUtc > now + TimeSpan.FromSeconds(1))
            return false;
        if (evidence.Resources.Count != expectedResources.Count || evidence.Resources.Any(resource => !resource.IsReadyAt(now)))
            return false;
        var runIds = evidence.Resources.Select(resource => resource.RunId).Distinct(StringComparer.Ordinal).ToArray();
        if (runIds.Length != 1)
            return false;
        return expectedResources.All(expected => evidence.Resources.Any(resource =>
            string.Equals(resource.ResourceId, expected, StringComparison.Ordinal)));
    }

    private async Task<bool> WaitForVisionAsync(CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(readinessTimeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            while (true)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (await IsVisionReadyAsync(linked.Token).ConfigureAwait(false))
                    return true;
                await Task.Delay(TimeSpan.FromMilliseconds(250), clock, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (
            deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task<bool> IsVisionReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClientFactory.CreateClient().GetAsync(visionHealthUri, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return false;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return IsVisionResponseReady(document.RootElement);
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    internal static bool IsVisionResponseReady(JsonElement response) =>
        response.TryGetProperty("IsReady", out var ready) && ready.ValueKind == JsonValueKind.True;

    private static string ReadParticipantStatusPath(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException("MatchController:ParticipantStatusPath must point to the AppHost participant status projection.")
            : Path.GetFullPath(value);

    private static Uri ReadRequiredUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp
            ? uri
            : throw new InvalidOperationException("MatchController:VisionHealthUri must be an absolute HTTP URI.");

    private static string[] ReadParticipantResources(string? value)
    {
        var resources = value?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        return resources.Length == 0
            ? throw new InvalidOperationException("MatchController:ParticipantResources must list the resources awaited by the AppHost.")
            : resources;
    }

    private static TimeSpan ReadTimeout(string? value) =>
        TimeSpan.TryParse(value, out var timeout) && timeout > TimeSpan.Zero
            ? timeout
            : TimeSpan.FromMinutes(2);
}

internal sealed record MatchControllerResult(
    string Status,
    string StageReached,
    string? TerminationKind,
    uint? FinalCommandCounter,
    ulong? FinalPacketTimestamp,
    MatchControllerReadiness? InitialReadiness,
    MatchControllerReadiness? FinalReadiness,
    string? Failure,
    IReadOnlyList<BallPlacementFixtureEvent>? BallPlacementFixtureEvents = null,
    RefereeCommandSnapshot? InitialStopSnapshot = null,
    long? RefereeSourceCaptureEpoch = null,
    GameControllerContinuationState? StartTimeoutContinuationState = null,
    GameControllerContinuationState? MaximumTimeContinuationState = null,
    SimulatorResponseObservation? InitialSetupSimulatorResponse = null)
{
    public string? InitialStopCommand => InitialStopSnapshot?.Command.ToString().ToUpperInvariant();
}

internal sealed class MatchControllerWorker(
    RefereeDriverFixture driver,
    IMatchControllerReadinessProbe readinessProbe,
    ISimulatorBallControlClient simulatorBallControl,
    IConfiguration configuration,
    RefereeReceiveService? receiver = null) : BackgroundService
{
    private readonly TimeSpan maximumMatchDuration = ReadDuration(configuration["MatchController:MaximumDuration"]);
    private readonly string resultPath = ReadResultPath(configuration["MatchController:ResultPath"]);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var result = await RunMatchAsync(stoppingToken).ConfigureAwait(false);
        if (result.Status != "passed")
            throw new InvalidOperationException(result.Failure ?? "Match controller reported a failed result.");
    }

    internal async Task<MatchControllerResult> RunMatchAsync(CancellationToken cancellationToken)
    {
        var stage = "readiness";
        RefereeCommandSnapshot? finalSnapshot = null;
        RefereeCommandSnapshot? initialStopSnapshot = null;
        string? terminationKind = null;
        MatchControllerReadiness? initialReadiness = null;
        MatchControllerReadiness? finalReadiness = null;
        GameControllerContinuationState? startTimeoutContinuationState = null;
        SimulatorBallPlacementFixture? ballPlacementFixture = null;
        var ballPlacementEvents = new List<BallPlacementFixtureEvent>();
        string? failure = null;
        try
        {
            initialReadiness = await readinessProbe.WaitForInitialReadinessAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!initialReadiness.IsComplete)
                throw new InvalidOperationException(initialReadiness.FailureReason ?? "Initial match readiness is incomplete.");

            var initialSimulator = initialReadiness.ParticipantEvidence?.Resources.SingleOrDefault(
                resource => resource.ResourceId == "simulator")
                ?? throw new InvalidOperationException("Initial readiness did not retain the current Simulator identity.");

            stage = "start";
            using (var startDeadline = new CancellationTokenSource(readinessProbe.ReadinessTimeout))
            using (var startCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken, startDeadline.Token))
            {
                try
                {
                    if (await driver.ReadAutoContinueAsync(startCancellation.Token).ConfigureAwait(false) != false)
                        throw new InvalidOperationException(
                            "The Game Controller did not confirm autoContinue=false before the initial STOP/HALT continuation.");

                    finalSnapshot = await driver.DriveToActiveAsync(startCancellation.Token, async (stopSnapshot, token) =>
                    {
                        initialStopSnapshot = stopSnapshot;
                        ballPlacementFixture = new(
                            driver, readinessProbe, simulatorBallControl, initialSimulator, stopSnapshot);
                        ballPlacementEvents = ballPlacementFixture.Events;
                        await ballPlacementFixture.SendInitialOwnHalfSetupAsync(token).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    startDeadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    startTimeoutContinuationState = await ReadStartTimeoutContinuationStateAsync()
                        .ConfigureAwait(false);
                    throw new TimeoutException(
                        "The match did not complete the initial STOP/HALT/PREPARE/NORMAL_START sequence within the configured readiness timeout.");
                }
            }

            stage = "match";
            if (!await driver.SetAutoContinueAsync(true, cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException(
                    "The Game Controller did not confirm autoContinue=true after observing NORMAL_START.");

            if (ballPlacementFixture is null)
                throw new InvalidOperationException("The initial own-half setup did not run at the verified STOP boundary.");
            ballPlacementEvents = ballPlacementFixture.Events;

            var termination = await driver.ContinueMatchUntilPostGameAsync(
                finalSnapshot, maximumMatchDuration, cancellationToken,
                ballPlacementFixture.ObserveAsync).ConfigureAwait(false);
            finalSnapshot = termination.LastSnapshot;
            terminationKind = termination.Kind.ToString();
            var maximumTimeContinuationState = termination.Kind == RefereeMatchTerminationKind.MaximumTimeReached
                ? await ReadTerminalContinuationStateAsync().ConfigureAwait(false)
                : null;

            stage = termination.Kind == RefereeMatchTerminationKind.PostGame ? "post_game" : "maximum_time";
            finalReadiness = await readinessProbe.ReadFinalReadinessAsync(finalSnapshot, cancellationToken)
                .ConfigureAwait(false);
            if (!termination.IsSuccess)
                failure = "MATCH_MAXIMUM_TIME_REACHED: execution stopped without POST_GAME.";
            else if (ballPlacementFixture.HasUnconfirmedPlacement)
                failure = "MATCH_BALL_PLACEMENT_UNCONFIRMED: a sent ball-only fixture did not receive a fresh matched GC readiness confirmation.";
            else if (!finalReadiness.IsComplete)
                failure = finalReadiness.FailureReason ?? "MATCH_FINAL_EVIDENCE_MISSING: final inputs were not ready.";

            return await PersistAsync(new(
                failure is null ? "passed" : "failed", stage, terminationKind,
                finalSnapshot.CommandCounter, finalSnapshot.PacketTimestamp, initialReadiness, finalReadiness,
                failure, ballPlacementEvents, initialStopSnapshot,
                StartTimeoutContinuationState: startTimeoutContinuationState,
                MaximumTimeContinuationState: maximumTimeContinuationState)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            return await PersistAsync(new(
                "failed", stage, terminationKind,
                finalSnapshot?.CommandCounter, finalSnapshot?.PacketTimestamp, initialReadiness, finalReadiness,
                failure, ballPlacementEvents, initialStopSnapshot,
                StartTimeoutContinuationState: startTimeoutContinuationState)).ConfigureAwait(false);
        }
    }

    private async Task<GameControllerContinuationState?> ReadStartTimeoutContinuationStateAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            return await driver.ReadContinuationStateAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Keep the primary timeout as the result; a diagnostics read must not mask it.
            return null;
        }
    }

    private async Task<GameControllerContinuationState?> ReadTerminalContinuationStateAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            return await driver.ReadContinuationStateAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Keep the maximum-time outcome primary; a diagnostic read must not mask it.
            return null;
        }
    }

    private async Task<MatchControllerResult> PersistAsync(MatchControllerResult result)
    {
        // Cancel any pending diagnostic receive instead of waiting for its network deadline.
        result = result with
        {
            InitialSetupSimulatorResponse = await simulatorBallControl.CompleteInitialSetupObservationAsync()
                .ConfigureAwait(false),
        };
        // Close and atomically publish the source window before exposing the result.
        var capture = receiver?.CompleteSourceCapture();
        if (capture is not null)
            result = result with { RefereeSourceCaptureEpoch = capture.ReceiverEpoch };
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }), CancellationToken.None).ConfigureAwait(false);
        return result;
    }

    private static TimeSpan ReadDuration(string? value) =>
        TimeSpan.TryParse(value, out var duration) && duration > TimeSpan.Zero
            ? duration
            : TimeSpan.FromMinutes(2);

    private static string ReadResultPath(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException("MatchController:ResultPath must point to the mounted results directory.")
            : Path.GetFullPath(value);
}
