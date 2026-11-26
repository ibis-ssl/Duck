using System.Globalization;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using Duck.Testing.DevelopmentUdpTransfer;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.RefereeDriver;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tracker.RuntimeHost;

namespace Duck.Testing.AppHost;

/// <summary>Runs the existing referee fixture against the owned Windows development stack.</summary>
internal sealed class WindowsDevelopmentMatch(
    ComposeSimulationStack stack,
    EndpointReference gameController,
    string runtimeResourceName,
    EndpointReference visionHealth,
    string resultDirectory,
    string sourceCommit,
    string yellowTeam,
    WindowsMatchPreflight preflight,
    ResourceNotificationService notifications,
    IHostApplicationLifetime lifetime,
    ILogger<WindowsDevelopmentMatch> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromMinutes(5);
    private readonly IComposeCommandRunner runner = new DockerComposeCommandRunner();
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(2) };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var progress = new RefereeMatchProgress();
        var stage = "transport";
        var outcome = "transport_failed";
        var imageEvidence = new List<WindowsMatchRunningImageEvidence>();
        string? trackerTransport = null;
        RuntimeVisionHealthResponse? before = null;
        RuntimeVisionHealthResponse? after = null;
        WindowsMatchVisionSample? firstVision = null;
        string? firstWorldModel = null;
        string? finalWorldModel = null;
        WindowsMatchTrackerSample? firstTracker = null;
        WindowsMatchTrackerSample? finalTracker = null;
        RefereeCommandSnapshot? kickoff = null;
        RefereeMatchTerminationResult? termination = null;
        GameControllerContinuationState? continuationAtFailure = null;
        var startDiagnostics = new WindowsMatchStartDiagnostics();
        WindowsMatchSimulatorSetupEvidence? simulatorSetup = null;
        Task<WindowsMatchVisionObservationEvidence>? postSetupVisionTask = null;
        WindowsMatchVisionObservationEvidence? postSetupVision = null;
        string? failure = null;
        try
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            startup.CancelAfter(StartupTimeout);
            await notifications.WaitForResourceAsync(runtimeResourceName, KnownResourceStates.Running, startup.Token);
            await VerifyRunningImagesAsync(imageEvidence, startup.Token);
            trackerTransport = await ComposeAsync(startup.Token, "port", "--protocol", "udp",
                ComposeSimulationStack.OwnerServiceName,
                UdpTransfer.LinuxTrackerTransportPort.ToString(CultureInfo.InvariantCulture));
            if (!trackerTransport.StartsWith("127.0.0.1:", StringComparison.Ordinal))
                throw new InvalidOperationException("The owned Tracker ingress is not published on Windows loopback.");

            var endpoint = new UriBuilder(await gameController.GetValueAsync(startup.Token)
                ?? throw new InvalidOperationException("The Game Controller control endpoint has not been allocated."))
            {
                Scheme = "ws",
                Path = "/api/control",
            }.Uri;
            if (!endpoint.IsLoopback)
                throw new InvalidOperationException("The development match only drives the locally published Game Controller.");

            var control = new RecordingGameControllerControlClient(
                new GameControllerWebSocketControlClient(endpoint), startDiagnostics);
            var udp = new ComposeMatchUdp(ComposeAsync);
            using var referee = new UdpRefereeCommandSource(udp);
            var recordedReferee = new RecordingRefereeCommandSource(referee, startDiagnostics);
            var observation = new ComposeRefereeObservation(recordedReferee, new GameControllerStateClient(endpoint, ConnectAsync), yellowTeam);
            var driver = new RefereeDriverFixture(recordedReferee, control, observation);
            before = await WaitForVisionAsync(null, startup.Token);
            firstVision = await ReadLinuxVisionAsync(udp, startup.Token);
            firstTracker = await ReadLinuxTrackerAsync(udp, startup.Token);
            firstWorldModel = await WaitForCraneTrackerAsync(startup.Token);
            after = await WaitForVisionAsync(before, startup.Token);
            stage = "prepare_kickoff";
            var initial = await observation.WaitForNextAsync(0, startup.Token);
            if (await driver.ReadAutoContinueAsync(startup.Token) != false ||
                initial.Udp.Stage != Referee.Types.Stage.NormalFirstHalfPre ||
                initial.Udp.Command != Referee.Types.Command.Stop ||
                initial.Udp.BlueTeamOnPositiveHalf is not { } bluePositive)
                throw new InvalidOperationException("The Game Controller did not start from the owned initial match fixture.");
            var setupDatagram = SimulatorKickoffFixture.CreateOwnHalfSetupCommand(bluePositive);
            var setupStartedAt = DateTimeOffset.UtcNow;
            try
            {
                await udp.SendSimulatorControlAsync(setupDatagram, startup.Token);
                simulatorSetup = CreateSimulatorSetupEvidence(stack.RunId, initial.Udp, setupDatagram,
                    setupStartedAt, DateTimeOffset.UtcNow, "send_completed_locally", null);
            }
            catch (Exception exception)
            {
                simulatorSetup = CreateSimulatorSetupEvidence(stack.RunId, initial.Udp, setupDatagram,
                    setupStartedAt, null, exception is OperationCanceledException ? "send_canceled_outcome_unknown" : "send_failed_outcome_unknown",
                    exception.GetType().Name);
                throw;
            }
            await WaitForFirstKickoffOfferAsync(control, initial.Udp.NextCommand, startup.Token);
            logger.LogInformation("Windows match transport passed; starting the initial kickoff.");

            stage = "start";
            using (var start = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
            {
                start.CancelAfter(StartTimeout);
                try
                {
                    kickoff = await driver.DriveToActiveAsync(start.Token);
                    var cursor = 0L;
                    while (progress.Evidence is null)
                    {
                        var sample = await observation.WaitForNextAsync(cursor, start.Token);
                        cursor = sample.RequestSequence;
                        if (sample.Udp.CommandCounter != kickoff.CommandCounter)
                            throw new InvalidOperationException("The initial kickoff ended before in-play clock progress was verified.");
                        progress.Observe(sample);
                    }
                    startDiagnostics.Freeze();
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    using var diagnostics = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    diagnostics.CancelAfter(TimeSpan.FromSeconds(3));
                    try { continuationAtFailure = await driver.ReadContinuationStateAsync(diagnostics.Token); }
                    catch (OperationCanceledException) when (diagnostics.IsCancellationRequested) { }
                    throw new TimeoutException("Initial kickoff did not reach RUNNING with a decreasing stage clock within ten seconds.");
                }
                finally
                {
                    (postSetupVisionTask, postSetupVision) = SchedulePostSetupVisionObservation(after,
                        simulatorSetup?.SendCompletedAtUtc);
                }
            }

            stage = "match";
            logger.LogInformation("Windows match is in play and the stage clock is advancing.");
            using var match = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            match.CancelAfter(MaximumDuration);
            if (!await driver.SetAutoContinueAsync(true, match.Token))
                throw new InvalidOperationException("The Game Controller did not confirm autoContinue after kickoff.");
            termination = await driver.ContinueMatchUntilPostGameAsync(kickoff, MaximumDuration, stoppingToken,
                async (sample, token) =>
                {
                    // The shared continuation gate drives the normal stages. Centre the
                    // existing fixture again if the second-half preparation is blocked.
                    if (sample.Udp.Stage == Referee.Types.Stage.NormalSecondHalfPre &&
                        sample.Udp.Command is Referee.Types.Command.Stop or Referee.Types.Command.PrepareKickoffBlue or Referee.Types.Command.PrepareKickoffYellow &&
                        !secondHalfSetup && sample.Udp.BlueTeamOnPositiveHalf is { } secondHalfBluePositive)
                    {
                        await udp.SendSimulatorControlAsync(SimulatorKickoffFixture.CreateOwnHalfSetupCommand(secondHalfBluePositive), token);
                        secondHalfSetup = true;
                    }
                });
            if (!termination.IsSuccess)
                throw new TimeoutException("The match duration elapsed before POST_GAME.");

            stage = "final_transport";
            using var final = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            final.CancelAfter(TimeSpan.FromSeconds(15));
            var finalBaseline = await WaitForVisionAsync(after, final.Token);
            finalTracker = await ReadLinuxTrackerAsync(udp, final.Token);
            if (finalTracker.FrameNumber <= firstTracker.FrameNumber || finalTracker.Timestamp <= firstTracker.Timestamp)
                throw new InvalidDataException("The Linux Tracker sample did not advance across the match.");
            finalWorldModel = await WaitForCraneTrackerAsync(final.Token);
            after = await WaitForVisionAsync(finalBaseline, final.Token);
            outcome = "passed";
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            outcome = "cancelled";
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            outcome = stage switch
            {
                "start" when exception is TimeoutException => "start_timeout",
                "start" => "start_failed",
                "prepare_kickoff" => "kickoff_preparation_failed",
                "match" when exception is TimeoutException or OperationCanceledException => "maximum_duration",
                "match" => "match_failed",
                _ => "transport_failed",
            };
            logger.LogError(exception, "Windows development match failed during {Stage}.", stage);
        }
        finally
        {
            if (simulatorSetup is not null && postSetupVisionTask is null && postSetupVision is null)
                (postSetupVisionTask, postSetupVision) = SchedulePostSetupVisionObservation(after,
                    simulatorSetup?.SendCompletedAtUtc);
            if (postSetupVisionTask is not null)
                postSetupVision = await postSetupVisionTask;
            logger.LogInformation("WINDOWS_MATCH_START_DIAGNOSTICS {Evidence}",
                JsonSerializer.Serialize(new { actions = startDiagnostics.Snapshot(), simulatorSetup, postSetupVision }, JsonOptions));
            try
            {
                await File.WriteAllTextAsync(Path.Combine(resultDirectory, "windows-match-result.json"), JsonSerializer.Serialize(new
                {
                    status = outcome, stage, failure, sourceCommit, stack.RunId, stack.ProjectName, preflight,
                    runningImages = imageEvidence, trackerTransport,
                    visionBefore = before, visionAfter = after, firstVision, firstTracker, finalTracker, firstWorldModel, finalWorldModel,
                    kickoff, inPlayProgress = progress.Evidence, termination, continuationAtFailure,
                    startDiagnostics = startDiagnostics.Snapshot(), simulatorSetup, postSetupVision,
                }, JsonOptions));
            }
            catch (Exception exception)
            {
                outcome = "evidence_failed";
                logger.LogError(exception, "Could not write the Windows development match result.");
            }
            if (outcome != "passed")
                AppHostTerminationState.MarkFailed();
            lifetime.StopApplication();
        }
    }

    private bool secondHalfSetup;

    private (Task<WindowsMatchVisionObservationEvidence>? Task, WindowsMatchVisionObservationEvidence? Failure)
        SchedulePostSetupVisionObservation(RuntimeVisionHealthResponse? baseline, DateTimeOffset? setupSendCompletedAtUtc)
    {
        try
        {
            return (Task.Run(() => ObservePostSetupVisionAsync(baseline, setupSendCompletedAtUtc)), null);
        }
        catch (Exception exception)
        {
            var failedAt = DateTimeOffset.UtcNow;
            return (null, CreatePostSetupVisionFailure(failedAt, baseline, setupSendCompletedAtUtc, "observation_schedule_failed",
                exception.GetType().Name));
        }
    }

    private static async Task WaitForFirstKickoffOfferAsync(IGameControllerControlClient control,
        Referee.Types.Command? nextRefereeCommand, CancellationToken token)
    {
        while (true)
        {
            var state = await control.ReadContinuationStateAsync(token);
            if (HasFirstKickoffOffer(state, nextRefereeCommand))
                return;
            await Task.Delay(250, token);
        }
    }

    internal static WindowsMatchSimulatorSetupEvidence CreateSimulatorSetupEvidence(string runId,
        RefereeCommandSnapshot referee, byte[] datagram, DateTimeOffset startedAtUtc, DateTimeOffset? completedAtUtc,
        string outcome, string? errorType) => new(
        runId, startedAtUtc, completedAtUtc, outcome, "acknowledgement_not_provided_by_udp_send",
        "127.0.0.1:10300 (development-udp-transfer namespace)", datagram.Length,
        Convert.ToHexString(SHA256.HashData(datagram)).ToLowerInvariant(), referee.Command.ToString(),
        referee.Stage?.ToString(), referee.CommandCounter, referee.PacketTimestamp,
        referee.StageTimeLeftMicroseconds, referee.BlueScore, referee.YellowScore, errorType);

    private async Task<WindowsMatchVisionObservationEvidence> ObservePostSetupVisionAsync(
        RuntimeVisionHealthResponse? previous, DateTimeOffset? setupSendCompletedAtUtc)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            var current = await WaitForFreshVisionDecodeAsync(previous, timeout.Token);
            return CreatePostSetupVisionEvidence(startedAtUtc, current, previous, setupSendCompletedAtUtc);
        }
        catch (Exception exception)
        {
            return CreatePostSetupVisionFailure(startedAtUtc, previous, setupSendCompletedAtUtc,
                exception is OperationCanceledException ? "observation_timeout_or_canceled" : "observation_failed",
                exception.GetType().Name);
        }
    }

    private async Task<RuntimeVisionHealthResponse> WaitForFreshVisionDecodeAsync(
        RuntimeVisionHealthResponse? previous, CancellationToken token)
    {
        var uri = new UriBuilder(await visionHealth.GetValueAsync(token)
            ?? throw new InvalidOperationException("The RuntimeHost diagnostic endpoint has not been allocated."))
        {
            Path = "/health/vision",
        }.Uri;
        if (!uri.IsLoopback)
            throw new InvalidOperationException("The RuntimeHost diagnostics must be on loopback.");
        while (true)
        {
            try
            {
                using var response = await http.GetAsync(uri, token);
                if (response.IsSuccessStatusCode)
                {
                    await using var body = await response.Content.ReadAsStreamAsync(token);
                    var current = await JsonSerializer.DeserializeAsync<RuntimeVisionHealthResponse>(body, JsonOptions, token);
                    if (current is not null && IsFreshVisionDecode(current, previous))
                        return current;
                }
            }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            await Task.Delay(250, token);
        }
    }

    internal static bool IsFreshVisionDecode(RuntimeVisionHealthResponse current, RuntimeVisionHealthResponse? previous) =>
        current.Diagnostics.SuccessfullyDecodedCount > (previous?.Diagnostics.SuccessfullyDecodedCount ?? 0) &&
        current.Diagnostics.DecodeStatus == Duck.Testing.Diagnostics.DiagnosticDecodeStatus.Succeeded &&
        current.Diagnostics.RemoteEndpoint is { } remoteEndpoint &&
        IPEndPoint.TryParse(remoteEndpoint, out var source) && source is not null &&
        IPAddress.IsLoopback(source.Address) &&
        current.Diagnostics.DecodeAgeUpperBound is { } age && age >= TimeSpan.Zero &&
        age <= Duck.Testing.Diagnostics.ServiceReceiveDiagnostics.FreshnessWindow &&
        current.Diagnostics.DecodeFailureCount == (previous?.Diagnostics.DecodeFailureCount ?? 0);

    internal static WindowsMatchVisionObservationEvidence CreatePostSetupVisionEvidence(DateTimeOffset startedAtUtc,
        RuntimeVisionHealthResponse current, RuntimeVisionHealthResponse? previous,
        DateTimeOffset? setupSendCompletedAtUtc)
    {
        var lastReceivedAtUtc = current.Diagnostics.LastReceivedAt;
        var timingStatus = setupSendCompletedAtUtc is not { } sendCompletedAtUtc ||
            lastReceivedAtUtc is not { } receivedAtUtc
                ? "unconfirmed_timing"
                : receivedAtUtc > sendCompletedAtUtc
                    ? "received_after_setup_send_completed"
                    : "received_before_or_at_setup_send_completed";
        return new(startedAtUtc, DateTimeOffset.UtcNow, "vision_decode_observed", timingStatus,
            setupSendCompletedAtUtc,
            previous?.Diagnostics.SuccessfullyDecodedCount ?? 0, current.Diagnostics.SuccessfullyDecodedCount,
            lastReceivedAtUtc, current.Diagnostics.RemoteEndpoint, null);
    }

    internal static WindowsMatchVisionObservationEvidence CreatePostSetupVisionFailure(DateTimeOffset startedAtUtc,
        RuntimeVisionHealthResponse? previous, DateTimeOffset? setupSendCompletedAtUtc, string outcome,
        string errorType) => new(
        startedAtUtc, DateTimeOffset.UtcNow, outcome, "unconfirmed_timing", setupSendCompletedAtUtc,
        previous?.Diagnostics.SuccessfullyDecodedCount, null, null, null, errorType);

    internal static bool HasFirstKickoffOffer(GameControllerContinuationState? state, Referee.Types.Command? nextRefereeCommand)
    {
        var nextTeam = nextRefereeCommand switch
        {
            Referee.Types.Command.PrepareKickoffBlue => "BLUE",
            Referee.Types.Command.PrepareKickoffYellow => "YELLOW",
            _ => null,
        };
        return nextTeam is not null &&
            state is { Command: nameof(GameControllerCommandKind.Stop), Phase: nameof(GameControllerMatchPhase.Stop),
                Stage: "NORMAL_FIRST_HALF_PRE", NextCommand: "KICKOFF" } &&
            state.NextCommandForTeam == nextTeam &&
            state.ContinueActions.Any(action =>
                action is { Type: "NEXT_COMMAND", State: "READY_AUTO", HasContinuationIssues: false } &&
                action.ForTeam == nextTeam);
    }

    private static async Task<WindowsMatchTrackerSample> ReadLinuxTrackerAsync(ComposeMatchUdp udp, CancellationToken token)
    {
        var datagram = await udp.ObserveAsync("tracker", token);
        var tracker = TrackerWrapperPacket.Parser.ParseFrom(datagram);
        if (tracker.SourceName != "ibis" || tracker.Uuid != "ibis" || tracker.TrackedFrame is null)
            throw new InvalidDataException("The owned Linux namespace did not receive the standard Duck Tracker frame.");
        return new(tracker.SourceName, tracker.Uuid, tracker.TrackedFrame.FrameNumber, tracker.TrackedFrame.Timestamp);
    }

    private static async Task<WindowsMatchVisionSample> ReadLinuxVisionAsync(ComposeMatchUdp udp, CancellationToken token)
    {
        while (true)
        {
            var datagram = await udp.ObserveAsync("vision", token);
            if (SSL_WrapperPacket.Parser.ParseFrom(datagram).Detection is { } detection)
                return new(detection.FrameNumber, detection.CameraId, detection.TCapture);
        }
    }

    private async Task VerifyRunningImagesAsync(List<WindowsMatchRunningImageEvidence> evidence, CancellationToken token)
    {
        foreach (var service in stack.Services)
        {
            var expected = preflight.Images.SingleOrDefault(candidate => candidate.Service == service.ServiceName);
            var item = WindowsMatchRunningImageEvidence.Begin(service.ServiceName, expected, service.Image is not null);
            var evidenceIndex = StoreImageEvidence(evidence, -1, item, logger);
            try
            {
                if (!HasRequiredPreflightImage(service.Image is not null, expected))
                {
                    item = item.WithFailure("preflight_image_id_missing",
                        $"The fixed image preflight ID is missing for {service.ServiceName}.");
                    StoreImageEvidence(evidence, evidenceIndex, item, logger);
                    EnsureImageVerificationSucceeded(item, service.ServiceName);
                }

                var id = await ComposeAsync(token, "ps", "--quiet", service.ServiceName);
                item = item.WithContainerId(id);
                StoreImageEvidence(evidence, evidenceIndex, item, logger);
                if (!Regex.IsMatch(id, "\\A[a-f0-9]{64}\\z", RegexOptions.CultureInvariant))
                    throw new InvalidOperationException($"No single owned container for {service.ServiceName}.");

                var json = await WindowsDevelopmentMatchInputs.RunDockerAsync(stack, runner,
                    ["inspect", "--type", "container", "--format", "{{json .}}", id], token);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                item = CaptureRuntimeImageAfterOwnershipCheck(item, HasOwnedContainerLabels(root, service.ServiceName), () =>
                    (ReadInspectString(root, "Image"), ReadNestedInspectString(root, "Config", "Image")));
                StoreImageEvidence(evidence, evidenceIndex, item, logger);
                EnsureImageVerificationSucceeded(item, service.ServiceName);

                item = item with { VerificationStatus = "verified" };
                StoreImageEvidence(evidence, evidenceIndex, item, logger);
            }
            catch (Exception exception)
            {
                item = evidence[evidenceIndex];
                if (item.VerificationStatus is "pending" or "inspecting" or "captured")
                {
                    item = item.WithFailure("failed", exception.Message);
                    StoreImageEvidence(evidence, evidenceIndex, item, logger);
                }
                throw;
            }
        }
    }

    internal static bool HasRequiredPreflightImage(bool required, WindowsMatchImage? expected) =>
        !required || expected is not null && !string.IsNullOrWhiteSpace(expected.DefaultImageId) &&
        !string.IsNullOrWhiteSpace(expected.PlatformImageId);

    internal static void EnsureImageVerificationSucceeded(WindowsMatchRunningImageEvidence evidence, string serviceName)
    {
        switch (evidence.VerificationStatus)
        {
            case "captured":
            case "verified":
                return;
            case "ownership_mismatch":
                throw new InvalidOperationException($"Container ownership does not match {serviceName}.");
            case "image_mismatch":
                throw new InvalidOperationException($"The running image for {serviceName} differs from preflight.");
            case "runtime_image_missing":
                throw new InvalidOperationException($"The running container image is missing for {serviceName}.");
            case "preflight_image_id_missing":
                throw new InvalidOperationException($"The preflight image ID for {serviceName} is missing.");
            default:
                throw new InvalidOperationException($"Image verification did not complete for {serviceName}.");
        }
    }

    internal static WindowsMatchRunningImageEvidence CaptureRuntimeImageAfterOwnershipCheck(
        WindowsMatchRunningImageEvidence evidence,
        bool ownershipMatches,
        Func<(string? Image, string? ConfigImage)> readRuntimeImage)
    {
        if (!ownershipMatches)
            return evidence.WithOwnershipFailure("Container ownership does not match the current run.");

        var (image, configImage) = readRuntimeImage();
        return evidence.WithRuntimeImage(image, configImage);
    }

    private bool HasOwnedContainerLabels(JsonElement root, string serviceName)
    {
        if (!root.TryGetProperty("Config", out var config) || config.ValueKind != JsonValueKind.Object ||
            !config.TryGetProperty("Labels", out var labels) || labels.ValueKind != JsonValueKind.Object)
            return false;
        return ReadLabel(labels, DockerContainerSpec.OwnerLabel) == DockerContainerSpec.OwnerValue &&
            ReadLabel(labels, DockerContainerSpec.StackLabel) == stack.StackId &&
            ReadLabel(labels, DockerContainerSpec.RunLabel) == stack.RunId &&
            ReadLabel(labels, "com.docker.compose.project") == stack.ProjectName &&
            ReadLabel(labels, "com.docker.compose.service") == serviceName;
    }

    private static string? ReadLabel(JsonElement labels, string name) =>
        labels.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? ReadInspectString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? ReadNestedInspectString(JsonElement root, string parentName, string name) =>
        root.TryGetProperty(parentName, out var parent) && parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    internal static int StoreImageEvidence(List<WindowsMatchRunningImageEvidence> evidence, int index,
        WindowsMatchRunningImageEvidence item, ILogger<WindowsDevelopmentMatch> logger)
    {
        if (index < 0)
        {
            index = evidence.Count;
            evidence.Add(item);
        }
        else
        {
            evidence[index] = item;
        }
        var serialized = JsonSerializer.Serialize(item, JsonOptions);
        if (item.VerificationStatus is not ("pending" or "inspecting" or "captured" or "verified"))
            logger.LogWarning("WINDOWS_MATCH_IMAGE_EVIDENCE {Evidence}", serialized);
        else
            logger.LogInformation("WINDOWS_MATCH_IMAGE_EVIDENCE {Evidence}", serialized);
        return index;
    }

    private async Task<RuntimeVisionHealthResponse> WaitForVisionAsync(RuntimeVisionHealthResponse? previous,
        CancellationToken token)
    {
        var uri = new UriBuilder(await visionHealth.GetValueAsync(token)
            ?? throw new InvalidOperationException("The RuntimeHost diagnostic endpoint has not been allocated."))
        {
            Path = "/health/vision",
        }.Uri;
        if (!uri.IsLoopback)
            throw new InvalidOperationException("The RuntimeHost diagnostics must be on loopback.");
        while (true)
        {
            try
            {
                using var response = await http.GetAsync(uri, token);
                if (response.IsSuccessStatusCode)
                {
                    await using var body = await response.Content.ReadAsStreamAsync(token);
                    var current = await JsonSerializer.DeserializeAsync<RuntimeVisionHealthResponse>(body, JsonOptions, token);
                    if (current is not null && IsFreshTransport(current, previous))
                        return current;
                }
            }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            await Task.Delay(250, token);
        }
    }

    internal static bool IsFreshTransport(RuntimeVisionHealthResponse current, RuntimeVisionHealthResponse? previous) =>
        current.Diagnostics.SuccessfullyDecodedCount > (previous?.Diagnostics.SuccessfullyDecodedCount ?? 0) &&
        current.Diagnostics.DecodeStatus == Duck.Testing.Diagnostics.DiagnosticDecodeStatus.Succeeded &&
        IPEndPoint.TryParse(current.Diagnostics.RemoteEndpoint, out var source) && IPAddress.IsLoopback(source.Address) &&
        current.Diagnostics.DecodeAgeUpperBound is { } age && age >= TimeSpan.Zero &&
        age <= Duck.Testing.Diagnostics.ServiceReceiveDiagnostics.FreshnessWindow &&
        current.Diagnostics.DecodeFailureCount == (previous?.Diagnostics.DecodeFailureCount ?? 0) &&
        current.TrackerOutput is { ActiveProfileName: "sim" } tracker &&
        tracker.PublishSuccessCount > (previous?.TrackerOutput?.PublishSuccessCount ?? 0) &&
        tracker.PublishFailureCount == (previous?.TrackerOutput?.PublishFailureCount ?? 0);

    private async Task<string> WaitForCraneTrackerAsync(CancellationToken token)
    {
        while (true)
        {
            var result = await runner.RunAsync(stack.DockerExecutable,
                stack.CreateComposeArguments("exec", "-T", "crane", "timeout", "8s", "bash", "-lc",
                    "source /root/ibis_ws/install/setup.bash && exec ros2 topic echo --qos-durability volatile --once /world_model"), token);
            if (result.ExitCode == 0 && HasCraneTracker(result.StandardOutput))
                return result.StandardOutput;
            await Task.Delay(250, token);
        }
    }

    internal static bool HasCraneTracker(string output) =>
        Regex.IsMatch(output, @"(?im)^\s*tracker_detected:\s*true\s*$") &&
        Regex.IsMatch(output, @"(?im)^\s*available_tracker:\s*true\s*$");

    private async Task<string> ComposeAsync(CancellationToken token, params string[] arguments)
    {
        var result = await runner.RunAsync(stack.DockerExecutable, stack.CreateComposeArguments(arguments), token);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Owned Compose {arguments[0]} failed ({result.ExitCode}): {result.StandardError.Trim()}");
        return result.StandardOutput.Trim();
    }

    private static async Task<WebSocket> ConnectAsync(Uri uri, CancellationToken token)
    {
        var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(uri, token);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public override void Dispose()
    {
        http.Dispose();
        base.Dispose();
    }
}

