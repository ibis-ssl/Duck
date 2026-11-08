using System.Text;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Duck.Testing.CraneLaunchOverlay;
using CraneOverlayProgram = Duck.Testing.CraneLaunchOverlay.CraneLaunchOverlayProgram;

namespace Tracker.Tests;

public sealed class CraneLaunchOverlayTests
{
    [Fact]
    public void Mode4AdapterSelectsCm4TargetWithoutChangingThePlanner()
    {
        Assert.Equal("cm4-sim", CraneOverlayProgram.ExpectedTargetResource("rvo2", useMode4PacketAdapter: true));
        Assert.Equal("simulator", CraneOverlayProgram.ExpectedTargetResource("rvo2", useMode4PacketAdapter: false));
        Assert.Equal("cm4-sim", CraneOverlayProgram.ExpectedTargetResource("visibility_graph", useMode4PacketAdapter: false));
    }

    [Theory]
    [InlineData("$(var sim)")]
    [InlineData(null)]
    public void PatchAcceptsPreviousConditionalAndCurrentUnconditionalTarget(string? condition)
    {
        var conditionAttribute = condition is null ? string.Empty : $" if=\"{condition}\"";
        var original = Encoding.UTF8.GetBytes(
            $"<launch><!-- <let name=\"ibis_target_address\" value=\"127.0.0.1\" /> --><let name=\"ibis_target_address\" value=\"127.0.0.1\"{conditionAttribute}/><let name=\"ibis_target_address\" value=\"192.168.20.255\" if=\"$(var sim) == false\"/><let name=\"other\" value=\"preserve\"/></launch>");

        var updated = Encoding.UTF8.GetString(CraneOverlayProgram.PatchSimulatorTarget(original, "192.0.2.31"));

        Assert.Contains("name=\"ibis_target_address\" value=\"192.0.2.31\"", updated, StringComparison.Ordinal);
        Assert.Contains("<!-- <let name=\"ibis_target_address\" value=\"127.0.0.1\" /> -->", updated, StringComparison.Ordinal);
        Assert.Contains("value=\"192.168.20.255\" if=\"$(var sim) == false\"", updated, StringComparison.Ordinal);
        Assert.Contains("name=\"other\" value=\"preserve\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void PatchRejectsMissingAmbiguousOrConditionallyExcludedTarget()
    {
        var candidates = new[]
        {
            "<launch><let name=\"other\" value=\"127.0.0.1\"/></launch>",
            "<launch><let name=\"ibis_target_address\" value=\"127.0.0.1\"/><let name=\"ibis_target_address\" value=\"127.0.0.1\"/></launch>",
            "<launch><let name=\"ibis_target_address\" value=\"127.0.0.1\" unless=\"$(var sim)\"/></launch>",
            "<launch><let name=\"ibis_target_address\" value=\"192.0.2.10\"/></launch>",
        };

        foreach (var candidate in candidates)
            Assert.Throws<InvalidOperationException>(() => CraneOverlayProgram.PatchSimulatorTarget(Encoding.UTF8.GetBytes(candidate), "192.0.2.31"));
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("192.0.2.1.example")]
    public void PatchRejectsUnsafeOrMalformedIpv4(string address) =>
        Assert.Throws<InvalidOperationException>(() => CraneOverlayProgram.PatchSimulatorTarget(
            Encoding.UTF8.GetBytes("<launch><let name=\"ibis_target_address\" value=\"127.0.0.1\"/></launch>"), address));

    [Fact]
    public async Task LiteralTargetIsRestrictedToLoopbackAndResourceMustMatchPlanner()
    {
        Assert.Equal("127.0.0.1", await CraneOverlayProgram.ResolveTargetAddressAsync("127.0.0.1", "cm4-sim", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CraneOverlayProgram.ResolveTargetAddressAsync("192.0.2.1", "cm4-sim", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CraneOverlayProgram.ResolveTargetAddressAsync("simulator", "cm4-sim", CancellationToken.None));
    }

    [Fact]
    public async Task ResourceTargetRequiresExactlyOneSafeUnicastIpv4()
    {
        static Task<IPAddress[]> Resolve(params string[] values) =>
            Task.FromResult(values.Select(IPAddress.Parse).ToArray());

        Assert.Equal("192.0.2.8", await CraneOverlayProgram.ResolveTargetAddressAsync(
            "cm4-sim", "cm4-sim", CancellationToken.None, (_, _) => Resolve("192.0.2.8")));
        foreach (var addresses in new[]
        {
            new[] { "192.0.2.8", "192.0.2.9" },
            new[] { "127.0.0.1" },
            new[] { "0.0.0.0" },
            new[] { "224.0.0.1" },
            new[] { "255.255.255.255" },
        })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CraneOverlayProgram.ResolveTargetAddressAsync(
                "cm4-sim", "cm4-sim", CancellationToken.None, (_, _) => Resolve(addresses)));
        }
    }

    [Fact]
    public void Mode4PacketAdapterWrapsOneRobotCommandIntoTheExpectedElevenSlotDatagram()
    {
        var command = new byte[CraneMode4PacketAdapter.SlotBytes];
        command[0] = 7;
        for (var index = 1; index < command.Length; index++)
            command[index] = (byte)index;

        Assert.True(CraneMode4PacketAdapter.TryCreateAggregateFrame(command, out var frame));
        Assert.Equal(715, frame.Length);
        Assert.Equal(command, frame.AsSpan(7 * 65, 65).ToArray());
        for (var robot = 0; robot < CraneMode4PacketAdapter.RobotCount; robot++)
        {
            if (robot == 7)
                continue;
            Assert.Equal(0xff, frame[robot * 65]);
        }
    }

    [Theory]
    [InlineData(64, 0)]
    [InlineData(66, 0)]
    [InlineData(65, 11)]
    [InlineData(65, 255)]
    public void Mode4PacketAdapterRejectsUnexpectedLengthOrRobotId(int length, int robotId)
    {
        var command = new byte[length];
        if (length > 0)
            command[0] = (byte)robotId;
        Assert.False(CraneMode4PacketAdapter.TryCreateAggregateFrame(command, out _));
    }

    [Fact]
    public async Task Mode4PacketAdapterForwardsAggregatedCommandsAndConfigurationPackets()
    {
        using var commandTarget = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var configTarget = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var adapter = new CraneMode4PacketAdapter(
            IPAddress.Loopback,
            commandInputPort: 0,
            configInputPort: 0,
            cm4CommandPort: ((IPEndPoint)commandTarget.Client.LocalEndPoint!).Port,
            cm4ConfigPort: ((IPEndPoint)configTarget.Client.LocalEndPoint!).Port);
        adapter.Start();
        using var sender = new UdpClient(AddressFamily.InterNetwork);

        var command = new byte[CraneMode4PacketAdapter.SlotBytes];
        command[0] = 3;
        command[8] = 0x5a;
        await sender.SendAsync(command, adapter.CommandInputEndpoint);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var aggregate = await commandTarget.ReceiveAsync(timeout.Token);
        Assert.True(CraneMode4PacketAdapter.TryCreateAggregateFrame(command, out var expected));
        Assert.Equal(expected, aggregate.Buffer);

        var config = new byte[] { (byte)'O', (byte)'C', (byte)'4', (byte)'C', 2, 255 };
        await sender.SendAsync(config, adapter.ConfigInputEndpoint);
        var forwardedConfig = await configTarget.ReceiveAsync(timeout.Token);
        Assert.Equal(config, forwardedConfig.Buffer);
    }

    [Fact]
    public async Task Mode4PacketAdapterSupervisionCancelsTheOtherForwarderAndSurfacesFaults()
    {
        using var stop = new CancellationTokenSource();
        var failed = Task.FromException(new SocketException((int)SocketError.NetworkDown));
        var waiting = Task.Delay(Timeout.InfiniteTimeSpan, stop.Token);

        await Assert.ThrowsAsync<SocketException>(() =>
            CraneMode4PacketAdapter.SuperviseForwardersAsync(failed, waiting, stop));
        Assert.True(stop.IsCancellationRequested);
    }

    [Fact]
    public void OverlayReplacesOnlyCraneLaunchAndCleansItsOwnPrefix()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using var fixture = new PackageFixture();
        var originalLaunch = File.ReadAllBytes(fixture.LaunchPath);
        string generatedPrefix;
        using (var overlay = CraneOverlayProgram.CreateOverlay(fixture.PackageShare, "192.0.2.41"))
        {
            generatedPrefix = overlay.RootDirectory;
            var generatedPackage = Path.Combine(generatedPrefix, "share", "crane_bringup");
            var generatedLaunch = File.ReadAllText(Path.Combine(generatedPackage, "launch", "crane.launch.xml"));
            Assert.Contains("value=\"192.0.2.41\"", generatedLaunch, StringComparison.Ordinal);
            Assert.Equal("<launch/>", File.ReadAllText(Path.Combine(generatedPackage, "launch", "foxglove_bridge.launch.xml")));
            Assert.Equal("mode: fixed", File.ReadAllText(Path.Combine(generatedPackage, "config", "settings.yaml")));
            Assert.True(File.Exists(Path.Combine(generatedPrefix, "share", "ament_index", "resource_index", "packages", "crane_bringup")));
            Assert.Equal(originalLaunch, File.ReadAllBytes(fixture.LaunchPath));
            Assert.True(File.Exists(fixture.SentinelPath));
        }
        Assert.False(Directory.Exists(generatedPrefix));
        Assert.True(File.Exists(fixture.SentinelPath));
    }

    [Fact]
    public async Task StopAndReapTerminatesOnlyTheOwnedProcessGroup()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var fixture = new TemporaryDirectory();
        var ownedReady = Path.Combine(fixture.Path, "owned-ready");
        var ownedStopped = Path.Combine(fixture.Path, "owned-stopped");
        var childStopped = Path.Combine(fixture.Path, "child-stopped");
        var unrelatedReady = Path.Combine(fixture.Path, "unrelated-ready");
        var unrelatedStopped = Path.Combine(fixture.Path, "unrelated-stopped");
        var owned = StartSession(
            "trap 'printf stopped > \"$1\"; exit 0' TERM; " +
            "(trap 'printf child > \"$2\"; exit 0' TERM; while :; do sleep 0.1; done) & " +
            "printf ready > \"$3\"; wait",
            ownedStopped, childStopped, ownedReady);
        var unrelated = StartSession(
            "trap 'printf stopped > \"$1\"; exit 0' TERM; printf ready > \"$2\"; while :; do sleep 0.1; done",
            unrelatedStopped, unrelatedReady);
        try
        {
            await WaitForFileAsync(ownedReady);
            await WaitForFileAsync(unrelatedReady);
            await CraneOverlayProgram.StopAndReapOwnedGroupAsync(owned);

            Assert.True(owned.HasExited);
            Assert.True(File.Exists(ownedStopped));
            Assert.True(File.Exists(childStopped));
            Assert.False(unrelated.HasExited);
            Assert.False(File.Exists(unrelatedStopped));
        }
        finally
        {
            if (!unrelated.HasExited)
                unrelated.Kill(entireProcessTree: true);
            unrelated.Dispose();
            owned.Dispose();
        }
    }

