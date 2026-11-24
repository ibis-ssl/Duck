using System.Globalization;
using System.Net;
using System.Net.WebSockets;
using Duck.Testing.DevelopmentUdpTransfer;
using System.Text.Json;
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
        object? imageEvidence = null;
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
        string? failure = null;
        try
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            startup.CancelAfter(StartupTimeout);
            await notifications.WaitForResourceAsync(runtimeResourceName, KnownResourceStates.Running, startup.Token);
            imageEvidence = await VerifyRunningImagesAsync(startup.Token);
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

            var control = new GameControllerWebSocketControlClient(endpoint);
            var udp = new ComposeMatchUdp(ComposeAsync);
            using var referee = new UdpRefereeCommandSource(udp);
            var observation = new ComposeRefereeObservation(referee, new GameControllerStateClient(endpoint, ConnectAsync), yellowTeam);
            var driver = new RefereeDriverFixture(referee, control, observation);
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
            await udp.SendSimulatorControlAsync(SimulatorKickoffFixture.CreateOwnHalfSetupCommand(bluePositive), startup.Token);
            await WaitForFirstKickoffOfferAsync(control, startup.Token);
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
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    using var diagnostics = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    diagnostics.CancelAfter(TimeSpan.FromSeconds(3));
                    try { continuationAtFailure = await driver.ReadContinuationStateAsync(diagnostics.Token); }
                    catch (OperationCanceledException) when (diagnostics.IsCancellationRequested) { }
                    throw new TimeoutException("Initial kickoff did not reach RUNNING with a decreasing stage clock within ten seconds.");
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
            try
            {
                await File.WriteAllTextAsync(Path.Combine(resultDirectory, "windows-match-result.json"), JsonSerializer.Serialize(new
                {
                    status = outcome, stage, failure, sourceCommit, stack.RunId, stack.ProjectName, preflight,
                    runningImages = imageEvidence, trackerTransport,
                    visionBefore = before, visionAfter = after, firstVision, firstTracker, finalTracker, firstWorldModel, finalWorldModel,
                    kickoff, inPlayProgress = progress.Evidence, termination, continuationAtFailure,
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

    private static async Task WaitForFirstKickoffOfferAsync(IGameControllerControlClient control, CancellationToken token)
    {
        while (true)
        {
            var state = await control.ReadContinuationStateAsync(token);
            if (state is { Command: nameof(GameControllerCommandKind.Stop), Stage: "NORMAL_FIRST_HALF_PRE", NextCommand: "KICKOFF" } &&
                state.ContinueActions.Any(action => action is
                    { Type: "NEXT_COMMAND", ForTeam: "UNKNOWN", State: "READY_AUTO", HasContinuationIssues: false }))
                return;
            await Task.Delay(250, token);
        }
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

    private async Task<object> VerifyRunningImagesAsync(CancellationToken token)
    {
        var observed = new List<object>();
        foreach (var service in stack.Services)
        {
            var id = await ComposeAsync(token, "ps", "--quiet", service.ServiceName);
            if (!Regex.IsMatch(id, "\\A[a-f0-9]{64}\\z", RegexOptions.CultureInvariant))
                throw new InvalidOperationException($"No single owned container for {service.ServiceName}.");
            var json = await WindowsDevelopmentMatchInputs.RunDockerAsync(stack, runner,
                ["inspect", "--type", "container", "--format", "{{json .}}", id], token);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var labels = root.GetProperty("Config").GetProperty("Labels");
            if (labels.GetProperty(DockerContainerSpec.OwnerLabel).GetString() != DockerContainerSpec.OwnerValue ||
                labels.GetProperty(DockerContainerSpec.StackLabel).GetString() != stack.StackId ||
                labels.GetProperty(DockerContainerSpec.RunLabel).GetString() != stack.RunId ||
                labels.GetProperty("com.docker.compose.project").GetString() != stack.ProjectName ||
                labels.GetProperty("com.docker.compose.service").GetString() != service.ServiceName)
                throw new InvalidOperationException($"Container ownership does not match {service.ServiceName}.");
            var image = root.GetProperty("Image").GetString();
            if (preflight.Images.SingleOrDefault(candidate => candidate.Service == service.ServiceName) is { } expected &&
                expected.Id != image)
                throw new InvalidOperationException($"The running image for {service.ServiceName} differs from preflight.");
            observed.Add(new { service = service.ServiceName, containerId = id, imageId = image });
        }
        return observed;
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

internal sealed record WindowsMatchTrackerSample(string SourceName, string Uuid, uint FrameNumber, double Timestamp);
internal sealed record WindowsMatchVisionSample(uint FrameNumber, uint CameraId, double CaptureTime);

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