internal sealed record WindowsMatchRunningImageEvidence(
    string Service,
    string? ContainerId,
    string? PreflightReference,
    string? PreflightImageId,
    string? PlatformImageId,
    string? SelectedPlatform,
    bool RequiresPreflightImage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RuntimeImage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RuntimeConfigImage,
    string VerificationStatus,
    string? Failure)
{
    internal static WindowsMatchRunningImageEvidence Begin(string service, WindowsMatchImage? expected,
        bool requiresPreflightImage = false) => new(
        service,
        null,
        expected?.Reference,
        expected?.DefaultImageId,
        expected?.PlatformImageId,
        expected?.Platform,
        requiresPreflightImage,
        null,
        null,
        "pending",
        null);

    internal WindowsMatchRunningImageEvidence WithContainerId(string id) => this with { ContainerId = id };

    internal WindowsMatchRunningImageEvidence WithRuntimeImage(string? image, string? configImage)
    {
        var status = string.IsNullOrWhiteSpace(image)
            ? "runtime_image_missing"
            : RequiresPreflightImage && string.IsNullOrWhiteSpace(PreflightImageId)
                ? "preflight_image_id_missing"
                : PreflightImageId is not null && PreflightImageId != image
                    ? "image_mismatch"
                    : "captured";
        return this with
        {
            RuntimeImage = image,
            RuntimeConfigImage = configImage,
            VerificationStatus = status,
            Failure = status switch
            {
                "runtime_image_missing" => "Runtime Image is missing from container inspect.",
                "preflight_image_id_missing" => "The fixed service preflight image ID is missing.",
                "image_mismatch" => "Runtime Image does not match the preflight image ID.",
                _ => null,
            },
        };
    }

    internal WindowsMatchRunningImageEvidence WithOwnershipFailure(string failure) => this with
    {
        VerificationStatus = "ownership_mismatch",
        Failure = failure,
    };

    internal WindowsMatchRunningImageEvidence WithFailure(string status, string failure) => this with
    {
        VerificationStatus = status,
        Failure = failure,
    };
}

