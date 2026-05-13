namespace Tracker.Server.Tracking;

/// <summary>
/// diagnostics replay timeline tick と tracker packet snapshot の保存済み対応付けを表す JSONL record。
/// </summary>
/// <param name="SchemaVersion">record schema version。</param>
/// <param name="ReplayTimelineIndex">session replay timeline 上の 0 始まり index。</param>
/// <param name="ReplayTimelineReceivedAt">timeline tick の capture-time UTC 時刻。</param>
/// <param name="ReplayTimelineKind">timeline tick の発生元。</param>
/// <param name="DiagnosticsLineNumber">対応する last-known diagnostics log line number。</param>
/// <param name="RenderFrameNumber">timeline tick で保持する render snapshot frame number。</param>
/// <param name="RenderReceivedAt">timeline tick で保持する render snapshot の capture-time UTC 時刻。</param>
/// <param name="RenderMatchRule">render snapshot の対応付け規則。</param>
/// <param name="SourceKey">source を一意化する key。</param>
/// <param name="SourceRole">対応 tracker source の role。</param>
/// <param name="SourceLabel">対応 tracker source の UI label。</param>
/// <param name="SourceUuid">対応 tracker source の UUID。</param>
/// <param name="RemoteEndpoint">対応 tracker source の remote endpoint。</param>
/// <param name="TrackerSnapshotRecordIndex">tracker-packet-snapshots.jsonl 上の 0 始まり record index。</param>
/// <param name="TrackerSnapshotReceivedAt">対応 tracker snapshot の capture-time UTC 時刻。</param>
/// <param name="TrackerSnapshotTrackedFrameNumber">対応 tracker snapshot の tracked frame number。</param>
/// <param name="TrackerSnapshotTimestampNs">対応 tracker snapshot の data timestamp。</param>
/// <param name="MatchingRule">対応付け規則。</param>
/// <param name="ReceivedAtDeltaTicks">timeline tick と tracker snapshot の capture-time 差分 ticks。</param>
/// <param name="Status">対応付け状態。</param>
public sealed record TrackerSnapshotAlignmentRecord(
    int SchemaVersion,
    int ReplayTimelineIndex,
    DateTimeOffset ReplayTimelineReceivedAt,
    string ReplayTimelineKind,
    int? DiagnosticsLineNumber,
    uint? RenderFrameNumber,
    DateTimeOffset? RenderReceivedAt,
    string RenderMatchRule,
    string SourceKey,
    string SourceRole,
    string SourceLabel,
    string SourceUuid,
    string RemoteEndpoint,
    int? TrackerSnapshotRecordIndex,
    DateTimeOffset? TrackerSnapshotReceivedAt,
    uint? TrackerSnapshotTrackedFrameNumber,
    long? TrackerSnapshotTimestampNs,
    string MatchingRule,
    long? ReceivedAtDeltaTicks,
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
    /// tracker packet snapshot tick を表す kind。
    /// </summary>
    public const string TrackerSnapshotTimelineKind = "tracker-snapshot";

    /// <summary>
    /// render snapshot tick を表す kind。
    /// </summary>
    public const string RenderSnapshotTimelineKind = "render-snapshot";

    /// <summary>
    /// diagnostics entry tick を表す kind。
    /// </summary>
    public const string DiagnosticsEntryTimelineKind = "diagnostics-entry";

    /// <summary>
    /// source key の構成要素を正規化する。
    /// </summary>
    public TrackerSnapshotAlignmentRecord Normalize()
    {
        var normalizedRole = TrackerPacketSnapshotRecord.NormalizeSourceRole(SourceRole);
        var normalizedLabel = TrackerPacketSnapshotRecord.NormalizeSourceLabel(
            SourceLabel,
            sourceName: null,
            SourceUuid,
            RemoteEndpoint,
            normalizedRole);
        var normalizedUuid = SourceUuid ?? string.Empty;
        var normalizedEndpoint = RemoteEndpoint ?? string.Empty;
        return this with
        {
            ReplayTimelineReceivedAt = ReplayTimelineReceivedAt.ToUniversalTime(),
            ReplayTimelineKind = string.IsNullOrWhiteSpace(ReplayTimelineKind)
                ? TrackerSnapshotTimelineKind
                : ReplayTimelineKind,
            RenderReceivedAt = RenderReceivedAt?.ToUniversalTime(),
            RenderMatchRule = string.IsNullOrWhiteSpace(RenderMatchRule) ? "unavailable" : RenderMatchRule,
            SourceRole = normalizedRole,
            SourceLabel = normalizedLabel,
            SourceUuid = normalizedUuid,
            RemoteEndpoint = normalizedEndpoint,
            SourceKey = string.IsNullOrWhiteSpace(SourceKey)
                ? CreateSourceKey(normalizedRole, normalizedLabel, normalizedUuid, normalizedEndpoint)
                : SourceKey,
            TrackerSnapshotReceivedAt = TrackerSnapshotReceivedAt?.ToUniversalTime(),
            MatchingRule = string.IsNullOrWhiteSpace(MatchingRule) ? SavedSessionAlignmentRule : MatchingRule,
            Status = string.IsNullOrWhiteSpace(Status) ? ReadyStatus : Status,
        };
    }

    /// <summary>
    /// source identity から stable key を作る。
    /// </summary>
    public static string CreateSourceKey(
        string sourceRole,
        string sourceLabel,
        string sourceUuid,
        string remoteEndpoint)
    {
        return string.Join(
            '\u001f',
            TrackerPacketSnapshotRecord.NormalizeSourceRole(sourceRole),
            TrackerPacketSnapshotRecord.NormalizeSourceLabel(sourceLabel, null, sourceUuid, remoteEndpoint, sourceRole),
            sourceUuid ?? string.Empty,
            remoteEndpoint ?? string.Empty);
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
