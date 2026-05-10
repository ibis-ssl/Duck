using Microsoft.Extensions.Options;
using Tracker.Core;

namespace Tracker.Server.Vision;

public sealed class VisionReceiverProfileSwitchObserver(
    IOptions<VisionReceiverOptions> options,
    VisionReceiverRuntimeOptionsStore runtimeOptionsStore) : ITrackerObserver
{
    public void OnProfileSwitched(string profileName)
    {
        runtimeOptionsStore.ApplyConfiguration(
            VisionReceiverConfigurationResolver.Resolve(options.Value, profileName));
    }

    public void OnGeometryReset()
    {
    }

    public void OnWorldFrameCommitted(TrackerFrame frame)
    {
    }

    public void OnKickDetected(KickEventState kick, TrackerFrame frame)
    {
    }

    public void OnContactChanged(TrackerFrame frame)
    {
    }

    public void OnBallLeftField(BallLeftFieldState state, TrackerFrame frame)
    {
    }
}
