using System.Globalization;
using Tracker.Core;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// diagnostics log の raw / tracked object 詳細を既存 log schema の文字列へ整形する。
/// </summary>
internal static class TrackerDiagnosticsFormatter
{
    /// <summary>
    /// source detection に含まれる frame number の distinct 表示を作る。
    /// </summary>
    public static string FormatSourceFrameNumbers(IReadOnlyList<TrackerSourceDetectionFrame> sourceDetections)
    {
        return FormatSourceValues(sourceDetections.Select(detection => detection.SourceFrameNumber));
    }

    /// <summary>
    /// source detection に含まれる camera id の distinct 表示を作る。
    /// </summary>
    public static string FormatSourceCameraIds(IReadOnlyList<TrackerSourceDetectionFrame> sourceDetections)
    {
        return FormatSourceValues(sourceDetections.Select(detection => detection.CameraId));
    }

    /// <summary>
    /// raw ball detection を diagnostics log の detail 文字列へ変換する。
    /// </summary>
    public static string FormatRawBalls(IEnumerable<SSL_DetectionBall>? balls)
    {
        return FormatItems(
            balls,
            ball => FormattableString.Invariant(
                $"x={ball.X:0.#},y={ball.Y:0.#},z={ball.Z:0.#},c={ball.Confidence:0.###}"));
    }

    /// <summary>
    /// raw robot detection を team prefix 付きの diagnostics log detail 文字列へ変換する。
    /// </summary>
    public static string FormatRawRobots(
        IEnumerable<SSL_DetectionRobot>? robots,
        TrackerTeam team)
    {
        var teamPrefix = team == TrackerTeam.Blue ? "B" : "Y";
        return FormatItems(
            robots,
            robot => FormattableString.Invariant(
                $"{teamPrefix}{robot.RobotId}:x={robot.X:0.#},y={robot.Y:0.#},o={robot.Orientation:0.###},c={robot.Confidence:0.###}"));
    }

    /// <summary>
    /// tracked ball state を diagnostics log detail 文字列へ変換する。
    /// </summary>
    public static string FormatTrackedBalls(IEnumerable<TrackedBallState> balls)
    {
        return FormatItems(
            balls,
            ball => FormattableString.Invariant(
                $"#{ball.InternalTrackId}:x={ball.XMm:0.#},y={ball.YMm:0.#},z={ball.ZMm:0.#},vis={ball.Visibility:0.###},q={ball.Quality:0.###},cams={string.Join("/", ball.SourceCameraIds)}"));
    }

    /// <summary>
    /// tracked robot state を diagnostics log detail 文字列へ変換する。
    /// </summary>
    public static string FormatTrackedRobots(IEnumerable<TrackedRobotState> robots)
    {
        return FormatItems(
            robots,
            robot => FormattableString.Invariant(
                $"{FormatTeam(robot.Team)}{robot.RobotId}:x={robot.XMm:0.#},y={robot.YMm:0.#},o={robot.OrientationRad:0.###},w={robot.AngularVelocityRadPerS:0.###},vis={robot.Visibility:0.###},q={robot.Quality:0.###}"));
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

    private static string FormatTeam(TrackerTeam team)
    {
        return team switch
        {
            TrackerTeam.Blue => "B",
            TrackerTeam.Yellow => "Y",
            _ => "?",
        };
    }

    private static string FormatItems<T>(
        IEnumerable<T>? items,
        Func<T, string> formatter)
    {
        if (items is null)
        {
            return "";
        }

        var formattedItems = items.Take(16).Select(formatter).ToList();
        return formattedItems.Count == 0 ? "" : string.Join("; ", formattedItems);
    }
}
