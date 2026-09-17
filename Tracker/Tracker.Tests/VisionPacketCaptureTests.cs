using System.Net;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tracker.Core;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Tracker.Tests.Contracts;
using TrackerConnectionLib;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: vision packet capture が replay record の保存、読み戻し、runtime toggle を扱う contract を検証する。
/// </summary>
public class VisionPacketCaptureTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public VisionPacketCaptureTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// 何を確認しているか: capture 有効時に payload、remote endpoint、metadata を gzip replay file として保存することを確認する。
    /// </summary>
    [Fact]
    public void Capture_WhenEnabled_WritesCompressedReplayRecords()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-capture-{Guid.NewGuid():N}");
        var session = CreateCaptureSession(
            captureDirectory,
            filePrefix: "test-vision",
            enabled: true,
            flushEachPacket: true);
        var writer = new VisionPacketCaptureWriter(
            session,
            NullLogger<VisionPacketCaptureWriter>.Instance);
        var receivedAt = new DateTimeOffset(2026, 5, 10, 17, 45, 0, TimeSpan.Zero);
        var packet = new SSL_WrapperPacket
        {
            Detection = new SSL_DetectionFrame
            {
                FrameNumber = 123,
                CameraId = 1,
                Balls =
                {
                    new SSL_DetectionBall
                    {
                        Confidence = 0.9f,
                        X = 100,
                        Y = -200,
                    },
                },
            },
        };

        writer.Capture(packet.ToByteArray(), new IPEndPoint(IPAddress.Loopback, 10020), receivedAt);
        writer.Dispose();

        var capturePath = Assert.Single(Directory.GetFiles(captureDirectory, "test-vision-*.jsonl.gz", SearchOption.AllDirectories));
        var metadataPath = Assert.Single(Directory.GetFiles(captureDirectory, "test-vision-*.metadata.json", SearchOption.AllDirectories));
        var record = Assert.Single(VisionPacketCaptureFile.ReadRecords(capturePath));
        var replayedPacket = record.ParsePacket();
        using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));

        Assert.Equal(receivedAt, record.ReceivedAt);
        Assert.Equal("127.0.0.1:10020", record.RemoteEndpoint);
        Assert.Equal(packet.ToByteArray(), record.Payload);
        Assert.Equal((uint)123, replayedPacket.Detection.FrameNumber);
        Assert.Equal((uint)1, replayedPacket.Detection.CameraId);
        Assert.Single(replayedPacket.Detection.Balls);
        Assert.Equal(Path.GetRelativePath(captureDirectory, capturePath), metadata.RootElement.GetProperty("PacketPath").GetString());
        Assert.Equal(Path.GetRelativePath(captureDirectory, metadataPath), metadata.RootElement.GetProperty("MetadataPath").GetString());
        Assert.EndsWith(
            ".tracker-diagnostics.log",
            metadata.RootElement.GetProperty("DiagnosticsLogPath").GetString(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            ".render-snapshots.jsonl.gz",
            metadata.RootElement.GetProperty("RenderSnapshotPath").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(
            "sim",
            metadata.RootElement
                .GetProperty("ResolvedTrackerOptions")
                .GetProperty("EngineSettings")
                .GetProperty("ProfileName")
                .GetString());
        var simProfile = metadata.RootElement
            .GetProperty("TrackerOptions")
            .GetProperty("Profiles")
            .GetProperty("sim");
        Assert.Equal(11010, simProfile.GetProperty("Publish").GetProperty("Port").GetInt32());
        Assert.Equal(
            0.85,
            simProfile.GetProperty("BallTracker").GetProperty("Gate").GetDouble(),
            precision: 3);
    }

    /// <summary>
    /// 何を確認しているか: capture 有効時に diagnostics sample sidecar と metadata 集計を保存することを確認する。
    /// </summary>
    [Fact]
    public void Capture_WhenEnabled_WritesDiagnosticsSampleSidecarMetadataAndRecords()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-diagnostics-sample-{Guid.NewGuid():N}");
        var session = CreateCaptureSession(
            captureDirectory,
            filePrefix: "sample-vision",
            enabled: true,
            flushEachPacket: true);
        using var writer = new DiagnosticsSampleLogWriter(
            session,
            NullLogger<DiagnosticsSampleLogWriter>.Instance);
        var rawStore = new VisionPacketStore();
        var trackedStore = new TrackedSnapshotStore();
        var externalStore = new ExternalTrackerSnapshotStore(new MultiTrackerManager<TrackerPacketAdapter>("ibis-uuid", "ibis"));
        var provider = new VisionLiveDisplaySnapshotProvider(
            rawStore,
            trackedStore,
            externalStore,
            new VisionLiveComparisonSnapshotComposer());
        var receivedAt = new DateTimeOffset(2026, 5, 14, 19, 30, 0, TimeSpan.Zero);

        rawStore.StorePacket(
            CreateRawPacket(cameraId: 1, frameNumber: 3000),
            new IPEndPoint(IPAddress.Loopback, 10020),
            receivedAt);
        trackedStore.UpdateLatestFrame(
            fixture.CreateFrame(
                frameNumber: 4000,
                dataTimestampNs: 4_000_000,
                balls: [fixture.CreateTrackedBall(trackId: 1, xMm: 10, yMm: 20)]),
            receivedAt.AddMilliseconds(1));
        writer.CaptureSample(provider.CaptureRenderTickSnapshot());

        rawStore.StorePacket(
            CreateRawPacket(cameraId: 1, frameNumber: 3001),
            new IPEndPoint(IPAddress.Loopback, 10020),
            receivedAt.AddMilliseconds(10));
        writer.CaptureSample(provider.CaptureRenderTickSnapshot());

        writer.Flush();

        var metadataPath = Assert.Single(Directory.GetFiles(captureDirectory, "sample-vision-*.metadata.json", SearchOption.AllDirectories));
        using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        var samplePath = Assert.Single(Directory.GetFiles(captureDirectory, "diagnostics-samples.jsonl", SearchOption.AllDirectories));
        var records = DiagnosticsSampleLogReader.ReadRecords(samplePath);

        Assert.Equal(
            Path.GetRelativePath(captureDirectory, samplePath),
            metadata.RootElement.GetProperty("DiagnosticsSampleSidecarPath").GetString());
        var log = metadata.RootElement.GetProperty("DiagnosticsSampleLog");
        Assert.Equal("jsonl", log.GetProperty("Format").GetString());
        Assert.True(log.GetProperty("IsCreated").GetBoolean());
        Assert.Equal(2, log.GetProperty("RecordCount").GetInt32());
        Assert.Equal(0, log.GetProperty("SkippedRecordCount").GetInt32());
        Assert.Equal(0, log.GetProperty("ErrorCount").GetInt32());
        Assert.Equal([0, 1], records.Select(record => record.SampleIndex).ToArray());
        Assert.Equal((uint)3000, records[0].RawFrameNumber);
        Assert.Equal((uint)1, records[0].RawCameraId);
        Assert.True(records[0].WorldFrameCommitted);
        Assert.Equal((uint)4000, records[0].RenderFrameNumber);
        Assert.Equal(1, records[0].RawSemanticSummary?.BallCount);
        Assert.Equal(1, records[0].TrackedSemanticSummary?.BallCount);
    }

    /// <summary>
    /// 何を確認しているか: tracker packet 受信が無効な通常構成でも、実metadataの diagnostics sample だけで診断再生を構成できることを確認する。
    /// </summary>
    [Fact]
    public void DiagnosticsReader_WithActualMetadataAndTrackerReceiveDisabled_UsesDiagnosticsSamples()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-diagnostics-reader-disabled-{Guid.NewGuid():N}");
        var session = CreateCaptureSession(
            captureDirectory,
            filePrefix: "reader-disabled",
            enabled: true,
            flushEachPacket: true);
        using var sampleWriter = new DiagnosticsSampleLogWriter(
            session,
            NullLogger<DiagnosticsSampleLogWriter>.Instance);
        var rawStore = new VisionPacketStore();
        var trackedStore = new TrackedSnapshotStore();
        var receivedAt = new DateTimeOffset(2026, 5, 14, 21, 0, 0, TimeSpan.Zero);

        rawStore.StorePacket(
            CreateRawPacket(cameraId: 4, frameNumber: 3300),
            new IPEndPoint(IPAddress.Loopback, 10020),
            receivedAt);
        trackedStore.UpdateLatestFrame(
            fixture.CreateFrame(
                frameNumber: 4300,
                dataTimestampNs: 4_300_000,
                balls: [fixture.CreateTrackedBall(trackId: 1, xMm: 31, yMm: 41)]),
            receivedAt.AddMilliseconds(1));
        sampleWriter.CaptureSample(CreateSnapshotProvider(rawStore, trackedStore).CaptureRenderTickSnapshot());
        sampleWriter.Flush();

        var metadataPath = Assert.Single(Directory.GetFiles(captureDirectory, "reader-disabled-*.metadata.json", SearchOption.AllDirectories));
        using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        Assert.True(metadata.RootElement.GetProperty("DiagnosticsSampleLog").GetProperty("IsCreated").GetBoolean());
        Assert.False(metadata.RootElement.GetProperty("TrackerSnapshotLog").GetProperty("IsCreated").GetBoolean());
        var diagnosticsPath = Path.Combine(
            captureDirectory,
            metadata.RootElement.GetProperty("DiagnosticsLogPath").GetString()!);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();

        var state = reader.Load(
            diagnosticsPath,
            selectedEntry: null,
            TrackerDiagnosticsComparisonSourceFilter.All);

        Assert.Equal(TrackerDiagnosticsComparisonSidecarStatus.Ready, state.SidecarStatus);
        var selectedTimeline = TrackerDiagnosticsReplayTimelineSelection.FromTick(Assert.Single(state.ReplayTimeline));
        var visionFrame = reader.LoadFieldSourceFrame(
            diagnosticsPath,
            selectedEntry: null,
            selectedTimeline,
            TrackerDiagnosticsFieldSource.VisionInput);
        var ibisFrame = reader.LoadFieldSourceFrame(
            diagnosticsPath,
            selectedEntry: null,
            selectedTimeline,
            TrackerDiagnosticsFieldSource.IbisTracker);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, visionFrame.Status);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, ibisFrame.Status);
        Assert.Equal("diagnostics-sample-sidecar", visionFrame.MatchingRule);
        Assert.Equal("diagnostics-sample-sidecar", ibisFrame.MatchingRule);
        Assert.Equal((uint)4300, ibisFrame.TrackedFrameNumber);
    }

    /// <summary>
    /// 何を確認しているか: tracker packet 受信が有効な通常構成でも、ibis tracker は diagnostics sample の同一採取記録を主経路にすることを確認する。
    /// </summary>
    [Fact]
    public void DiagnosticsReader_WithActualMetadataAndTrackerReceiveEnabled_PrefersDiagnosticsSampleForIbisTracker()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-diagnostics-reader-enabled-{Guid.NewGuid():N}");
        var session = CreateCaptureSession(
            captureDirectory,
            filePrefix: "reader-enabled",
            enabled: true,
            flushEachPacket: true);
        using var sampleWriter = new DiagnosticsSampleLogWriter(
            session,
            NullLogger<DiagnosticsSampleLogWriter>.Instance);
        using var snapshotWriter = new TrackerPacketSnapshotLogWriter(
            session,
            NullLogger<TrackerPacketSnapshotLogWriter>.Instance);
        var rawStore = new VisionPacketStore();
        var trackedStore = new TrackedSnapshotStore();
        var receivedAt = new DateTimeOffset(2026, 5, 14, 21, 10, 0, TimeSpan.Zero);

        rawStore.StorePacket(
            CreateRawPacket(cameraId: 5, frameNumber: 3400),
            new IPEndPoint(IPAddress.Loopback, 10020),
            receivedAt);
        trackedStore.UpdateLatestFrame(
            fixture.CreateFrame(
                frameNumber: 4400,
                dataTimestampNs: 4_400_000,
                balls: [fixture.CreateTrackedBall(trackId: 1, xMm: 32, yMm: 42)]),
            receivedAt.AddMilliseconds(1));
        sampleWriter.CaptureSample(CreateSnapshotProvider(rawStore, trackedStore).CaptureRenderTickSnapshot());
        var externalFrame = fixture.CreateFrame(
            frameNumber: 5400,
            dataTimestampNs: 5_400_000,
            balls: [fixture.CreateTrackedBall(trackId: 2, xMm: 320, yMm: 420)]);
        snapshotWriter.CapturePacket(
            fixture.CreatePacketGenerator("ER-FORCE", "er-force-uuid").Generate(externalFrame),
            receivedAt.AddMilliseconds(2),
            remoteEndpoint: "192.0.2.50:12010",
            sourceRole: "external",
            sourceLabel: "ER-FORCE");
        sampleWriter.Flush();
        snapshotWriter.Flush();

        var metadataPath = Assert.Single(Directory.GetFiles(captureDirectory, "reader-enabled-*.metadata.json", SearchOption.AllDirectories));
        using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        Assert.True(metadata.RootElement.GetProperty("DiagnosticsSampleLog").GetProperty("IsCreated").GetBoolean());
        Assert.True(metadata.RootElement.GetProperty("TrackerSnapshotLog").GetProperty("IsCreated").GetBoolean());
        var diagnosticsPath = Path.Combine(
            captureDirectory,
            metadata.RootElement.GetProperty("DiagnosticsLogPath").GetString()!);
        var reader = new TrackerDiagnosticsComparisonViewStateReader();
        var state = reader.Load(
            diagnosticsPath,
            selectedEntry: null,
            TrackerDiagnosticsComparisonSourceFilter.All);
        var selectedTimeline = TrackerDiagnosticsReplayTimelineSelection.FromTick(Assert.Single(state.ReplayTimeline));

        var ibisFrame = reader.LoadFieldSourceFrame(
            diagnosticsPath,
            selectedEntry: null,
            selectedTimeline,
            TrackerDiagnosticsFieldSource.IbisTracker);

        Assert.Equal(TrackerDiagnosticsComparisonSidecarStatus.Ready, state.SidecarStatus);
        Assert.Equal(TrackerDiagnosticsFieldSourceFrameStatus.Ready, ibisFrame.Status);
        Assert.Equal("diagnostics-sample-sidecar", ibisFrame.MatchingRule);
        Assert.Equal((uint)4400, ibisFrame.TrackedFrameNumber);
        Assert.Contains(state.SourceOptions, option => option.Filter == TrackerDiagnosticsComparisonSourceFilter.External);
    }

    /// <summary>
    /// 何を確認しているか: Home UI を介さない DebugHost sample loop が CaptureOn 中に diagnostics sample を保存することを確認する。
    /// </summary>
    [Fact]
    public void DiagnosticsSampleCaptureLoop_WhenCaptureEnabled_WritesSamplesWithoutHomeUi()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-diagnostics-loop-{Guid.NewGuid():N}");
        var runtimeControl = new VisionPacketCaptureRuntimeControl(initialEnabled: false);
        var session = CreateCaptureSession(
            captureDirectory,
            filePrefix: "loop-vision",
            enabled: false,
            flushEachPacket: true,
            runtimeControl: runtimeControl);
        using var writer = new DiagnosticsSampleLogWriter(
            session,
            NullLogger<DiagnosticsSampleLogWriter>.Instance);
        var rawStore = new VisionPacketStore();
        var trackedStore = new TrackedSnapshotStore();
        var loop = new DiagnosticsSampleCaptureLoop(
            runtimeControl,
            CreateSnapshotProvider(rawStore, trackedStore),
            writer);
        var receivedAt = new DateTimeOffset(2026, 5, 14, 20, 0, 0, TimeSpan.Zero);

        loop.CaptureOnce();
        Assert.False(Directory.Exists(captureDirectory));

        runtimeControl.SetEnabled(true);
        rawStore.StorePacket(
            CreateRawPacket(cameraId: 2, frameNumber: 3100),
            new IPEndPoint(IPAddress.Loopback, 10020),
            receivedAt);
        trackedStore.UpdateLatestFrame(
            fixture.CreateFrame(
                frameNumber: 4100,
                dataTimestampNs: 4_100_000,
                balls: [fixture.CreateTrackedBall(trackId: 1, xMm: 11, yMm: 21)]),
            receivedAt.AddMilliseconds(1));

        loop.CaptureOnce();
        writer.Flush();

        var samplePath = Assert.Single(Directory.GetFiles(captureDirectory, "diagnostics-samples.jsonl", SearchOption.AllDirectories));
        var record = Assert.Single(DiagnosticsSampleLogReader.ReadRecords(samplePath));
        Assert.Equal((uint)3100, record.RawFrameNumber);
        Assert.Equal((uint)2, record.RawCameraId);
        Assert.Equal((uint)4100, record.TrackedFrameNumber);
        Assert.Equal(1, record.RawSemanticSummary?.BallCount);
        Assert.Equal(1, record.TrackedSemanticSummary?.BallCount);
    }

    /// <summary>
    /// 何を確認しているか: diagnostics sample writer の停止が shared capture session 全体を止めないことを確認する。
    /// </summary>
    [Fact]
    public void DiagnosticsSampleLogWriterStop_DoesNotStopSharedCaptureSession()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-diagnostics-stop-{Guid.NewGuid():N}");
        var session = CreateCaptureSession(
            captureDirectory,
            filePrefix: "stop-vision",
            enabled: true,
            flushEachPacket: true);
        using var writer = new DiagnosticsSampleLogWriter(
            session,
            NullLogger<DiagnosticsSampleLogWriter>.Instance);
        var rawStore = new VisionPacketStore();
        var trackedStore = new TrackedSnapshotStore();
        var receivedAt = new DateTimeOffset(2026, 5, 14, 20, 10, 0, TimeSpan.Zero);
        rawStore.StorePacket(
            CreateRawPacket(cameraId: 3, frameNumber: 3200),
            new IPEndPoint(IPAddress.Loopback, 10020),
            receivedAt);

        writer.CaptureSample(CreateSnapshotProvider(rawStore, trackedStore).CaptureRenderTickSnapshot());
        Assert.NotNull(session.Current);

        writer.Stop();

        Assert.NotNull(session.Current);
    }

    /// <summary>
    /// 何を確認しているか: 保存済み capture record を読み戻し、TrackerEngine に replay して geometry と detection を再現できることを確認する。
    /// </summary>
    [Fact]
    public void ReadRecords_CanReplayCapturedPacketsThroughTrackerEngine()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-capture-replay-{Guid.NewGuid():N}");
        var writer = new VisionPacketCaptureWriter(
            CreateCaptureSession(captureDirectory, filePrefix: "replay-vision", enabled: true),
            NullLogger<VisionPacketCaptureWriter>.Instance);
        var remoteEndpoint = new IPEndPoint(IPAddress.Loopback, 10020);
        var receivedAt = new DateTimeOffset(2026, 5, 10, 18, 15, 0, TimeSpan.Zero);
        var packets = new[]
        {
            TrackerContractTestData.CreateGeometryPacket(fieldLength: 12000, fieldWidth: 9000),
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 50, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
        };

        foreach (var packet in packets)
        {
            writer.Capture(packet.ToByteArray(), remoteEndpoint, receivedAt);
        }

        writer.Dispose();

        var capturePath = Assert.Single(Directory.GetFiles(captureDirectory, "replay-vision-*.jsonl.gz", SearchOption.AllDirectories));
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);
        var results = VisionPacketCaptureFile.ReadRecords(capturePath)
            .Select(record => engine.Update(packet: record.ParsePacket(), settings: settings))
            .ToArray();

        Assert.Equal(2, results.Length);
        Assert.Empty(results[0].CommittedFrames);

        var committedFrame = Assert.Single(results[1].CommittedFrames);
        var committedBall = Assert.Single(committedFrame.Balls);

        Assert.NotNull(committedFrame.GeometrySnapshot);
        Assert.Equal(12000, committedFrame.GeometrySnapshot!.FieldLengthMm);
        Assert.Equal(9000, committedFrame.GeometrySnapshot.FieldWidthMm);
        Assert.Equal(100, committedBall.XMm, precision: 3);
        Assert.Equal(50, committedBall.YMm, precision: 3);
    }

    /// <summary>
    /// 何を確認しているか: capture 無効時に capture directory と writer path が作られないことを確認する。
    /// </summary>
    [Fact]
    public void Capture_WhenDisabled_DoesNotCreateCaptureFile()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-capture-disabled-{Guid.NewGuid():N}");
        using var writer = new VisionPacketCaptureWriter(
            CreateCaptureSession(captureDirectory, filePrefix: "test-vision", enabled: false),
            NullLogger<VisionPacketCaptureWriter>.Instance);

        writer.Capture([1, 2, 3], new IPEndPoint(IPAddress.Loopback, 10020), DateTimeOffset.UtcNow);

        Assert.False(Directory.Exists(captureDirectory));
        Assert.Null(writer.CapturePath);
    }

    /// <summary>
    /// 何を確認しているか: runtime toggle が初期無効状態を尊重し、有効化後だけ新しい capture file を作ることを確認する。
    /// </summary>
    [Fact]
    public void Capture_RuntimeToggleStartsFromConfiguredDisabledValueAndCreatesFilesAfterEnable()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-capture-runtime-{Guid.NewGuid():N}");
        var runtimeControl = new VisionPacketCaptureRuntimeControl(initialEnabled: false);
        using var writer = new VisionPacketCaptureWriter(
            CreateCaptureSession(
                captureDirectory,
                filePrefix: "runtime-vision",
                enabled: false,
                flushEachPacket: true,
                runtimeControl: runtimeControl),
            NullLogger<VisionPacketCaptureWriter>.Instance);
        var remoteEndpoint = new IPEndPoint(IPAddress.Loopback, 10020);

        writer.Capture([1, 2, 3], remoteEndpoint, new DateTimeOffset(2026, 5, 10, 20, 0, 0, TimeSpan.Zero));
        Assert.False(Directory.Exists(captureDirectory));

        runtimeControl.SetEnabled(true);
        writer.Capture([4, 5, 6], remoteEndpoint, new DateTimeOffset(2026, 5, 10, 20, 0, 1, TimeSpan.Zero));
        runtimeControl.SetEnabled(false);
        writer.Stop();
        runtimeControl.SetEnabled(true);
        writer.Capture([7, 8, 9], remoteEndpoint, new DateTimeOffset(2026, 5, 10, 20, 0, 2, TimeSpan.Zero));

        var captureFiles = Directory.GetFiles(captureDirectory, "runtime-vision-*.jsonl.gz", SearchOption.AllDirectories);

        Assert.Equal(2, captureFiles.Length);
    }

    private VisionPacketCaptureSession CreateCaptureSession(
        string captureDirectory,
        string filePrefix,
        bool enabled,
        bool flushEachPacket = false,
        VisionPacketCaptureRuntimeControl? runtimeControl = null)
    {
        return new VisionPacketCaptureSession(
            Options.Create(new VisionReceiverOptions
            {
                PacketCapture = new VisionPacketCaptureOptions
                {
                    Enabled = enabled,
                    DirectoryPath = captureDirectory,
                    FilePrefix = filePrefix,
                    FlushEachPacket = flushEachPacket,
                },
            }),
            Options.Create(new TrackerOptions
            {
                ActiveProfileName = "sim",
                Profiles = new Dictionary<string, TrackerProfileOptions>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sim"] = new()
                    {
                        Publish = new TrackerPublishProfileOptions
                        {
                            Port = 11010,
                        },
                        BallTracker = new TrackerBallTrackerOverrides
                        {
                            Gate = 0.85,
                        },
                    },
                },
            }),
            fixture.CreateResolvedOptions(fixture.CreateSettings(profileName: "sim")),
            NullLogger<VisionPacketCaptureSession>.Instance,
            runtimeControl);
    }

    private static VisionLiveDisplaySnapshotProvider CreateSnapshotProvider(
        VisionPacketStore rawStore,
        TrackedSnapshotStore trackedStore)
    {
        return new VisionLiveDisplaySnapshotProvider(
            rawStore,
            trackedStore,
            new ExternalTrackerSnapshotStore(new MultiTrackerManager<TrackerPacketAdapter>("ibis-uuid", "ibis")),
            new VisionLiveComparisonSnapshotComposer());
    }

    private static SSL_WrapperPacket CreateRawPacket(uint cameraId, uint frameNumber)
    {
        return new SSL_WrapperPacket
        {
            Detection = new SSL_DetectionFrame
            {
                CameraId = cameraId,
                FrameNumber = frameNumber,
                Balls =
                {
                    new SSL_DetectionBall
                    {
                        Confidence = 0.8f,
                        X = 100,
                        Y = 200,
                    },
                },
            },
        };
    }
}
