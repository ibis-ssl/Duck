using Tracker.Core;

namespace Tracker.Server.Tracking;

public static class TrackerConfigurationResolver
{
    public static TrackerResolvedOptions Resolve(TrackerOptions options)
    {
        if (!options.Profiles.TryGetValue(options.ActiveProfileName, out var activeProfile))
        {
            throw new InvalidOperationException(
                $"Tracker active profile '{options.ActiveProfileName}' was not found in Tracker:Profiles.");
        }

        return new TrackerResolvedOptions
        {
            Enabled = options.Enabled,
            EngineSettings = new TrackerEngineSettings
            {
                ProfileName = options.ActiveProfileName,
                ReorderWindowNs = activeProfile.Engine.ReorderWindowNs,
                MergeWindowNs = activeProfile.Engine.MergeWindowNs,
                GeometryResetFieldLengthThresholdMm = activeProfile.Engine.GeometryResetFieldLengthThresholdMm,
                GeometryResetFieldWidthThresholdMm = activeProfile.Engine.GeometryResetFieldWidthThresholdMm,
                RobotTracker = ResolveRobotTracker(activeProfile.RobotTracker, options.RuntimeOverrides.RobotTracker),
                BallTracker = ResolveBallTracker(activeProfile.BallTracker, options.RuntimeOverrides.BallTracker),
                KickDetector = ResolveKickDetector(activeProfile.KickDetector, options.RuntimeOverrides.KickDetector),
            },
            PublisherOptions = new TrackerPublisherOptions
            {
                PublishUdp = options.PublishUdp,
                MulticastAddress = options.RuntimeOverrides.Publish.MulticastAddress ?? activeProfile.Publish.MulticastAddress,
                Port = options.RuntimeOverrides.Publish.Port ?? activeProfile.Publish.Port,
                SourceName = options.RuntimeOverrides.Publish.SourceName ?? options.SourceName,
                Uuid = options.RuntimeOverrides.Publish.Uuid ?? options.Uuid,
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
