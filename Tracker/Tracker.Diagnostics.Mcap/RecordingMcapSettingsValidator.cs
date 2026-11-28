using System.Text.Json;
using Tracker.Core;

namespace Tracker.Diagnostics.Mcap;

/// <summary>Shared required-field validation and deserialization for recorded tracker settings.</summary>
public static class RecordingMcapSettingsValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Validate and parse one recorded settings generation using the replay contract.</summary>
    public static TrackerEngineSettings Read(RecordingMcapEffectiveConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var json = configuration.Configuration;
        RequireProperties(json,
        [
            "ProfileName",
            "ReorderWindowNs",
            "MergeWindowNs",
            "GeometryResetFieldLengthThresholdMm",
            "GeometryResetFieldWidthThresholdMm",
            "KalmanInitialVelocityVariance",
            "KalmanProcessNoiseScale",
            "MeasurementNoiseVarianceScale",
            "RobotTracker",
            "BallTracker",
            "KickDetector",
            "AreaSelection",
        ]);
        RequireProperties(GetRequiredProperty(json, "RobotTracker"),
        [
            "ProcessNoise", "MeasurementNoise", "VisibilityHalfLifeSeconds", "OutputVisibilityThreshold",
            "Gate", "OutlierLimitMm", "IdentitySwitchDistanceMm", "OrientationMeasurementNoiseRad",
            "OrientationProcessNoise", "InitialAngularVelocityVariance", "AngularVelocityLimitRadPerS",
        ]);
        RequireProperties(GetRequiredProperty(json, "BallTracker"),
        [
            "ProcessNoise", "MeasurementNoise", "VisibilityHalfLifeSeconds", "OutputVisibilityThreshold",
            "Gate", "OutlierLimitMm", "TrackLifetimeNs",
        ]);
        RequireProperties(GetRequiredProperty(json, "KickDetector"),
            ["KickSpeedThresholdMmPerS", "ChipHeightThresholdMm", "ContactMarginMm"]);

        var settings = json.Deserialize<TrackerEngineSettings>(JsonOptions)
            ?? throw new InvalidDataException("Recording MCAP effective configuration is empty.");
        if (string.IsNullOrWhiteSpace(settings.ProfileName))
        {
            throw new InvalidDataException("Recording MCAP effective configuration has no profile name.");
        }

        var areaSelection = GetRequiredProperty(json, "AreaSelection");
        RequireProperties(areaSelection, ["Enabled", "Scope", "CoordinateSystem", "Units"]);
        if (GetRequiredProperty(areaSelection, "Enabled").ValueKind != JsonValueKind.False
            || !string.Equals(TryGetStringProperty(areaSelection, "Scope"), "all-field", StringComparison.Ordinal)
            || !string.Equals(TryGetStringProperty(areaSelection, "CoordinateSystem"), "ssl-field-center", StringComparison.Ordinal)
            || !string.Equals(TryGetStringProperty(areaSelection, "Units"), "mm", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Recording MCAP must explicitly record the applied all-field, area-selection-disabled state.");
        }

        return settings;
    }

    /// <summary>Reject a profile-switch event whose payload disagrees with the selected stored settings generation.</summary>
    public static void ValidateAppliedProfileSwitch(
        RecordingMcapControlEvent controlEvent,
        JsonElement effectiveConfiguration)
    {
        ArgumentNullException.ThrowIfNull(controlEvent);
        var eventProfileName = TryGetStringProperty(controlEvent.Payload, "profileName");
        var settingsProfileName = TryGetStringProperty(effectiveConfiguration, "ProfileName");
        if (!string.Equals(controlEvent.EventKind, "profile-switch-applied", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(eventProfileName)
            || string.IsNullOrWhiteSpace(settingsProfileName)
            || !string.Equals(eventProfileName, settingsProfileName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Recording MCAP applied profile-switch event does not match its effective settings profile name.");
        }
    }

    /// <summary>Validate that all effective generations in a recording satisfy the replay contract.</summary>
    public static void Validate(RecordingMcapSnapshot recording) => _ = ReadAll(recording);

    /// <summary>Validate and parse all effective generations in a recording.</summary>
    public static IReadOnlyDictionary<string, TrackerEngineSettings> ReadAll(RecordingMcapSnapshot recording)
    {
        ArgumentNullException.ThrowIfNull(recording);
        var settingsByGeneration = new Dictionary<string, TrackerEngineSettings>(StringComparer.Ordinal);
        foreach (var configuration in recording.EffectiveConfigurations)
        {
            if (!settingsByGeneration.TryAdd(configuration.GenerationId, Read(configuration)))
            {
                throw new InvalidDataException($"Recording MCAP repeats configuration generation '{configuration.GenerationId}'.");
            }
        }

        return settingsByGeneration;
    }

    private static void RequireProperties(JsonElement element, IReadOnlyList<string> names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Recording MCAP effective tracker settings must contain JSON objects.");
        }

        foreach (var name in names)
        {
            if (!element.EnumerateObject().Any(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException($"Recording MCAP effective tracker settings are missing required field '{name}'.");
            }
        }
    }

    private static JsonElement GetRequiredProperty(JsonElement element, string name) =>
        element.EnumerateObject()
            .First(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            .Value;

    private static string? TryGetStringProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }
}
