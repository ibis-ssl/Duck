using Tracker.Core;

namespace Tracker.Server.Tracking;

public sealed class TrackedSnapshotStore
{
    private readonly object gate = new();
    private TrackerFrame? latestFrame;
    private DateTimeOffset? receivedAt;
    private string activeProfileName;
    private long publishSuccessCount;
    private long publishFailureCount;

    public TrackedSnapshotStore(string initialActiveProfileName = "default")
    {
        activeProfileName = string.IsNullOrWhiteSpace(initialActiveProfileName)
            ? "default"
            : initialActiveProfileName;
    }

    public TrackedSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new TrackedSnapshot(
                latestFrame,
                receivedAt,
                activeProfileName,
                publishSuccessCount,
                publishFailureCount);
        }
    }

    public void UpdateLatestFrame(TrackerFrame frame, DateTimeOffset frameReceivedAt)
    {
        lock (gate)
        {
            latestFrame = frame;
            receivedAt = frameReceivedAt;
            activeProfileName = frame.Metadata.ProfileName ?? activeProfileName;
        }
    }

    public void ClearLatestFrame()
    {
        lock (gate)
        {
            latestFrame = null;
            receivedAt = null;
        }
    }

    public void SwitchActiveProfile(string profileName)
    {
        lock (gate)
        {
            activeProfileName = profileName;
            latestFrame = null;
            receivedAt = null;
        }
    }

    public void SetActiveProfileName(string profileName)
    {
        lock (gate)
        {
            activeProfileName = profileName;
        }
    }

    public void RecordPublishSuccess()
    {
        lock (gate)
        {
            publishSuccessCount++;
        }
    }

    public void RecordPublishFailure()
    {
        lock (gate)
        {
            publishFailureCount++;
        }
    }
}
