using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Tracker.RuntimeHost;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public sealed class RuntimeVisionReceiverDiagnosticsTddTests
{
    [Fact]
    public void ProcessDatagram_WithValidPacket_StoresPacketAndIncrementsDiagnostics()
    {
        var packetBuffer = new RuntimeVisionPacketBuffer();
        var diagnostics = new RuntimeVisionReceiverDiagnostics();
        var logger = new RecordingLogger();
        var receivedAt = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
        var packet = TrackerContractTestData.CreateDetectionPacket(frameNumber: 42, cameraId: 3);

        var accepted = RuntimeVisionReceiverService.ProcessDatagram(
            packet.ToByteArray(),
            receivedAt,
            packetBuffer,
            diagnostics,
            logger);

        Assert.True(accepted);
        Assert.Equal(1, diagnostics.VisionPacketsReceivedTotal);
        Assert.True(packetBuffer.TryTakeLatestBatch(out var bufferedPackets));
        var buffered = Assert.Single(bufferedPackets);
        Assert.Equal((uint)42, buffered.Packet.Detection.FrameNumber);
        Assert.Equal((uint)3, buffered.Packet.Detection.CameraId);
        Assert.Equal(receivedAt, buffered.ReceivedAt);
    }

    [Fact]
    public void ProcessDatagram_WithDecodeFailure_DoesNotIncrementDiagnosticsOrStorePacket()
    {
        var packetBuffer = new RuntimeVisionPacketBuffer();
        var diagnostics = new RuntimeVisionReceiverDiagnostics();
        var logger = new RecordingLogger();

        var accepted = RuntimeVisionReceiverService.ProcessDatagram(
            [0xFF, 0xFF, 0xFF],
            DateTimeOffset.UtcNow,
            packetBuffer,
            diagnostics,
            logger);

        Assert.False(accepted);
        Assert.Equal(0, diagnostics.VisionPacketsReceivedTotal);
        Assert.False(packetBuffer.TryTakeLatestBatch(out _));
        Assert.Contains(
            logger.Messages,
            message => message.Contains("Failed to decode RuntimeHost SSL-Vision packet.", StringComparison.Ordinal));
    }

    [Fact]
    public void LogDiagnostics_IncludesEndpointInterfacesAndCumulativeCount()
    {
        var logger = new RecordingLogger();

        RuntimeVisionReceiverService.LogDiagnostics(
            logger,
            "224.5.23.2:10020",
            ["192.0.2.10", "192.0.2.11"],
            visionPacketsReceivedTotal: 17);

        var message = Assert.Single(logger.Messages);
        Assert.Contains("224.5.23.2:10020", message, StringComparison.Ordinal);
        Assert.Contains("192.0.2.10, 192.0.2.11", message, StringComparison.Ordinal);
        Assert.Contains("VisionPacketsReceivedTotal=17", message, StringComparison.Ordinal);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
