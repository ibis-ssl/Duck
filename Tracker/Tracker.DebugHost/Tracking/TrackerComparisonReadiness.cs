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
            .Select(snapshot =>
            {
                var sourceName = string.IsNullOrWhiteSpace(snapshot.Packet.SourceName)
                    ? snapshot.SourceName
                    : snapshot.Packet.SourceName;
                var sourceUuid = string.IsNullOrWhiteSpace(snapshot.Packet.Uuid)
                    ? snapshot.SourceUuid
                    : snapshot.Packet.Uuid;
                var resolution = TrackerComparisonSourceResolver.Resolve(
                    sourceName,
                    snapshot.SourceLabel,
                    sourceUuid,
                    snapshot.RemoteEndpoint,
                    snapshot.SourceRole);
                return (Resolution: resolution, Snapshot: snapshot, SourceName: sourceName, SourceUuid: sourceUuid);
            })
            .Where(item => item.Resolution.LogicalRole is not null)
            .Select(item => new TrackerComparisonSourceStatus(
                item.Resolution.LogicalRole!,
                item.Resolution.IdentityKey,
                item.SourceName,
                item.SourceUuid,
                item.Snapshot.RemoteEndpoint,
                item.Snapshot.ReceivedAt!.Value))
            .ToArray();

        var missingRoles = RequiredRoles
            .Where(role => !candidates.Any(item => item.LogicalRole == role))
            .ToArray();
        var conflictingRoles = candidates
            .GroupBy(item => item.LogicalRole, StringComparer.Ordinal)
            .Where(group => group.Select(item => item.IdentityKey).Distinct(StringComparer.Ordinal).Count() != 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var identityGroup in candidates.GroupBy(item => item.IdentityKey, StringComparer.Ordinal))
        {
            var roles = identityGroup.Select(item => item.LogicalRole).Distinct(StringComparer.Ordinal).ToArray();
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
            ConflictingRoles: RequiredRoles.Where(conflictingRoles.Contains).ToArray(),
            Sources: candidates);
    }
}

public sealed record TrackerComparisonReadinessResult(
    bool IsReady,
    IReadOnlyList<string> MissingRoles,
    IReadOnlyList<string> ConflictingRoles,
    IReadOnlyList<TrackerComparisonSourceStatus> Sources);

public sealed record TrackerComparisonSourceStatus(
    string LogicalRole,
    string IdentityKey,
    string SourceName,
    string SourceUuid,
    string RemoteEndpoint,
    DateTimeOffset ReceivedAt);
