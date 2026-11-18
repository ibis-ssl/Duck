namespace Tracker.RuntimeHost;

/// <summary>Simulator UDP control settings used to move its localhost Vision stream off a shared consumer port.</summary>
public sealed class RuntimeSimulatorVisionPortOptions
{
    public const string SectionName = "SimulatorVisionPort";

    public bool Enabled { get; init; }
    public string ControlAddress { get; init; } = "127.0.0.1";
    public int ControlPort { get; init; } = 10300;
    public int VisionPort { get; init; } = 10021;
    public bool SpawnDetectionRobot { get; init; }
}