internal sealed record WindowsMatchTrackerSample(string SourceName, string Uuid, uint FrameNumber, double Timestamp);
internal sealed record WindowsMatchVisionSample(uint FrameNumber, uint CameraId, double CaptureTime);
internal sealed record WindowsMatchSimulatorSetupEvidence(
    string RunId, DateTimeOffset SendStartedAtUtc, DateTimeOffset? SendCompletedAtUtc, string LocalSendOutcome,
    string Acknowledgement, string Destination, int DatagramLength, string DatagramSha256,
    string RefereeCommandBeforeSend, string? RefereeStageBeforeSend, uint RefereeCommandCounterBeforeSend,
    ulong? RefereePacketTimestampBeforeSend, long? RefereeStageTimeLeftMicrosecondsBeforeSend,
    uint BlueScoreBeforeSend, uint YellowScoreBeforeSend, string? ErrorType);
internal sealed record WindowsMatchVisionObservationEvidence(
    DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc, string Outcome, string SetupTimingStatus,
    DateTimeOffset? SimulatorSetupSendCompletedAtUtc, long? BaselineDecodedCount, long? ObservedDecodedCount,
    DateTimeOffset? LastReceivedAtUtc, string? RemoteEndpoint, string? ErrorType);
internal sealed record WindowsMatchTimedControlState(GameControllerContinuationState? State, DateTimeOffset ObservedAtUtc);
internal sealed record WindowsMatchTimedRefereeSnapshot(RefereeCommandSnapshot Snapshot, DateTimeOffset ObservedAtUtc);
internal sealed record WindowsMatchControlActionEvidence(
    string Action, DateTimeOffset SendStartedAtUtc, DateTimeOffset? SendCompletedAtUtc, string LocalSendOutcome,
    string Acknowledgement, WindowsMatchTimedControlState? LatestControllerStateBeforeSend,
    WindowsMatchTimedRefereeSnapshot? LatestRefereeBeforeSend, string? ErrorType,
    WindowsMatchTimedControlState? LatestControllerStateAfterSend = null,
    WindowsMatchTimedRefereeSnapshot? LatestRefereeAfterSend = null);

