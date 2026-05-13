using System.Text.Json;
using Google.Protobuf;
using Tracker.Server.Vision;

namespace Tracker.Server.Tracking;

/// <summary>
/// CaptureOn session folder の tracker packet snapshot sidecar JSONL へ official tracker packet を追記する。
/// </summary>
public sealed class TrackerPacketSnapshotLogWriter : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly object gate = new();
    private readonly VisionPacketCaptureSession session;
    private readonly ILogger<TrackerPacketSnapshotLogWriter> logger;
    private readonly Dictionary<string, TrackerPacketSnapshotSourceMetadata> sources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TrackerPacketSnapshotIndexedRecord> latestSnapshotsBySource = new(StringComparer.Ordinal);
    private StreamWriter? writer;
    private string? capturePath;
    private bool writeFailed;
    private int recordCount;
    private int skippedRecordCount;
    private int errorCount;

    /// <summary>
    /// snapshot record を sidecar へ保存した後に通知する。
    /// </summary>
    public event Action<TrackerPacketSnapshotIndexedRecord>? SnapshotAppended;

    /// <summary>
    /// capture session と logger を受け取り、必要になるまで file writer を遅延初期化する。
    /// </summary>
    public TrackerPacketSnapshotLogWriter(
        VisionPacketCaptureSession session,
        ILogger<TrackerPacketSnapshotLogWriter> logger)
    {
        this.session = session;
        this.logger = logger;
    }

    /// <summary>
    /// 現在書き込み中の tracker packet snapshot sidecar path。未開始または停止後は null。
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
    /// sidecar に書き込めた record 数。
    /// </summary>
    public int RecordCount
    {
        get
        {
            lock (gate)
            {
                return recordCount;
            }
        }
    }

    /// <summary>
    /// decode または書き込み失敗で sidecar record として保存しなかった packet 数。
    /// </summary>
    public int SkippedRecordCount
    {
        get
        {
            lock (gate)
            {
                return skippedRecordCount;
            }
        }
    }

    /// <summary>
    /// decode または書き込み失敗として記録した error 数。
    /// </summary>
    public int ErrorCount
    {
        get
        {
            lock (gate)
            {
                return errorCount;
            }
        }
    }

    /// <summary>
    /// official tracker packet を raw payload 付き snapshot record として追記する。
    /// </summary>
    public void CapturePacket(
        TrackerWrapperPacket packet,
        DateTimeOffset receivedAt,
        string? remoteEndpoint = null,
        string sourceRole = "unknown",
        string? sourceLabel = null)
    {
        var record = TrackerPacketSnapshotRecord.FromPacket(
            packet,
            receivedAt,
            remoteEndpoint,
            sourceRole,
            sourceLabel);
        Append(record);
    }

    /// <summary>
    /// raw payload を official tracker packet として decode できる場合だけ snapshot record として追記する。
    /// </summary>
    public bool TryCapturePayload(
        ReadOnlySpan<byte> payload,
        DateTimeOffset receivedAt,
        string? remoteEndpoint = null,
        string sourceRole = "unknown",
        string? sourceLabel = null)
    {
        try
        {
            var packet = TrackerWrapperPacket.Parser.ParseFrom(payload);
            CapturePacket(packet, receivedAt, remoteEndpoint, sourceRole, sourceLabel);
            return true;
        }
        catch (Exception ex) when (ex is InvalidProtocolBufferException or ArgumentException)
        {
            lock (gate)
            {
                _ = session.EnsureStarted(receivedAt);
                skippedRecordCount++;
                errorCount++;
                UpdateMetadata();
            }

            logger.LogWarning(ex, "Skipped invalid tracker packet snapshot payload from {RemoteEndpoint}", remoteEndpoint);
            return false;
        }
    }

    /// <summary>
    /// 既に構築済みの snapshot record を sidecar に追記する。
    /// </summary>
    public void Append(TrackerPacketSnapshotRecord record)
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
                EnsureWriter(record.ReceivedAt);
                var normalizedRecord = record.EnsureSemanticSummary();
                writer!.WriteLine(JsonSerializer.Serialize(normalizedRecord, JsonOptions));
                var recordIndex = recordCount;
                recordCount++;
                UpdateSource(normalizedRecord, recordIndex);
                UpdateMetadata();
                SnapshotAppended?.Invoke(new TrackerPacketSnapshotIndexedRecord(recordIndex, normalizedRecord));
                if (session.FlushEachPacket)
                {
                    writer.Flush();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or InvalidProtocolBufferException)
            {
                writeFailed = true;
                skippedRecordCount++;
                errorCount++;
                UpdateMetadata();
                logger.LogWarning(ex, "Failed to write tracker packet snapshot {CapturePath}", capturePath);
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
    /// source key ごとの最新 snapshot record を alignment writer に渡す。
    /// </summary>
    public IReadOnlyList<TrackerPacketSnapshotIndexedRecord> GetLatestSnapshotsBySource()
    {
        lock (gate)
        {
            return latestSnapshotsBySource.Values
                .OrderBy(snapshot => snapshot.Record.SourceRole, StringComparer.Ordinal)
                .ThenBy(snapshot => snapshot.Record.SourceLabel, StringComparer.Ordinal)
                .ThenBy(snapshot => snapshot.Record.RemoteEndpoint, StringComparer.Ordinal)
                .ToArray();
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
            recordCount = 0;
            skippedRecordCount = 0;
            errorCount = 0;
            sources.Clear();
            latestSnapshotsBySource.Clear();
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
            ?? throw new InvalidOperationException("Tracker packet snapshot capture session is disabled.");
        capturePath = sessionState.TrackerSnapshotSidecarPath;
        var directory = Path.GetDirectoryName(capturePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        writer = new StreamWriter(new FileStream(capturePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        logger.LogInformation("Writing tracker packet snapshots to {CapturePath}", capturePath);
    }

    private void UpdateSource(TrackerPacketSnapshotRecord record, int recordIndex)
    {
        var key = string.Join(
            '\u001f',
            record.SourceUuid,
            record.SourceName,
            record.SourceRole,
            record.RemoteEndpoint);
        latestSnapshotsBySource[key] = new TrackerPacketSnapshotIndexedRecord(recordIndex, record);
        var source = sources.TryGetValue(key, out var existing)
            ? existing with
            {
                RecordCount = existing.RecordCount + 1,
                LastReceivedAt = record.ReceivedAt.ToUniversalTime(),
            }
            : new TrackerPacketSnapshotSourceMetadata(
                SourceUuid: record.SourceUuid,
                SourceName: record.SourceName,
                SourceRole: record.SourceRole,
                SourceLabel: record.SourceLabel,
                RemoteEndpoint: record.RemoteEndpoint,
                RecordCount: 1,
                LastReceivedAt: record.ReceivedAt.ToUniversalTime());
        sources[key] = source;
    }

    private void UpdateMetadata()
    {
        session.UpdateTrackerSnapshotLogMetadata(new TrackerPacketSnapshotLogMetadataSnapshot(
            RecordCount: recordCount,
            SkippedRecordCount: skippedRecordCount,
            ErrorCount: errorCount,
            Sources: sources.Values
                .OrderBy(source => source.SourceRole, StringComparer.Ordinal)
                .ThenBy(source => source.SourceLabel, StringComparer.Ordinal)
                .ThenBy(source => source.RemoteEndpoint, StringComparer.Ordinal)
                .ToArray()));
    }
}

/// <summary>
/// metadata に反映する tracker packet snapshot sidecar の集計。
/// </summary>
public sealed record TrackerPacketSnapshotLogMetadataSnapshot(
    int RecordCount,
    int SkippedRecordCount,
    int ErrorCount,
    IReadOnlyList<TrackerPacketSnapshotSourceMetadata> Sources);

/// <summary>
/// metadata に反映する tracker packet source ごとの集計。
/// </summary>
public sealed record TrackerPacketSnapshotSourceMetadata(
    string SourceUuid,
    string SourceName,
    string SourceRole,
    string SourceLabel,
    string RemoteEndpoint,
    int RecordCount,
    DateTimeOffset LastReceivedAt);
