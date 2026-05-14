using Tracker.Core;
using Tracker.DebugHost.Tracking;

namespace Tracker.DebugHost.Components.Pages;

/// <summary>
/// render snapshot に保存された tracker geometry と source detections を VisionFieldCanvas 用 DTO に戻す。
/// </summary>
internal static class DiagnosticsFieldViewFactory
{
    /// <summary>
    /// tracker render snapshot の geometry を SSL_GeometryData に変換する。
    /// </summary>
    public static SSL_GeometryData? CreateGeometry(TrackerGeometrySnapshot? geometrySnapshot)
    {
        if (geometrySnapshot is null)
        {
            return null;
        }

        var field = new SSL_GeometryFieldSize
        {
            FieldLength = geometrySnapshot.FieldLengthMm,
            FieldWidth = geometrySnapshot.FieldWidthMm,
            GoalWidth = geometrySnapshot.GoalWidthMm,
            GoalDepth = geometrySnapshot.GoalDepthMm,
            BoundaryWidth = geometrySnapshot.BoundaryWidthMm,
            BoundaryWidthGoalLine = geometrySnapshot.BoundaryWidthGoalLineMm,
            PenaltyAreaDepth = geometrySnapshot.PenaltyAreaDepthMm,
            PenaltyAreaWidth = geometrySnapshot.PenaltyAreaWidthMm,
            CenterCircleRadius = geometrySnapshot.CenterCircleRadiusMm,
            LineThickness = geometrySnapshot.LineThicknessMm,
        };
        field.FieldLines.AddRange(geometrySnapshot.FieldLines.Select(CreateFieldLine));
        field.FieldArcs.AddRange(geometrySnapshot.FieldArcs.Select(CreateFieldArc));

        return new SSL_GeometryData { Field = field };
    }

    /// <summary>
    /// tracker frame に保存された raw ball source detections を表示用に結合する。
    /// </summary>
    public static IReadOnlyList<SSL_DetectionBall> CreateRawBalls(TrackerFrame frame)
    {
        return frame.SourceDetections
            .SelectMany(detection => detection.Balls)
            .ToArray();
    }

    /// <summary>
    /// tracker frame に保存された yellow robot source detections を表示用に結合する。
    /// </summary>
    public static IReadOnlyList<SSL_DetectionRobot> CreateRawYellowRobots(TrackerFrame frame)
    {
        return frame.SourceDetections
            .SelectMany(detection => detection.RobotsYellow)
            .ToArray();
    }

    /// <summary>
    /// tracker frame に保存された blue robot source detections を表示用に結合する。
    /// </summary>
    public static IReadOnlyList<SSL_DetectionRobot> CreateRawBlueRobots(TrackerFrame frame)
    {
        return frame.SourceDetections
            .SelectMany(detection => detection.RobotsBlue)
            .ToArray();
    }

    /// <summary>
    /// tracker packet semantic summary の ball を Field 表示用 DTO に変換する。
    /// </summary>
    public static IReadOnlyList<SSL_DetectionBall> CreateTrackerSourceBalls(
        TrackerPacketSnapshotSemanticSummary? summary)
    {
        return summary?.Balls
            .Select(ball => new SSL_DetectionBall
            {
                Confidence = ball.Visibility,
                X = (float)ball.XMm,
                Y = (float)ball.YMm,
                Z = (float)ball.ZMm,
            })
            .ToArray() ?? [];
    }

    /// <summary>
    /// tracker packet semantic summary の yellow robot を Field 表示用 DTO に変換する。
    /// </summary>
    public static IReadOnlyList<SSL_DetectionRobot> CreateTrackerSourceYellowRobots(
        TrackerPacketSnapshotSemanticSummary? summary)
    {
        return CreateTrackerSourceRobots(summary, "Yellow");
    }

    /// <summary>
    /// tracker packet semantic summary の blue robot を Field 表示用 DTO に変換する。
    /// </summary>
    public static IReadOnlyList<SSL_DetectionRobot> CreateTrackerSourceBlueRobots(
        TrackerPacketSnapshotSemanticSummary? summary)
    {
        return CreateTrackerSourceRobots(summary, "Blue");
    }

    private static SSL_FieldLineSegment CreateFieldLine(TrackerGeometryLineSegment line)
    {
        return new SSL_FieldLineSegment
        {
            Name = line.Name,
            P1 = new Vector2f { X = (float)line.P1XMm, Y = (float)line.P1YMm },
            P2 = new Vector2f { X = (float)line.P2XMm, Y = (float)line.P2YMm },
            Thickness = (float)line.ThicknessMm,
            Type = line.Type,
        };
    }

    private static IReadOnlyList<SSL_DetectionRobot> CreateTrackerSourceRobots(
        TrackerPacketSnapshotSemanticSummary? summary,
        string team)
    {
        return summary?.Robots
            .Where(robot => string.Equals(robot.Team, team, StringComparison.OrdinalIgnoreCase))
            .Select(robot => new SSL_DetectionRobot
            {
                Confidence = robot.Visibility,
                RobotId = robot.RobotId,
                X = (float)robot.XMm,
                Y = (float)robot.YMm,
                Orientation = robot.OrientationRad,
            })
            .ToArray() ?? [];
    }

    private static SSL_FieldCircularArc CreateFieldArc(TrackerGeometryCircularArc arc)
    {
        return new SSL_FieldCircularArc
        {
            Name = arc.Name,
            Center = new Vector2f { X = (float)arc.CenterXMm, Y = (float)arc.CenterYMm },
            Radius = (float)arc.RadiusMm,
            A1 = (float)arc.A1Rad,
            A2 = (float)arc.A2Rad,
            Thickness = (float)arc.ThicknessMm,
            Type = arc.Type,
        };
    }
}
