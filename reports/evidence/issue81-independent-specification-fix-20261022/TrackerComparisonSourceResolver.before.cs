namespace Tracker.DebugHost.Tracking;

/// <summary>Resolves comparison roles and identities from source metadata shared by live and replay paths.</summary>
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

        var identityKey = TrackerSourceIdentity.CreateUuidPreferredKey(
            "comparison",
            sourceLabel,
            sourceName,
            sourceUuid,
            remoteEndpoint,
            sourceRole);

        return new TrackerComparisonSourceResolution(logicalRole, identityKey);
    }
}

/// <summary>Resolved logical role and identity key for one comparison source.</summary>
public sealed record TrackerComparisonSourceResolution(
    string? LogicalRole,
    string IdentityKey);
