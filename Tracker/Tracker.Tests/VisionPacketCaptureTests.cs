using System.Net;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tracker.Core;
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
        var writer = new VisionPacketCaptureWriter(
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
        var record = Assert.Single(VisionPacketCaptureFile.ReadRecords(capturePath));
        var replayedPacket = record.ParsePacket();

        Assert.Equal(receivedAt, record.ReceivedAt);
        Assert.Equal("127.0.0.1:10020", record.RemoteEndpoint);
        Assert.Equal(packet.ToByteArray(), record.Payload);
        Assert.Equal((uint)123, replayedPacket.Detection.FrameNumber);
        Assert.Equal((uint)1, replayedPacket.Detection.CameraId);
        Assert.Single(replayedPacket.Detection.Balls);
    }

    [Fact]
    public void ReadRecords_CanReplayCapturedPacketsThroughTrackerEngine()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-capture-replay-{Guid.NewGuid():N}");
        var writer = new VisionPacketCaptureWriter(
            Options.Create(new VisionReceiverOptions
            {
                PacketCapture = new VisionPacketCaptureOptions
                {
                    Enabled = true,
                    DirectoryPath = captureDirectory,
                    FilePrefix = "replay-vision",
                },
            }),
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
            Options.Create(new VisionReceiverOptions
            {
                PacketCapture = new VisionPacketCaptureOptions
                {
                    Enabled = false,
                    DirectoryPath = captureDirectory,
                    FilePrefix = "test-vision",
                },
            }),
            NullLogger<VisionPacketCaptureWriter>.Instance);

        writer.Capture([1, 2, 3], new IPEndPoint(IPAddress.Loopback, 10020), DateTimeOffset.UtcNow);

        Assert.False(Directory.Exists(captureDirectory));
        Assert.Null(writer.CapturePath);
    }
}
