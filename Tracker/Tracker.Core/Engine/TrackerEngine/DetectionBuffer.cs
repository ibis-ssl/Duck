namespace Tracker.Core;

/// <summary>
/// Core tracker engine の責務別 partial 実装。
/// </summary>
public sealed partial class TrackerEngine
{
    /// <summary>
    /// SSL_DetectionFrame を event-time buffer 用の immutable snapshot に変換する。
    /// </summary>
    private static BufferedDetection CreateBufferedDetection(SSL_DetectionFrame detection)
    {
        return new BufferedDetection(
            detection.FrameNumber,
            detection.CameraId,
            ConvertSecondsToNanoseconds(SelectEventTimeSeconds(detection)),
            detection.Balls.ToList(),
            detection.RobotsYellow.ToList(),
            detection.RobotsBlue.ToList());
    }

    /// <summary>
    /// committed frame に記録する raw detection snapshot を clone して作る。
    /// </summary>
    private static IReadOnlyList<TrackerSourceDetectionFrame> CreateSourceDetectionFrames(
        IReadOnlyList<BufferedDetection> detections)
    {
        return detections
            .Select(detection => new TrackerSourceDetectionFrame
            {
                SourceFrameNumber = detection.SourceFrameNumber,
                CameraId = detection.CameraId,
                EventTimestampNs = detection.EventTimestampNs,
                Balls = detection.Balls.Select(ball => ball.Clone()).ToArray(),
                RobotsYellow = detection.RobotsYellow.Select(robot => robot.Clone()).ToArray(),
                RobotsBlue = detection.RobotsBlue.Select(robot => robot.Clone()).ToArray(),
            })
            .ToArray();
    }

    /// <summary>
    /// detection の event time を選ぶ。TCapture が正なら優先し、0 以下なら TSent を使う。
    /// </summary>
    private static double SelectEventTimeSeconds(SSL_DetectionFrame detection)
    {
        return detection.TCapture > 0 ? detection.TCapture : detection.TSent;
    }

    /// <summary>
    /// event time 順の detection を merge window 単位の committed frame 候補へまとめる。
    /// </summary>
    private static List<BufferedDetectionGroup> BuildDetectionGroups(
        List<BufferedDetection> orderedDetections,
        long mergeWindowNs)
    {
        var groups = new List<BufferedDetectionGroup>();
        var currentGroup = new List<BufferedDetection>();
        var currentAnchorTimestampNs = 0L;

        foreach (var detection in orderedDetections)
        {
            if (currentGroup.Count == 0)
            {
                currentGroup.Add(detection);
                currentAnchorTimestampNs = detection.EventTimestampNs;
                continue;
            }

            if (detection.EventTimestampNs - currentAnchorTimestampNs <= mergeWindowNs)
            {
                currentGroup.Add(detection);
                continue;
            }

            groups.Add(new BufferedDetectionGroup(
                currentAnchorTimestampNs,
                currentAnchorTimestampNs + mergeWindowNs,
                [.. currentGroup]));
            currentGroup = [detection];
            currentAnchorTimestampNs = detection.EventTimestampNs;
        }

        if (currentGroup.Count > 0)
        {
            groups.Add(new BufferedDetectionGroup(
                currentAnchorTimestampNs,
                currentAnchorTimestampNs + mergeWindowNs,
                [.. currentGroup]));
        }

        return groups;
    }

    /// <summary>
    /// reorder buffer に保持する raw detection frame の snapshot。
    /// </summary>
    private sealed record BufferedDetection(
        uint SourceFrameNumber,
        uint CameraId,
        long EventTimestampNs,
        IReadOnlyList<SSL_DetectionBall> Balls,
        IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
        IReadOnlyList<SSL_DetectionRobot> RobotsBlue);

    /// <summary>
    /// merge window 内で同じ world frame に統合する detection group。
    /// </summary>
    private sealed record BufferedDetectionGroup(
        long AnchorTimestampNs,
        long CloseTimestampNs,
        IReadOnlyList<BufferedDetection> Detections);
}
