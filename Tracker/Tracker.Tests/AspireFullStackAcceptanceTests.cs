using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;
using Xunit.Abstractions;

namespace Tracker.Tests;

public sealed class AspireFullStackAcceptanceTests(ITestOutputHelper output)
{
    private static readonly TimeSpan StackSignalTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan MotionTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task FullStack_ReachesActiveRefereeAndMovesCraneRobot()
    {
        var artifactsDirectory = Environment.GetEnvironmentVariable("DUCK_ASPIRE_ARTIFACTS")
            ?? Path.Combine(Path.GetTempPath(), "duck-aspire-full-stack-artifacts");
        Directory.CreateDirectory(artifactsDirectory);

        var refereeSnapshots = new ConcurrentQueue<RefereeCommandSnapshot>();
        var visionPacketCount = 0;
        var trackerPacketCount = 0;
        string? failure = null;
        RobotMotionDelta? observedMovement = null;
        UdpMulticastReceiver? visionReceiver = null;
        UdpMulticastReceiver? trackerReceiver = null;

        try
        {
            using var timeout = new CancellationTokenSource(StackSignalTimeout);
            using var refereeSource = new RecordingRefereeCommandSource(
                new UdpRefereeCommandSource(),
                refereeSnapshots,
                output);
            visionReceiver = new UdpMulticastReceiver("224.5.23.2", 10020);
            trackerReceiver = new UdpMulticastReceiver("224.5.23.2", 11010);

            RefereeCommandSnapshot initial;
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
                    if (!packet.HasDetection)
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
            output.WriteLine(
                $"Duck tracker packet verified: uuid={tracker.Uuid}; source={tracker.SourceName}; " +
                $"frame={tracker.TrackedFrame.FrameNumber}; robots={tracker.TrackedFrame.Robots.Count}");
            Assert.Equal("ibis", tracker.Uuid);
            Assert.Equal("ibis", tracker.SourceName);
            Assert.True(tracker.HasTrackedFrame);
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            throw;
        }
        finally
        {
            visionPacketCount = visionReceiver?.PacketCount ?? 0;
            trackerPacketCount = trackerReceiver?.PacketCount ?? 0;
            visionReceiver?.Dispose();
            trackerReceiver?.Dispose();
            var evidence = new
            {
                status = failure is null ? "passed" : "failed",
                failure,
                refereeCommands = refereeSnapshots.ToArray().Select(snapshot => new
                {
                    command = snapshot.Command.ToString(),
                    snapshot.CommandCounter,
                }),
                visionPacketCount,
                trackerPacketCount,
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
            if (packet.HasDetection && packet.Detection.RobotsYellow.Count > 0)
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
            if (packet.Uuid == "ibis" && packet.SourceName == "ibis" && packet.HasTrackedFrame)
            {
                return packet;
            }
        }
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

    private sealed class UdpMulticastReceiver : IDisposable
    {
        private readonly UdpClient client;
        private int packetCount;

        public int PacketCount => Volatile.Read(ref packetCount);

        public UdpMulticastReceiver(string address, int port)
        {
            var group = IPAddress.Parse(address);
            client = new UdpClient(AddressFamily.InterNetwork)
            {
                ExclusiveAddressUse = false,
            };
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            client.JoinMulticastGroup(group);
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
}
