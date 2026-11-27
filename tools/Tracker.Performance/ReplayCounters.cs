using Tracker.Core;

namespace Tracker.Performance;

/// <summary>
/// 各更新を計測の外で照合して、置換、確定、破棄、保留と仮想時刻上の待ち時間を集計する。
/// </summary>
internal sealed class ReplayCounters
{
    private readonly Dictionary<uint, long> latestDetectionTimes = [];
    private readonly Dictionary<(uint Camera, uint Frame, long EventNs), Queue<long>> pending = [];
    private int engineUpdates;
    private int latestDetectionReplacements;
    private int latestNonDetectionReplacements;
    private int lateDrops;
    private int geometryResetDiscards;
    private int pendingDetections;
    private int committedDetections;
    private int committedFrames;
    private bool hasLatestNonDetection;

    /// <summary>
    /// 確定フレームごとに、最も新しい入力受信時刻から確定周期までの仮想時間をミリ秒で保持する。
    /// </summary>
    internal List<double> CommitLagMilliseconds { get; } = [];

    /// <summary>
    /// 製品バッファに入ったデータの受信時刻と、周期内で上書きされた件数を記録する。
    /// </summary>
    internal void RecordInput(SSL_DetectionFrame? detection, long receivedAtNs)
    {
        if (detection is not null)
        {
            if (!latestDetectionTimes.TryAdd(detection.CameraId, receivedAtNs))
            {
                latestDetectionReplacements++;
                latestDetectionTimes[detection.CameraId] = receivedAtNs;
            }
        }
        else
        {
            if (hasLatestNonDetection)
            {
                latestNonDetectionReplacements++;
            }

            hasLatestNonDetection = true;
        }
    }

    /// <summary>
    /// 公開更新結果を処理順に照合してから、次の周期に向けて最新入力の記録を空にする。
    /// </summary>
    internal void CompleteCycle(ReplayEngine engine, long tickOffsetNs, long firstReceiverNs)
    {
        foreach (var (detection, result) in engine.Updates)
        {
            engineUpdates++;
            lateDrops += result.Diagnostics.LatePacketDropCount;
            if (result.EmittedEvents.Any(item => item.Kind == TrackerEventKind.GeometryReset))
            {
                geometryResetDiscards += pendingDetections;
                pending.Clear();
                pendingDetections = 0;
            }

            if (detection is not null && result.Diagnostics.LatePacketDropCount == 0)
            {
                RecordAcceptedDetection(detection);
            }

            foreach (var frame in result.CommittedFrames)
            {
                RecordCommittedFrame(frame, tickOffsetNs, firstReceiverNs);
            }
        }

        engine.Updates.Clear();
        latestDetectionTimes.Clear();
        hasLatestNonDetection = false;
    }

    /// <summary>
    /// 最後の通常周期を終えた時点の集計を取得する。
    /// </summary>
    internal ReplayCounts GetCounts(int cycles, ReplayPublisher publisher, TrackedSnapshot snapshot) =>
        new(
            cycles,
            engineUpdates,
            latestDetectionReplacements,
            latestNonDetectionReplacements,
            lateDrops,
            geometryResetDiscards,
            pendingDetections,
            committedDetections,
            committedFrames,
            publisher.PacketCount,
            publisher.ByteCount,
            snapshot.PublishFailureCount);

    /// <summary>
    /// 確定結果と照合するため、Core と同じ event time の選択と丸めで受信時刻を記録する。
    /// 同じ識別子の複数の観測にも、確定順で各受信時刻を対応付ける。
    /// </summary>
    private void RecordAcceptedDetection(SSL_DetectionFrame detection)
    {
        var seconds = detection.TCapture > 0 ? detection.TCapture : detection.TSent;
        var eventNs = (long)Math.Round(seconds * 1_000_000_000d, MidpointRounding.AwayFromZero);
        var key = (detection.CameraId, detection.FrameNumber, eventNs);
        if (!pending.TryGetValue(key, out var times))
        {
            times = new Queue<long>();
            pending.Add(key, times);
        }

        times.Enqueue(latestDetectionTimes[detection.CameraId]);
        pendingDetections++;
    }

    /// <summary>
    /// エンジンが確定した各検出を保留から取り除き、最後に届いた検出を起点に仮想遅延を求める。
    /// </summary>
    private void RecordCommittedFrame(TrackerFrame frame, long tickOffsetNs, long firstReceiverNs)
    {
        committedFrames++;
        long? latestReceiveNs = null;
        foreach (var source in frame.SourceDetections)
        {
            var key = (source.CameraId, source.SourceFrameNumber, source.EventTimestampNs);
            if (!pending.TryGetValue(key, out var times))
            {
                throw new InvalidOperationException("確定フレームの検出を入力記録と照合できませんでした。");
            }

            var receivedAtNs = times.Dequeue();
            if (times.Count == 0)
            {
                pending.Remove(key);
            }

            latestReceiveNs = latestReceiveNs is long previous ? Math.Max(previous, receivedAtNs) : receivedAtNs;
            committedDetections++;
            pendingDetections--;
        }

        if (latestReceiveNs is long received)
        {
            CommitLagMilliseconds.Add((tickOffsetNs - (received - firstReceiverNs)) / 1_000_000d);
        }
    }
}
