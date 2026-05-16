using System.Text.Json;

namespace Tracker.CaptureReplay;

/// <summary>
/// CaptureReplay CLI の --capture / --settings 入力から実際に読む packet file と metadata file を解決する。
/// </summary>
internal static class ReplayInputPathResolver
{
    /// <summary>
    /// --capture に file または CaptureOn session folder が渡された場合の replay 入力 path を解決する。
    /// </summary>
    public static ReplayInputPaths Resolve(string capturePath, string? settingsPath)
    {
        var fullCapturePath = Path.GetFullPath(capturePath);
        if (Directory.Exists(fullCapturePath))
        {
            return ResolveSessionFolder(fullCapturePath, settingsPath);
        }

        var fullSettingsPath = string.IsNullOrWhiteSpace(settingsPath)
            ? null
            : Path.GetFullPath(settingsPath);
        return new ReplayInputPaths(
            fullCapturePath,
            fullSettingsPath,
            IsMetadataFile(fullSettingsPath) ? fullSettingsPath : null);
    }

    private static ReplayInputPaths ResolveSessionFolder(string sessionFolderPath, string? settingsPath)
    {
        var metadataPath = ResolveSingleFile(sessionFolderPath, "*.metadata.json");
        if (metadataPath is null)
        {
            var packetPath = ResolveSingleFile(sessionFolderPath, "*.jsonl.gz")
                ?? throw new InvalidDataException($"Capture session folder '{sessionFolderPath}' does not contain a metadata or packet file.");
            var fullSettingsPath = string.IsNullOrWhiteSpace(settingsPath)
                ? null
                : Path.GetFullPath(settingsPath);
            return new ReplayInputPaths(packetPath, fullSettingsPath, IsMetadataFile(fullSettingsPath) ? fullSettingsPath : null);
        }

        var captureDirectory = ResolveCaptureDirectory(metadataPath);
        var packetPathFromMetadata = ResolvePacketPath(metadataPath, captureDirectory);
        var resolvedSettingsPath = string.IsNullOrWhiteSpace(settingsPath)
            ? metadataPath
            : Path.GetFullPath(settingsPath);
        return new ReplayInputPaths(
            packetPathFromMetadata,
            resolvedSettingsPath,
            metadataPath);
    }

    private static string ResolvePacketPath(string metadataPath, string captureDirectory)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
        if (!document.RootElement.TryGetProperty("PacketPath", out var packetPathElement))
        {
            throw new InvalidDataException($"Capture metadata '{metadataPath}' does not contain PacketPath.");
        }

        var packetPath = packetPathElement.GetString();
        if (string.IsNullOrWhiteSpace(packetPath))
        {
            throw new InvalidDataException($"Capture metadata '{metadataPath}' has an empty PacketPath.");
        }

        return Path.GetFullPath(Path.IsPathRooted(packetPath)
            ? packetPath
            : Path.Combine(captureDirectory, packetPath));
    }

    private static string ResolveCaptureDirectory(string metadataPath)
    {
        var sessionDirectory = Path.GetDirectoryName(metadataPath)
            ?? throw new InvalidDataException("Capture metadata path must have a parent directory.");
        using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
        if (!document.RootElement.TryGetProperty("SessionFolder", out var sessionFolderElement))
        {
            return sessionDirectory;
        }

        var sessionFolder = sessionFolderElement.GetString();
        if (string.IsNullOrWhiteSpace(sessionFolder))
        {
            return sessionDirectory;
        }

        var directoryName = Path.GetFileName(
            sessionDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(directoryName, sessionFolder, StringComparison.Ordinal)
            ? Path.GetDirectoryName(sessionDirectory) ?? sessionDirectory
            : sessionDirectory;
    }

    private static string? ResolveSingleFile(string directory, string searchPattern)
    {
        var candidates = Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
        if (candidates.Length == 0)
        {
            return null;
        }

        if (candidates.Length > 1)
        {
            throw new InvalidDataException($"Capture session folder '{directory}' contains multiple '{searchPattern}' files.");
        }

        return Path.GetFullPath(candidates[0]);
    }

    private static bool IsMetadataFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("PacketPath", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>
/// CaptureReplay が実際に使う packet capture、settings、metadata path の解決結果。
/// </summary>
/// <param name="CapturePath">replay する jsonl.gz packet capture file。</param>
/// <param name="SettingsPath">tracker settings として読む file。metadata が解決できた場合は metadata を使う。</param>
/// <param name="MetadataPath">snapshot / comparison / latency 補助情報として読む metadata file。</param>
internal sealed record ReplayInputPaths(
    string CapturePath,
    string? SettingsPath,
    string? MetadataPath);
