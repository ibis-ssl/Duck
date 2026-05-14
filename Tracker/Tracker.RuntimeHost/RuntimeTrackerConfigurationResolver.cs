using Tracker.Core;

namespace Tracker.RuntimeHost;

/// <summary>
/// RuntimeHost の tracker appsettings schema から Core runtime 設定を解決する。
/// </summary>
public static class RuntimeTrackerConfigurationResolver
{
    /// <summary>
    /// RuntimeHost tracker options を `TrackerCoordinator` が使う resolved options に変換する。
    /// </summary>
    public static TrackerRuntimeResolvedOptions Resolve(RuntimeTrackerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var profileName = string.IsNullOrWhiteSpace(options.ActiveProfileName)
            ? "default"
            : options.ActiveProfileName;
        var profile = ResolveProfile(options, profileName);

        return new TrackerRuntimeResolvedOptions
        {
            Enabled = options.Enabled,
            EngineSettings = new TrackerEngineSettings
            {
                ProfileName = profileName,
                ReorderWindowNs = profile.Engine.ReorderWindowNs,
                MergeWindowNs = profile.Engine.MergeWindowNs,
                GeometryResetFieldLengthThresholdMm = profile.Engine.GeometryResetFieldLengthThresholdMm,
                GeometryResetFieldWidthThresholdMm = profile.Engine.GeometryResetFieldWidthThresholdMm,
                KalmanInitialVelocityVariance = profile.Engine.KalmanInitialVelocityVariance,
                KalmanProcessNoiseScale = profile.Engine.KalmanProcessNoiseScale,
                MeasurementNoiseVarianceScale = profile.Engine.MeasurementNoiseVarianceScale,
                RobotTracker = profile.RobotTracker,
                BallTracker = profile.BallTracker,
                KickDetector = profile.KickDetector,
            },
            PublisherOptions = new TrackerPublisherOptions
            {
                PublishUdp = options.PublishUdp,
                MulticastAddress = profile.Publish.MulticastAddress,
                Port = profile.Publish.Port,
                SourceName = options.SourceName,
                Uuid = options.Uuid,
            },
        };
    }

    private static RuntimeTrackerProfileOptions ResolveProfile(
        RuntimeTrackerOptions options,
        string profileName)
    {
        if (options.Profiles.TryGetValue(profileName, out var profile))
        {
            return profile;
        }

        throw new InvalidOperationException(
            $"Tracker active profile '{profileName}' was not found in Tracker:Profiles.");
    }
}
