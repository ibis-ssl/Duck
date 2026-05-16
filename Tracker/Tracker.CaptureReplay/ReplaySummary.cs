namespace Tracker.CaptureReplay;

/// <summary>
/// Capture replay が標準出力へ出す summary metric を保持する。
/// </summary>
/// <param name="PacketCount">capture から読み取った vision packet 数。</param>
/// <param name="DetectionCount">replay 入力に含まれる detection packet 数。</param>
/// <param name="GeometryCount">replay 入力に含まれる geometry packet 数。</param>
/// <param name="CommittedFrameCount">tracker engine が commit した frame 数。</param>
/// <param name="MaxBallCount">committed frame の最大 tracked ball 数。</param>
/// <param name="MaxRobotCount">committed frame の最大 tracked robot 数。</param>
/// <param name="MaxRawBallCount">committed frame に紐づく raw ball の最大数。</param>
/// <param name="MaxRawYellowCount">committed frame に紐づく raw yellow robot の最大数。</param>
/// <param name="MaxRawBlueCount">committed frame に紐づく raw blue robot の最大数。</param>
/// <param name="DetailFrames">detail filter に一致した committed frame の表示行。</param>
/// <param name="OmittedDetailFrameCount">detail 上限により省略された matching frame 数。</param>
/// <param name="TrackerSnapshotLines">metadata から解決した tracker snapshot / comparison の表示行。</param>
/// <param name="LatencyLines">raw vision cadence と ibis tracker commit lag の分析行。</param>
internal sealed record ReplaySummary(
    int PacketCount,
    int DetectionCount,
    int GeometryCount,
    int CommittedFrameCount,
    int MaxBallCount,
    int MaxRobotCount,
    int MaxRawBallCount,
    int MaxRawYellowCount,
    int MaxRawBlueCount,
    IReadOnlyList<string> DetailFrames,
    int OmittedDetailFrameCount,
    IReadOnlyList<string> TrackerSnapshotLines,
    IReadOnlyList<string> LatencyLines)
{
    /// <summary>
    /// --expect が参照する metric 名を既存 CLI schema の値へ解決する。
    /// </summary>
    public int GetMetric(string metric)
    {
        return metric switch
        {
            "packets" => PacketCount,
            "detections" => DetectionCount,
            "geometries" => GeometryCount,
            "committed-frames" => CommittedFrameCount,
            "max-balls" => MaxBallCount,
            "max-robots" => MaxRobotCount,
            "max-raw-balls" => MaxRawBallCount,
            "max-raw-yellow" => MaxRawYellowCount,
            "max-raw-blue" => MaxRawBlueCount,
            _ => throw new InvalidOperationException($"Unsupported summary metric '{metric}'."),
        };
    }
}
