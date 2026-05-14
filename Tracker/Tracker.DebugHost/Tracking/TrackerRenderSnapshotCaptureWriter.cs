using System.Text.Json;
using Tracker.Core;
using Tracker.DebugHost.Vision;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// tracker render snapshot を capture session の sidecar JSONL gzip file へ書き出す writer。
/// </summary>
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

    /// <summary>
    /// capture session と logger を受け取り、必要になるまで file writer を遅延初期化する。
    /// </summary>
    public TrackerRenderSnapshotCaptureWriter(
        VisionPacketCaptureSession session,
        ILogger<TrackerRenderSnapshotCaptureWriter> logger)
    {
        this.session = session;
        this.logger = logger;
    }

    /// <summary>
    /// 現在書き込み中の render snapshot capture path。未開始または停止後は null。
    /// </summary>
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

    /// <summary>
    /// 指定 frame を render snapshot record として追記し、capture 無効時は session を停止する。
    /// </summary>
    public void CaptureFrame(TrackerFrame frame, DateTimeOffset receivedAt)
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

    /// <summary>
    /// writer を停止して保持中の file handle を解放する。
    /// </summary>
    public void Dispose()
    {
        Stop();
    }

    /// <summary>
    /// capture writer と session を停止し、次回 capture 時に新しい file を開始できる状態へ戻す。
    /// </summary>
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
            ?? throw new InvalidOperationException("Tracker render snapshot capture session is disabled.");
        capturePath = sessionState.RenderSnapshotPath;
        writer = VisionPacketCaptureFile.CreateWriter(capturePath);
        logger.LogInformation("Writing tracker render snapshots to {CapturePath}", capturePath);
    }

}

/// <summary>
/// tracker render snapshot capture の 1 行分の JSON record。
/// </summary>
/// <param name="SchemaVersion">record schema version。</param>
/// <param name="ReceivedAt">tracked frame を受信した UTC 時刻。</param>
/// <param name="Frame">capture した tracker frame。</param>
public sealed record TrackerRenderSnapshotRecord(
    int SchemaVersion,
    DateTimeOffset ReceivedAt,
    TrackerFrame Frame);
