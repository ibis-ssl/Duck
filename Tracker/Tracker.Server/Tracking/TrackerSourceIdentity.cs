namespace Tracker.Server.Tracking;

/// <summary>
/// tracker source identity の key / label 正規化を共有する helper。
/// </summary>
internal static class TrackerSourceIdentity
{
    internal const char KeySeparator = '\u001f';

    internal static string CreateEndpointSensitiveKey(
        string sourceRole,
        string sourceLabel,
        string sourceUuid,
        string remoteEndpoint)
    {
        return string.Join(
            KeySeparator,
            TrackerPacketSnapshotRecord.NormalizeSourceRole(sourceRole),
            TrackerPacketSnapshotRecord.NormalizeSourceLabel(sourceLabel, null, sourceUuid, remoteEndpoint, sourceRole),
            sourceUuid ?? string.Empty,
            remoteEndpoint ?? string.Empty);
    }

    internal static string CreateUuidPreferredKey(
        string keyPrefix,
        string? sourceLabel,
        string? sourceName,
        string? sourceUuid,
        string? remoteEndpoint,
        string? sourceRole)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);

        if (!string.IsNullOrWhiteSpace(sourceUuid))
        {
            return $"{keyPrefix}:uuid:{sourceUuid}";
        }

        var normalizedLabel = TrackerPacketSnapshotRecord.NormalizeSourceLabel(
            sourceLabel,
            sourceName,
            sourceUuid,
            remoteEndpoint,
            sourceRole);
        return $"{keyPrefix}:fallback:{normalizedLabel}{KeySeparator}{remoteEndpoint ?? string.Empty}";
    }

    internal static string CreateDisambiguatedLabel(string label, string? sourceUuid, string? remoteEndpoint)
    {
        var suffix = !string.IsNullOrWhiteSpace(sourceUuid)
            ? ShortenUuid(sourceUuid)
            : remoteEndpoint;
        return string.IsNullOrWhiteSpace(suffix)
            ? label
            : $"{label} ({suffix})";
    }

    private static string ShortenUuid(string sourceUuid)
    {
        return sourceUuid.Length <= 12 ? sourceUuid : sourceUuid[..8];
    }
}
