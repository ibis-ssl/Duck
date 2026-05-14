using System.Net;
using System.Reflection;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Tracker.Core;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: CaptureOn 比較ログの session folder、metadata relative path、tracker snapshot sidecar / reader contract を検証する。
/// </summary>
public class TrackerCaptureOnSessionSnapshotContractTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;
    private readonly TrackerCoordinatorTestFactory factory;

    public TrackerCaptureOnSessionSnapshotContractTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
        factory = new TrackerCoordinatorTestFactory(fixture);
    }

    /// <summary>
    /// 何を確認しているか: 同一 CaptureOn session の成果物が一つの session folder 配下にまとまり、metadata から相対 path で辿れることを確認する。
    /// </summary>
    [Fact]
    public void CaptureOnSession_MetadataListsRelativePathsUnderOneSessionFolder()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-capture-session-{Guid.NewGuid():N}");
        var captureSession = factory.CreateCaptureSession(captureDirectory);
        using var packetWriter = new VisionPacketCaptureWriter(
            captureSession,
            NullLogger<VisionPacketCaptureWriter>.Instance);
        using var renderSnapshotWriter = new TrackerRenderSnapshotCaptureWriter(
            captureSession,
            NullLogger<TrackerRenderSnapshotCaptureWriter>.Instance);
        var coordinator = CreateCoordinator(captureSession, renderSnapshotWriter);
        var receivedAt = new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero);

        packetWriter.Capture([1, 2, 3], Endpoint(10020), receivedAt);
        _ = coordinator.ProcessPacket(CreateDetectionPacket(frameNumber: 10), receivedAt);

        packetWriter.Dispose();
        renderSnapshotWriter.Dispose();

        var metadataPath = Assert.Single(Directory.GetFiles(captureDirectory, "*.metadata.json", SearchOption.AllDirectories));
        using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        var root = metadata.RootElement;
        var sessionFolder = GetRequiredString(root, "SessionFolder");

        Assert.False(Path.IsPathRooted(sessionFolder));

        var sessionFolderPath = Path.GetFullPath(Path.Combine(captureDirectory, sessionFolder));
        Assert.True(Directory.Exists(sessionFolderPath));
        Assert.StartsWith(
            Path.GetFullPath(captureDirectory) + Path.DirectorySeparatorChar,
            sessionFolderPath,
            StringComparison.Ordinal);

        AssertArtifactPath(root, captureDirectory, sessionFolder, "PacketPath", mustExist: true);
        AssertArtifactPath(root, captureDirectory, sessionFolder, "MetadataPath", mustExist: true);
        AssertArtifactPath(root, captureDirectory, sessionFolder, "DiagnosticsLogPath", mustExist: true);
        AssertArtifactPath(root, captureDirectory, sessionFolder, "RenderSnapshotPath", mustExist: true);
        AssertArtifactPath(root, captureDirectory, sessionFolder, "TrackerSnapshotSidecarPath", mustExist: false);
        AssertArtifactPath(root, captureDirectory, sessionFolder, "TrackerSnapshotAlignmentPath", mustExist: false);

        var snapshotLog = root.GetProperty("TrackerSnapshotLog");
        Assert.Equal("jsonl", GetRequiredString(snapshotLog, "Format"));
        Assert.False(snapshotLog.GetProperty("IsCreated").GetBoolean());
        Assert.Equal(0, snapshotLog.GetProperty("RecordCount").GetInt32());

        var alignmentLog = root.GetProperty("TrackerSnapshotAlignmentLog");
        Assert.Equal("jsonl", GetRequiredString(alignmentLog, "Format"));
        Assert.False(alignmentLog.GetProperty("IsCreated").GetBoolean());
        Assert.Equal(0, alignmentLog.GetProperty("RecordCount").GetInt32());

        var sources = root.GetProperty("TrackerSnapshotSources").EnumerateArray().ToArray();
        Assert.Empty(sources);
    }

    /// <summary>
    /// 何を確認しているか: Capture Off / 再On で前 session folder へ追記せず、新しい session folder に切り替わることを確認する。
    /// </summary>
    [Fact]
    public void CaptureOnSession_ReenabledCaptureCreatesDifferentSessionFolder()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-capture-session-reenabled-{Guid.NewGuid():N}");
        var runtimeControl = new VisionPacketCaptureRuntimeControl(initialEnabled: true);
        var captureSession = factory.CreateCaptureSession(captureDirectory, runtimeControl: runtimeControl);
        using var packetWriter = new VisionPacketCaptureWriter(
            captureSession,
            NullLogger<VisionPacketCaptureWriter>.Instance);
        var remoteEndpoint = Endpoint(10020);

        packetWriter.Capture([1], remoteEndpoint, new DateTimeOffset(2026, 5, 12, 12, 5, 0, TimeSpan.Zero));
        runtimeControl.SetEnabled(false);
        packetWriter.Stop();
        runtimeControl.SetEnabled(true);
        packetWriter.Capture([2], remoteEndpoint, new DateTimeOffset(2026, 5, 12, 12, 6, 0, TimeSpan.Zero));
        packetWriter.Dispose();

        var metadataPaths = Directory.GetFiles(captureDirectory, "*.metadata.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var sessionFolders = metadataPaths
            .Select(ReadSessionFolder)
            .ToArray();

        Assert.Equal(2, sessionFolders.Length);
        Assert.Equal(2, sessionFolders.Distinct(StringComparer.Ordinal).Count());
        Assert.All(sessionFolders, sessionFolder => Assert.False(Path.IsPathRooted(sessionFolder)));
        Assert.All(
            sessionFolders,
            sessionFolder => Assert.True(Directory.Exists(Path.Combine(captureDirectory, sessionFolder))));
    }

    /// <summary>
    /// 何を確認しているか: tracker snapshot sidecar が own / external / unknown を保存対象として扱える record contract を持つことを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotSidecar_RecordContractAcceptsOwnExternalAndUnknownSources()
    {
        var recordType = GetRequiredServerType("Tracker.DebugHost.Tracking.TrackerPacketSnapshotRecord");
        var sourceRoleProperty = GetRequiredProperty(recordType, "SourceRole", typeof(string));
        var requiredRoles = new[] { "own", "external", "unknown" };

        Assert.Equal("SourceRole", sourceRoleProperty.Name);
        Assert.All(requiredRoles, role => Assert.False(string.IsNullOrWhiteSpace(role)));
    }

    /// <summary>
    /// 何を確認しているか: tracker snapshot record が replay に必要な source identity、受信時刻、tracked frame、summary、payload 復元情報を持つことを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotSidecar_RecordContractKeepsReplayRequiredFields()
    {
        var recordType = GetRequiredServerType("Tracker.DebugHost.Tracking.TrackerPacketSnapshotRecord");

        GetRequiredProperty(recordType, "ReceivedAt", typeof(DateTimeOffset));
        GetRequiredProperty(recordType, "RemoteEndpoint", typeof(string));
        GetRequiredProperty(recordType, "SourceUuid", typeof(string));
        GetRequiredProperty(recordType, "SourceName", typeof(string));
        GetRequiredProperty(recordType, "SourceRole", typeof(string));
        GetRequiredProperty(recordType, "SourceLabel", typeof(string));
        GetRequiredProperty(recordType, "TrackedFrameNumber", typeof(uint));
        GetRequiredProperty(recordType, "TrackedFrameTimestampNs", typeof(long));
        GetRequiredProperty(recordType, "Summary", typeof(string));
        Assert.True(
            recordType.GetProperty("PayloadBase64") is not null ||
            recordType.GetProperty("PayloadRelativePath") is not null ||
            recordType.GetProperty("ReplayData") is not null,
            "TrackerPacketSnapshotRecord must keep raw payload restoration data or replay data.");
    }

    /// <summary>
    /// 何を確認しているか: snapshot log reader が session folder 内の sidecar を読み、後続 replay / diagnostics / playback の入力を返せることを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotLogReader_ReadsSessionSidecarAsReplayInput()
    {
        var readerType = GetRequiredServerType("Tracker.DebugHost.Tracking.TrackerPacketSnapshotLogReader");
        var method = readerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, "ReadSession", StringComparison.Ordinal) ||
                string.Equals(candidate.Name, "ReadRecords", StringComparison.Ordinal));

        Assert.NotNull(method);
        Assert.NotEqual(typeof(void), method!.ReturnType);
    }

    /// <summary>
    /// 何を確認しているか: alignment sidecar writer が snapshot sidecar と別 file に source key と capture-time 対応を保存し、metadata から辿れることを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotAlignmentWriter_WritesSeparateSidecarAndMetadata()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-alignment-session-{Guid.NewGuid():N}");
        var captureSession = factory.CreateCaptureSession(captureDirectory);
        using var snapshotWriter = new TrackerPacketSnapshotLogWriter(
            captureSession,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);
        using var alignmentWriter = new TrackerSnapshotAlignmentLogWriter(
            captureSession,
            snapshotWriter,
            NullLogger<TrackerSnapshotAlignmentLogWriter>.Instance);
        var receivedAt = new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero);
        var ownFrame = fixture.CreateFrame(
            frameNumber: 1200,
            dataTimestampNs: 81_686_200_000_000,
            balls: [fixture.CreateTrackedBall(trackId: 1, xMm: 100, yMm: 200)],
            robots: [],
            primaryBallTrackId: 1);
        var ownPacket = fixture.CreatePacketGenerator("ibis", "ibis-runtime").Generate(ownFrame);
        var externalFrame = fixture.CreateFrame(
            frameNumber: 2200,
            dataTimestampNs: 1_778_620_919_000_000_000,
            balls: [fixture.CreateTrackedBall(trackId: 2, xMm: 300, yMm: 400)],
            robots: [],
            primaryBallTrackId: 2);
        var externalPacket = fixture.CreatePacketGenerator("ER-FORCE", "er-force-uuid").Generate(externalFrame);

        snapshotWriter.CapturePacket(ownPacket, receivedAt, remoteEndpoint: null, sourceRole: "own", sourceLabel: "ibis");
        snapshotWriter.CapturePacket(
            externalPacket,
            receivedAt.AddMilliseconds(3),
            remoteEndpoint: "192.0.2.50:12010",
            sourceRole: "external",
            sourceLabel: "ER-FORCE");
        alignmentWriter.CaptureRenderSnapshot(ownFrame, receivedAt.AddMilliseconds(5));
        alignmentWriter.CaptureDiagnosticsEntry(ownFrame, receivedAt.AddMilliseconds(5));
        snapshotWriter.Flush();
        alignmentWriter.Flush();

        var metadataPath = Assert.Single(Directory.GetFiles(captureDirectory, "*.metadata.json", SearchOption.AllDirectories));
        using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        var root = metadata.RootElement;
        var sessionFolder = GetRequiredString(root, "SessionFolder");
        AssertArtifactPath(root, captureDirectory, sessionFolder, "TrackerSnapshotSidecarPath", mustExist: true);
        AssertArtifactPath(root, captureDirectory, sessionFolder, "TrackerSnapshotAlignmentPath", mustExist: true);
        Assert.True(root.GetProperty("TrackerSnapshotAlignmentLog").GetProperty("RecordCount").GetInt32() >= 2);

        var alignmentPath = Path.Combine(
            captureDirectory,
            GetRequiredString(root, "TrackerSnapshotAlignmentPath"));
        var externalAlignment = TrackerSnapshotAlignmentLogReader.ReadRecords(alignmentPath)
            .First(record => record.SourceLabel == "ER-FORCE" && record.RenderFrameNumber is not null);

        Assert.Equal(TrackerSnapshotAlignmentRecord.SavedSessionAlignmentRule, externalAlignment.MatchingRule);
        Assert.Equal("external", externalAlignment.SourceRole);
        Assert.Equal("er-force-uuid", externalAlignment.SourceUuid);
        Assert.Equal("192.0.2.50:12010", externalAlignment.RemoteEndpoint);
        Assert.Equal(1200u, externalAlignment.RenderFrameNumber);
        Assert.Equal(2200u, externalAlignment.TrackerSnapshotTrackedFrameNumber);
        Assert.True(externalAlignment.ReceivedAtDeltaTicks <= TimeSpan.FromMilliseconds(10).Ticks);
    }

    /// <summary>
    /// 何を確認しているか: alignment sidecar が diagnostics line 数ではなく最速 tracker source cadence の record を保存することを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotAlignmentWriter_WritesFastestSourceCadenceRecordsWithHeldRenderFrame()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-alignment-fastest-{Guid.NewGuid():N}");
        var captureSession = factory.CreateCaptureSession(captureDirectory);
        using var snapshotWriter = new TrackerPacketSnapshotLogWriter(
            captureSession,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);
        using var alignmentWriter = new TrackerSnapshotAlignmentLogWriter(
            captureSession,
            snapshotWriter,
            NullLogger<TrackerSnapshotAlignmentLogWriter>.Instance);
        var receivedAt = new DateTimeOffset(2026, 5, 13, 9, 0, 0, TimeSpan.Zero);
        var render0 = fixture.CreateFrame(
            frameNumber: 1000,
            dataTimestampNs: 81_686_200_000_000,
            balls: [fixture.CreateTrackedBall(trackId: 1, xMm: 100, yMm: 200)],
            robots: [],
            primaryBallTrackId: 1);
        var render100 = fixture.CreateFrame(
            frameNumber: 1100,
            dataTimestampNs: 81_686_200_100_000,
            balls: [fixture.CreateTrackedBall(trackId: 1, xMm: 110, yMm: 210)],
            robots: [],
            primaryBallTrackId: 1);

        alignmentWriter.CaptureRenderSnapshot(render0, receivedAt);
        alignmentWriter.CaptureDiagnosticsEntry(render0, receivedAt);
        for (var index = 0; index < 6; index++)
        {
            var offset = TimeSpan.FromMilliseconds(index * 20);
            if (index == 5)
            {
                alignmentWriter.CaptureRenderSnapshot(render100, receivedAt.AddMilliseconds(100));
                alignmentWriter.CaptureDiagnosticsEntry(render100, receivedAt.AddMilliseconds(100));
            }

            var externalFrame = fixture.CreateFrame(
                frameNumber: (uint)(3000 + index),
                dataTimestampNs: 1_778_620_918_834_101_760 + index,
                balls: [fixture.CreateTrackedBall(trackId: index + 10, xMm: 300 + index, yMm: 400 + index)],
                robots: [],
                primaryBallTrackId: index + 10);
            var packet = fixture.CreatePacketGenerator("ER-FORCE", "er-force-uuid").Generate(externalFrame);
            snapshotWriter.CapturePacket(
                packet,
                receivedAt.Add(offset),
                remoteEndpoint: "192.0.2.50:12010",
                sourceRole: "external",
                sourceLabel: "ER-FORCE");
        }

        snapshotWriter.Flush();
        alignmentWriter.Flush();

        var metadataPath = Assert.Single(Directory.GetFiles(captureDirectory, "*.metadata.json", SearchOption.AllDirectories));
        using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        var root = metadata.RootElement;
        var alignmentPath = Path.Combine(
            captureDirectory,
            GetRequiredString(root, "TrackerSnapshotAlignmentPath"));
        var records = TrackerSnapshotAlignmentLogReader.ReadRecords(alignmentPath)
            .Where(record => record.SourceLabel == "ER-FORCE" &&
                record.ReplayTimelineKind == TrackerSnapshotAlignmentRecord.TrackerSnapshotTimelineKind)
            .OrderBy(record => record.ReplayTimelineReceivedAt)
            .ToArray();

        Assert.True(records.Length >= 6, "alignment sidecar must not collapse to the two diagnostics lines.");
        Assert.All(records, record => Assert.Equal(2, record.SchemaVersion));
        Assert.Equal(
            [1000u, 1000u, 1000u, 1000u, 1000u, 1100u],
            records.Take(6).Select(record => record.RenderFrameNumber).ToArray());
        Assert.Equal(
            [3000u, 3001u, 3002u, 3003u, 3004u, 3005u],
            records.Take(6).Select(record => record.TrackerSnapshotTrackedFrameNumber).ToArray());
    }

    /// <summary>
    /// 何を確認しているか: fast tracker tick 上でも append source だけでなく source ごとの latest-before snapshot を保存することを確認する。
    /// </summary>
    [Fact]
    public void TrackerSnapshotAlignmentWriter_WritesLatestSnapshotForEachSourceOnFastTrackerTick()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-alignment-latest-sources-{Guid.NewGuid():N}");
        var captureSession = factory.CreateCaptureSession(captureDirectory);
        using var snapshotWriter = new TrackerPacketSnapshotLogWriter(
            captureSession,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);
        using var alignmentWriter = new TrackerSnapshotAlignmentLogWriter(
            captureSession,
            snapshotWriter,
            NullLogger<TrackerSnapshotAlignmentLogWriter>.Instance);
        var receivedAt = new DateTimeOffset(2026, 5, 13, 9, 0, 0, TimeSpan.Zero);
        var ownFrame = fixture.CreateFrame(
            frameNumber: 4000,
            dataTimestampNs: 81_686_200_000_000,
            balls: [fixture.CreateTrackedBall(trackId: 1, xMm: 100, yMm: 200)],
            robots: [],
            primaryBallTrackId: 1);
        var ownPacket = fixture.CreatePacketGenerator("ibis", "ibis-runtime").Generate(ownFrame);

        snapshotWriter.CapturePacket(
            ownPacket,
            receivedAt,
            remoteEndpoint: "self",
            sourceRole: "own",
            sourceLabel: "ibis");
        alignmentWriter.CaptureRenderSnapshot(ownFrame, receivedAt);
        alignmentWriter.CaptureDiagnosticsEntry(ownFrame, receivedAt);
        for (var index = 0; index < 3; index++)
        {
            var externalFrame = fixture.CreateFrame(
                frameNumber: (uint)(5000 + index),
                dataTimestampNs: 1_778_620_918_834_101_760 + index,
                balls: [fixture.CreateTrackedBall(trackId: index + 10, xMm: 300 + index, yMm: 400 + index)],
                robots: [],
                primaryBallTrackId: index + 10);
            var externalPacket = fixture.CreatePacketGenerator("ER-FORCE", "er-force-uuid").Generate(externalFrame);
            snapshotWriter.CapturePacket(
                externalPacket,
                receivedAt.AddMilliseconds(index * 20),
                remoteEndpoint: "192.0.2.50:12010",
                sourceRole: "external",
                sourceLabel: "ER-FORCE");
        }

        snapshotWriter.Flush();
        alignmentWriter.Flush();

        var metadataPath = Assert.Single(Directory.GetFiles(captureDirectory, "*.metadata.json", SearchOption.AllDirectories));
        using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        var alignmentPath = Path.Combine(
            captureDirectory,
            GetRequiredString(metadata.RootElement, "TrackerSnapshotAlignmentPath"));
        var fastTick20Records = TrackerSnapshotAlignmentLogReader.ReadRecords(alignmentPath)
            .Where(record => record.ReplayTimelineKind == TrackerSnapshotAlignmentRecord.TrackerSnapshotTimelineKind &&
                record.ReplayTimelineReceivedAt == receivedAt.AddMilliseconds(20))
            .OrderBy(record => record.SourceRole, StringComparer.Ordinal)
            .ThenBy(record => record.SourceLabel, StringComparer.Ordinal)
            .ToArray();
        var fastTick40Records = TrackerSnapshotAlignmentLogReader.ReadRecords(alignmentPath)
            .Where(record => record.ReplayTimelineKind == TrackerSnapshotAlignmentRecord.TrackerSnapshotTimelineKind &&
                record.ReplayTimelineReceivedAt == receivedAt.AddMilliseconds(40))
            .OrderBy(record => record.SourceRole, StringComparer.Ordinal)
            .ThenBy(record => record.SourceLabel, StringComparer.Ordinal)
            .ToArray();

        Assert.Contains(fastTick20Records, record => record.SourceLabel == "ER-FORCE" && record.TrackerSnapshotTrackedFrameNumber == 5001u);
        Assert.Contains(fastTick20Records, record => record.SourceLabel == "ibis" && record.TrackerSnapshotTrackedFrameNumber == 4000u);
        Assert.Contains(fastTick40Records, record => record.SourceLabel == "ER-FORCE" && record.TrackerSnapshotTrackedFrameNumber == 5002u);
        Assert.Contains(fastTick40Records, record => record.SourceLabel == "ibis" && record.TrackerSnapshotTrackedFrameNumber == 4000u);
        Assert.All(
            fastTick20Records.Concat(fastTick40Records),
            record => Assert.Equal(TrackerSnapshotAlignmentRecord.SavedSessionAlignmentRule, record.MatchingRule));
    }

    private TrackerCoordinator CreateCoordinator(
        VisionPacketCaptureSession captureSession,
        TrackerRenderSnapshotCaptureWriter renderSnapshotWriter)
    {
        return factory.CreateCoordinator(
            new TrackedSnapshotStore(),
            new TrackerCoordinatorRecordingTrackerPacketPublisher(),
            [],
            fixture.CreateSettings(profileName: "sim", reorderWindowNs: 0, mergeWindowNs: 0),
            fixture.CreatePublisherOptions(),
            new TrackerDiagnosticsOptions(),
            captureSession,
            renderSnapshotWriter);
    }

    private SSL_WrapperPacket CreateDetectionPacket(uint frameNumber)
    {
        return TrackerContractTestData.CreateDetectionPacket(
            frameNumber: frameNumber,
            cameraId: 1,
            balls: [TrackerContractTestData.CreateBall(x: 100, confidence: 1.0f)],
            captureTimeSeconds: 1.000);
    }

    private static string ReadSessionFolder(string metadataPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
        return GetRequiredString(document.RootElement, "SessionFolder");
    }

    private static void AssertArtifactPath(
        JsonElement metadata,
        string captureDirectory,
        string sessionFolder,
        string propertyName,
        bool mustExist)
    {
        var relativePath = GetRequiredString(metadata, propertyName);
        Assert.False(Path.IsPathRooted(relativePath));
        Assert.StartsWith(sessionFolder + Path.DirectorySeparatorChar, relativePath, StringComparison.Ordinal);

        var fullPath = Path.GetFullPath(Path.Combine(captureDirectory, relativePath));
        Assert.StartsWith(
            Path.GetFullPath(Path.Combine(captureDirectory, sessionFolder)) + Path.DirectorySeparatorChar,
            fullPath,
            StringComparison.Ordinal);
        if (mustExist)
        {
            Assert.True(File.Exists(fullPath), $"{propertyName} must point to an existing session artifact.");
        }
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        Assert.True(element.TryGetProperty(propertyName, out var property), $"metadata must include {propertyName}.");
        var value = property.GetString();
        Assert.False(string.IsNullOrWhiteSpace(value), $"{propertyName} must not be empty.");
        return value!;
    }

    private static Type GetRequiredServerType(string fullName)
    {
        var type = typeof(TrackerDiagnosticsLogReader).Assembly.GetType(fullName, throwOnError: false);
        Assert.NotNull(type);
        return type!;
    }

    private static PropertyInfo GetRequiredProperty(Type type, string propertyName, Type propertyType)
    {
        var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        Assert.Equal(propertyType, property!.PropertyType);
        return property;
    }

    private static IPEndPoint Endpoint(int port)
    {
        return new IPEndPoint(IPAddress.Parse("192.0.2.10"), port);
    }
}
