using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Tracker.Core;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Tracker.Tests.Contracts;
using TrackerConnectionLib;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TRACKER-046 の runtime 起動登録と receiver 常駐 contract を production 実装前に固定する。
/// </summary>
public class TrackerRuntimeRegistrationTddTests : IClassFixture<TrackerContractFixture>
{
    private const string IbisUuid = "ibis-runtime-uuid";
    private const string IbisSourceName = "ibis-runtime-source";

    private readonly TrackerContractFixture fixture;
    private readonly TrackerCoordinatorTestFactory factory;

    public TrackerRuntimeRegistrationTddTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
        factory = new TrackerCoordinatorTestFactory(fixture);
    }

    /// <summary>
    /// 何を確認しているか: 通常アプリ起動で live tracker receiver / manager / recorder を常駐接続する HostedService が存在することを確認する。
    /// </summary>
    [Fact]
    public void RuntimeStartup_RegistersLiveTrackerReceiverRecorderAndHostedConnection()
    {
        var serverAssembly = typeof(TrackerCoordinator).Assembly;
        var receiverType = typeof(UdpTrackerReceiver<TrackerPacketAdapter>);
        var managerType = typeof(MultiTrackerManager<TrackerPacketAdapter>);
        var recorderType = typeof(TrackerConnectionLibSnapshotRecorder);
        var hostedServices = serverAssembly.GetTypes()
            .Where(type => type != typeof(VisionReceiverService))
            .Where(type => typeof(IHostedService).IsAssignableFrom(type))
            .ToArray();

        var runtimeHostedService = hostedServices.SingleOrDefault(type =>
        {
            return type.GetConstructors().Any(constructor =>
            {
                var parameters = constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
                return parameters.Contains(receiverType)
                    && parameters.Contains(managerType)
                    && parameters.Contains(recorderType);
            });
        });

        Assert.NotNull(runtimeHostedService);
    }

    /// <summary>
    /// 何を確認しているか: CaptureOn 中、実 UDP receiver 由来 packet が session sidecar writer へ接続され、own / external / unknown をすべて保存することを確認する。
    /// </summary>
    [Fact]
    public async Task CaptureOn_LiveUdpReceiver_WritesOwnExternalAndUnknownPacketsToSessionSidecar()
    {
        var captureDirectory = CreateCaptureDirectory("tracker-runtime-receiver");
        var session = factory.CreateCaptureSession(captureDirectory);
        using var writer = new TrackerPacketSnapshotLogWriter(
            session,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);
        var manager = new MultiTrackerManager<TrackerPacketAdapter>(IbisUuid, IbisSourceName);
        using var recorder = new TrackerConnectionLibSnapshotRecorder(manager, writer);
        var port = GetFreeUdpPort();
        using var receiver = new UdpTrackerReceiver<TrackerPacketAdapter>(
            port,
            new TrackerWrapperPacketDeserializer());
        receiver.PacketReceived += (packet, endpoint) =>
            manager.ProcessPacket(packet, endpoint, DateTimeOffset.UtcNow);

        receiver.Start();
        try
        {
            await SendPacketAsync(port, CreatePacket(IbisUuid, IbisSourceName, 5601));
            await SendPacketAsync(port, CreatePacket("thirdparty-runtime-uuid", "thirdparty-runtime-source", 5602));
            await SendPacketAsync(port, CreatePacket(string.Empty, string.Empty, 5603));

            var completed = await WaitUntilAsync(
                () => writer.RecordCount >= 3,
                TimeSpan.FromSeconds(2));
            writer.Flush();

            Assert.True(completed, "live receiver must forward own, external, and unknown tracker packets to the snapshot writer.");
            var records = ReadSnapshotRecords(captureDirectory)
                .OrderBy(record => record.TrackedFrameNumber)
                .ToArray();
            Assert.Equal([5601u, 5602u, 5603u], records.Select(record => record.TrackedFrameNumber).ToArray());
            Assert.Equal(["own", "external", "unknown"], records.Select(record => record.SourceRole).ToArray());
            Assert.All(records, record => Assert.False(string.IsNullOrWhiteSpace(record.PayloadBase64)));
            Assert.All(records, record => Assert.NotNull(record.SemanticSummary));
        }
        finally
        {
            await receiver.StopAsync();
        }
    }

    /// <summary>
    /// 何を確認しているか: CaptureOff 競合時に writer 例外が handler から出ても、常駐 receiver loop を停止させないことを確認する。
    /// </summary>
    [Fact]
    public async Task CaptureOffRace_WriterExceptionFromHandler_DoesNotStopLiveReceiverLoop()
    {
        var port = GetFreeUdpPort();
        using var receiver = new UdpTrackerReceiver<TrackerPacketAdapter>(
            port,
            new TrackerWrapperPacketDeserializer());
        var handledCount = 0;
        receiver.PacketReceived += (_, _) =>
        {
            if (Interlocked.Increment(ref handledCount) == 1)
            {
                throw new InvalidOperationException("Simulated snapshot writer CaptureOff race.");
            }
        };

        receiver.Start();
        try
        {
            await SendPacketAsync(port, CreatePacket("thirdparty-race-a", "thirdparty-race-a", 5701));
            var firstHandled = await WaitUntilAsync(
                () => Volatile.Read(ref handledCount) >= 1,
                TimeSpan.FromSeconds(1));
            Assert.True(firstHandled, "test setup must deliver the first tracker packet before checking loop survival.");

            await SendPacketAsync(port, CreatePacket("thirdparty-race-b", "thirdparty-race-b", 5702));
            var loopSurvived = await WaitUntilAsync(
                () => Volatile.Read(ref handledCount) >= 2,
                TimeSpan.FromSeconds(1));

            Assert.True(loopSurvived, "writer InvalidOperationException must be converted to skip/error handling or otherwise isolated from the live receiver loop.");
        }
        finally
        {
            await receiver.StopAsync();
        }
    }

    private TrackerWrapperPacket CreatePacket(string uuid, string sourceName, uint frameNumber)
    {
        var frame = fixture.CreateFrame(
            frameNumber: frameNumber,
            dataTimestampNs: 12_600_000_000 + frameNumber,
            balls:
            [
                fixture.CreateTrackedBall(trackId: 10, xMm: 100, yMm: 200),
            ],
            robots:
            [
                new TrackedRobotState { Team = TrackerTeam.Yellow, RobotId = 3, XMm = 1200, YMm = -300 },
            ],
            primaryBallTrackId: 10);
        return fixture.CreatePacketGenerator(sourceName, uuid).Generate(frame);
    }

    private static async Task SendPacketAsync(int port, IMessage packet)
    {
        using var udpClient = new UdpClient(AddressFamily.InterNetwork);
        var payload = packet.ToByteArray();
        await udpClient.SendAsync(payload, new IPEndPoint(IPAddress.Loopback, port));
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        return condition();
    }

    private static int GetFreeUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private static IReadOnlyList<TrackerPacketSnapshotRecord> ReadSnapshotRecords(string captureDirectory)
    {
        var sidecarPath = Assert.Single(Directory.GetFiles(
            captureDirectory,
            TrackerPacketSnapshotLogReader.SidecarFileName,
            SearchOption.AllDirectories));
        return TrackerPacketSnapshotLogReader.ReadRecords(sidecarPath).ToArray();
    }

    private static string CreateCaptureDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
