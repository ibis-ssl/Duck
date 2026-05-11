using System.Net;
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

        var capturePath = Assert.Single(Directory.GetFiles(captureDirectory, "test-vision-*.jsonl.gz"));
        var metadataPath = Assert.Single(Directory.GetFiles(captureDirectory, "test-vision-*.metadata.json"));
        var record = Assert.Single(VisionPacketCaptureFile.ReadRecords(capturePath));
        var replayedPacket = record.ParsePacket();
        using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));

        Assert.Equal(receivedAt, record.ReceivedAt);
        Assert.Equal("127.0.0.1:10020", record.RemoteEndpoint);
        Assert.Equal(packet.ToByteArray(), record.Payload);
        Assert.Equal((uint)123, replayedPacket.Detection.FrameNumber);
        Assert.Equal((uint)1, replayedPacket.Detection.CameraId);
        Assert.Single(replayedPacket.Detection.Balls);
        Assert.Equal(capturePath, metadata.RootElement.GetProperty("PacketPath").GetString());
        Assert.Equal(metadataPath, metadata.RootElement.GetProperty("MetadataPath").GetString());
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

        var capturePath = Assert.Single(Directory.GetFiles(captureDirectory, "replay-vision-*.jsonl.gz"));
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

        var captureFiles = Directory.GetFiles(captureDirectory, "runtime-vision-*.jsonl.gz");

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
}