internal sealed class WindowsMatchStartDiagnostics
{
    private readonly object gate = new();
    private readonly List<WindowsMatchControlActionEvidence> actions = [];
    private WindowsMatchTimedControlState? latestController;
    private WindowsMatchTimedRefereeSnapshot? latestReferee;
    private bool isFrozen;

    internal void RecordControllerState(GameControllerContinuationState? state)
    {
        lock (gate)
        {
            if (isFrozen) return;
            latestController = new(state, DateTimeOffset.UtcNow);
            if (actions.Count > 0)
                actions[^1] = actions[^1] with { LatestControllerStateAfterSend = latestController };
        }
    }

    internal void RecordReferee(RefereeCommandSnapshot snapshot)
    {
        lock (gate)
        {
            if (isFrozen) return;
            latestReferee = new(snapshot, DateTimeOffset.UtcNow);
            if (actions.Count > 0)
                actions[^1] = actions[^1] with { LatestRefereeAfterSend = latestReferee };
        }
    }

    internal WindowsMatchControlActionEvidence? BeginAction(GameControllerContinueAction action)
    {
        lock (gate)
        {
            if (isFrozen) return null;
            return new(action.ToString(), DateTimeOffset.UtcNow, null, "send_in_progress",
                "acknowledgement_not_provided_by_control_send", latestController, latestReferee, null, null, null);
        }
    }

