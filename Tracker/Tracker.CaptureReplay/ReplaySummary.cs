namespace Tracker.CaptureReplay;

/// <summary>
/// Capture replay が標準出力へ出す summary metric を保持する。
/// </summary>
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
    int OmittedDetailFrameCount)
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
