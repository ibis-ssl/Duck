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
    private int replayTimelineIndex;
    private int recordCount;
    private int skippedRecordCount;
    private int errorCount;
    private TrackerRenderSnapshotRecord? latestRenderSnapshot;
    private TrackerFrame? latestDiagnosticsFrame;
    private DateTimeOffset? latestDiagnosticsReceivedAt;

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
        this.snapshotWriter.SnapshotAppended += CaptureTrackerSnapshot;
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
    /// render snapshot tick に対する保存済み alignment record を source ごとに追記する。
    /// </summary>
    public void CaptureRenderSnapshot(TrackerFrame frame, DateTimeOffset receivedAt)
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

        var candidates = snapshotWriter.GetLatestSnapshotsBySource();
        lock (gate)
        {
            latestRenderSnapshot = new TrackerRenderSnapshotRecord(1, receivedAt.ToUniversalTime(), frame);
            WriteTimelineRecords(
                TrackerSnapshotAlignmentRecord.RenderSnapshotTimelineKind,
                receivedAt,
                candidates);
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

        var candidates = snapshotWriter.GetLatestSnapshotsBySource();
        lock (gate)
        {
            try
            {
                diagnosticsLineNumber++;
                latestDiagnosticsFrame = frame;
                latestDiagnosticsReceivedAt = receivedAt.ToUniversalTime();
                WriteTimelineRecords(
                    TrackerSnapshotAlignmentRecord.DiagnosticsEntryTimelineKind,
                    receivedAt,
                    candidates);
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
        snapshotWriter.SnapshotAppended -= CaptureTrackerSnapshot;
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
            replayTimelineIndex = 0;
            recordCount = 0;
            skippedRecordCount = 0;
            errorCount = 0;
            latestRenderSnapshot = null;
            latestDiagnosticsFrame = null;
            latestDiagnosticsReceivedAt = null;
            session.Stop();
        }
    }

    private void CaptureTrackerSnapshot(TrackerPacketSnapshotIndexedRecord snapshot)
    {
        if (!session.Enabled || writeFailed)
        {
            return;
        }

        lock (gate)
        {
            WriteTimelineRecords(
                TrackerSnapshotAlignmentRecord.TrackerSnapshotTimelineKind,
                snapshot.Record.ReceivedAt,
                [snapshot]);
        }
    }

    private void WriteTimelineRecords(
        string replayTimelineKind,
        DateTimeOffset replayTimelineReceivedAt,
        IReadOnlyList<TrackerPacketSnapshotIndexedRecord> candidates)
    {
        if (candidates.Count == 0)
        {
            return;
        }

        try
        {
            _ = EnsureWriter(replayTimelineReceivedAt);
            var timelineIndex = replayTimelineIndex++;
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
                var renderMatch = ResolveRenderMatchRule(replayTimelineReceivedAt);
                var alignment = new TrackerSnapshotAlignmentRecord(
                    SchemaVersion: 2,
                    ReplayTimelineIndex: timelineIndex,
                    ReplayTimelineReceivedAt: replayTimelineReceivedAt.ToUniversalTime(),
                    ReplayTimelineKind: replayTimelineKind,
                    DiagnosticsLineNumber: latestDiagnosticsFrame is null ? null : diagnosticsLineNumber,
                    RenderFrameNumber: latestRenderSnapshot?.Frame.FrameNumber,
                    RenderReceivedAt: latestRenderSnapshot?.ReceivedAt.ToUniversalTime(),
                    RenderMatchRule: renderMatch,
                    SourceKey: TrackerSnapshotAlignmentRecord.CreateSourceKey(
                        normalizedRole,
                        normalizedLabel,
                        record.SourceUuid,
                        record.RemoteEndpoint),
                    SourceRole: normalizedRole,
                    SourceLabel: normalizedLabel,
                    SourceUuid: record.SourceUuid,
                    RemoteEndpoint: record.RemoteEndpoint,
                    TrackerSnapshotRecordIndex: candidate.RecordIndex,
                    TrackerSnapshotReceivedAt: record.ReceivedAt.ToUniversalTime(),
                    TrackerSnapshotTrackedFrameNumber: record.TrackedFrameNumber,
                    TrackerSnapshotTimestampNs: record.TrackedFrameTimestampNs,
                    MatchingRule: TrackerSnapshotAlignmentRecord.SavedSessionAlignmentRule,
                    ReceivedAtDeltaTicks: Math.Abs((record.ReceivedAt.ToUniversalTime() - replayTimelineReceivedAt.ToUniversalTime()).Ticks),
                    Status: TrackerSnapshotAlignmentRecord.ReadyStatus).Normalize();
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

    private string ResolveRenderMatchRule(DateTimeOffset replayTimelineReceivedAt)
    {
        if (latestRenderSnapshot is null)
        {
            return "unavailable";
        }

        if (latestRenderSnapshot.ReceivedAt == replayTimelineReceivedAt.ToUniversalTime())
        {
            return "exact";
        }

        return latestRenderSnapshot.ReceivedAt <= replayTimelineReceivedAt.ToUniversalTime()
            ? "latest-before"
            : "nearest-after";
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
