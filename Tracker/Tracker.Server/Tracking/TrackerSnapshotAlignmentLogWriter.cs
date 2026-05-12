using System.Text.Json;
using Tracker.Core;
using Tracker.Server.Vision;

namespace Tracker.Server.Tracking;

/// <summary>
/// CaptureOn session folder の tracker snapshot alignment sidecar JSONL へ diagnostics entry と source snapshot の対応を追記する。
/// </summary>
public sealed class TrackerSnapshotAlignmentLogWriter : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly object gate = new();
    private readonly VisionPacketCaptureSession session;
    private readonly TrackerPacketSnapshotLogWriter snapshotWriter;
    private readonly ILogger<TrackerSnapshotAlignmentLogWriter> logger;
    private StreamWriter? writer;
    private string? capturePath;
    private bool writeFailed;
    private int diagnosticsLineNumber;
    private int recordCount;
    private int skippedRecordCount;
    private int errorCount;

    /// <summary>
    /// capture session、snapshot writer、logger を受け取り、必要になるまで file writer を遅延初期化する。
    /// </summary>
    public TrackerSnapshotAlignmentLogWriter(
        VisionPacketCaptureSession session,
        TrackerPacketSnapshotLogWriter snapshotWriter,
        ILogger<TrackerSnapshotAlignmentLogWriter> logger)
    {
        this.session = session;
        this.snapshotWriter = snapshotWriter;
        this.logger = logger;
    }

    /// <summary>
    /// 現在書き込み中の tracker snapshot alignment sidecar path。未開始または停止後は null。
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
    /// diagnostics entry に対する保存済み alignment record を source ごとに追記する。
    /// </summary>
    public void CaptureDiagnosticsEntry(TrackerFrame frame, DateTimeOffset receivedAt)
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
                var sessionState = EnsureWriter(receivedAt);
                diagnosticsLineNumber++;
                var candidates = snapshotWriter.GetLatestSnapshotsBySource();
                foreach (var candidate in candidates)
                {
                    var record = candidate.Record.EnsureSemanticSummary();
                    var normalizedRole = TrackerPacketSnapshotRecord.NormalizeSourceRole(record.SourceRole);
                    var normalizedLabel = TrackerPacketSnapshotRecord.NormalizeSourceLabel(
                        record.SourceLabel,
                        record.SourceName,
                        record.SourceUuid,
                        record.RemoteEndpoint,
                        normalizedRole);
                    var alignment = new TrackerSnapshotAlignmentRecord(
                        SchemaVersion: 1,
                        DiagnosticsLineNumber: diagnosticsLineNumber,
                        DiagnosticsTrackedFrameNumber: frame.FrameNumber,
                        DiagnosticsReceivedAt: receivedAt.ToUniversalTime(),
                        DiagnosticsSessionRelativeTicks: receivedAt.ToUniversalTime().Ticks - sessionState.StartedAt.UtcTicks,
                        OwnSnapshotTimestampNs: frame.DataTimestampNs,
                        SourceRole: normalizedRole,
                        SourceLabel: normalizedLabel,
                        SourceUuid: record.SourceUuid,
                        RemoteEndpoint: record.RemoteEndpoint,
                        TrackerSnapshotRecordIndex: candidate.RecordIndex,
                        TrackerSnapshotReceivedAt: record.ReceivedAt.ToUniversalTime(),
                        TrackerSnapshotTrackedFrameNumber: record.TrackedFrameNumber,
                        TrackerSnapshotTimestampNs: record.TrackedFrameTimestampNs,
                        MatchingRule: TrackerSnapshotAlignmentRecord.SavedSessionAlignmentRule,
                        ReceivedAtDeltaTicks: Math.Abs((record.ReceivedAt.ToUniversalTime() - receivedAt.ToUniversalTime()).Ticks),
                        Status: TrackerSnapshotAlignmentRecord.ReadyStatus);
                    writer!.WriteLine(JsonSerializer.Serialize(alignment, JsonOptions));
                    recordCount++;
                }

                UpdateMetadata();
                if (session.FlushEachPacket)
                {
                    writer!.Flush();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                writeFailed = true;
                skippedRecordCount++;
                errorCount++;
                UpdateMetadata();
                logger.LogWarning(ex, "Failed to write tracker snapshot alignment {CapturePath}", capturePath);
            }
        }
    }

    /// <summary>
    /// writer buffer と metadata を flush する。
    /// </summary>
    public void Flush()
    {
        lock (gate)
        {
            writer?.Flush();
            UpdateMetadata();
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
    /// alignment writer と session を停止し、次回 capture 時に新しい file を開始できる状態へ戻す。
    /// </summary>
    public void Stop()
    {
        lock (gate)
        {
            writer?.Dispose();
            writer = null;
            capturePath = null;
            writeFailed = false;
            diagnosticsLineNumber = 0;
            recordCount = 0;
            skippedRecordCount = 0;
            errorCount = 0;
            session.Stop();
        }
    }

    private VisionPacketCaptureSessionState EnsureWriter(DateTimeOffset receivedAt)
    {
        var sessionState = session.EnsureStarted(receivedAt)
            ?? throw new InvalidOperationException("Tracker snapshot alignment capture session is disabled.");
        if (writer is not null)
        {
            return sessionState;
        }

        capturePath = sessionState.TrackerSnapshotAlignmentSidecarPath;
        var directory = Path.GetDirectoryName(capturePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        writer = new StreamWriter(new FileStream(capturePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        logger.LogInformation("Writing tracker snapshot alignment to {CapturePath}", capturePath);
        return sessionState;
    }

    private void UpdateMetadata()
    {
        session.UpdateTrackerSnapshotAlignmentLogMetadata(new TrackerSnapshotAlignmentLogMetadataSnapshot(
            RecordCount: recordCount,
            SkippedRecordCount: skippedRecordCount,
            ErrorCount: errorCount));
    }
}
