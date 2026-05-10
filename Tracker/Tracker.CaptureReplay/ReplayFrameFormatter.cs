using System.Globalization;
using Tracker.Core;

/// <summary>
/// Capture replay の detail 行を、従来 CLI と同じ schema の文字列へ整形する。
/// </summary>
internal static class ReplayFrameFormatter
{
    /// <summary>
    /// detail 出力対象の committed frame を 1 行の互換フォーマットへ変換する。
    /// </summary>
    public static string FormatFrame(
        int packetIndex,
        DateTimeOffset receivedAt,
        TrackerFrame frame)
    {
        var rawSummary =
            $"rawFrame={FormatSourceFrameNumbers(frame)} rawCamera={FormatSourceCameraIds(frame)} rawBalls={CountRawBalls(frame)} rawYellow={CountRawYellowRobots(frame)} rawBlue={CountRawBlueRobots(frame)}";
        var balls = string.Join("; ", frame.Balls.Select(ball =>
            $"#{ball.InternalTrackId}:x={ball.XMm.ToString("F1", CultureInfo.InvariantCulture)},y={ball.YMm.ToString("F1", CultureInfo.InvariantCulture)},vis={ball.Visibility.ToString("F3", CultureInfo.InvariantCulture)},cams={string.Join("/", ball.SourceCameraIds.OrderBy(id => id))}"));
        var robots = string.Join("; ", frame.Robots.Take(8).Select(robot =>
            $"{robot.Team}{robot.RobotId}:x={robot.XMm.ToString("F1", CultureInfo.InvariantCulture)},y={robot.YMm.ToString("F1", CultureInfo.InvariantCulture)},vis={robot.Visibility.ToString("F3", CultureInfo.InvariantCulture)}"));
        var robotSuffix = frame.Robots.Count > 8 ? $"; ... +{frame.Robots.Count - 8}" : "";

        return $"input={packetIndex} receivedAt={receivedAt:O} {rawSummary} committedFrame={frame.FrameNumber} dataTs={frame.DataTimestampNs} balls={frame.Balls.Count} [{balls}] robots={frame.Robots.Count} [{robots}{robotSuffix}]";
    }

    /// <summary>
    /// committed frame に紐づく source detection の ball 数を集計する。
    /// </summary>
    public static int CountRawBalls(TrackerFrame frame)
    {
        return frame.SourceDetections.Sum(detection => detection.Balls.Count);
    }

    /// <summary>
    /// committed frame に紐づく yellow robot の source detection 数を集計する。
    /// </summary>
    public static int CountRawYellowRobots(TrackerFrame frame)
    {
        return frame.SourceDetections.Sum(detection => detection.RobotsYellow.Count);
    }

    /// <summary>
    /// committed frame に紐づく blue robot の source detection 数を集計する。
    /// </summary>
    public static int CountRawBlueRobots(TrackerFrame frame)
    {
        return frame.SourceDetections.Sum(detection => detection.RobotsBlue.Count);
    }

    private static string FormatSourceFrameNumbers(TrackerFrame frame)
    {
        return FormatSourceValues(frame.SourceDetections.Select(detection => detection.SourceFrameNumber));
    }

    private static string FormatSourceCameraIds(TrackerFrame frame)
    {
        return FormatSourceValues(frame.SourceDetections.Select(detection => detection.CameraId));
    }

    private static string FormatSourceValues(IEnumerable<uint> values)
    {
        var distinctValues = values
            .Distinct()
            .Order()
            .Select(value => value.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        return distinctValues.Length == 0 ? "-" : string.Join("/", distinctValues);
    }
}
