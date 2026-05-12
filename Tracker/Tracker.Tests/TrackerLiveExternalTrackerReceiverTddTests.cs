using System.Net;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Tracker.Core;
using Tracker.Server.Tracking;
using Tracker.Server.Vision;
using Tracker.Tests.Contracts;
using TrackerConnectionLib;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TRACKER-045 の live 外部 tracker 受信接続 contract を production 実装前に固定する。
/// </summary>
public class TrackerLiveExternalTrackerReceiverTddTests : IClassFixture<TrackerContractFixture>
{
    private const string IbisUuid = "ibis-runtime-uuid";
    private const string IbisSourceName = "ibis-runtime-source";

    private readonly TrackerContractFixture fixture;
    private readonly TrackerCoordinatorTestFactory factory;

    public TrackerLiveExternalTrackerReceiverTddTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
        factory = new TrackerCoordinatorTestFactory(fixture);
    }

    /// <summary>
    /// 何を確認しているか: CaptureOn 中、TrackerConnectionLib 由来の live tracker packet が snapshot sidecar writer へ渡ることを確認する。
    /// </summary>
    [Fact]
    public void CaptureOn_LiveTrackerConnection_WritesTrackerConnectionLibPacketToSidecar()
    {
        var captureDirectory = CreateCaptureDirectory("tracker-live-receiver");
        var session = factory.CreateCaptureSession(captureDirectory);
        using var writer = new TrackerPacketSnapshotLogWriter(
            session,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);
        var manager = CreateManager();
        using var recorder = CreateRequiredRecorder(manager, writer);
        var receivedAt = new DateTimeOffset(2026, 5, 12, 12, 50, 0, TimeSpan.Zero);
        var packet = CreateAdapter("thirdparty-live-uuid", "thirdparty-live-source", frameNumber: 5101);

        manager.ProcessPacket(packet, Endpoint(12001), receivedAt);
        writer.Flush();

        var record = Assert.Single(ReadSnapshotRecords(captureDirectory));
        Assert.Equal("thirdparty-live-uuid", record.SourceUuid);
        Assert.Equal("thirdparty-live-source", record.SourceName);
        Assert.Equal("external", record.SourceRole);
        Assert.Equal("192.0.2.10:12001", record.RemoteEndpoint);
        Assert.Equal(5101u, record.TrackedFrameNumber);
    }

    /// <summary>
    /// 何を確認しているか: own / external / unknown tracker packet は self 判定で落とされず、すべて保存対象になることを確認する。
    /// </summary>
    [Fact]
    public void CaptureOn_LiveTrackerConnection_KeepsOwnExternalAndUnknownPackets()
    {
        var captureDirectory = CreateCaptureDirectory("tracker-live-receiver-roles");
        var session = factory.CreateCaptureSession(captureDirectory);
        using var writer = new TrackerPacketSnapshotLogWriter(
            session,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);
        var manager = CreateManager();
        using var recorder = CreateRequiredRecorder(manager, writer);
        var receivedAt = new DateTimeOffset(2026, 5, 12, 12, 51, 0, TimeSpan.Zero);

        manager.ProcessPacket(CreateAdapter(IbisUuid, IbisSourceName, 5201), Endpoint(12002), receivedAt);
        manager.ProcessPacket(CreateAdapter("thirdparty-live-b-uuid", "thirdparty-live-b-source", 5202), Endpoint(12003), receivedAt.AddMilliseconds(1));
        manager.ProcessPacket(CreateAdapter(string.Empty, string.Empty, 5203), Endpoint(12004), receivedAt.AddMilliseconds(2));
        writer.Flush();

        var records = ReadSnapshotRecords(captureDirectory)
            .OrderBy(record => record.TrackedFrameNumber)
            .ToArray();

        Assert.Equal(3, records.Length);
        Assert.Equal(["own", "external", "unknown"], records.Select(record => record.SourceRole).ToArray());
        Assert.All(records, record => Assert.False(string.IsNullOrWhiteSpace(record.PayloadBase64)));
        Assert.All(records, record => Assert.NotNull(record.SemanticSummary));
    }

    /// <summary>
    /// 何を確認しているか: CaptureOff 中は live tracker packet を session sidecar に書かないことを確認する。
    /// </summary>
    [Fact]
    public void CaptureOff_LiveTrackerConnection_DoesNotWriteSessionSidecar()
    {
        var captureDirectory = CreateCaptureDirectory("tracker-live-receiver-off");
        var runtimeControl = new VisionPacketCaptureRuntimeControl(initialEnabled: false);
        var session = factory.CreateCaptureSession(captureDirectory, runtimeControl: runtimeControl);
        using var writer = new TrackerPacketSnapshotLogWriter(
            session,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);
        var manager = CreateManager();
        using var recorder = CreateRequiredRecorder(manager, writer);

        manager.ProcessPacket(
            CreateAdapter("thirdparty-off-uuid", "thirdparty-off-source", 5301),
            Endpoint(12005),
            new DateTimeOffset(2026, 5, 12, 12, 52, 0, TimeSpan.Zero));
        writer.Flush();

        Assert.Null(session.Current);
        Assert.Empty(Directory.GetFiles(
            captureDirectory,
            TrackerPacketSnapshotLogReader.SidecarFileName,
            SearchOption.AllDirectories));
    }

    /// <summary>
    /// 何を確認しているか: CaptureOn / Off / 再On で session folder と writer が切り替わり、別タイミングのログが別 folder に分かれることを確認する。
    /// </summary>
    [Fact]
    public void CaptureOnOffReOn_LiveTrackerConnection_WritesDifferentSessionsToDifferentFolders()
    {
        var captureDirectory = CreateCaptureDirectory("tracker-live-receiver-reenabled");
        var runtimeControl = new VisionPacketCaptureRuntimeControl(initialEnabled: true);
        var session = factory.CreateCaptureSession(captureDirectory, runtimeControl: runtimeControl);
        using var writer = new TrackerPacketSnapshotLogWriter(
            session,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);
        var manager = CreateManager();
        using var recorder = CreateRequiredRecorder(manager, writer);

        manager.ProcessPacket(
            CreateAdapter("thirdparty-first-uuid", "thirdparty-first-source", 5401),
            Endpoint(12006),
            new DateTimeOffset(2026, 5, 12, 12, 53, 0, TimeSpan.Zero));
        writer.Flush();

        runtimeControl.SetEnabled(false);
        writer.Stop();
        manager.ProcessPacket(
            CreateAdapter("thirdparty-off-gap-uuid", "thirdparty-off-gap-source", 5402),
            Endpoint(12007),
            new DateTimeOffset(2026, 5, 12, 12, 53, 30, TimeSpan.Zero));

        runtimeControl.SetEnabled(true);
        manager.ProcessPacket(
            CreateAdapter("thirdparty-second-uuid", "thirdparty-second-source", 5403),
            Endpoint(12008),
            new DateTimeOffset(2026, 5, 12, 12, 54, 0, TimeSpan.Zero));
        writer.Flush();

        var sidecarPaths = Directory.GetFiles(
                captureDirectory,
                TrackerPacketSnapshotLogReader.SidecarFileName,
                SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var sessionFolders = sidecarPaths
            .Select(path => Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty)
            .ToArray();
        var records = sidecarPaths
            .Select(path => Assert.Single(TrackerPacketSnapshotLogReader.ReadRecords(path)))
            .OrderBy(record => record.TrackedFrameNumber)
            .ToArray();

        Assert.Equal(2, sidecarPaths.Length);
        Assert.Equal(2, sessionFolders.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal([5401u, 5403u], records.Select(record => record.TrackedFrameNumber).ToArray());
    }

    /// <summary>
    /// 何を確認しているか: live receiver 接続が snapshot だけでなく、replay / comparison 用の raw payload と semantic summary を writer に渡すことを確認する。
    /// </summary>
    [Fact]
    public void CaptureOn_LiveTrackerConnection_PassesRawPayloadForReplayComparison()
    {
        var captureDirectory = CreateCaptureDirectory("tracker-live-receiver-payload");
        var session = factory.CreateCaptureSession(captureDirectory);
        using var writer = new TrackerPacketSnapshotLogWriter(
            session,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);
        var manager = CreateManager();
        using var recorder = CreateRequiredRecorder(manager, writer);
        var packet = CreateAdapter("thirdparty-payload-uuid", "thirdparty-payload-source", frameNumber: 5501);

        manager.ProcessPacket(
            packet,
            Endpoint(12009),
            new DateTimeOffset(2026, 5, 12, 12, 55, 0, TimeSpan.Zero));
        writer.Flush();

        var record = Assert.Single(ReadSnapshotRecords(captureDirectory));
        var payload = Convert.FromBase64String(record.PayloadBase64);
        var decoded = TrackerWrapperPacket.Parser.ParseFrom(payload);
        var summary = Assert.IsType<TrackerPacketSnapshotSemanticSummary>(record.SemanticSummary);

        Assert.Equal(packet.Packet.ToByteArray(), payload);
        Assert.Equal("thirdparty-payload-uuid", decoded.Uuid);
        Assert.Equal("thirdparty-payload-source", decoded.SourceName);
        Assert.Equal(5501u, summary.TrackedFrameNumber);
        Assert.Equal("external", summary.SourceRole);
        Assert.Equal(2, summary.BallCount);
        Assert.Equal(2, summary.RobotCount);
    }

    private static IDisposable? CreateRequiredRecorder(
        MultiTrackerManager<TrackerPacketAdapter> manager,
        TrackerPacketSnapshotLogWriter writer)
    {
        var recorderType = typeof(TrackerDiagnosticsLogReader).Assembly.GetType(
            "Tracker.Server.Tracking.TrackerConnectionLibSnapshotRecorder",
            throwOnError: false);
        Assert.NotNull(recorderType);

        var recorder = Activator.CreateInstance(recorderType!, manager, writer);
        Assert.NotNull(recorder);
        return recorder as IDisposable;
    }

    private static MultiTrackerManager<TrackerPacketAdapter> CreateManager()
    {
        return new MultiTrackerManager<TrackerPacketAdapter>(IbisUuid, IbisSourceName);
    }

    private TrackerPacketAdapter CreateAdapter(string uuid, string sourceName, uint frameNumber)
    {
        var frame = fixture.CreateFrame(
            frameNumber: frameNumber,
            dataTimestampNs: 12_500_000_000 + frameNumber,
            balls:
            [
                fixture.CreateTrackedBall(trackId: 10, xMm: 100, yMm: 200),
                fixture.CreateTrackedBall(trackId: 20, xMm: 300, yMm: 400),
            ],
            robots:
            [
                new TrackedRobotState { Team = TrackerTeam.Yellow, RobotId = 3, XMm = 1200, YMm = -300 },
                new TrackedRobotState { Team = TrackerTeam.Blue, RobotId = 7, XMm = -500, YMm = 900 },
            ],
            primaryBallTrackId: 10);
        var packet = fixture.CreatePacketGenerator(sourceName, uuid).Generate(frame);
        return new TrackerPacketAdapter(packet);
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
        return Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
    }

    private static IPEndPoint Endpoint(int port)
    {
        return new IPEndPoint(IPAddress.Parse("192.0.2.10"), port);
    }
}
