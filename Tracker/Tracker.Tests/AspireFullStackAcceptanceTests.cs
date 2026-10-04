using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;
using Xunit.Abstractions;

namespace Tracker.Tests;

public sealed class AspireFullStackAcceptanceTests(ITestOutputHelper output)
{
    private static readonly TimeSpan StackSignalTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MotionTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task FullStack_ReachesActiveRefereeAndMovesCraneRobot()
    {
        var artifactsDirectory = Environment.GetEnvironmentVariable("DUCK_ASPIRE_ARTIFACTS")
            ?? Path.Combine(Path.GetTempPath(), "duck-aspire-full-stack-artifacts");
        Directory.CreateDirectory(artifactsDirectory);

        var refereeSnapshots = new ConcurrentQueue<RefereeCommandSnapshot>();
        var stageReached = "test_setup";
        var refereeRawDatagramCount = 0;
        int? visionPacketsConsumed = null;
        int? trackerPacketsConsumed = null;
        string? failure = null;
        RobotMotionDelta? observedMovement = null;
        MulticastInterface? selectedMulticastInterface = null;
        UdpMulticastReceiver? refereeReceiver = null;
        UdpMulticastReceiver? visionReceiver = null;
        UdpMulticastReceiver? trackerReceiver = null;

        try
        {
            using var timeout = new CancellationTokenSource(StackSignalTimeout);
            var multicastInterface = FindMulticastInterface();
            selectedMulticastInterface = multicastInterface;
            output.WriteLine(
                $"multicast interface={multicastInterface.Name}; ipv4={multicastInterface.Address}");
            refereeReceiver = new UdpMulticastReceiver("224.5.23.1", 11003, multicastInterface.Address);
            using var refereeSource = new RecordingRefereeCommandSource(
                new UdpRefereeCommandSource(refereeReceiver),
                refereeSnapshots,
                output);
            visionReceiver = new UdpMulticastReceiver("224.5.23.2", 10020, multicastInterface.Address);
            trackerReceiver = new UdpMulticastReceiver("224.5.23.2", 11010, multicastInterface.Address);

            RefereeCommandSnapshot initial;
            stageReached = "initial_referee_wait";
            try
            {
                initial = await refereeSource.WaitForChangeAsync(null, timeout.Token);
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    "REFEREE_FIXTURE_FAILURE: Game Controller did not publish a referee packet on UDP 11003 before the deadline.",
                    exception);
            }

            if (initial.Command != Referee.Types.Command.Halt)
            {
                throw new InvalidOperationException(
                    $"REFEREE_FIXTURE_FAILURE: expected initial HALT, observed {initial.Command}.");
            }

            SSL_WrapperPacket initialVision;
            stageReached = "initial_vision_wait";
            try
            {
                initialVision = await ReadVisionFrameWithYellowRobotsAsync(visionReceiver, timeout.Token);
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    "VISION_INGRESS_FAILURE: no SSL-Vision detection frame with Yellow robots arrived on UDP 10020 before the deadline.",
                    exception);
            }
            visionPacketsConsumed = visionReceiver.PacketCount;
            stageReached = "active_referee_transition";
            var motionVerifier = new RobotMotionAcceptanceVerifier(minimumDistanceMm: 100);
            motionVerifier.CaptureBaseline(initialVision.Detection);
            output.WriteLine(
                $"baseline referee={initial.Command} counter={initial.CommandCounter}; " +
                $"visionFrame={initialVision.Detection.FrameNumber}; " +
                $"yellowRobots={initialVision.Detection.RobotsYellow.Count}");