    [Fact]
    public async Task AdapterWaitPathReapsRosGroupWhenTerminationSignalArrives()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var fixture = new TemporaryDirectory();
        var ready = Path.Combine(fixture.Path, "ready");
        var stopped = Path.Combine(fixture.Path, "stopped");
        var owned = StartSession(
            "trap 'printf stopped > \"$1\"; exit 0' TERM; printf ready > \"$2\"; while :; do sleep 0.1; done",
            stopped, ready);
        var signal = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var adapter = Task.Delay(Timeout.InfiniteTimeSpan);
        var waitStarted = false;
        try
        {
            await WaitForFileAsync(ready);
            var wait = CraneOverlayProgram.WaitForOwnedProcessCoreAsync(owned, adapter, signal.Task);
            waitStarted = true;
            signal.SetResult(15);

            Assert.Equal(143, await wait);
            Assert.True(File.Exists(stopped));
        }
        finally
        {
            if (!waitStarted)
            {
                if (!owned.HasExited)
                    owned.Kill(entireProcessTree: true);
                owned.Dispose();
            }
        }
    }

    [Fact]
    public async Task StopAndReapContinuesAfterGracefulExitTakesLongerThanOnePoll()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var fixture = new TemporaryDirectory();
        var ready = Path.Combine(fixture.Path, "ready");
        var stopped = Path.Combine(fixture.Path, "stopped");
        var owned = StartSession(
            "trap 'sleep 0.2; printf stopped > \"$1\"; exit 0' TERM; printf ready > \"$2\"; while :; do sleep 0.1; done",
            stopped, ready);
        try
        {
            await WaitForFileAsync(ready);
            await CraneOverlayProgram.StopAndReapOwnedGroupAsync(owned);

            Assert.True(owned.HasExited);
            Assert.True(File.Exists(stopped));
        }
        finally
        {
            if (!owned.HasExited)
                owned.Kill(entireProcessTree: true);
            owned.Dispose();
        }
    }

    [Fact]
    public async Task StopAndReapForceKillsGroupThatIgnoresTerminate()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var fixture = new TemporaryDirectory();
        var ready = Path.Combine(fixture.Path, "ready");
        var owned = StartSession("trap '' TERM; printf ready > \"$1\"; while :; do sleep 0.1; done", ready);
        try
        {
            await WaitForFileAsync(ready);
            await CraneOverlayProgram.StopAndReapOwnedGroupAsync(owned);

            Assert.True(owned.HasExited);
        }
        finally
        {
            if (!owned.HasExited)
                owned.Kill(entireProcessTree: true);
            owned.Dispose();
        }
    }

    private static Process StartSession(string script, params string[] arguments)
    {
        var start = new ProcessStartInfo("setsid") { UseShellExecute = false };
        start.ArgumentList.Add("bash");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("--");
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start process-group fixture.");
    }

    private static async Task WaitForFileAsync(string path)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!File.Exists(path) && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.True(File.Exists(path), $"Timed out waiting for {path}.");
    }

    private sealed class PackageFixture : IDisposable
    {
        private readonly TemporaryDirectory temporary = new();
        public string PackageShare { get; }
        public string LaunchPath => Path.Combine(PackageShare, "launch", "crane.launch.xml");
        public string SentinelPath => Path.Combine(temporary.Path, "sibling-sentinel");

        public PackageFixture()
        {
            PackageShare = Path.Combine(temporary.Path, "install", "share", "crane_bringup");
            Directory.CreateDirectory(Path.Combine(PackageShare, "launch"));
            Directory.CreateDirectory(Path.Combine(PackageShare, "config"));
            Directory.CreateDirectory(Path.Combine(temporary.Path, "install", "share", "ament_index", "resource_index", "packages"));
            File.WriteAllText(LaunchPath, "<launch><let name=\"ibis_target_address\" value=\"127.0.0.1\" if=\"$(var sim)\"/></launch>");
            File.WriteAllText(Path.Combine(PackageShare, "launch", "foxglove_bridge.launch.xml"), "<launch/>");
            File.WriteAllText(Path.Combine(PackageShare, "config", "settings.yaml"), "mode: fixed");
            File.WriteAllText(Path.Combine(temporary.Path, "install", "share", "ament_index", "resource_index", "packages", "crane_bringup"), string.Empty);
            File.WriteAllText(SentinelPath, "keep");
        }

        public void Dispose() => temporary.Dispose();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "duck-crane-test-" + Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
    }
}
