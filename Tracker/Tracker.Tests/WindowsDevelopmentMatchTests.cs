using System.Text.Json;
using Duck.Testing.AppHost;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;
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
}
