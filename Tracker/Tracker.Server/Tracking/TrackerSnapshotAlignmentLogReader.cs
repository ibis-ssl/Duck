using System.Text.Json;

namespace Tracker.Server.Tracking;

/// <summary>
/// CaptureOn session folder 内の tracker snapshot alignment sidecar を読み取る。
/// </summary>
public sealed class TrackerSnapshotAlignmentLogReader
{
    private const int SchemaVersion = 1;

    /// <summary>
    /// session folder 配下に置く tracker snapshot alignment sidecar の file 名。
    /// </summary>
    public const string SidecarFileName = "tracker-snapshot-alignment.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 指定した tracker snapshot alignment sidecar file から record を順に読み取る。
    /// </summary>
    public static IEnumerable<TrackerSnapshotAlignmentRecord> ReadRecords(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var record = JsonSerializer.Deserialize<TrackerSnapshotAlignmentRecord>(line, JsonOptions)
                ?? throw new InvalidDataException("Tracker snapshot alignment record is empty.");
            if (record.SchemaVersion != SchemaVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported tracker snapshot alignment schema version '{record.SchemaVersion}'.");
            }

            yield return record.Normalize();
        }
    }

    /// <summary>
    /// session folder から tracker snapshot alignment sidecar の絶対 path を解決する。
    /// </summary>
    public static string ResolveSidecarPath(string sessionFolderPath)
    {
        return Path.Combine(sessionFolderPath, SidecarFileName);
    }
}
