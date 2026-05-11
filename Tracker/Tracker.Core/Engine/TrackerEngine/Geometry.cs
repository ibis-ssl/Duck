namespace Tracker.Core;

/// <summary>
/// Core tracker engine の責務別 partial 実装。
/// </summary>
public sealed partial class TrackerEngine
{
    /// <summary>
    /// geometry snapshot の field size 差分から tracking state reset が必要か判定する。
    /// </summary>
    private bool ShouldResetForGeometryChange(
        TrackerGeometrySnapshot updatedGeometrySnapshot,
        TrackerEngineSettings settings)
    {
        if (geometrySnapshot is null)
        {
            return false;
        }

        return Math.Abs(updatedGeometrySnapshot.FieldLengthMm - geometrySnapshot.FieldLengthMm)
                >= settings.GeometryResetFieldLengthThresholdMm
            || Math.Abs(updatedGeometrySnapshot.FieldWidthMm - geometrySnapshot.FieldWidthMm)
                >= settings.GeometryResetFieldWidthThresholdMm
            || updatedGeometrySnapshot.GoalWidthMm != geometrySnapshot.GoalWidthMm
            || updatedGeometrySnapshot.GoalDepthMm != geometrySnapshot.GoalDepthMm;
    }

    /// <summary>
    /// SSL_GeometryData を Core 内部の geometry snapshot に変換する。
    /// </summary>
    private static TrackerGeometrySnapshot CreateGeometrySnapshot(SSL_GeometryData geometry)
    {
        var field = geometry.Field;
        return new TrackerGeometrySnapshot
        {
            FieldLengthMm = field?.FieldLength ?? 0,
            FieldWidthMm = field?.FieldWidth ?? 0,
            GoalWidthMm = field?.GoalWidth ?? 0,
            GoalDepthMm = field?.GoalDepth ?? 0,
            BoundaryWidthMm = field?.BoundaryWidth ?? 0,
            BoundaryWidthGoalLineMm = field is not null && field.BoundaryWidthGoalLine > 0
                ? field.BoundaryWidthGoalLine
                : field?.BoundaryWidth ?? 0,
            PenaltyAreaDepthMm = field?.PenaltyAreaDepth ?? 0,
            PenaltyAreaWidthMm = field?.PenaltyAreaWidth ?? 0,
            CenterCircleRadiusMm = field?.CenterCircleRadius ?? 0,
            LineThicknessMm = field is not null && field.LineThickness > 0
                ? field.LineThickness
                : 10,
            FieldLines = field?.FieldLines.Select(CreateGeometryLineSegment).ToArray() ?? [],
            FieldArcs = field?.FieldArcs.Select(CreateGeometryCircularArc).ToArray() ?? [],
        };
    }

    /// <summary>
    /// SSL-Vision の field line を内部 geometry line segment に変換する。
    /// </summary>
    private static TrackerGeometryLineSegment CreateGeometryLineSegment(SSL_FieldLineSegment line)
    {
        return new TrackerGeometryLineSegment
        {
            Name = line.Name,
            P1XMm = line.P1?.X ?? 0,
            P1YMm = line.P1?.Y ?? 0,
            P2XMm = line.P2?.X ?? 0,
            P2YMm = line.P2?.Y ?? 0,
            ThicknessMm = line.Thickness,
            Type = line.Type,
        };
    }

    /// <summary>
    /// SSL-Vision の field arc を内部 geometry circular arc に変換する。
    /// </summary>
    private static TrackerGeometryCircularArc CreateGeometryCircularArc(SSL_FieldCircularArc arc)
    {
        return new TrackerGeometryCircularArc
        {
            Name = arc.Name,
            CenterXMm = arc.Center?.X ?? 0,
            CenterYMm = arc.Center?.Y ?? 0,
            RadiusMm = arc.Radius,
            A1Rad = arc.A1,
            A2Rad = arc.A2,
            ThicknessMm = arc.Thickness,
            Type = arc.Type,
        };
    }
}
