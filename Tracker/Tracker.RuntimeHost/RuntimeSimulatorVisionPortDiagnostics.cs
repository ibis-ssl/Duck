namespace Tracker.RuntimeHost;

public sealed record RuntimeSimulatorVisionPortSnapshot(
    bool VisionPortCommandSent,
    int VisionPort,
    bool DetectionRobotCommandSent,
    bool PositiveSuccessResponseExpected,
    int DetectionRobotObservedCount);

public sealed class RuntimeSimulatorVisionPortDiagnostics
{
    private readonly object gate = new();
    private bool visionPortCommandSent;
    private int visionPort;
    private bool detectionRobotCommandSent;
    private int detectionRobotObservedCount;

    public RuntimeSimulatorVisionPortSnapshot Snapshot()
    {
        lock (gate)
        {
            return new(visionPortCommandSent, visionPort, detectionRobotCommandSent, false,
                detectionRobotObservedCount);
        }
    }

    internal void RecordVisionPortCommandSent(int port)
    {
        lock (gate)
        {
            visionPort = port;
            visionPortCommandSent = true;
        }
    }

    internal void RecordDetectionRobotCommandSent()
    {
        lock (gate) detectionRobotCommandSent = true;
    }

    internal void RecordDetectionRobotObserved()
    {
        lock (gate) detectionRobotObservedCount++;
    }

}
