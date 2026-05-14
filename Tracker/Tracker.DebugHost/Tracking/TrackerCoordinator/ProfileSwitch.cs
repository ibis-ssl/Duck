using Tracker.Core;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// TrackerCoordinator の profile switch 要求昇格と適用タイミングを担当する partial class。
/// </summary>
public sealed partial class TrackerCoordinator
{
    /// <summary>
    /// ProfileSwitched 受信後にだけ applied/current 設定と publisher / snapshot store を新 profile へ同期する。
    /// </summary>
    private void ApplyProfileSwitch(string profileName)
    {
        if (inFlightRequest is not null)
        {
            appliedOptions = TrackerOptionsCloner.CloneResolvedOptions(inFlightRequest.TargetOptions);
            desiredOptions = TrackerOptionsCloner.CloneResolvedOptions(inFlightRequest.TargetOptions);
            desiredRuntimeOverrides = TrackerOptionsCloner.CloneRuntimeOverrides(inFlightRequest.RuntimeOverrides);
            currentSettings = TrackerOptionsCloner.CloneSettings(inFlightRequest.TargetOptions.EngineSettings);
            currentPublisherOptions = TrackerOptionsCloner.ClonePublisherOptions(inFlightRequest.TargetOptions.PublisherOptions);
            publisher.ApplyConfiguration(currentPublisherOptions);
            snapshotStore.SwitchActiveProfile(profileName);
            inFlightRequest = null;
        }
        else
        {
            snapshotStore.SwitchActiveProfile(profileName);
        }

        NotifyObservers(observer => observer.OnProfileSwitched(profileName));
    }

    /// <summary>
    /// pending 要求を engine 投入中の要求へ昇格し、ProfileSwitched まで UI / publisher 側の反映を遅延させる。
    /// </summary>
    private TrackerProfileSwitchRequest? PromotePendingRequest()
    {
        if (inFlightRequest is not null || pendingRequest is null)
        {
            return null;
        }

        inFlightRequest = pendingRequest;
        pendingRequest = null;
        return new TrackerProfileSwitchRequest
        {
            RequestVersion = inFlightRequest.RequestVersion,
            ProfileName = inFlightRequest.TargetOptions.EngineSettings.ProfileName,
            ResolvedBaseSettings = TrackerOptionsCloner.CloneSettings(inFlightRequest.TargetOptions.EngineSettings),
            RuntimeOverrides = TrackerOptionsCloner.CloneRuntimeOverrides(inFlightRequest.RuntimeOverrides),
        };
    }

    /// <summary>
    /// engine へ投入済みまたは投入待ちの profile switch 要求を保持する。
    /// </summary>
    private sealed record PendingProfileSwitchRequest(
        int RequestVersion,
        TrackerResolvedOptions TargetOptions,
        TrackerRuntimeOverrides RuntimeOverrides);
}
