namespace Tracker.DebugHost.Components.Vision;

public sealed record VisionRenderOptions
{
    public static VisionRenderOptions Default { get; } = new();

    public double RobotRadius { get; init; } = 10;

    public double RobotFrontGapAngle { get; init; } = 1.1;

    public double RobotNoseOffsetFactor { get; init; } = 0.55;

    public double RobotNoseRadius { get; init; } = 2.0;

    public double RobotLabelYOffset { get; init; } = 4.25;
}
