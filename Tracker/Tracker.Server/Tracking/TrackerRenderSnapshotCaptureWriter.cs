using System.Text.Json;
using Tracker.Core;
using Tracker.Server.Vision;

namespace Tracker.Server.Tracking;

public sealed class TrackerRenderSnapshotCaptureWriter : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly object gate = new();
    private readonly VisionPacketCaptureSession session;
    private readonly ILogger<TrackerRenderSnapshotCaptureWriter> logger;
    private StreamWriter? writer;
    private string? capturePath;
    private bool writeFailed;

    public TrackerRenderSnapshotCaptureWriter(
        VisionPacketCaptureSession session,
        ILogger<TrackerRenderSnapshotCaptureWriter> logger)
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

    public void CaptureFrame(TrackerFrame frame, DateTimeOffset receivedAt)
    {
        if (!session.Enabled || writeFailed)
        {
            return;
        }

        lock (gate)
        {
            try
            {
                EnsureWriter(receivedAt);
                var record = new TrackerRenderSnapshotRecord(
                    SchemaVersion: 1,
                    ReceivedAt: receivedAt.ToUniversalTime(),
                    Frame: frame);
                writer!.WriteLine(JsonSerializer.Serialize(record, JsonOptions));
                if (session.FlushEachPacket)
                {
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                writeFailed = true;
                logger.LogWarning(ex, "Failed to write tracker render snapshot capture {CapturePath}", capturePath);
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

        var sessionState = session.EnsureStarted(receivedAt)
            ?? throw new InvalidOperationException("Tracker render snapshot capture session is disabled.");
        capturePath = sessionState.RenderSnapshotPath;
        writer = VisionPacketCaptureFile.CreateWriter(capturePath);
        logger.LogInformation("Writing tracker render snapshots to {CapturePath}", capturePath);
    }
}

public sealed record TrackerRenderSnapshotRecord(
    int SchemaVersion,
    DateTimeOffset ReceivedAt,
    TrackerFrame Frame);
