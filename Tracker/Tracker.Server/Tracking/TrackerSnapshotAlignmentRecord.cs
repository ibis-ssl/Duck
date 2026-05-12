namespace Tracker.Server.Tracking;

/// <summary>
/// diagnostics entry と tracker packet snapshot の保存済み対応付けを表す JSONL record。
/// </summary>
/// <param name="SchemaVersion">record schema version。</param>
/// <param name="DiagnosticsLineNumber">diagnostics log 上の 1 始まり line number。</param>
/// <param name="DiagnosticsTrackedFrameNumber">diagnostics entry の tracked frame number。</param>
/// <param name="DiagnosticsReceivedAt">diagnostics entry を記録した capture-time UTC 時刻。</param>
/// <param name="DiagnosticsSessionRelativeTicks">capture session 開始から diagnostics entry までの ticks。</param>
/// <param name="OwnSnapshotTimestampNs">対応する ibis own snapshot の data timestamp。</param>
/// <param name="SourceRole">対応 tracker source の role。</param>
/// <param name="SourceLabel">対応 tracker source の UI label。</param>
/// <param name="SourceUuid">対応 tracker source の UUID。</param>
/// <param name="RemoteEndpoint">対応 tracker source の remote endpoint。</param>
/// <param name="TrackerSnapshotRecordIndex">tracker-packet-snapshots.jsonl 上の 0 始まり record index。</param>
/// <param name="TrackerSnapshotReceivedAt">対応 tracker snapshot の capture-time UTC 時刻。</param>
/// <param name="TrackerSnapshotTrackedFrameNumber">対応 tracker snapshot の tracked frame number。</param>
/// <param name="TrackerSnapshotTimestampNs">対応 tracker snapshot の data timestamp。</param>
/// <param name="MatchingRule">対応付け規則。</param>
/// <param name="ReceivedAtDeltaTicks">diagnostics と tracker snapshot の capture-time 差分 ticks。</param>
/// <param name="Status">対応付け状態。</param>
public sealed record TrackerSnapshotAlignmentRecord(
    int SchemaVersion,
    int DiagnosticsLineNumber,
    uint DiagnosticsTrackedFrameNumber,
    DateTimeOffset DiagnosticsReceivedAt,
    long DiagnosticsSessionRelativeTicks,
    long OwnSnapshotTimestampNs,
    string SourceRole,
    string SourceLabel,
    string SourceUuid,
    string RemoteEndpoint,
    int TrackerSnapshotRecordIndex,
    DateTimeOffset TrackerSnapshotReceivedAt,
    uint TrackerSnapshotTrackedFrameNumber,
    long TrackerSnapshotTimestampNs,
    string MatchingRule,
    long ReceivedAtDeltaTicks,
    string Status)
{
    /// <summary>
    /// 保存済み対応付けで使う matching rule 名。
    /// </summary>
    public const string SavedSessionAlignmentRule = "saved-session-alignment";

    /// <summary>
    /// 正常に対応 snapshot を選べた状態名。
    /// </summary>
    public const string ReadyStatus = "ready";

    /// <summary>
    /// source key の構成要素を正規化する。
    /// </summary>
    public TrackerSnapshotAlignmentRecord Normalize()
    {
        return this with
        {
            SourceRole = TrackerPacketSnapshotRecord.NormalizeSourceRole(SourceRole),
            SourceLabel = TrackerPacketSnapshotRecord.NormalizeSourceLabel(
                SourceLabel,
                sourceName: null,
                SourceUuid,
                RemoteEndpoint,
                SourceRole),
            SourceUuid = SourceUuid ?? string.Empty,
            RemoteEndpoint = RemoteEndpoint ?? string.Empty,
            MatchingRule = string.IsNullOrWhiteSpace(MatchingRule) ? SavedSessionAlignmentRule : MatchingRule,
            Status = string.IsNullOrWhiteSpace(Status) ? ReadyStatus : Status,
        };
    }
}

/// <summary>
/// metadata に反映する tracker snapshot alignment sidecar の集計。
/// </summary>
public sealed record TrackerSnapshotAlignmentLogMetadataSnapshot(
    int RecordCount,
    int SkippedRecordCount,
    int ErrorCount);

/// <summary>
/// tracker snapshot record index と record 本体の組。
/// </summary>
public sealed record TrackerPacketSnapshotIndexedRecord(
    int RecordIndex,
    TrackerPacketSnapshotRecord Record);
