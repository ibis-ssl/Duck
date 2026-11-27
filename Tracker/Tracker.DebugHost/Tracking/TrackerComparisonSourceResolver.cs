namespace Tracker.DebugHost.Tracking;

/// <summary>liveとreplayで共有するsource metadataから比較roleとidentityを解決する。</summary>
public static class TrackerComparisonSourceResolver
{
    public static TrackerComparisonSourceResolution Resolve(
        string? sourceName,
        string? sourceLabel,
        string? sourceUuid,
        string? remoteEndpoint,
        string? sourceRole)
    {
        var logicalRole = sourceName switch
        {
            "ibis" => "Duck",
            "TIGERs" => "TIGERs",
            "ER-FORCE" => "ER-Force",
            _ => null,
        };

        var identityKey = !string.IsNullOrWhiteSpace(sourceUuid)
            ? $"comparison:uuid:{sourceUuid}"
            : $"comparison:fallback:{sourceName ?? string.Empty}\u001f{remoteEndpoint ?? string.Empty}";

        return new TrackerComparisonSourceResolution(logicalRole, identityKey);
    }
}

/// <summary>一つの比較sourceのlogical roleとidentity keyを表す。</summary>
public sealed record TrackerComparisonSourceResolution(
    string? LogicalRole,
    string IdentityKey);
