using System.Text.Json;
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
            "linux/amd64", ["registry/simulator@sha256:repo"], "sha256:platform");
        var evidence = new List<WindowsMatchRunningImageEvidence>();
        var logger = new RecordingLogger<WindowsDevelopmentMatch>();

        var verified = WindowsMatchRunningImageEvidence.Begin("simulator", expected, requiresPreflightImage: true)
            .WithContainerId(new string('a', 64))
            .WithRuntimeImage("sha256:preflight", "registry/simulator:unexpected-tag") with
            {
                VerificationStatus = "verified",
            };
        WindowsDevelopmentMatch.StoreImageEvidence(evidence, -1, verified, logger);
        var mismatch = WindowsMatchRunningImageEvidence.Begin("simulator", expected, requiresPreflightImage: true)
            .WithContainerId(new string('b', 64))
            .WithRuntimeImage("sha256:runtime", "registry/simulator:runtime-tag");
        WindowsDevelopmentMatch.StoreImageEvidence(evidence, -1, mismatch, logger);

        var serialized = JsonSerializer.Serialize(new { runningImages = evidence }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(serialized);
        var entries = document.RootElement.GetProperty("runningImages").EnumerateArray().ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal("verified", entries[0].GetProperty("verificationStatus").GetString());
        Assert.Equal("registry/simulator:fixed", entries[0].GetProperty("preflightReference").GetString());
        Assert.Equal("sha256:preflight", entries[0].GetProperty("preflightImageId").GetString());
        Assert.Equal("registry/simulator:unexpected-tag", entries[0].GetProperty("runtimeConfigImage").GetString());
        Assert.Equal("linux/amd64", entries[0].GetProperty("selectedPlatform").GetString());
        Assert.Equal(new string('a', 64), entries[0].GetProperty("containerId").GetString());
        Assert.Equal("image_mismatch", entries[1].GetProperty("verificationStatus").GetString());
        Assert.Equal("sha256:runtime", entries[1].GetProperty("runtimeImage").GetString());
        Assert.Equal("registry/simulator:runtime-tag", entries[1].GetProperty("runtimeConfigImage").GetString());
        Assert.Equal(new string('b', 64), entries[1].GetProperty("containerId").GetString());
        Assert.Contains(logger.Entries, entry => entry.Contains("sha256:runtime", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Contains("registry/simulator:runtime-tag", StringComparison.Ordinal));
    }

    [Fact]
    public void PartialRunningImageFailureRetainsPreflightAndContainerEvidenceInJsonAndLogs()
    {
        var expected = new WindowsMatchImage("simulator", "registry/simulator:fixed", "sha256:preflight",
            "linux/amd64", ["registry/simulator@sha256:repo"], "sha256:platform");
        var evidence = new List<WindowsMatchRunningImageEvidence>();
        var logger = new RecordingLogger<WindowsDevelopmentMatch>();
        var partial = WindowsMatchRunningImageEvidence.Begin("simulator", expected)
            .WithContainerId(new string('c', 64))
            .WithFailure("failed", "Docker inspect failed before returning container image data.");
        WindowsDevelopmentMatch.StoreImageEvidence(evidence, -1, partial, logger);

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
            "linux/amd64", ["registry/simulator@sha256:repo"], "sha256:platform");
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
        Assert.Equal("preflight_image_id_missing", evidence.VerificationStatus);
        Assert.Null(evidence.RuntimeImage);
        Assert.Equal("preflight_image_id_missing", saved.GetProperty("verificationStatus").GetString());
        Assert.Equal("registry/simulator:fixed", saved.GetProperty("preflightReference").GetString());
        Assert.False(saved.TryGetProperty("runtimeImage", out _));
        Assert.Contains(logger.Entries, entry => entry.Contains("registry/simulator:fixed", StringComparison.Ordinal));
    }

    [Fact]
    public void OwnershipMismatchTakesPrecedenceAndDoesNotReadOrStoreForeignImageFields()
    {
        var expected = new WindowsMatchImage("simulator", "registry/simulator:fixed", "sha256:preflight",
            "linux/amd64", ["registry/simulator@sha256:repo"], "sha256:platform");
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
        Assert.Equal("crane", sample.YellowTeamName);
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
}
