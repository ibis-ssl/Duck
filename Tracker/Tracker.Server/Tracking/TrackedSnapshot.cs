using Tracker.Core;

namespace Tracker.Server.Tracking;

public sealed record TrackedSnapshot(
    TrackerFrame? LatestFrame,
    DateTimeOffset? ReceivedAt,
    string ActiveProfileName,
    long PublishSuccessCount,
    long PublishFailureCount);
