namespace Tracker.DebugHost.Tracking;

/// <summary>
/// diagnostics replay 用に alignment v2 records から unified timeline tick を作る UI 非依存 index。
/// </summary>
public sealed class TrackerDiagnosticsReplayTimelineIndex
{
    private readonly IReadOnlyDictionary<int, TrackerSnapshotAlignmentRecord[]> recordsByTimelineIndex;

    private TrackerDiagnosticsReplayTimelineIndex(
        IReadOnlyList<TrackerDiagnosticsReplayTimelineTick> ticks,
        IReadOnlyDictionary<int, TrackerSnapshotAlignmentRecord[]> recordsByTimelineIndex)
    {
        Ticks = ticks;
        this.recordsByTimelineIndex = recordsByTimelineIndex;
    }

    /// <summary>
    /// replay timeline ticks。
    /// </summary>
    public IReadOnlyList<TrackerDiagnosticsReplayTimelineTick> Ticks { get; }

    /// <summary>
    /// alignment records から capture-time ReceivedAt 順の replay timeline index を構築する。
    /// </summary>
    public static TrackerDiagnosticsReplayTimelineIndex Build(
        IReadOnlyList<TrackerSnapshotAlignmentRecord> alignmentRecords)
    {
        var normalizedRecords = alignmentRecords
            .Where(record => record.SchemaVersion == 2)
            .Select(record => record.Normalize())
            .Where(record => string.Equals(record.Status, TrackerSnapshotAlignmentRecord.ReadyStatus, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var groupedRecords = normalizedRecords
            .GroupBy(record => record.ReplayTimelineIndex)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(record => record.SourceRole, StringComparer.Ordinal)
                    .ThenBy(record => record.SourceLabel, StringComparer.Ordinal)
                    .ThenBy(record => record.RemoteEndpoint, StringComparer.Ordinal)
                    .ToArray());
        var ticks = groupedRecords
            .Select(pair => CreateTick(pair.Key, pair.Value))
            .OrderBy(tick => tick.ReceivedAt)
            .ThenBy(tick => tick.ReplayTimelineIndex)
            .ToArray();
        return new TrackerDiagnosticsReplayTimelineIndex(
            ApplyRenderSnapshotFallbacks(ticks, normalizedRecords),
            groupedRecords);
    }

    /// <summary>
    /// 指定 timeline index の alignment records を返す。
    /// </summary>
    public IReadOnlyList<TrackerSnapshotAlignmentRecord> GetRecordsForTimelineIndex(int replayTimelineIndex)
    {
        return recordsByTimelineIndex.TryGetValue(replayTimelineIndex, out var records)
            ? records
            : [];
    }

    private static TrackerDiagnosticsReplayTimelineTick CreateTick(
        int replayTimelineIndex,
        IReadOnlyList<TrackerSnapshotAlignmentRecord> records)
    {
        var representative = records
            .OrderBy(record => record.ReplayTimelineReceivedAt)
            .ThenBy(record => record.SourceRole, StringComparer.Ordinal)
            .ThenBy(record => record.SourceLabel, StringComparer.Ordinal)
            .First();
        var trackerRecord = records
            .Where(record => record.TrackerSnapshotRecordIndex is not null)
            .OrderBy(record => Math.Abs(record.ReceivedAtDeltaTicks ?? long.MaxValue))
            .ThenBy(record => record.TrackerSnapshotRecordIndex)
            .FirstOrDefault() ?? representative;
        return new TrackerDiagnosticsReplayTimelineTick(
            replayTimelineIndex,
            representative.ReplayTimelineReceivedAt,
            representative.ReplayTimelineKind,
            representative.DiagnosticsLineNumber,
            representative.RenderFrameNumber,
            representative.RenderReceivedAt,
            representative.RenderMatchRule,
            trackerRecord.SourceRole,
            trackerRecord.SourceLabel,
            trackerRecord.SourceUuid,
            trackerRecord.RemoteEndpoint,
            trackerRecord.TrackerSnapshotRecordIndex,
            trackerRecord.TrackerSnapshotReceivedAt,
            trackerRecord.TrackerSnapshotTrackedFrameNumber,
            trackerRecord.TrackerSnapshotTimestampNs);
    }

    private static IReadOnlyList<TrackerDiagnosticsReplayTimelineTick> ApplyRenderSnapshotFallbacks(
        IReadOnlyList<TrackerDiagnosticsReplayTimelineTick> ticks,
        IReadOnlyList<TrackerSnapshotAlignmentRecord> records)
    {
        var renderSnapshots = records
            .Where(record => record.RenderFrameNumber is not null && record.RenderReceivedAt is not null)
            .Select(record => new
            {
                FrameNumber = record.RenderFrameNumber!.Value,
                ReceivedAt = record.RenderReceivedAt!.Value,
            })
            .GroupBy(render => new { render.FrameNumber, render.ReceivedAt })
            .Select(group => group.Key)
            .OrderBy(render => render.ReceivedAt)
            .ThenBy(render => render.FrameNumber)
            .ToArray();
        if (renderSnapshots.Length == 0)
        {
            return ticks;
        }

        return ticks
            .Select(tick =>
            {
                var render = renderSnapshots.LastOrDefault(candidate => candidate.ReceivedAt <= tick.ReceivedAt)
                    ?? renderSnapshots.First();
                var matchRule = render.ReceivedAt == tick.ReceivedAt
                    ? "exact"
                    : render.ReceivedAt < tick.ReceivedAt
                        ? "latest-before"
                        : "nearest-after";
                return tick with
                {
                    RenderFrameNumber = render.FrameNumber,
                    RenderReceivedAt = render.ReceivedAt,
                    RenderMatchRule = matchRule,
                };
            })
            .ToArray();
    }
}

/// <summary>
/// diagnostics replay timeline の 1 tick。
/// </summary>
/// <param name="ReplayTimelineIndex">session replay timeline 上の 0 始まり index。</param>
/// <param name="ReceivedAt">timeline tick の capture-time UTC 時刻。</param>
/// <param name="Kind">timeline tick の発生元。</param>
/// <param name="DiagnosticsLineNumber">対応する last-known diagnostics log line number。</param>
/// <param name="RenderFrameNumber">timeline tick で保持する render snapshot frame number。</param>
/// <param name="RenderReceivedAt">timeline tick で保持する render snapshot の capture-time UTC 時刻。</param>
/// <param name="RenderMatchRule">render snapshot の対応付け規則。</param>
/// <param name="SourceRole">代表 tracker source role。</param>
/// <param name="SourceLabel">代表 tracker source label。</param>
/// <param name="SourceUuid">代表 tracker source UUID。</param>
/// <param name="RemoteEndpoint">代表 tracker source remote endpoint。</param>
/// <param name="TrackerSnapshotRecordIndex">代表 tracker snapshot record index。</param>
/// <param name="TrackerSnapshotReceivedAt">代表 tracker snapshot capture-time UTC 時刻。</param>
/// <param name="TrackerSnapshotTrackedFrameNumber">代表 tracker snapshot tracked frame number。</param>
/// <param name="TrackerSnapshotTimestampNs">代表 tracker snapshot timestamp。</param>
public sealed record TrackerDiagnosticsReplayTimelineTick(
    int ReplayTimelineIndex,
    DateTimeOffset ReceivedAt,
    string Kind,
    int? DiagnosticsLineNumber,
    uint? RenderFrameNumber,
    DateTimeOffset? RenderReceivedAt,
    string RenderMatchRule,
    string SourceRole,
    string SourceLabel,
    string SourceUuid,
    string RemoteEndpoint,
    int? TrackerSnapshotRecordIndex,
    DateTimeOffset? TrackerSnapshotReceivedAt,
    uint? TrackerSnapshotTrackedFrameNumber,
    long? TrackerSnapshotTimestampNs);

/// <summary>
/// diagnostics comparison reader に渡す selected replay timeline tick。
/// </summary>
/// <param name="ReplayTimelineIndex">session replay timeline 上の 0 始まり index。</param>
/// <param name="DiagnosticsLineNumber">対応する last-known diagnostics log line number。</param>
/// <param name="ReceivedAt">timeline tick の capture-time UTC 時刻。</param>
public sealed record TrackerDiagnosticsReplayTimelineSelection(
    int ReplayTimelineIndex,
    int? DiagnosticsLineNumber,
    DateTimeOffset ReceivedAt)
{
    /// <summary>
    /// timeline tick から selection を作る。
    /// </summary>
    public static TrackerDiagnosticsReplayTimelineSelection FromTick(TrackerDiagnosticsReplayTimelineTick tick)
    {
        return new TrackerDiagnosticsReplayTimelineSelection(
            tick.ReplayTimelineIndex,
            tick.DiagnosticsLineNumber,
            tick.ReceivedAt);
    }
}
