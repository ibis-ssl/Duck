using System.Net;
using Microsoft.Extensions.Options;

namespace Tracker.Server.Vision;

public sealed class VisionPacketCaptureWriter : IDisposable
{
    private readonly object gate = new();
    private readonly VisionPacketCaptureOptions options;
    private readonly ILogger<VisionPacketCaptureWriter> logger;
    private StreamWriter? writer;
    private string? capturePath;
    private bool writeFailed;

    public VisionPacketCaptureWriter(
        IOptions<VisionReceiverOptions> options,
        ILogger<VisionPacketCaptureWriter> logger)
    {
        this.options = options.Value.PacketCapture;
        this.logger = logger;
    }

    public string? CapturePath
    {
        get
        {
            lock (gate)
            {
                return capturePath;
            }
        }
    }

    public void Capture(ReadOnlySpan<byte> payload, EndPoint remoteEndpoint, DateTimeOffset receivedAt)
    {
        if (!options.Enabled || writeFailed)
        {
            return;
        }

        lock (gate)
        {
            try
            {
                EnsureWriter(receivedAt);
                VisionPacketCaptureFile.WriteRecord(writer!, receivedAt, remoteEndpoint, payload);
                if (options.FlushEachPacket)
                {
                    writer!.Flush();
                }
            }
            catch (Exception ex)
            {
                writeFailed = true;
                logger.LogWarning(ex, "Failed to write SSL-Vision packet capture {CapturePath}", capturePath);
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            writer?.Dispose();
            writer = null;
        }
    }

    private void EnsureWriter(DateTimeOffset receivedAt)
    {
        if (writer is not null)
        {
            return;
        }

        capturePath = VisionPacketCaptureFile.BuildCapturePath(options, receivedAt);
        writer = VisionPacketCaptureFile.CreateWriter(capturePath);
        logger.LogInformation("Writing SSL-Vision packet capture to {CapturePath}", capturePath);
    }
}
