namespace Tracker.DebugHost.Tracking;

/// <summary>Evaluates live comparison source identity and freshness without merging endpoint snapshots.</summary>
public static class TrackerComparisonReadiness
{
    public static readonly TimeSpan DefaultFreshnessWindow = TimeSpan.FromSeconds(5);
    private static readonly string[] RequiredRoles = ["Duck", "TIGERs", "ER-Force"];

    public static TrackerComparisonReadinessResult Evaluate(
        IReadOnlyList<ExternalTrackerReadSideSnapshot> snapshots,
        DateTimeOffset now,
        TimeSpan freshnessWindow)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        if (freshnessWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(freshnessWindow));
        }

        var candidates = snapshots
            .Where(snapshot => snapshot.ReceivedAt is { } receivedAt &&
                receivedAt <= now && now - receivedAt <= freshnessWindow)
            .Select(snapshot => (Role: ResolveRole(snapshot.SourceName), Snapshot: snapshot))
            .Where(item => item.Role is not null)
            .Select(item => (Role: item.Role!, Identity: TrackerSourceIdentity.CreateUuidPreferredKey(
                "comparison",
                item.Snapshot.SourceLabel,
                item.Snapshot.SourceName,
                item.Snapshot.Packet.Uuid ?? item.Snapshot.SourceUuid,
                item.Snapshot.RemoteEndpoint,
                item.Snapshot.SourceRole)))
            .ToArray();

        var missingRoles = RequiredRoles
            .Where(role => !candidates.Any(item => item.Role == role))
            .ToArray();
        var conflictingRoles = candidates
            .GroupBy(item => item.Role, StringComparer.Ordinal)
            .Where(group => group.Select(item => item.Identity).Distinct(StringComparer.Ordinal).Count() != 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var identityGroup in candidates.GroupBy(item => item.Identity, StringComparer.Ordinal))
        {
            var roles = identityGroup.Select(item => item.Role).Distinct(StringComparer.Ordinal).ToArray();
            if (roles.Length > 1)
            {
                foreach (var role in roles)
                {
                    conflictingRoles.Add(role);
                }
            }
        }

        return new TrackerComparisonReadinessResult(
            IsReady: missingRoles.Length == 0 && conflictingRoles.Count == 0,
            MissingRoles: missingRoles,
            ConflictingRoles: RequiredRoles.Where(conflictingRoles.Contains).ToArray());
    }

    private static string? ResolveRole(string? sourceName) => sourceName switch
    {
        "ibis" => "Duck",
        "TIGERs" => "TIGERs",
        "ER-FORCE" => "ER-Force",
        _ => null,
    };
}

public sealed record TrackerComparisonReadinessResult(
    bool IsReady,
    IReadOnlyList<string> MissingRoles,
    IReadOnlyList<string> ConflictingRoles);
