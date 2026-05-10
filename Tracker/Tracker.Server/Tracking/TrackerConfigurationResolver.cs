using Tracker.Core;

namespace Tracker.Server.Tracking;

public static class TrackerConfigurationResolver
{
    public static TrackerResolvedOptions Resolve(TrackerOptions options)
    {
        return Resolve(options, options.ActiveProfileName, options.RuntimeOverrides);
    }

    public static TrackerResolvedOptions Resolve(
        TrackerOptions options,
        string profileName,
        TrackerRuntimeOverrides? runtimeOverrides = null)
    {
        if (!options.Profiles.TryGetValue(profileName, out var activeProfile))
        {
            throw new InvalidOperationException(
                $"Tracker active profile '{profileName}' was not found in Tracker:Profiles.");
        }

        var effectiveRuntimeOverrides = runtimeOverrides ?? options.RuntimeOverrides;

        return new TrackerResolvedOptions
        {
            Enabled = options.Enabled,
            EngineSettings = new TrackerEngineSettings
            {
                ProfileName = profileName,
                ReorderWindowNs = activeProfile.Engine.ReorderWindowNs,
                MergeWindowNs = activeProfile.Engine.MergeWindowNs,
                GeometryResetFieldLengthThresholdMm = activeProfile.Engine.GeometryResetFieldLengthThresholdMm,
                GeometryResetFieldWidthThresholdMm = activeProfile.Engine.GeometryResetFieldWidthThresholdMm,
                RobotTracker = ResolveRobotTracker(activeProfile.RobotTracker, effectiveRuntimeOverrides.RobotTracker),
                BallTracker = ResolveBallTracker(activeProfile.BallTracker, effectiveRuntimeOverrides.BallTracker),
                KickDetector = ResolveKickDetector(activeProfile.KickDetector, effectiveRuntimeOverrides.KickDetector),
            },
            PublisherOptions = new TrackerPublisherOptions
            {
                PublishUdp = options.PublishUdp,
                MulticastAddress = effectiveRuntimeOverrides.Publish.MulticastAddress ?? activeProfile.Publish.MulticastAddress,
                Port = effectiveRuntimeOverrides.Publish.Port ?? activeProfile.Publish.Port,
                SourceName = effectiveRuntimeOverrides.Publish.SourceName ?? options.SourceName,
                Uuid = effectiveRuntimeOverrides.Publish.Uuid ?? options.Uuid,
            },
        };
    }

    private static TrackerRobotTrackerOverrides ResolveRobotTracker(
        TrackerRobotTrackerOverrides profile,
        TrackerRobotTrackerOverrides runtimeOverrides)
    {
        return new TrackerRobotTrackerOverrides
        {
            ProcessNoise = runtimeOverrides.ProcessNoise ?? profile.ProcessNoise,
            MeasurementNoise = runtimeOverrides.MeasurementNoise ?? profile.MeasurementNoise,
            VisibilityHalfLifeSeconds = runtimeOverrides.VisibilityHalfLifeSeconds ?? profile.VisibilityHalfLifeSeconds,
            OutputVisibilityThreshold = runtimeOverrides.OutputVisibilityThreshold ?? profile.OutputVisibilityThreshold,
            Gate = runtimeOverrides.Gate ?? profile.Gate,
            OutlierLimitMm = runtimeOverrides.OutlierLimitMm ?? profile.OutlierLimitMm,
        };
    }

    private static TrackerBallTrackerOverrides ResolveBallTracker(
        TrackerBallTrackerOverrides profile,
        TrackerBallTrackerOverrides runtimeOverrides)
    {
        return new TrackerBallTrackerOverrides
        {
            ProcessNoise = runtimeOverrides.ProcessNoise ?? profile.ProcessNoise,
            MeasurementNoise = runtimeOverrides.MeasurementNoise ?? profile.MeasurementNoise,
            VisibilityHalfLifeSeconds = runtimeOverrides.VisibilityHalfLifeSeconds ?? profile.VisibilityHalfLifeSeconds,
            OutputVisibilityThreshold = runtimeOverrides.OutputVisibilityThreshold ?? profile.OutputVisibilityThreshold,
            Gate = runtimeOverrides.Gate ?? profile.Gate,
            OutlierLimitMm = runtimeOverrides.OutlierLimitMm ?? profile.OutlierLimitMm,
            TrackLifetimeNs = runtimeOverrides.TrackLifetimeNs ?? profile.TrackLifetimeNs,
        };
    }

    private static TrackerKickDetectorOverrides ResolveKickDetector(
        TrackerKickDetectorOverrides profile,
        TrackerKickDetectorOverrides runtimeOverrides)
    {
        return new TrackerKickDetectorOverrides
        {
            KickSpeedThresholdMmPerS = runtimeOverrides.KickSpeedThresholdMmPerS ?? profile.KickSpeedThresholdMmPerS,
            ChipHeightThresholdMm = runtimeOverrides.ChipHeightThresholdMm ?? profile.ChipHeightThresholdMm,
            ContactMarginMm = runtimeOverrides.ContactMarginMm ?? profile.ContactMarginMm,
        };
    }
}
