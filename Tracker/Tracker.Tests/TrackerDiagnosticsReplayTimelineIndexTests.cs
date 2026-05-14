using Tracker.DebugHost.Tracking;

namespace Tracker.Tests;

/// <summary>
/// TRACKER-059 の unified replay timeline index contract を固定する。
/// </summary>
public class TrackerDiagnosticsReplayTimelineIndexTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 5, 13, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Vision/render より ER-FORCE が高速な場合、ReceivedAt 軸の fast ticks と render hold を作ることを確認する。
    /// </summary>
    [Fact]
    public void Build_UsesFastestTrackerCadenceAndHoldsLatestRenderSnapshotByReceivedAt()
    {
        var records = FastCadenceRecords().ToArray();

        var index = TrackerDiagnosticsReplayTimelineIndex.Build(records);

        Assert.Equal(
            [0, 20, 40, 60, 80, 100],
            index.Ticks.Select(tick => (int)(tick.ReceivedAt - BaseTime).TotalMilliseconds).ToArray());
        Assert.Equal([1000u, 1000u, 1000u, 1000u, 1000u, 1100u], index.Ticks.Select(tick => tick.RenderFrameNumber).ToArray());
        Assert.Equal([3000u, 3001u, 3002u, 3003u, 3004u, 3005u], index.Ticks.Select(tick => tick.TrackerSnapshotTrackedFrameNumber).ToArray());
    }

    /// <summary>
    /// ER-FORCE の TrackedFrame.timestamp が ibis own と非重複でも、timeline ordering は ReceivedAt で決まることを確認する。
    /// </summary>
    [Fact]
    public void Build_WhenTrackerTimestampsDoNotOverlapWithOwn_OrdersByReceivedAt()
    {
        var records = FastCadenceRecords()
            .Reverse()
            .Select(record => record with
            {
                TrackerSnapshotTimestampNs = 1_778_620_918_834_101_760 + record.ReplayTimelineIndex,
            })
            .ToArray();

        var index = TrackerDiagnosticsReplayTimelineIndex.Build(records);

        Assert.Equal(
            [0, 20, 40, 60, 80, 100],
            index.Ticks.Select(tick => (int)(tick.ReceivedAt - BaseTime).TotalMilliseconds).ToArray());
        Assert.True(index.Ticks.All(tick => tick.TrackerSnapshotTimestampNs >= 1_778_620_918_834_101_760));
    }

    /// <summary>
    /// 先頭 tracker tick に prior render がない場合だけ nearest-after render snapshot を使うことを確認する。
    /// </summary>
    [Fact]
    public void Build_WhenFirstTickHasNoPriorRender_UsesNearestAfterFallback()
    {
        var records = FastCadenceRecords()
            .Select((record, index) => index == 0
                ? record with
                {
                    RenderFrameNumber = null,
                    RenderReceivedAt = null,
                    RenderMatchRule = "unavailable",
                }
                : record)
            .ToArray();

        var index = TrackerDiagnosticsReplayTimelineIndex.Build(records);

        Assert.Equal(1000u, index.Ticks[0].RenderFrameNumber);
        Assert.Equal("exact", index.Ticks[0].RenderMatchRule);
    }

    private static IEnumerable<TrackerSnapshotAlignmentRecord> FastCadenceRecords()
    {
        var renderFrames = new[] { 1000u, 1000u, 1000u, 1000u, 1000u, 1100u };
        for (var index = 0; index < 6; index++)
        {
            var offset = TimeSpan.FromMilliseconds(index * 20);
            yield return new TrackerSnapshotAlignmentRecord(
                SchemaVersion: 2,
                ReplayTimelineIndex: index,
                ReplayTimelineReceivedAt: BaseTime.Add(offset),
                ReplayTimelineKind: "tracker-snapshot",
                DiagnosticsLineNumber: index == 0 ? 1 : index == 5 ? 2 : 1,
                RenderFrameNumber: renderFrames[index],
                RenderReceivedAt: index == 5 ? BaseTime.AddMilliseconds(100) : BaseTime,
                RenderMatchRule: index == 0 ? "exact" : "latest-before",
                SourceKey: "external|ER-FORCE|192.0.2.50:12010",
                SourceRole: "external",
                SourceLabel: "ER-FORCE",
                SourceUuid: "er-force-uuid",
                RemoteEndpoint: "192.0.2.50:12010",
                TrackerSnapshotRecordIndex: index,
                TrackerSnapshotReceivedAt: BaseTime.Add(offset),
                TrackerSnapshotTrackedFrameNumber: (uint)(3000 + index),
                TrackerSnapshotTimestampNs: 1_778_620_918_834_101_760 + index,
                MatchingRule: TrackerSnapshotAlignmentRecord.SavedSessionAlignmentRule,
                ReceivedAtDeltaTicks: 0,
                Status: TrackerSnapshotAlignmentRecord.ReadyStatus);
        }
    }
}
