using System.Reflection;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tracker.Core;
using Tracker.Server.Tracking;
using Tracker.Server.Vision;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TRACKER-044 の比較用元データ保持 contract を production 実装前に固定する。
/// </summary>
public class TrackerComparisonSourceTddTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public TrackerComparisonSourceTddTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// 何を確認しているか: sidecar reader で raw payload を復元し、official tracker packet として再decodeできることを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotSidecar_RoundTripPayload_RestoresRawTrackerPacketForReplayDecode()
    {
        var packet = CreatePacket(
            sourceUuid: "thirdparty-a-uuid",
            sourceName: "thirdparty-a-source",
            frameNumber: 4401,
            timestampNs: 12_345_000_000,
            role: "external");
        var payload = packet.ToByteArray();
        var sidecarPath = WriteSidecarRecord(packet, payload, role: "external");

        var record = Assert.Single(TrackerPacketSnapshotLogReader.ReadRecords(sidecarPath));
        var restoredPayload = Convert.FromBase64String(record.PayloadBase64);
        var decoded = TrackerWrapperPacket.Parser.ParseFrom(restoredPayload);

        Assert.Equal(payload, restoredPayload);
        Assert.Equal("thirdparty-a-uuid", decoded.Uuid);
        Assert.Equal("thirdparty-a-source", decoded.SourceName);
        Assert.Equal(4401u, decoded.TrackedFrame.FrameNumber);
        Assert.Equal(2, decoded.TrackedFrame.Balls.Count);
        Assert.Equal(2, decoded.TrackedFrame.Robots.Count);
    }

    /// <summary>
    /// 何を確認しているか: CaptureOn sidecar 追記用 writer が存在し、flush 可能な保存経路を持つことを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotSidecar_WriterContract_ExistsForCaptureOnSidecarPersistence()
    {
        var writerType = GetRequiredServerType("Tracker.Server.Tracking.TrackerPacketSnapshotLogWriter");
        var recordType = GetRequiredServerType("Tracker.Server.Tracking.TrackerPacketSnapshotRecord");

        Assert.True(
            typeof(IDisposable).IsAssignableFrom(writerType) ||
            writerType.GetMethod("Flush", BindingFlags.Public | BindingFlags.Instance) is not null,
            "Tracker packet snapshot writer must expose a flush/dispose path so CaptureOn can persist sidecar records.");
        Assert.Contains(
            writerType.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method =>
            {
                var parameters = method.GetParameters();
                return (string.Equals(method.Name, "Append", StringComparison.Ordinal) ||
                        string.Equals(method.Name, "WriteRecord", StringComparison.Ordinal)) &&
                    parameters.Length >= 1 &&
                    parameters[0].ParameterType == recordType;
            });
    }

    /// <summary>
    /// 何を確認しているか: writer が own / external / unknown を sidecar に保存し、metadata の record/source 集計を更新することを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotSidecar_WriterAppendsRecordsAndUpdatesMetadataSourceSummary()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-comparison-writer-{Guid.NewGuid():N}");
        var session = CreateCaptureSession(captureDirectory);
        using var writer = new TrackerPacketSnapshotLogWriter(
            session,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);
        var receivedAt = new DateTimeOffset(2026, 5, 12, 12, 25, 0, TimeSpan.Zero);

        writer.CapturePacket(
            CreatePacket("ibis-runtime-uuid", "ibis-runtime-source", 4501, 12_500_000_000, "own"),
            receivedAt,
            remoteEndpoint: "self",
            sourceRole: "own");
        writer.CapturePacket(
            CreatePacket("thirdparty-d-uuid", "thirdparty-d-source", 4502, 12_501_000_000, "external"),
            receivedAt.AddMilliseconds(1),
            remoteEndpoint: "192.0.2.10:12010",
            sourceRole: "external");
        writer.CapturePacket(
            CreatePacket(string.Empty, string.Empty, 4503, 12_502_000_000, "unknown"),
            receivedAt.AddMilliseconds(2),
            remoteEndpoint: "192.0.2.11:12011",
            sourceRole: "unknown");
        writer.Flush();

        var sidecarPath = Assert.Single(Directory.GetFiles(
            captureDirectory,
            TrackerPacketSnapshotLogReader.SidecarFileName,
            SearchOption.AllDirectories));
        var metadataPath = Assert.Single(Directory.GetFiles(captureDirectory, "test-vision-*.metadata.json", SearchOption.AllDirectories));
        var records = TrackerPacketSnapshotLogReader.ReadRecords(sidecarPath).ToArray();
        using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        var root = metadata.RootElement;
        var snapshotLog = root.GetProperty("TrackerSnapshotLog");
        var sources = root.GetProperty("TrackerSnapshotSources").EnumerateArray().ToArray();

        Assert.Equal(["own", "external", "unknown"], records.Select(record => record.SourceRole).ToArray());
        Assert.All(records, record => Assert.NotNull(record.SemanticSummary));
        Assert.Equal(3, snapshotLog.GetProperty("RecordCount").GetInt32());
        Assert.Equal(0, snapshotLog.GetProperty("SkippedRecordCount").GetInt32());
        Assert.Equal(0, snapshotLog.GetProperty("ErrorCount").GetInt32());
        Assert.Equal(3, sources.Length);
        Assert.Equal(
            ["external", "own", "unknown"],
            sources.Select(source => source.GetProperty("SourceRole").GetString() ?? string.Empty).ToArray());
        Assert.All(sources, source => Assert.Equal(1, source.GetProperty("RecordCount").GetInt32()));
    }

    /// <summary>
    /// 何を確認しているか: 壊れた raw payload は sidecar record にせず、skipped/error count として metadata に残ることを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotSidecar_WriterTracksSkippedErrorCountForInvalidPayload()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-comparison-writer-error-{Guid.NewGuid():N}");
        var session = CreateCaptureSession(captureDirectory);
        using var writer = new TrackerPacketSnapshotLogWriter(
            session,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);

        var captured = writer.TryCapturePayload(
            [0x01, 0x02, 0x03],
            new DateTimeOffset(2026, 5, 12, 12, 26, 0, TimeSpan.Zero),
            remoteEndpoint: "192.0.2.12:12012");
        writer.Flush();

        var metadataPath = Assert.Single(Directory.GetFiles(captureDirectory, "test-vision-*.metadata.json", SearchOption.AllDirectories));
        using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        var snapshotLog = metadata.RootElement.GetProperty("TrackerSnapshotLog");

        Assert.False(captured);
        Assert.Equal(0, snapshotLog.GetProperty("RecordCount").GetInt32());
        Assert.Equal(1, snapshotLog.GetProperty("SkippedRecordCount").GetInt32());
        Assert.Equal(1, snapshotLog.GetProperty("ErrorCount").GetInt32());
        Assert.Empty(Directory.GetFiles(captureDirectory, TrackerPacketSnapshotLogReader.SidecarFileName, SearchOption.AllDirectories));
    }

    /// <summary>
    /// 何を確認しているか: raw payload 由来の ball / robot / tracked frame / source summary を構造化して比較入力にできることを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotSidecar_RecordContract_KeepsRawDerivedSemanticSummary()
    {
        var recordType = GetRequiredServerType("Tracker.Server.Tracking.TrackerPacketSnapshotRecord");
        var summaryProperty = GetRequiredProperty(recordType, "SemanticSummary");
        var summaryType = summaryProperty.PropertyType;

        GetRequiredProperty(summaryType, "BallCount", typeof(int));
        GetRequiredProperty(summaryType, "RobotCount", typeof(int));
        GetRequiredProperty(summaryType, "TrackedFrameNumber", typeof(uint));
        GetRequiredProperty(summaryType, "TrackedFrameTimestampNs", typeof(long));
        GetRequiredProperty(summaryType, "SourceUuid", typeof(string));
        GetRequiredProperty(summaryType, "SourceName", typeof(string));
        GetRequiredProperty(summaryType, "SourceRole", typeof(string));
        var robotsProperty = GetRequiredProperty(summaryType, "Robots");
        Assert.True(
            typeof(System.Collections.IEnumerable).IsAssignableFrom(robotsProperty.PropertyType),
            "SemanticSummary.Robots must expose team/id/representative position entries for comparison.");
    }

    /// <summary>
    /// 何を確認しているか: own / external / unknown の全 tracker packet が比較元データとして sidecar から落ちないことを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotSidecar_ReaderKeepsOwnExternalAndUnknownComparisonSources()
    {
        var records = new (TrackerWrapperPacket Packet, byte[] Payload, string Role, string RemoteEndpoint)[]
        {
            CreateSidecarInput(
                CreatePacket("ibis-runtime-uuid", "ibis-runtime-source", 4410, 12_350_000_000, "own"),
                "own",
                "192.0.2.10:12010"),
            CreateSidecarInput(
                CreatePacket("thirdparty-b-uuid", "thirdparty-b-source", 4411, 12_351_000_000, "external"),
                "external",
                "192.0.2.11:12011"),
            CreateSidecarInput(
                CreatePacket(string.Empty, string.Empty, 4412, 12_352_000_000, "unknown"),
                "unknown",
                "192.0.2.12:12012"),
        };
        var sidecarPath = WriteSidecarRecords(records);

        var readRecords = TrackerPacketSnapshotLogReader.ReadRecords(sidecarPath).ToArray();

        Assert.Equal(3, readRecords.Length);
        Assert.Equal(["own", "external", "unknown"], readRecords.Select(record => record.SourceRole).ToArray());
        Assert.All(readRecords, record => Assert.False(string.IsNullOrWhiteSpace(record.PayloadBase64)));
    }

    /// <summary>
    /// 何を確認しているか: 同一 uuid 衝突時も source identity を潰さず、通常経路に必要な最小識別情報を保持することを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotSidecar_ReaderKeepsSameUuidCollisionSourcesDistinct()
    {
        var first = CreatePacket("shared-uuid", "thirdparty-c-source", 4420, 12_360_000_000, "external");
        var second = CreatePacket("shared-uuid", "ambiguous-source", 4421, 12_361_000_000, "ambiguous");
        var sidecarPath = WriteSidecarRecords(
        [
            CreateSidecarInput(first, "external", "192.0.2.10:12010"),
            CreateSidecarInput(second, "ambiguous", "192.0.2.11:12011"),
        ]);

        var records = TrackerPacketSnapshotLogReader.ReadRecords(sidecarPath).ToArray();

        Assert.Equal(2, records.Length);
        Assert.Equal(["thirdparty-c-source", "ambiguous-source"], records.Select(record => record.SourceName).ToArray());
        Assert.Equal(["192.0.2.10:12010", "192.0.2.11:12011"], records.Select(record => record.RemoteEndpoint).ToArray());
        Assert.All(records, record => Assert.Equal("shared-uuid", record.SourceUuid));
    }

    private TrackerWrapperPacket CreatePacket(
        string sourceUuid,
        string sourceName,
        uint frameNumber,
        long timestampNs,
        string role)
    {
        var frame = fixture.CreateFrame(
            frameNumber: frameNumber,
            dataTimestampNs: timestampNs,
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
        var packet = fixture.CreatePacketGenerator(sourceName, sourceUuid).Generate(frame);
        Assert.False(string.IsNullOrWhiteSpace(role));
        return packet;
    }

    private static string WriteSidecarRecord(TrackerWrapperPacket packet, byte[] payload, string role)
    {
        return WriteSidecarRecords([(packet, payload, role, "192.0.2.10:12010")]);
    }

    private static string WriteSidecarRecords(
        IReadOnlyList<(TrackerWrapperPacket Packet, byte[] Payload, string Role, string RemoteEndpoint)> records)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tracker-comparison-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var sidecarPath = Path.Combine(directory, TrackerPacketSnapshotLogReader.SidecarFileName);
        var lines = records.Select(record => JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            ReceivedAt = DateTimeOffset.UtcNow,
            record.RemoteEndpoint,
            SourceUuid = record.Packet.Uuid,
            SourceName = record.Packet.SourceName,
            SourceRole = record.Role,
            SourceLabel = string.IsNullOrWhiteSpace(record.Packet.SourceName) ? record.Role : record.Packet.SourceName,
            TrackedFrameNumber = record.Packet.TrackedFrame.FrameNumber,
            TrackedFrameTimestampNs = (long)(record.Packet.TrackedFrame.Timestamp * 1_000_000_000L),
            Summary = "display-only summary must not be the only comparison source",
            PayloadBase64 = Convert.ToBase64String(record.Payload),
        }));
        File.WriteAllLines(sidecarPath, lines);
        return sidecarPath;
    }

    private static (TrackerWrapperPacket Packet, byte[] Payload, string Role, string RemoteEndpoint) CreateSidecarInput(
        TrackerWrapperPacket packet,
        string role,
        string remoteEndpoint)
    {
        return (packet, packet.ToByteArray(), role, remoteEndpoint);
    }

    private VisionPacketCaptureSession CreateCaptureSession(string captureDirectory)
    {
        return new VisionPacketCaptureSession(
            Options.Create(new VisionReceiverOptions
            {
                PacketCapture = new VisionPacketCaptureOptions
                {
                    Enabled = true,
                    DirectoryPath = captureDirectory,
                    FilePrefix = "test-vision",
                    FlushEachPacket = true,
                },
            }),
            Options.Create(new TrackerOptions
            {
                ActiveProfileName = "sim",
            }),
            fixture.CreateResolvedOptions(fixture.CreateSettings(profileName: "sim")),
            NullLogger<VisionPacketCaptureSession>.Instance);
    }

    private static Type GetRequiredServerType(string fullName)
    {
        var type = typeof(TrackerDiagnosticsLogReader).Assembly.GetType(fullName, throwOnError: false);
        Assert.NotNull(type);
        return type!;
    }

    private static PropertyInfo GetRequiredProperty(Type type, string propertyName, Type? propertyType = null)
    {
        var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        if (propertyType is not null)
        {
            Assert.Equal(propertyType, property!.PropertyType);
        }

        return property!;
    }
}
