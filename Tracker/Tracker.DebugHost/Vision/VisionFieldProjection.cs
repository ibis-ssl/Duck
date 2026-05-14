namespace Tracker.DebugHost.Vision;

public sealed class VisionFieldProjection
{
    public const double DefaultFieldLength = 12000;
    public const double DefaultFieldWidth = 9000;
    public const double DefaultViewBoxWidth = 1000;
    public const double DefaultViewBoxHeight = 720;
    public const double DefaultMargin = 48;
    public const double DefaultBoundaryWidth = 300;

    private VisionFieldProjection(double fieldLength, double fieldWidth, double outerLengthMargin, double outerWidthMargin)
    {
        FieldLength = fieldLength;
        FieldWidth = fieldWidth;
        OuterLengthMargin = outerLengthMargin;
        OuterWidthMargin = outerWidthMargin;
        ViewBoxWidth = DefaultViewBoxWidth;
        ViewBoxHeight = DefaultViewBoxHeight;
        Scale = Math.Min(
            (ViewBoxWidth - (DefaultMargin * 2)) / (FieldLength + (OuterLengthMargin * 2)),
            (ViewBoxHeight - (DefaultMargin * 2)) / (FieldWidth + (OuterWidthMargin * 2)));
    }

    public double FieldLength { get; }

    public double FieldWidth { get; }

    public double OuterLengthMargin { get; }

    public double OuterWidthMargin { get; }

    public double ViewBoxWidth { get; }

    public double ViewBoxHeight { get; }

    public double Scale { get; }

    public static VisionFieldProjection FromGeometry(SSL_GeometryData? geometry)
    {
        var field = geometry?.Field;
        var fieldLength = field?.FieldLength > 0 ? field.FieldLength : DefaultFieldLength;
        var fieldWidth = field?.FieldWidth > 0 ? field.FieldWidth : DefaultFieldWidth;
        var boundaryWidth = field?.BoundaryWidth > 0 ? field.BoundaryWidth : DefaultBoundaryWidth;
        var boundaryWidthGoalLine = field?.BoundaryWidthGoalLine > 0 ? field.BoundaryWidthGoalLine : boundaryWidth;
        var goalDepth = field?.GoalDepth > 0 ? field.GoalDepth : 0;
        var outerLengthMargin = Math.Max(boundaryWidthGoalLine, goalDepth);
        var outerWidthMargin = boundaryWidth;

        return new VisionFieldProjection(fieldLength, fieldWidth, outerLengthMargin, outerWidthMargin);
    }

    public SvgPoint Project(double x, double y)
    {
        return new SvgPoint(
            (ViewBoxWidth / 2) + (x * Scale),
            (ViewBoxHeight / 2) - (y * Scale));
    }

    public double ProjectLength(double length)
    {
        return length * Scale;
    }

    public FieldPoint Unproject(double svgX, double svgY)
    {
        return new FieldPoint(
            (svgX - (ViewBoxWidth / 2)) / Scale,
            ((ViewBoxHeight / 2) - svgY) / Scale);
    }
}

public readonly record struct SvgPoint(double X, double Y);

public readonly record struct FieldPoint(double X, double Y);
