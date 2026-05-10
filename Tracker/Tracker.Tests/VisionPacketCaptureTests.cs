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

public class VisionPacketCaptureTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public VisionPacketCaptureTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

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
    }

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

    private VisionPacketCaptureSession CreateCaptureSession(
        string captureDirectory,
        string filePrefix,
        bool enabled,
        bool flushEachPacket = false)
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
            Options.Create(new TrackerOptions { ActiveProfileName = "sim" }),
            fixture.CreateResolvedOptions(fixture.CreateSettings(profileName: "sim")),
            NullLogger<VisionPacketCaptureSession>.Instance);
    }
}
