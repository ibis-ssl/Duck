using System.Text.Json;

namespace Tracker.DebugHost.Components.Pages;

/// <summary>
/// capture sidecar diagnostics log から対応する metadata JSON を読み、profile modal 用の索引を作る。
/// </summary>
internal static class DiagnosticsProfileMetadataLoader
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// diagnostics log path から metadata path を解決し、configured profile と resolved settings を読み込む。
    /// </summary>
    public static DiagnosticsProfileMetadataIndex Load(string? selectedLogPath)
    {
        if (selectedLogPath is null)
        {
            return DiagnosticsProfileMetadataIndex.Empty;
        }

        var metadataPath = ResolveMetadataPath(selectedLogPath);
        if (metadataPath is null)
        {
            return DiagnosticsProfileMetadataIndex.WithError(
                "Capture metadata is available only for capture sidecar diagnostics logs.");
        }

        if (!File.Exists(metadataPath))
        {
            return DiagnosticsProfileMetadataIndex.WithError(
                "Capture metadata file was not found for this diagnostics log.");
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
            var root = document.RootElement;
            var trackerOptions = root.GetProperty("TrackerOptions");
            var activeProfileName = trackerOptions.GetProperty("ActiveProfileName").GetString();
            var configuredProfilesByName = trackerOptions
                .GetProperty("Profiles")
                .EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => FormatJson(property.Value),
                    StringComparer.OrdinalIgnoreCase);
            var resolvedSettingsJson = root.TryGetProperty("ResolvedTrackerOptions", out var resolvedElement)
                ? FormatJson(resolvedElement)
                : "Resolved settings were not found in metadata.";

            return new DiagnosticsProfileMetadataIndex(
                configuredProfilesByName,
                activeProfileName,
                resolvedSettingsJson,
                Error: null);
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return DiagnosticsProfileMetadataIndex.WithError($"Capture metadata could not be read: {ex.Message}");
        }
    }

    private static string? ResolveMetadataPath(string diagnosticsLogPath)
    {
        const string diagnosticsSuffix = ".tracker-diagnostics.log";
        const string metadataSuffix = ".metadata.json";

        return diagnosticsLogPath.EndsWith(diagnosticsSuffix, StringComparison.Ordinal)
            ? string.Concat(diagnosticsLogPath.AsSpan(0, diagnosticsLogPath.Length - diagnosticsSuffix.Length), metadataSuffix)
            : null;
    }

    private static string FormatJson(JsonElement element)
    {
        return JsonSerializer.Serialize(element, MetadataJsonOptions);
    }
}

/// <summary>
/// profile metadata modal が参照する configured profile と resolved settings の索引。
/// </summary>
internal sealed record DiagnosticsProfileMetadataIndex(
    IReadOnlyDictionary<string, string> ConfiguredProfilesByName,
    string? ActiveProfileName,
    string? ResolvedSettingsJson,
    string? Error)
{
    /// <summary>
    /// metadata 未選択時に使う空の索引。
    /// </summary>
    public static DiagnosticsProfileMetadataIndex Empty { get; } = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        ActiveProfileName: null,
        ResolvedSettingsJson: null,
        Error: null);

    /// <summary>
    /// metadata を UI に出せない理由を持つ索引を作る。
    /// </summary>
    public static DiagnosticsProfileMetadataIndex WithError(string error)
    {
        return new DiagnosticsProfileMetadataIndex(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ActiveProfileName: null,
            ResolvedSettingsJson: null,
            error);
    }
}
