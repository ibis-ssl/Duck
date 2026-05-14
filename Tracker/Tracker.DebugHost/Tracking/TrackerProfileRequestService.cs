using Microsoft.Extensions.Options;
using Tracker.Core;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// UI などの外部要求を受け取り、tracker profile switch 要求として coordinator へ渡す service。
/// </summary>
public sealed class TrackerProfileRequestService(
    IOptions<TrackerOptions> options,
    TrackerCoordinator coordinator)
{
    /// <summary>
    /// 指定 profile と任意の runtime override を解決し、tracker coordinator へ profile switch を要求する。
    /// </summary>
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
