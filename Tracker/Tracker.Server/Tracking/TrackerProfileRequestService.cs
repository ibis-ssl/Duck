using Microsoft.Extensions.Options;
using Tracker.Core;

namespace Tracker.Server.Tracking;

public sealed class TrackerProfileRequestService(
    IOptions<TrackerOptions> options,
    TrackerCoordinator coordinator)
{
    public void RequestProfileSwitch(
        string profileName,
        DateTimeOffset? requestedAt = null,
        TrackerRuntimeOverrides? runtimeOverrides = null)
    {
        var resolved = TrackerConfigurationResolver.Resolve(options.Value, profileName, runtimeOverrides);
        coordinator.RequestProfileSwitch(
            resolved,
            requestedAt ?? DateTimeOffset.UtcNow,
            runtimeOverrides);
    }
}