    internal void Freeze()
    {
        lock (gate) isFrozen = true;
    }

    internal void CompleteAction(WindowsMatchControlActionEvidence evidence, string outcome, string? errorType = null)
    {
        lock (gate)
        {
            if (isFrozen) return;
            actions.Add(evidence with
            {
                SendCompletedAtUtc = outcome == "send_completed_locally" ? DateTimeOffset.UtcNow : null,
                LocalSendOutcome = outcome,
                ErrorType = errorType,
            });
        }
    }

    internal IReadOnlyList<WindowsMatchControlActionEvidence> Snapshot()
    {
        lock (gate) return actions.ToArray();
    }
}

internal sealed class RecordingGameControllerControlClient(
    IGameControllerControlClient inner, WindowsMatchStartDiagnostics diagnostics) : IGameControllerControlClient
{
    public Task<bool?> ReadAutoContinueAsync(CancellationToken cancellationToken) =>
        inner.ReadAutoContinueAsync(cancellationToken);

    public Task<bool> SetAutoContinueAsync(bool enabled, CancellationToken cancellationToken) =>
        inner.SetAutoContinueAsync(enabled, cancellationToken);

    public async Task<GameControllerContinuationState?> ReadContinuationStateAsync(CancellationToken cancellationToken)
    {
        var state = await inner.ReadContinuationStateAsync(cancellationToken);
        diagnostics.RecordControllerState(state);
        return state;
    }

    public async Task SendContinueActionAsync(GameControllerContinueAction action, CancellationToken cancellationToken)
    {
        var evidence = diagnostics.BeginAction(action);
        if (evidence is null)
        {
            await inner.SendContinueActionAsync(action, cancellationToken);
            return;
        }
        try
        {
            await inner.SendContinueActionAsync(action, cancellationToken);
            diagnostics.CompleteAction(evidence, "send_completed_locally");
        }
        catch (OperationCanceledException exception)
        {
            diagnostics.CompleteAction(evidence, "send_canceled_outcome_unknown", exception.GetType().Name);
            throw;
        }
        catch (Exception exception)
        {
            diagnostics.CompleteAction(evidence, "send_failed_outcome_unknown", exception.GetType().Name);
            throw;
        }
    }
}