            var cachedInitialSource = new InitialSnapshotRefereeCommandSource(refereeSource, initial);
            var driver = new RefereeDriverFixture(
                cachedInitialSource,
                new GameControllerWebSocketControlClient());
            try
            {
                await driver.DriveToActiveAsync(timeout.Token);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"REFEREE_FIXTURE_FAILURE: could not drive Game Controller from HALT to active: {exception.Message}",
                    exception);
            }

            var active = refereeSnapshots.LastOrDefault(snapshot =>
                snapshot.Command is Referee.Types.Command.NormalStart or Referee.Types.Command.ForceStart);
            Assert.True(
                active is not null,
                "REFEREE_FIXTURE_FAILURE: Game Controller did not publish an active command on UDP 11003.");
            output.WriteLine($"active referee={active!.Command} counter={active.CommandCounter}");

            stageReached = "active_robot_motion_wait";
            var discardedPreMotionPackets = visionReceiver.DrainPendingPackets();
            output.WriteLine($"discarded queued vision packets before active-motion window={discardedPreMotionPackets}");

            using var motionTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            motionTimeout.CancelAfter(MotionTimeout);
            while (observedMovement is null)
            {
                try
                {
                    var datagram = await visionReceiver.ReceiveAsync(motionTimeout.Token);
                    var packet = SSL_WrapperPacket.Parser.ParseFrom(datagram.ToArray());
                    if (packet.Detection is null)
                    {
                        continue;
                    }

                    observedMovement = motionVerifier.FindMovement(packet.Detection);
                }
                catch (OperationCanceledException exception)
                {
                    throw new InvalidOperationException(
                        "UDP_MOTION_FAILURE: no Yellow robot moved at least 100 mm after the active referee transition within 60 seconds.",
                        exception);
                }
            }

            output.WriteLine(
                $"UDP motion verified: team={observedMovement.Team}; " +
                $"robotId={observedMovement.RobotId}; distanceMm={observedMovement.DistanceMm:F1}");

            TrackerWrapperPacket tracker;
            stageReached = "duck_tracker_output_wait";
            try
            {
                tracker = await ReadDuckTrackerPacketAsync(trackerReceiver, timeout.Token);
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    "DUCK_TRACKER_OUTPUT_FAILURE: no ibis TrackerWrapperPacket was received on UDP 11010 before the deadline.",
                    exception);
            }
            trackerPacketsConsumed = trackerReceiver.PacketCount;
            output.WriteLine(
                $"Duck tracker packet verified: uuid={tracker.Uuid}; source={tracker.SourceName}; " +
                $"frame={tracker.TrackedFrame!.FrameNumber}; robots={tracker.TrackedFrame.Robots.Count}");
            Assert.Equal("ibis", tracker.Uuid);
            Assert.Equal("ibis", tracker.SourceName);
            Assert.NotNull(tracker.TrackedFrame);
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            throw;
        }
        finally
        {
            refereeRawDatagramCount = refereeReceiver?.PacketCount ?? 0;
            if (stageReached is "initial_vision_wait" or "active_referee_transition" or "active_robot_motion_wait" or "duck_tracker_output_wait")
            {
                visionPacketsConsumed = visionReceiver?.PacketCount ?? visionPacketsConsumed;
            }

            if (stageReached == "duck_tracker_output_wait")
            {
                trackerPacketsConsumed = trackerReceiver?.PacketCount;
            }

            var linuxMulticastMemberships = ReadLinuxMulticastMemberships();
            refereeReceiver?.Dispose();
            visionReceiver?.Dispose();
            trackerReceiver?.Dispose();
            var evidence = new
            {
                status = failure is null ? "passed" : "failed",
                failure,
                stageReached,
                refereeCommands = refereeSnapshots.ToArray().Select(snapshot => new
                {
                    command = snapshot.Command.ToString(),
                    snapshot.CommandCounter,
                }),
                refereeRawDatagramCount,
                visionPacketsConsumed,
                trackerPacketsConsumed,
                packetCountSemantics = "Consumed counts record datagrams read by the harness; null means that reader stage was not reached and does not imply network ingress was zero.",
                receiverSockets = new
                {
                    interfaceName = selectedMulticastInterface?.Name,
                    interfaceAddress = selectedMulticastInterface?.Address.ToString(),
                    bindAddress = IPAddress.Any.ToString(),
                    reuseAddress = true,
                    referee = new { group = "224.5.23.1", port = 11003 },
                    sslVision = new { group = "224.5.23.2", port = 10020 },
                    duckTracker = new { group = "224.5.23.2", port = 11010 },
                    linuxMemberships = linuxMulticastMemberships,
                },
                movement = observedMovement,
                runner = Environment.GetEnvironmentVariable("RUNNER_OS"),
                sha = Environment.GetEnvironmentVariable("GITHUB_SHA"),
            };
            var path = Path.Combine(artifactsDirectory, "acceptance-result.json");
            await File.WriteAllTextAsync(
                path,
                System.Text.Json.JsonSerializer.Serialize(
                    evidence,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            output.WriteLine($"Acceptance evidence: {path}");
        }
    }

    private static async Task<SSL_WrapperPacket> ReadVisionFrameWithYellowRobotsAsync(
        UdpMulticastReceiver receiver,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var packet = SSL_WrapperPacket.Parser.ParseFrom((await receiver.ReceiveAsync(cancellationToken)).ToArray());
            if (packet.Detection is not null && packet.Detection.RobotsYellow.Count > 0)
            {
                return packet;
            }
        }
    }

    private static async Task<TrackerWrapperPacket> ReadDuckTrackerPacketAsync(
        UdpMulticastReceiver receiver,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var packet = TrackerWrapperPacket.Parser.ParseFrom((await receiver.ReceiveAsync(cancellationToken)).ToArray());
            if (packet.Uuid == "ibis" && packet.SourceName == "ibis" && packet.TrackedFrame is not null)
            {
                return packet;
            }
        }
    }

    private static MulticastInterface FindMulticastInterface()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(networkInterface =>
            {
                var properties = networkInterface.GetIPProperties();
                var hasDefaultGateway = properties.GatewayAddresses.Any(gateway =>
                    gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !gateway.Address.Equals(IPAddress.Any));
                return properties.UnicastAddresses
                    .Where(unicast => unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(unicast => new MulticastInterface(
                        networkInterface.Name,
                        unicast.Address,
                        hasDefaultGateway));
            })
            .OrderByDescending(candidate => candidate.HasDefaultGateway)
            .FirstOrDefault();

        return interfaces ?? throw new InvalidOperationException(
            "MULTICAST_INTERFACE_FAILURE: no operational non-loopback IPv4 interface was found.");
    }

    private static string? ReadLinuxMulticastMemberships()
    {
        const string membershipPath = "/proc/net/igmp";
        return File.Exists(membershipPath) ? File.ReadAllText(membershipPath) : null;
    }

    private sealed class RecordingRefereeCommandSource(
        IRefereeCommandSource inner,
        ConcurrentQueue<RefereeCommandSnapshot> snapshots,
        ITestOutputHelper output) : IRefereeCommandSource, IDisposable
    {
        public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
            uint? previousCommandCounter,
            CancellationToken cancellationToken)
        {
            var snapshot = await inner.WaitForChangeAsync(previousCommandCounter, cancellationToken);
            snapshots.Enqueue(snapshot);
            output.WriteLine($"referee command={snapshot.Command} counter={snapshot.CommandCounter}");
            return snapshot;
        }

        public void Dispose()
        {
            (inner as IDisposable)?.Dispose();
        }
    }

    private sealed class InitialSnapshotRefereeCommandSource(
        IRefereeCommandSource inner,
        RefereeCommandSnapshot initial) : IRefereeCommandSource
    {
        private bool initialAvailable = true;

        public ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
            uint? previousCommandCounter,
            CancellationToken cancellationToken)
        {
            if (initialAvailable && previousCommandCounter is null)
            {
                initialAvailable = false;
                return ValueTask.FromResult(initial);
            }

            return inner.WaitForChangeAsync(previousCommandCounter, cancellationToken);
        }
    }

    private sealed class UdpMulticastReceiver : IRefereePacketReceiver, IDisposable
    {
        private readonly UdpClient client;
        private int packetCount;

        public int PacketCount => Volatile.Read(ref packetCount);

        public UdpMulticastReceiver(string address, int port, IPAddress interfaceAddress)
        {
            var group = IPAddress.Parse(address);
            client = new UdpClient(AddressFamily.InterNetwork)
            {
                ExclusiveAddressUse = false,
            };
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            client.JoinMulticastGroup(group, interfaceAddress);
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
        {
            var datagram = await client.ReceiveAsync(cancellationToken);
            Interlocked.Increment(ref packetCount);
            return datagram.Buffer;
        }

        public int DrainPendingPackets()
        {
            var discarded = 0;
            while (client.Available > 0)
            {
                var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                _ = client.Receive(ref remoteEndpoint);
                Interlocked.Increment(ref packetCount);
                discarded++;
            }

            return discarded;
        }

        public void Dispose() => client.Dispose();
    }

    private sealed record MulticastInterface(string Name, IPAddress Address, bool HasDefaultGateway);
}
