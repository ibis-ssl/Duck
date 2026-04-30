namespace Tracker.Server.Vision;

public sealed class VisionFieldProjection
{
    public const double DefaultFieldLength = 12000;
    public const double DefaultFieldWidth = 9000;
    public const double DefaultViewBoxWidth = 1000;
    public const double DefaultViewBoxHeight = 720;
    public const double DefaultMargin = 48;

    private VisionFieldProjection(double fieldLength, double fieldWidth)
    {
        FieldLength = fieldLength;
        FieldWidth = fieldWidth;
        ViewBoxWidth = DefaultViewBoxWidth;
        ViewBoxHeight = DefaultViewBoxHeight;
        Scale = Math.Min((ViewBoxWidth - (DefaultMargin * 2)) / FieldLength, (ViewBoxHeight - (DefaultMargin * 2)) / FieldWidth);
    }

    public double FieldLength { get; }

    public double FieldWidth { get; }

    public double ViewBoxWidth { get; }

    public double ViewBoxHeight { get; }

    public double Scale { get; }

    public static VisionFieldProjection FromGeometry(SSL_GeometryData? geometry)
    {
        var field = geometry?.Field;
        var fieldLength = field?.FieldLength > 0 ? field.FieldLength : DefaultFieldLength;
        var fieldWidth = field?.FieldWidth > 0 ? field.FieldWidth : DefaultFieldWidth;

        return new VisionFieldProjection(fieldLength, fieldWidth);
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
}

public readonly record struct SvgPoint(double X, double Y);
