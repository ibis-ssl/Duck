using System.Text.Json;

namespace Tracker.Server.Tracking;

/// <summary>
/// CaptureOn session folder 内の tracker packet snapshot sidecar を replay 入力として読み取る。
/// </summary>
public sealed class TrackerPacketSnapshotLogReader
{
    private const int SchemaVersion = 1;

    /// <summary>
    /// session folder 配下に置く tracker packet snapshot sidecar の file 名。
    /// </summary>
    public const string SidecarFileName = "tracker-packet-snapshots.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// session folder 内の tracker packet snapshot sidecar をすべて読み取る。
    /// </summary>
    public IReadOnlyList<TrackerPacketSnapshotRecord> ReadSession(string sessionFolderPath)
    {
        var sidecarPath = ResolveSidecarPath(sessionFolderPath);
        return File.Exists(sidecarPath)
            ? ReadRecords(sidecarPath).ToList()
            : [];
    }

    /// <summary>
    /// 指定した tracker packet snapshot sidecar file から record を順に読み取る。
    /// </summary>
    public static IEnumerable<TrackerPacketSnapshotRecord> ReadRecords(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var record = JsonSerializer.Deserialize<TrackerPacketSnapshotRecord>(line, JsonOptions)
                ?? throw new InvalidDataException("Tracker packet snapshot record is empty.");
            if (record.SchemaVersion != SchemaVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported tracker packet snapshot schema version '{record.SchemaVersion}'.");
            }

            yield return record.EnsureSemanticSummary();
        }
    }

    /// <summary>
    /// session folder から tracker packet snapshot sidecar の絶対 path を解決する。
    /// </summary>
    public static string ResolveSidecarPath(string sessionFolderPath)
    {
        return Path.Combine(sessionFolderPath, SidecarFileName);
    }
}
