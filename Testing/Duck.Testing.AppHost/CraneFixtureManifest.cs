using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Duck.Testing.AppHost;

public sealed record CraneFixtureManifest(string StackId, string TeamName, string TeamColor)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = false };

    public static string GetDefaultPath(string stackId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stackId);
        var fileKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stackId))).ToLowerInvariant();
        return Path.Combine(Path.GetTempPath(), "duck-aspire-fixtures", $"{fileKey}.json");
    }

    public static void WriteAtomically(string path, string stackId, CraneFixtureSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(stackId);
        ArgumentNullException.ThrowIfNull(settings);
        var manifest = new CraneFixtureManifest(stackId, settings.TeamName, settings.TeamColor);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, manifest, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static CraneFixtureSettings ReadForStack(string path, string expectedStackId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedStackId);
        try
        {
            using var stream = File.OpenRead(path);
            var manifest = JsonSerializer.Deserialize<CraneFixtureManifest>(stream, JsonOptions)
                ?? throw new InvalidDataException("Crane fixture manifest is empty.");
            if (!string.Equals(manifest.StackId, expectedStackId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Crane fixture manifest belongs to a different Aspire stack.");
            }

            return CraneFixtureSettings.FromValues(manifest.TeamName, manifest.TeamColor);
        }
        catch (Exception exception) when (exception is IOException or JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException($"Crane fixture manifest '{path}' is missing or invalid.", exception);
        }
    }
}
