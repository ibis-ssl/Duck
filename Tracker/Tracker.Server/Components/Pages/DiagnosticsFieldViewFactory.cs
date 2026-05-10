using Tracker.Core;

namespace Tracker.Server.Components.Pages;

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
