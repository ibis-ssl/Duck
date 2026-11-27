using System.Text.Json;
using System.Security.Cryptography;
using Aspire.Hosting;
using Duck.Testing.AppHost;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Tracker.RuntimeHost;

namespace Tracker.Tests;

public sealed class WindowsDevelopmentMatchTests
{
    [Fact]
    public void CustomNormalHalfDurationKeepsOvertimeAndOriginalFixture()
    {
        var output = Directory.CreateTempSubdirectory("duck-windows-match-");
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "match");
        var originalPath = Path.Combine(root, "ssl-game-controller-match.yaml");
        var original = File.ReadAllBytes(originalPath);
        try
        {
            WindowsDevelopmentMatchInputs.PrepareFixture(output.FullName, root, "crane", 60);
            var actual = File.ReadAllText(Path.Combine(output.FullName, "ssl-game-controller.yaml"));
            Assert.Contains("half-duration: 60s", actual);
            Assert.Contains("half-duration: 30s", actual);
            Assert.DoesNotContain("half-duration: 45s", actual);
            Assert.Equal(original, File.ReadAllBytes(originalPath));
            Assert.Throws<ArgumentOutOfRangeException>(() => WindowsDevelopmentMatchInputs.PrepareFixture(output.FullName, root, "crane", 149));
        }
        finally { output.Delete(recursive: true); }
    }

    private const string SimulatorReference = "ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd";
    private const string SimulatorDefaultImageId = "sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79";
    private const string SimulatorPlatformImageId = "sha256:9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554";

    [Fact]
    public async Task ImagePreflightKeepsDefaultImageIdSeparateFromSelectedPlatformImageId()
    {
        var temporary = Directory.CreateTempSubdirectory("duck-image-preflight-");
        try
        {
            var dockerfileDirectory = Path.Combine(temporary.FullName, "transfer");
            Directory.CreateDirectory(dockerfileDirectory);
            await File.WriteAllTextAsync(Path.Combine(dockerfileDirectory, "Dockerfile"),
                "FROM mcr.microsoft.com/dotnet/runtime@sha256:" + new string('a', 64) + "\n");
            var builder = DistributedApplication.CreateBuilder([]);
            var stack = new ComposeSimulationStack("image-preflight", Path.Combine(temporary.FullName, "compose.yml"),
                "docker", WindowsDevelopmentMatchInputs.DockerContext);
            stack.AddService(builder, "cm4-sim", ComposeSimulationStack.OwnerServiceName,
                "ghcr.io/ibis-ssl/orion-cm4-sim:fixed", isOwner: true);
            stack.AddService(builder, "simulator", "simulator", SimulatorReference);
            stack.AddService(builder, "game-controller", "game-controller", "robocupssl/ssl-game-controller:3.20.3");
            stack.AddService(builder, "crane", "crane", "ghcr.io/ibis-ssl/crane:fixed");
            stack.AddService(builder, "development-udp-transfer", "development-udp-transfer", image: null,
                buildContext: dockerfileDirectory);
            var runner = new ImageInspectionRunner(SimulatorReference, SimulatorDefaultImageId, SimulatorPlatformImageId);

            var preflight = await WindowsDevelopmentMatchInputs.ReadImagesAsync(stack, "engine-id", runner,
                CancellationToken.None);

            var simulator = Assert.Single(preflight.Images, image => image.Service == "simulator");
            Assert.Equal(SimulatorDefaultImageId, simulator.DefaultImageId);
            Assert.Equal(SimulatorPlatformImageId, simulator.PlatformImageId);
            Assert.Equal("linux/amd64", simulator.Platform);
            var simulatorCalls = runner.Calls.Where(call => call[^1] == SimulatorReference).ToArray();
            Assert.Equal(2, simulatorCalls.Length);
            Assert.Contains(simulatorCalls, call => !call.Contains("--platform", StringComparer.Ordinal));
            Assert.Contains(simulatorCalls, call => call.Contains("--platform", StringComparer.Ordinal));

            var observedRuntime = WindowsMatchRunningImageEvidence.Begin("simulator", simulator, requiresPreflightImage: true)
                .WithRuntimeImage(SimulatorDefaultImageId, SimulatorReference);
            Assert.Equal("captured", observedRuntime.VerificationStatus);
            WindowsDevelopmentMatch.EnsureImageVerificationSucceeded(observedRuntime, "simulator");
            var differentImage = WindowsMatchRunningImageEvidence.Begin("simulator", simulator, requiresPreflightImage: true)
                .WithRuntimeImage($"sha256:{new string('9', 64)}", SimulatorReference);
            Assert.Equal("image_mismatch", differentImage.VerificationStatus);
            Assert.Throws<InvalidOperationException>(() =>
                WindowsDevelopmentMatch.EnsureImageVerificationSucceeded(differentImage, "simulator"));
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }

    [Fact]
    public void ProgressRequiresRunningAndALaterFreshRefereePacketWithALowerClock()
    {
        var progress = new RefereeMatchProgress();
        progress.Observe(Sample(100, 45, "Kickoff"));
        progress.Observe(Sample(101, 44));
        progress.Observe(Sample(101, 43)); // Same referee datagram.
        progress.Observe(Sample(102, 44)); // Same clock.
        progress.Observe(Sample(103, 43, stage: "NORMAL_SECOND_HALF"));
        Assert.Null(progress.Evidence);

        progress.Observe(Sample(104, 43));
        Assert.Equal(TimeSpan.FromSeconds(44), progress.Evidence!.First.StageTimeLeft);
        Assert.Equal(TimeSpan.FromSeconds(43), progress.Evidence.Later.StageTimeLeft);
        Assert.Equal(104ul, progress.Evidence.Later.PacketTimestamp);
    }

    [Theory]
    [InlineData("YELLOW", "YELLOW", "READY_AUTO", false, Referee.Types.Command.PrepareKickoffBlue)]
    [InlineData("YELLOW", "BLUE", "READY_AUTO", false, Referee.Types.Command.PrepareKickoffYellow)]
    [InlineData("BLUE", "YELLOW", "READY_AUTO", false, Referee.Types.Command.PrepareKickoffYellow)]
    [InlineData("YELLOW", "UNKNOWN", "READY_AUTO", false, Referee.Types.Command.PrepareKickoffYellow)]
    [InlineData("YELLOW", "YELLOW", "BLOCKED", false, Referee.Types.Command.PrepareKickoffYellow)]
    [InlineData("YELLOW", "YELLOW", "READY_AUTO", true, Referee.Types.Command.PrepareKickoffYellow)]
    [InlineData("YELLOW", "YELLOW", "READY_AUTO", false, null)]
    public void FirstKickoffRejectsMissingOrMismatchedTeamAndBlockedActions(
        string nextTeam, string actionTeam, string actionState, bool hasIssues, Referee.Types.Command? udpNext)
    {
        var offer = new GameControllerContinuationState("Stop", "Stop", "NORMAL_FIRST_HALF_PRE", TimeSpan.FromSeconds(45),
            [new("NEXT_COMMAND", actionTeam, actionState, hasIssues)], "KICKOFF", nextTeam);
        Assert.False(WindowsDevelopmentMatch.HasFirstKickoffOffer(offer, udpNext));
    }

    [Fact]
    public void TransportUsesFreshDetectionAndTrackerDeltasRatherThanHostReadiness()
    {
        var initial = Health(3, 2);
        Assert.True(WindowsDevelopmentMatch.IsFreshTransport(initial, null));
        Assert.False(WindowsDevelopmentMatch.IsFreshTransport(Health(3, 4), initial));
        Assert.False(WindowsDevelopmentMatch.IsFreshTransport(Health(6, 2), initial));
        Assert.False(WindowsDevelopmentMatch.IsFreshTransport(Health(6, 4, ageSeconds: 6), initial));
        Assert.True(WindowsDevelopmentMatch.IsFreshTransport(Health(6, 4), initial));
    }

    [Theory]
    [InlineData("ball_info:\n  tracker_detected: true\nrobot_info_ours:\n- id: 0\n  available_tracker: true\n", true)]
    [InlineData("ball_info:\n  tracker_detected: true\nrobot_info_ours:\n- id: 0\n  available_tracker: false\n", false)]
    [InlineData("ball_info:\n  tracker_detected: false\nrobot_info_ours:\n- id: 0\n  available_tracker: true\n", false)]
    public void CraneProbeRequiresBothTrackerFlags(string output, bool expected) =>
        Assert.Equal(expected, WindowsDevelopmentMatch.HasCraneTracker(output));

    [Fact]
    public void RunningImageEvidencePreservesSuccessAndMismatchValuesInJsonAndLogs()
    {
        var expected = new WindowsMatchImage("simulator", "registry/simulator:fixed", "sha256:preflight",
            "linux/amd64", ["registry/simulator@sha256:repo"], "sha256:platform", "sha256:platform-image");
        var evidence = new List<WindowsMatchRunningImageEvidence>();
        var logger = new RecordingLogger<WindowsDevelopmentMatch>();

        var verified = WindowsMatchRunningImageEvidence.Begin("simulator", expected, requiresPreflightImage: true)
            .WithContainerId(new string('a', 64))
            .WithRuntimeImage("sha256:preflight", "registry/simulator:unexpected-tag") with
            {
                VerificationStatus = "verified",
            };
        WindowsDevelopmentMatch.StoreImageEvidence(evidence, -1, verified, logger);
        WindowsDevelopmentMatch.EnsureImageVerificationSucceeded(verified, "simulator");
        var mismatch = WindowsMatchRunningImageEvidence.Begin("simulator", expected, requiresPreflightImage: true)
            .WithContainerId(new string('b', 64))
            .WithRuntimeImage("sha256:runtime", "registry/simulator:fixed");
        WindowsDevelopmentMatch.StoreImageEvidence(evidence, -1, mismatch, logger);
        Assert.Contains("differs from preflight", Assert.Throws<InvalidOperationException>(() =>
            WindowsDevelopmentMatch.EnsureImageVerificationSucceeded(mismatch, "simulator")).Message, StringComparison.Ordinal);

        var serialized = JsonSerializer.Serialize(new { runningImages = evidence }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(serialized);
        var entries = document.RootElement.GetProperty("runningImages").EnumerateArray().ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal("verified", entries[0].GetProperty("verificationStatus").GetString());
        Assert.Equal("registry/simulator:fixed", entries[0].GetProperty("preflightReference").GetString());
        Assert.Equal("sha256:preflight", entries[0].GetProperty("preflightImageId").GetString());
        Assert.Equal("sha256:platform-image", entries[0].GetProperty("platformImageId").GetString());
        Assert.Equal("registry/simulator:unexpected-tag", entries[0].GetProperty("runtimeConfigImage").GetString());
        Assert.Equal("linux/amd64", entries[0].GetProperty("selectedPlatform").GetString());
        Assert.Equal(new string('a', 64), entries[0].GetProperty("containerId").GetString());
        Assert.Equal("image_mismatch", entries[1].GetProperty("verificationStatus").GetString());
        Assert.Equal("sha256:runtime", entries[1].GetProperty("runtimeImage").GetString());
        Assert.Equal("registry/simulator:fixed", entries[1].GetProperty("runtimeConfigImage").GetString());
        Assert.Equal(new string('b', 64), entries[1].GetProperty("containerId").GetString());
        Assert.Contains(logger.Entries, entry => entry.Contains("sha256:runtime", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Contains("registry/simulator:fixed", StringComparison.Ordinal));
    }

    [Fact]
    public void PartialRunningImageFailureRetainsPreflightAndContainerEvidenceInJsonAndLogs()
    {
        var expected = new WindowsMatchImage("simulator", "registry/simulator:fixed", "sha256:preflight",
            "linux/amd64", ["registry/simulator@sha256:repo"], "sha256:platform", "sha256:platform-image");
        var evidence = new List<WindowsMatchRunningImageEvidence>();
        var logger = new RecordingLogger<WindowsDevelopmentMatch>();
        var partial = WindowsMatchRunningImageEvidence.Begin("simulator", expected)
            .WithContainerId(new string('c', 64))
            .WithFailure("failed", "Docker inspect failed before returning container image data.");
        WindowsDevelopmentMatch.StoreImageEvidence(evidence, -1, partial, logger);
        Assert.Throws<InvalidOperationException>(() =>
            WindowsDevelopmentMatch.EnsureImageVerificationSucceeded(partial, "simulator"));

        var serialized = JsonSerializer.Serialize(new { runningImages = evidence }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(serialized);
        var saved = Assert.Single(document.RootElement.GetProperty("runningImages").EnumerateArray().ToArray());
        Assert.Equal("failed", saved.GetProperty("verificationStatus").GetString());
        Assert.Equal("sha256:preflight", saved.GetProperty("preflightImageId").GetString());
        Assert.Equal("linux/amd64", saved.GetProperty("selectedPlatform").GetString());
        Assert.Equal(new string('c', 64), saved.GetProperty("containerId").GetString());
        Assert.False(saved.TryGetProperty("runtimeImage", out _));
        Assert.False(saved.TryGetProperty("runtimeConfigImage", out _));
        Assert.Contains(logger.Entries, entry => entry.Contains("Docker inspect failed", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Contains("sha256:preflight", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingRuntimeImageForLocalTransferIsARecordedFailure()
    {
        var evidence = WindowsMatchRunningImageEvidence.Begin("development-udp-transfer", null)
            .WithContainerId(new string('d', 64));
        var result = WindowsDevelopmentMatch.CaptureRuntimeImageAfterOwnershipCheck(evidence, true,
            () => (null, "duck-development-udp-transfer:local"));
        var items = new List<WindowsMatchRunningImageEvidence>();
        var logger = new RecordingLogger<WindowsDevelopmentMatch>();
        WindowsDevelopmentMatch.StoreImageEvidence(items, -1, result, logger);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(items, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var saved = Assert.Single(document.RootElement.EnumerateArray().ToArray());

        Assert.Equal("runtime_image_missing", result.VerificationStatus);
        Assert.Equal("Runtime Image is missing from container inspect.", result.Failure);
        Assert.Contains("image is missing", Assert.Throws<InvalidOperationException>(() =>
            WindowsDevelopmentMatch.EnsureImageVerificationSucceeded(result, "development-udp-transfer")).Message,
            StringComparison.Ordinal);
        Assert.Null(result.RuntimeImage);
        Assert.Equal("duck-development-udp-transfer:local", result.RuntimeConfigImage);
        Assert.Equal("runtime_image_missing", saved.GetProperty("verificationStatus").GetString());
        Assert.Equal(new string('d', 64), saved.GetProperty("containerId").GetString());
        Assert.Contains(logger.Entries, entry => entry.Contains("runtime_image_missing", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingPreflightImageIdForFixedServiceIsRejectedBeforeContainerVerification()
    {
        var expected = new WindowsMatchImage("simulator", "registry/simulator:fixed", "",
            "linux/amd64", ["registry/simulator@sha256:repo"], "sha256:platform", "sha256:platform-image");
        var evidence = WindowsMatchRunningImageEvidence.Begin("simulator", expected, requiresPreflightImage: true)
            .WithFailure("preflight_image_id_missing", "The fixed image preflight ID is missing for simulator.");
        var items = new List<WindowsMatchRunningImageEvidence>();
        var logger = new RecordingLogger<WindowsDevelopmentMatch>();
        WindowsDevelopmentMatch.StoreImageEvidence(items, -1, evidence, logger);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(items, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var saved = Assert.Single(document.RootElement.EnumerateArray().ToArray());

        Assert.False(WindowsDevelopmentMatch.HasRequiredPreflightImage(true, expected));
        Assert.False(WindowsDevelopmentMatch.HasRequiredPreflightImage(true, null));
        Assert.True(WindowsDevelopmentMatch.HasRequiredPreflightImage(false, null));
        Assert.False(WindowsDevelopmentMatch.HasRequiredPreflightImage(true,
            new WindowsMatchImage("simulator", expected.Reference, "sha256:present", "linux/amd64",
                expected.RepoDigests, expected.PlatformDigest, "")));
        Assert.Contains("preflight image ID", Assert.Throws<InvalidOperationException>(() =>
            WindowsDevelopmentMatch.EnsureImageVerificationSucceeded(evidence, "simulator")).Message,
            StringComparison.Ordinal);
        Assert.Equal("preflight_image_id_missing", evidence.VerificationStatus);
        Assert.Null(evidence.RuntimeImage);
        Assert.Equal("preflight_image_id_missing", saved.GetProperty("verificationStatus").GetString());
        Assert.Equal("registry/simulator:fixed", saved.GetProperty("preflightReference").GetString());
        Assert.False(saved.TryGetProperty("runtimeImage", out _));
        Assert.Contains(logger.Entries, entry => entry.Contains("registry/simulator:fixed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiagnosticLoggerFailureDoesNotPreventResultSaveOrApplicationStop()
    {
        var temporary = Directory.CreateTempSubdirectory("duck-windows-match-result-");
        try
        {
            var resultPath = Path.Combine(temporary.FullName, "windows-match-result.json");
            var logger = new ThrowingLogger<WindowsDevelopmentMatch>();
            var markedFailed = false;
            var stopped = false;

            var outcome = await WindowsDevelopmentMatch.PersistResultAndStopAsync(
                resultPath,
                "passed",
                () => JsonSerializer.Serialize(new { status = "passed", runId = "run-128" }),
                new { actions = Array.Empty<object>() },
                Array.Empty<object>(),
                logger,
                () => markedFailed = true,
                () => stopped = true);

            Assert.Equal("passed", outcome);
            Assert.True(File.Exists(resultPath));
            using var result = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
            Assert.Equal("passed", result.RootElement.GetProperty("status").GetString());
            Assert.Equal("run-128", result.RootElement.GetProperty("runId").GetString());
            Assert.False(markedFailed);
            Assert.True(stopped);
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }

    [Fact]
    public void OwnershipMismatchTakesPrecedenceAndDoesNotReadOrStoreForeignImageFields()
    {
        var expected = new WindowsMatchImage("simulator", "registry/simulator:fixed", "sha256:preflight",
            "linux/amd64", ["registry/simulator@sha256:repo"], "sha256:platform", "sha256:platform-image");
        var evidence = WindowsMatchRunningImageEvidence.Begin("simulator", expected, requiresPreflightImage: true)
            .WithContainerId(new string('e', 64));
        var readRuntimeFields = false;
        var result = WindowsDevelopmentMatch.CaptureRuntimeImageAfterOwnershipCheck(evidence, false, () =>
        {
            readRuntimeFields = true;
            return ("sha256:foreign-image", "foreign/simulator:tag");
        });
        var items = new List<WindowsMatchRunningImageEvidence>();
        var logger = new RecordingLogger<WindowsDevelopmentMatch>();
        WindowsDevelopmentMatch.StoreImageEvidence(items, -1, result, logger);

        Assert.False(readRuntimeFields);
        Assert.Equal("ownership_mismatch", result.VerificationStatus);
        Assert.Equal("Container ownership does not match the current run.", result.Failure);
        Assert.Contains("ownership", Assert.Throws<InvalidOperationException>(() =>
            WindowsDevelopmentMatch.EnsureImageVerificationSucceeded(result, "simulator")).Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.RuntimeImage);
        Assert.Null(result.RuntimeConfigImage);
        Assert.Equal("registry/simulator:fixed", result.PreflightReference);
        Assert.Equal("sha256:preflight", result.PreflightImageId);
        Assert.Equal("linux/amd64", result.SelectedPlatform);
        Assert.Equal(new string('e', 64), result.ContainerId);
        var serialized = JsonSerializer.Serialize(items, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("foreign-image", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("foreign/simulator", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("foreign-image", Assert.Single(logger.Entries), StringComparison.Ordinal);
        Assert.DoesNotContain("foreign/simulator", Assert.Single(logger.Entries), StringComparison.Ordinal);
    }

    [Fact]
    public void MatchOptionUsesTheExistingOwnedWindowsSession()
    {
        var match = Launcher.Options.Parse(["--play-match"]);
        Assert.True(match.HostRuntime);
        Assert.True(match.PlayMatch);
        Assert.False(Launcher.Options.Parse([]).PlayMatch);
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--play-match", "--helper-free-compose"]));
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--play-match", "--persistent"]));
    }

    [Fact]
    public async Task RefereeObserverParsesTheOwnedNamespaceUdpPacket()
    {
        var referee = new Referee
        {
            Command = Referee.Types.Command.NormalStart,
            CommandCounter = 9,
            PacketTimestamp = 123,
            StageTimeLeft = 456789,
            Stage = Referee.Types.Stage.NormalFirstHalf,
            Yellow = new() { Name = "crane" },
        };
        var receiver = new ComposeMatchUdp((_, arguments) =>
        {
            Assert.Equal(new[]
            {
                "exec", "-T", "development-udp-transfer", "dotnet", "/app/Duck.Testing.DevelopmentUdpTransfer.dll",
                "--observe", "referee",
            }, arguments);
            return Task.FromResult(Convert.ToBase64String(referee.ToByteArray()));
        });
        using var source = new UdpRefereeCommandSource(receiver);
        var sample = await source.WaitForChangeAsync(8, CancellationToken.None);
        Assert.Equal(9u, sample.CommandCounter);
        Assert.Equal(123ul, sample.PacketTimestamp);
        Assert.Equal(456789, sample.StageTimeLeftMicroseconds);
        Assert.Equal("crane", sample.YellowTeamName);
    }

    [Fact]
    public async Task RefereeObserverDistinguishesMissingStageTimeFromExplicitZero()
    {
        static async Task<long?> ObserveStageTimeAsync(Referee packet)
        {
            var source = new UdpRefereeCommandSource(new ComposeMatchUdp((_, _) =>
                Task.FromResult(Convert.ToBase64String(packet.ToByteArray()))));
            return (await source.WaitForChangeAsync(null, CancellationToken.None)).StageTimeLeftMicroseconds;
        }

        var omitted = new Referee { Command = Referee.Types.Command.Stop, CommandCounter = 1 };
        var explicitZero = new Referee { Command = Referee.Types.Command.Stop, CommandCounter = 1, StageTimeLeft = 0 };

        Assert.False(omitted.HasStageTimeLeft);
        Assert.True(explicitZero.HasStageTimeLeft);
        Assert.Null(await ObserveStageTimeAsync(omitted));
        Assert.Equal(0L, await ObserveStageTimeAsync(explicitZero));
    }

    [Fact]
    public async Task StartDiagnosticsSeparatesLocalContinueSendFromControllerAndRefereeEvidence()
    {
        var diagnostics = new WindowsMatchStartDiagnostics();
        var controllerState = new GameControllerContinuationState("Stop", "Stop", "NORMAL_FIRST_HALF_PRE",
            TimeSpan.FromMinutes(5), [new("NORMAL_START", "YELLOW", "READY_AUTO", false)],
            "KICKOFF", "YELLOW");
        var control = new RecordingGameControllerControlClient(new DiagnosticControlClient(controllerState), diagnostics);
        var refereeSnapshot = new RefereeCommandSnapshot(Referee.Types.Command.PrepareKickoffYellow, 12, "crane",
            Referee.Types.Stage.NormalFirstHalfPre, 3456, 0, 0, Referee.Types.Command.NormalStart,
            StageTimeLeftMicroseconds: 300_000_000);
        var activeSnapshot = refereeSnapshot with
        {
            Command = Referee.Types.Command.NormalStart,
            CommandCounter = 13,
            Stage = Referee.Types.Stage.NormalFirstHalf,
            PacketTimestamp = 4567,
            StageTimeLeftMicroseconds = 299_000_000,
        };
        var referee = new Duck.Testing.AppHost.RecordingRefereeCommandSource(
            new DiagnosticRefereeSource(refereeSnapshot, activeSnapshot), diagnostics);

        Assert.Equal(refereeSnapshot, await referee.WaitForChangeAsync(null, CancellationToken.None));
        Assert.Equal(controllerState, await control.ReadContinuationStateAsync(CancellationToken.None));
        await control.SendContinueActionAsync(GameControllerContinueAction.NormalStart, CancellationToken.None);
        Assert.Equal(activeSnapshot, await referee.WaitForChangeAsync(12, CancellationToken.None));
        await control.ReadContinuationStateAsync(CancellationToken.None);

        var action = Assert.Single(diagnostics.Snapshot());
        Assert.Equal("NormalStart", action.Action);
        Assert.Equal("send_completed_locally", action.LocalSendOutcome);
        Assert.Equal("acknowledgement_not_provided_by_control_send", action.Acknowledgement);
        Assert.Equal("READY_AUTO", action.LatestControllerStateBeforeSend!.State!.ContinueActions[0].State);
        Assert.Equal(12u, action.LatestRefereeBeforeSend!.Snapshot.CommandCounter);
        Assert.Equal(300_000_000, action.LatestRefereeBeforeSend.Snapshot.StageTimeLeftMicroseconds);
        Assert.Equal(13u, action.LatestRefereeAfterSend!.Snapshot.CommandCounter);
        Assert.Equal(4567ul, action.LatestRefereeAfterSend.Snapshot.PacketTimestamp);
        Assert.Equal(299_000_000, action.LatestRefereeAfterSend.Snapshot.StageTimeLeftMicroseconds);
        Assert.Equal("NORMAL_FIRST_HALF_PRE", action.LatestControllerStateAfterSend!.State!.Stage);
        Assert.NotNull(action.SendCompletedAtUtc);
        Assert.True(action.SendCompletedAtUtc >= action.SendStartedAtUtc);
        diagnostics.Freeze();
        await control.SendContinueActionAsync(GameControllerContinueAction.NextCommand, CancellationToken.None);
        Assert.Single(diagnostics.Snapshot());
    }

    [Fact]
    public async Task StartDiagnosticsMarksCanceledContinueSendOutcomeUnknown()
    {
        var diagnostics = new WindowsMatchStartDiagnostics();
        var control = new RecordingGameControllerControlClient(new DiagnosticControlClient(null, cancelSend: true), diagnostics);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            control.SendContinueActionAsync(GameControllerContinueAction.NextCommand, CancellationToken.None));

        var action = Assert.Single(diagnostics.Snapshot());
        Assert.Equal("send_canceled_outcome_unknown", action.LocalSendOutcome);
        Assert.Null(action.SendCompletedAtUtc);
        Assert.Equal("OperationCanceledException", action.ErrorType);
        Assert.Equal("acknowledgement_not_provided_by_control_send", action.Acknowledgement);
    }

    [Fact]
    public void SetupAndPostSetupVisionEvidenceUsesFreshRuntimeDecodeWithoutClaimingRemoteReceipt()
    {
        var datagram = SimulatorKickoffFixture.CreateOwnHalfSetupCommand(false);
        var setup = WindowsDevelopmentMatch.CreateSimulatorSetupEvidence("run-128", new(
            Referee.Types.Command.Stop, 10, "crane", Referee.Types.Stage.NormalFirstHalfPre,
            456, 0, 0, Referee.Types.Command.PrepareKickoffYellow, StageTimeLeftMicroseconds: 300_000_000),
            datagram, DateTimeOffset.Parse("2026-11-26T00:00:00Z"), DateTimeOffset.Parse("2026-11-26T00:00:01Z"),
            "send_completed_locally", null);
        Assert.Equal(datagram.Length, setup.DatagramLength);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(datagram)).ToLowerInvariant(), setup.DatagramSha256);
        Assert.Equal("acknowledgement_not_provided_by_udp_send", setup.Acknowledgement);
        Assert.DoesNotContain(Convert.ToBase64String(datagram), JsonSerializer.Serialize(setup), StringComparison.Ordinal);

        var previous = Health(3, 2);
        var receivedAt = DateTimeOffset.Parse("2026-11-26T00:00:02Z");
        var setupCompletedAt = DateTimeOffset.Parse("2026-11-26T00:00:01Z");
        var current = Health(4, 3) with
        {
            Diagnostics = Health(4, 3).Diagnostics with { LastReceivedAt = receivedAt },
        };
        Assert.True(WindowsDevelopmentMatch.IsFreshVisionDecode(current, previous));
        var vision = WindowsDevelopmentMatch.CreatePostSetupVisionEvidence(
            DateTimeOffset.Parse("2026-11-26T00:00:01Z"), current, previous, setupCompletedAt);
        Assert.Equal("vision_decode_observed", vision.Outcome);
        Assert.Equal("received_after_setup_send_completed", vision.SetupTimingStatus);
        Assert.Equal(setupCompletedAt, vision.SimulatorSetupSendCompletedAtUtc);
        Assert.Equal(3, vision.BaselineDecodedCount);
        Assert.Equal(4, vision.ObservedDecodedCount);
        Assert.Equal(receivedAt, vision.LastReceivedAtUtc);
        Assert.Equal("127.0.0.1:12345", vision.RemoteEndpoint);
        Assert.False(WindowsDevelopmentMatch.IsFreshVisionDecode(previous, previous));
        var earlierReceive = current with
        {
            Diagnostics = current.Diagnostics with { LastReceivedAt = setupCompletedAt.AddTicks(-1) },
        };
        Assert.Equal("received_before_or_at_setup_send_completed", WindowsDevelopmentMatch.CreatePostSetupVisionEvidence(
            DateTimeOffset.UtcNow, earlierReceive, previous, setupCompletedAt).SetupTimingStatus);
        Assert.Equal("unconfirmed_timing", WindowsDevelopmentMatch.CreatePostSetupVisionEvidence(
            DateTimeOffset.UtcNow, current, previous, null).SetupTimingStatus);
        var noReceiveTime = current with { Diagnostics = current.Diagnostics with { LastReceivedAt = null } };
        Assert.Equal("unconfirmed_timing", WindowsDevelopmentMatch.CreatePostSetupVisionEvidence(
            DateTimeOffset.UtcNow, noReceiveTime, previous, setupCompletedAt).SetupTimingStatus);
        var timeout = WindowsDevelopmentMatch.CreatePostSetupVisionFailure(DateTimeOffset.UtcNow, previous,
            setupCompletedAt, "observation_timeout_or_canceled", "OperationCanceledException");
        Assert.Equal("observation_timeout_or_canceled", timeout.Outcome);
        Assert.Equal(3, timeout.BaselineDecodedCount);
        Assert.Null(timeout.ObservedDecodedCount);
        Assert.Equal("OperationCanceledException", timeout.ErrorType);
    }

    [Fact]
    public async Task SimulatorControlUsesTheOwnedTransferExecutableAndPreservesTheDatagram()
    {
        var packet = SimulatorKickoffFixture.CreateOwnHalfSetupCommand(false);
        var udp = new ComposeMatchUdp((_, arguments) =>
        {
            Assert.Equal(new[]
            {
                "exec", "-T", "development-udp-transfer", "dotnet", "/app/Duck.Testing.DevelopmentUdpTransfer.dll",
                "--send-simulator-control", Convert.ToBase64String(packet),
            }, arguments);
            return Task.FromResult(string.Empty);
        });
        await udp.SendSimulatorControlAsync(packet, CancellationToken.None);
    }

    [Fact]
    public void WindowsFixtureReusesShortMatchButSetsTheCurrentTeamAndKickoffTimeout()
    {
        var output = Directory.CreateTempSubdirectory("duck-windows-match-");
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "match");
        try
        {
            var mounts = WindowsDevelopmentMatchInputs.PrepareFixture(output.FullName, fixtureRoot, "crane");
            Assert.Equal(3, mounts.Count);
            Assert.All(mounts, mount => Assert.StartsWith(output.FullName, mount.Source, StringComparison.Ordinal));
            var configuration = File.ReadAllText(Path.Combine(output.FullName, "ssl-game-controller.yaml"));
            Assert.Contains("half-duration: 45s", configuration, StringComparison.Ordinal);
            Assert.Contains("prepare-timeout: 3s", configuration, StringComparison.Ordinal);
            using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(output.FullName, "state-store.json.stream")));
            Assert.Equal("crane", state.RootElement.GetProperty("state").GetProperty("teamState").GetProperty("YELLOW")
                .GetProperty("name").GetString());
            Assert.Equal("NORMAL_FIRST_HALF_PRE", state.RootElement.GetProperty("state").GetProperty("stage").GetString());
        }
        finally
        {
            output.Delete(recursive: true);
        }
    }

    private static RefereeLifecycleObservation Sample(ulong packet, int seconds, string phase = "Running",
        string stage = "NORMAL_FIRST_HALF") =>
        new((long)packet,
            new(Referee.Types.Command.NormalStart, 5, Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: packet),
            "NormalStart", phase, stage, TimeSpan.FromSeconds(seconds));

    private static RuntimeVisionHealthResponse Health(long decoded, long sent, int ageSeconds = 1) =>
        new(decoded, false,
            new(null, "vision", 1, "", null, null, "127.0.0.1:12345", decoded, 0,
                DateTimeOffset.UtcNow, TimeSpan.FromSeconds(ageSeconds), DiagnosticDecodeStatus.Succeeded, false, "Unattributed"),
            []) { TrackerOutput = new("sim", sent, 0, (uint)sent, 1, 1, DateTimeOffset.UtcNow) };

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        internal List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add(formatter(state, exception));
    }

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("synthetic logger failure");
    }

    private sealed class DiagnosticControlClient(GameControllerContinuationState? state, bool cancelSend = false)
        : IGameControllerControlClient
    {
        public Task<GameControllerContinuationState?> ReadContinuationStateAsync(CancellationToken cancellationToken) =>
            Task.FromResult(state);

        public Task SendContinueActionAsync(GameControllerContinueAction action, CancellationToken cancellationToken) =>
            cancelSend ? Task.FromException(new OperationCanceledException()) : Task.CompletedTask;
    }

    private sealed class DiagnosticRefereeSource(params RefereeCommandSnapshot[] snapshots) : IRefereeCommandSource
    {
        private int index;

        public ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(uint? previousCommandCounter,
            CancellationToken cancellationToken) => ValueTask.FromResult(snapshots[Math.Min(index++, snapshots.Length - 1)]);
    }

    private sealed class ImageInspectionRunner(
        string simulatorReference, string simulatorDefaultId, string simulatorPlatformId) : IComposeCommandRunner
    {
        internal List<string[]> Calls { get; } = [];

        public Task<ComposeCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            var call = arguments.ToArray();
            Calls.Add(call);
            if (call.Contains("info", StringComparer.Ordinal))
                return Task.FromResult(new ComposeCommandResult(0, "engine-id|linux|x86_64|29.9.1|Docker Desktop", ""));

            var reference = call[^1];
            var platformInspection = call.Contains("--platform", StringComparer.Ordinal);
            var imageId = reference == simulatorReference
                ? platformInspection ? simulatorPlatformId : simulatorDefaultId
                : $"sha256:{new string('d', 64)}";
            var repository = reference.Split('@', 2)[0];
            var colon = repository.LastIndexOf(':');
            var slash = repository.LastIndexOf('/');
            if (colon > slash)
                repository = repository[..colon];
            var response = JsonSerializer.Serialize(new
            {
                Id = imageId,
                RepoDigests = new[] { $"{repository}@sha256:{new string('e', 64)}" },
                Os = "linux",
                Architecture = "amd64",
                Descriptor = new { digest = $"sha256:{new string('f', 64)}" },
            });
            return Task.FromResult(new ComposeCommandResult(0, response, ""));
        }

        public IAsyncDisposable StartLogs(string executable, IReadOnlyList<string> arguments, Action<string> writeLine) =>
            throw new NotSupportedException();
    }
}