internal sealed class RecordingRefereeCommandSource(
    IRefereeCommandSource inner, WindowsMatchStartDiagnostics diagnostics) : IRefereeCommandSource
{
    public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(uint? previousCommandCounter,
        CancellationToken cancellationToken)
    {
        var snapshot = await inner.WaitForChangeAsync(previousCommandCounter, cancellationToken);
        diagnostics.RecordReferee(snapshot);
        return snapshot;
    }
}

internal sealed class ComposeMatchUdp(Func<CancellationToken, string[], Task<string>> compose)
    : IRefereePacketReceiver
{
    private static readonly string[] TransferExecutable =
        ["exec", "-T", "development-udp-transfer", "dotnet", "/app/Duck.Testing.DevelopmentUdpTransfer.dll"];

    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) =>
        await ObserveAsync("referee", cancellationToken);

    internal async Task<byte[]> ObserveAsync(string stream, CancellationToken token) =>
        Convert.FromBase64String(await compose(token, [.. TransferExecutable, "--observe", stream]));

    internal Task SendSimulatorControlAsync(byte[] datagram, CancellationToken token) =>
        compose(token, [.. TransferExecutable, "--send-simulator-control", Convert.ToBase64String(datagram)]);
}

/// <summary>Matches the owned controller API against fresh, actual referee packets from its shared Linux namespace.</summary>
internal sealed class ComposeRefereeObservation(
    IRefereeCommandSource referee,
    GameControllerStateClient api,
    string yellowTeam) : IRefereeLifecycleObservationSource
{
    private long sequence;
    private ulong? previousPacketTimestamp;

    public async Task<RefereeLifecycleObservation> WaitForNextAsync(
        long previousRequestSequence, CancellationToken cancellationToken)
    {
        while (true)
        {
            var udp = await referee.WaitForChangeAsync(null, cancellationToken);
            var state = await api.ReadMatchStateAsync(cancellationToken);
            if (udp.YellowTeamName == yellowTeam && udp.PacketTimestamp is { } timestamp &&
                (previousPacketTimestamp is not { } previous || timestamp > previous) &&
                state is not null && GameControllerStateMapping.TryParseStage(state.Stage, out var stage) &&
                stage == udp.Stage && GameControllerStateMapping.MatchesUdpCommand(state, udp) &&
                state.BlueScore == udp.BlueScore && state.YellowScore == udp.YellowScore)
            {
                previousPacketTimestamp = timestamp;
                sequence = checked(Math.Max(sequence, previousRequestSequence) + 1);
                return new(sequence, udp, state.Command.ToString(), state.Phase.ToString(), state.Stage,
                    state.StageTimeLeft, state);
            }

            await Task.Delay(100, cancellationToken);
        }
    }
}
