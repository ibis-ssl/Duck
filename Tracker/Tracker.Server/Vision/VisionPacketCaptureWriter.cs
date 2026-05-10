using System.Net;

namespace Tracker.Server.Vision;

public sealed class VisionPacketCaptureWriter : IDisposable
{
    private readonly object gate = new();
    private readonly VisionPacketCaptureSession session;
    private readonly ILogger<VisionPacketCaptureWriter> logger;
    private StreamWriter? writer;
    private string? capturePath;
    private bool writeFailed;

    public VisionPacketCaptureWriter(
        VisionPacketCaptureSession session,
        ILogger<VisionPacketCaptureWriter> logger)
    {
        this.session = session;
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
        if (!session.Enabled)
        {
            Stop();
            return;
        }

        if (writeFailed)
        {
            return;
        }

        lock (gate)
        {
            try
            {
                EnsureWriter(receivedAt);
                VisionPacketCaptureFile.WriteRecord(writer!, receivedAt, remoteEndpoint, payload);
                if (session.FlushEachPacket)
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
        Stop();
    }

    public void Stop()
    {
        lock (gate)
        {
            writer?.Dispose();
            writer = null;
            capturePath = null;
            writeFailed = false;
            session.Stop();
        }
    }

    private void EnsureWriter(DateTimeOffset receivedAt)
    {
        if (writer is not null)
        {
            return;
        }

        var sessionState = session.EnsureStarted(receivedAt)
            ?? throw new InvalidOperationException("SSL-Vision packet capture session is disabled.");
        capturePath = sessionState.PacketPath;
        writer = VisionPacketCaptureFile.CreateWriter(capturePath);
        logger.LogInformation(
            "Writing SSL-Vision packet capture to {CapturePath} with metadata {MetadataPath}",
            capturePath,
            sessionState.MetadataPath);
    }

}
