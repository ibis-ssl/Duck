using System.Text.Json;
using Tracker.Core;

/// <summary>
/// Capture replay 用の TrackerEngineSettings を既定値、appsettings、capture metadata から解決する。
/// </summary>
internal static class TrackerSettingsFactory
{
    /// <summary>
    /// profile と任意の settings file、CLI override を統合して engine settings を作る。
    /// </summary>
    public static TrackerEngineSettings Create(
        string profileName,
        string? settingsPath,
        TrackerSettingOverrides overrides)
    {
        var settings = settingsPath is null
            ? CreateDefault(profileName)
            : CreateFromFile(profileName, settingsPath);

        return ApplyOverrides(settings, overrides);
    }

    private static TrackerEngineSettings CreateDefault(string profileName)
    {
        var gate = string.Equals(profileName, "fast", StringComparison.OrdinalIgnoreCase) ? 0.85d : 1.0d;
        return new TrackerEngineSettings
        {
            ProfileName = profileName,
            ReorderWindowNs = 100_000_000,
            MergeWindowNs = 20_000_000,
            GeometryResetFieldLengthThresholdMm = 500,
            GeometryResetFieldWidthThresholdMm = 500,
            RobotTracker = new TrackerRobotTrackerOverrides
            {
                ProcessNoise = 0.1,
                MeasurementNoise = 20.0,
                VisibilityHalfLifeSeconds = 0.462756,
                OutputVisibilityThreshold = 0.05,
                Gate = gate,
                OutlierLimitMm = 120.0,
            },
            BallTracker = new TrackerBallTrackerOverrides
            {
                ProcessNoise = 0.1,
                MeasurementNoise = 100.0,
                VisibilityHalfLifeSeconds = 1.0,
                OutputVisibilityThreshold = 0.0,
                Gate = gate,
                OutlierLimitMm = 120.0,
                TrackLifetimeNs = 1_000_000_000,
            },
            KickDetector = new TrackerKickDetectorOverrides
            {
                KickSpeedThresholdMmPerS = 800.0,
                ChipHeightThresholdMm = 120.0,
                ContactMarginMm = 25.0,
            },
        };
    }

    private static TrackerEngineSettings CreateFromFile(string profileName, string settingsPath)
    {
        using var stream = File.OpenRead(settingsPath);
        var document = JsonSerializer.Deserialize<ReplaySettingsFile>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Settings file '{settingsPath}' is empty.");
        if (document.ResolvedTrackerOptions?.EngineSettings is not null)
        {
            return document.ResolvedTrackerOptions.EngineSettings;
        }

        var tracker = document.Tracker ?? document.TrackerOptions ?? document;

        if (tracker.Profiles is null || !tracker.Profiles.TryGetValue(profileName, out var profile))
        {
            throw new InvalidOperationException(
                $"Tracker profile '{profileName}' was not found in settings file '{settingsPath}'.");
        }

        var defaults = CreateDefault(profileName);
        var profileSettings = new TrackerEngineSettings
        {
            ProfileName = profileName,
            ReorderWindowNs = profile.Engine?.ReorderWindowNs ?? defaults.ReorderWindowNs,
            MergeWindowNs = profile.Engine?.MergeWindowNs ?? defaults.MergeWindowNs,
            GeometryResetFieldLengthThresholdMm = profile.Engine?.GeometryResetFieldLengthThresholdMm
                ?? defaults.GeometryResetFieldLengthThresholdMm,
            GeometryResetFieldWidthThresholdMm = profile.Engine?.GeometryResetFieldWidthThresholdMm
                ?? defaults.GeometryResetFieldWidthThresholdMm,
            RobotTracker = MergeRobotTracker(defaults.RobotTracker, profile.RobotTracker),
            BallTracker = MergeBallTracker(defaults.BallTracker, profile.BallTracker),
            KickDetector = MergeKickDetector(defaults.KickDetector, profile.KickDetector),
        };

        return tracker.RuntimeOverrides is null
            ? profileSettings
            : ApplyRuntimeOverrides(profileSettings, tracker.RuntimeOverrides);
    }

    private static TrackerEngineSettings ApplyOverrides(
        TrackerEngineSettings settings,
        TrackerSettingOverrides overrides)
    {
        return new TrackerEngineSettings
        {
            ProfileName = settings.ProfileName,
            ReorderWindowNs = overrides.ReorderWindowNs ?? settings.ReorderWindowNs,
            MergeWindowNs = overrides.MergeWindowNs ?? settings.MergeWindowNs,
            GeometryResetFieldLengthThresholdMm = settings.GeometryResetFieldLengthThresholdMm,
            GeometryResetFieldWidthThresholdMm = settings.GeometryResetFieldWidthThresholdMm,
            RobotTracker = settings.RobotTracker,
            BallTracker = new TrackerBallTrackerOverrides
            {
                ProcessNoise = settings.BallTracker.ProcessNoise,
                MeasurementNoise = settings.BallTracker.MeasurementNoise,
                VisibilityHalfLifeSeconds = settings.BallTracker.VisibilityHalfLifeSeconds,
                OutputVisibilityThreshold = overrides.BallOutputVisibility
                    ?? settings.BallTracker.OutputVisibilityThreshold,
                Gate = overrides.BallGate ?? settings.BallTracker.Gate,
                OutlierLimitMm = overrides.BallOutlierLimitMm ?? settings.BallTracker.OutlierLimitMm,
                TrackLifetimeNs = overrides.BallTrackLifetimeNs ?? settings.BallTracker.TrackLifetimeNs,
            },
            KickDetector = settings.KickDetector,
        };
    }

