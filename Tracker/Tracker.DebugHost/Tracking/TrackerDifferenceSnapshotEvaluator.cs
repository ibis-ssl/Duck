namespace Tracker.DebugHost.Tracking;

/// <summary>Resolves the latest snapshot for each live tracker role and calculates all source-pair differences.</summary>
public static class TrackerDifferenceSnapshotEvaluator
{
    private static readonly string[] ComparisonRoles = ["Duck", "TIGERs", "ER-Force"];

    /// <summary>
    /// Resolves the three comparison roles from current snapshots, then calculates every unordered pair.
    /// Snapshots sharing one identity are represented by their newest received packet.
    /// </summary>
    public static TrackerDifferenceSnapshotResult Evaluate(
        IReadOnlyList<ExternalTrackerReadSideSnapshot> snapshots,
        DateTimeOffset now,
        TimeSpan freshnessWindow,
        double ballDistanceGateMeters)
    {
        var readiness = TrackerComparisonReadiness.Evaluate(snapshots, now, freshnessWindow);
        if (!readiness.IsReady)
        {
            return new TrackerDifferenceSnapshotResult(readiness, []);
        }

        var sourcesByRole = readiness.ResolvedSnapshots
            .GroupBy(source => source.Status.LogicalRole, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(source => source.Status.ReceivedAt)
                    .ThenBy(source => source.Snapshot.SourceLabel, StringComparer.Ordinal)
                    .ThenBy(source => source.Snapshot.RemoteEndpoint, StringComparer.Ordinal)
                    .First(),
                StringComparer.Ordinal);
        var pairs = new List<TrackerDifferencePairResult>(3);

        for (var referenceIndex = 0; referenceIndex < ComparisonRoles.Length; referenceIndex++)
        {
            var reference = sourcesByRole[ComparisonRoles[referenceIndex]];
            for (var comparisonIndex = referenceIndex + 1; comparisonIndex < ComparisonRoles.Length; comparisonIndex++)
            {
                var comparison = sourcesByRole[ComparisonRoles[comparisonIndex]];
                pairs.Add(CalculatePair(reference, comparison, ballDistanceGateMeters));
            }
        }

        return new TrackerDifferenceSnapshotResult(readiness, pairs);
    }

    private static TrackerDifferencePairResult CalculatePair(
        TrackerComparisonResolvedSnapshot reference,
        TrackerComparisonResolvedSnapshot comparison,
        double ballDistanceGateMeters)
    {
        var referenceFrame = reference.Snapshot.Packet.TrackedFrame;
        var comparisonFrame = comparison.Snapshot.Packet.TrackedFrame;
        var referenceHasFrame = referenceFrame is not null;
        var comparisonHasFrame = comparisonFrame is not null;
        var status = (referenceHasFrame, comparisonHasFrame) switch
        {
            (true, true) => TrackerDifferencePairStatus.Compared,
            (false, true) => TrackerDifferencePairStatus.MissingReferenceFrame,
            (true, false) => TrackerDifferencePairStatus.MissingComparisonFrame,
            (false, false) => TrackerDifferencePairStatus.MissingBothFrames,
        };
        var difference = referenceFrame is not null && comparisonFrame is not null
            ? TrackerDifferenceCalculator.Calculate(
                referenceFrame,
                comparisonFrame,
                ballDistanceGateMeters)
            : null;

        return new TrackerDifferencePairResult(
            reference.Status.LogicalRole,
            reference.Status.SourceName,
            reference.Status.IdentityKey,
            reference.Status.RemoteEndpoint,
            referenceFrame?.FrameNumber,
            comparison.Status.LogicalRole,
            comparison.Status.SourceName,
            comparison.Status.IdentityKey,
            comparison.Status.RemoteEndpoint,
            comparisonFrame?.FrameNumber,
            status,
            difference);
    }
}

public sealed record TrackerDifferenceSnapshotResult(
    TrackerComparisonReadinessResult Readiness,
    IReadOnlyList<TrackerDifferencePairResult> Pairs);

public sealed record TrackerDifferencePairResult(
    string ReferenceRole,
    string ReferenceSourceName,
    string ReferenceIdentityKey,
    string ReferenceRemoteEndpoint,
    uint? ReferenceFrameNumber,
    string ComparisonRole,
    string ComparisonSourceName,
    string ComparisonIdentityKey,
    string ComparisonRemoteEndpoint,
    uint? ComparisonFrameNumber,
    TrackerDifferencePairStatus Status,
    TrackerDifferenceResult? Difference);

public enum TrackerDifferencePairStatus
{
    Compared,
    MissingReferenceFrame,
    MissingComparisonFrame,
    MissingBothFrames,
    UnsupportedSource,
}
