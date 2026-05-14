namespace Tracker.DebugHost.Vision;

public sealed class VisionPacketCaptureRuntimeControl
{
    private readonly object gate = new();
    private bool enabled;

    public VisionPacketCaptureRuntimeControl(bool initialEnabled)
    {
        enabled = initialEnabled;
    }

    public bool Enabled
    {
        get
        {
            lock (gate)
            {
                return enabled;
            }
        }
    }

    public void SetEnabled(bool value)
    {
        lock (gate)
        {
            enabled = value;
        }
    }
}