    private static TrackerEngineSettings ApplyRuntimeOverrides(
        TrackerEngineSettings settings,
        TrackerRuntimeOverrides overrides)
    {
        return new TrackerEngineSettings
        {
            ProfileName = settings.ProfileName,
            ReorderWindowNs = settings.ReorderWindowNs,
            MergeWindowNs = settings.MergeWindowNs,
            GeometryResetFieldLengthThresholdMm = settings.GeometryResetFieldLengthThresholdMm,
            GeometryResetFieldWidthThresholdMm = settings.GeometryResetFieldWidthThresholdMm,
            RobotTracker = MergeRobotTracker(settings.RobotTracker, overrides.RobotTracker),
            BallTracker = MergeBallTracker(settings.BallTracker, overrides.BallTracker),
            KickDetector = MergeKickDetector(settings.KickDetector, overrides.KickDetector),
        };
    }

    private static TrackerRobotTrackerOverrides MergeRobotTracker(
        TrackerRobotTrackerOverrides defaults,
        TrackerRobotTrackerOptions? options)
    {
        return new TrackerRobotTrackerOverrides
        {
            ProcessNoise = options?.ProcessNoise ?? defaults.ProcessNoise,
            MeasurementNoise = options?.MeasurementNoise ?? defaults.MeasurementNoise,
            VisibilityHalfLifeSeconds = options?.VisibilityHalfLifeSeconds ?? defaults.VisibilityHalfLifeSeconds,
            OutputVisibilityThreshold = options?.OutputVisibilityThreshold ?? defaults.OutputVisibilityThreshold,
            Gate = options?.Gate ?? defaults.Gate,
            OutlierLimitMm = options?.OutlierLimitMm ?? defaults.OutlierLimitMm,
        };
    }

    private static TrackerBallTrackerOverrides MergeBallTracker(
        TrackerBallTrackerOverrides defaults,
        TrackerBallTrackerOptions? options)
    {
        return new TrackerBallTrackerOverrides
        {
            ProcessNoise = options?.ProcessNoise ?? defaults.ProcessNoise,
            MeasurementNoise = options?.MeasurementNoise ?? defaults.MeasurementNoise,
            VisibilityHalfLifeSeconds = options?.VisibilityHalfLifeSeconds ?? defaults.VisibilityHalfLifeSeconds,
            OutputVisibilityThreshold = options?.OutputVisibilityThreshold ?? defaults.OutputVisibilityThreshold,
            Gate = options?.Gate ?? defaults.Gate,
            OutlierLimitMm = options?.OutlierLimitMm ?? defaults.OutlierLimitMm,
            TrackLifetimeNs = options?.TrackLifetimeNs ?? defaults.TrackLifetimeNs,
        };
    }

    private static TrackerKickDetectorOverrides MergeKickDetector(
        TrackerKickDetectorOverrides defaults,
        TrackerKickDetectorOptions? options)
    {
        return new TrackerKickDetectorOverrides
        {
            KickSpeedThresholdMmPerS = options?.KickSpeedThresholdMmPerS ?? defaults.KickSpeedThresholdMmPerS,
            ChipHeightThresholdMm = options?.ChipHeightThresholdMm ?? defaults.ChipHeightThresholdMm,
            ContactMarginMm = options?.ContactMarginMm ?? defaults.ContactMarginMm,
        };
    }

    private static TrackerRobotTrackerOverrides MergeRobotTracker(
        TrackerRobotTrackerOverrides defaults,
        TrackerRobotTrackerOverrides overrides)
    {
        return new TrackerRobotTrackerOverrides
        {
            ProcessNoise = overrides.ProcessNoise ?? defaults.ProcessNoise,
            MeasurementNoise = overrides.MeasurementNoise ?? defaults.MeasurementNoise,
            VisibilityHalfLifeSeconds = overrides.VisibilityHalfLifeSeconds ?? defaults.VisibilityHalfLifeSeconds,
            OutputVisibilityThreshold = overrides.OutputVisibilityThreshold ?? defaults.OutputVisibilityThreshold,
            Gate = overrides.Gate ?? defaults.Gate,
            OutlierLimitMm = overrides.OutlierLimitMm ?? defaults.OutlierLimitMm,
        };
    }

    private static TrackerBallTrackerOverrides MergeBallTracker(
        TrackerBallTrackerOverrides defaults,
        TrackerBallTrackerOverrides overrides)
    {
        return new TrackerBallTrackerOverrides
        {
            ProcessNoise = overrides.ProcessNoise ?? defaults.ProcessNoise,
            MeasurementNoise = overrides.MeasurementNoise ?? defaults.MeasurementNoise,
            VisibilityHalfLifeSeconds = overrides.VisibilityHalfLifeSeconds ?? defaults.VisibilityHalfLifeSeconds,
            OutputVisibilityThreshold = overrides.OutputVisibilityThreshold ?? defaults.OutputVisibilityThreshold,
            Gate = overrides.Gate ?? defaults.Gate,
            OutlierLimitMm = overrides.OutlierLimitMm ?? defaults.OutlierLimitMm,
            TrackLifetimeNs = overrides.TrackLifetimeNs ?? defaults.TrackLifetimeNs,
        };
    }

    private static TrackerKickDetectorOverrides MergeKickDetector(
        TrackerKickDetectorOverrides defaults,
        TrackerKickDetectorOverrides overrides)
    {
        return new TrackerKickDetectorOverrides
        {
            KickSpeedThresholdMmPerS = overrides.KickSpeedThresholdMmPerS ?? defaults.KickSpeedThresholdMmPerS,
            ChipHeightThresholdMm = overrides.ChipHeightThresholdMm ?? defaults.ChipHeightThresholdMm,
            ContactMarginMm = overrides.ContactMarginMm ?? defaults.ContactMarginMm,
        };
    }
}
