using System.Numerics;

namespace Tracker.DebugHost.Tracking;

/// <summary>Calculates deterministic object correspondence and numeric differences between two tracker frames.</summary>
public static class TrackerDifferenceCalculator
{
    /// <summary>
    /// Compares objects in two frames. Ball matching maximizes pair count, then minimizes total XY distance,
    /// then chooses the lexicographically smallest source-local index pairs.
    /// </summary>
    public static TrackerDifferenceResult Calculate(
        TrackedFrame reference,
        TrackedFrame comparison,
        double ballDistanceGateMeters)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(comparison);
        if (!double.IsFinite(ballDistanceGateMeters) || ballDistanceGateMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ballDistanceGateMeters));
        }

        return new TrackerDifferenceResult(
            CompareRobots(reference.Robots, comparison.Robots),
            CompareBalls(reference.Balls, comparison.Balls, ballDistanceGateMeters));
    }

    private static IReadOnlyList<TrackerRobotDifference> CompareRobots(
        IEnumerable<TrackedRobot> reference,
        IEnumerable<TrackedRobot> comparison)
    {
        var referenceByKey = reference.ToDictionary(robot => RobotKey.From(robot));
        var comparisonByKey = comparison.ToDictionary(robot => RobotKey.From(robot));
        var keys = referenceByKey.Keys
            .Union(comparisonByKey.Keys)
            .OrderBy(key => key.Team)
            .ThenBy(key => key.Id);
        var result = new List<TrackerRobotDifference>();

        foreach (var key in keys)
        {
            var hasReference = referenceByKey.TryGetValue(key, out var referenceRobot);
            var hasComparison = comparisonByKey.TryGetValue(key, out var comparisonRobot);
            if (hasReference && hasComparison)
            {
                result.Add(new TrackerRobotDifference(
                    key.Team,
                    key.Id,
                    TrackerRobotDifferenceStatus.Matched,
                    PositionDelta: new Vector2Difference(
                        comparisonRobot!.Pos.X - referenceRobot!.Pos.X,
                        comparisonRobot.Pos.Y - referenceRobot.Pos.Y),
                    VelocityDelta: referenceRobot.Vel is not null && comparisonRobot!.Vel is not null
                        ? new Vector2Difference(
                            comparisonRobot.Vel.X - referenceRobot.Vel.X,
                            comparisonRobot.Vel.Y - referenceRobot.Vel.Y)
                        : null,
                    OrientationDeltaRadians: ShortestAngleDelta(
                        referenceRobot!.Orientation,
                        comparisonRobot!.Orientation),
                    AngularVelocityDeltaRadiansPerSecond:
                        referenceRobot.HasVelAngular && comparisonRobot.HasVelAngular
                            ? comparisonRobot.VelAngular - referenceRobot.VelAngular
                            : null));
            }
            else
            {
                result.Add(new TrackerRobotDifference(
                    key.Team,
                    key.Id,
                    hasReference
                        ? TrackerRobotDifferenceStatus.MissingInComparison
                        : TrackerRobotDifferenceStatus.MissingInReference,
                    PositionDelta: null,
                    VelocityDelta: null,
                    OrientationDeltaRadians: null,
                    AngularVelocityDeltaRadiansPerSecond: null));
            }
        }

        return result;
    }

    private static TrackerBallDifferences CompareBalls(
        IReadOnlyList<TrackedBall> reference,
        IReadOnlyList<TrackedBall> comparison,
        double distanceGateMeters)
    {
        var candidates = new List<BallCandidate>();
        for (var referenceIndex = 0; referenceIndex < reference.Count; referenceIndex++)
        {
            var referenceBall = reference[referenceIndex];
            if (!IsFinitePosition(referenceBall))
            {
                continue;
            }

            for (var comparisonIndex = 0; comparisonIndex < comparison.Count; comparisonIndex++)
            {
                var comparisonBall = comparison[comparisonIndex];
                if (!IsFinitePosition(comparisonBall))
                {
                    continue;
                }

                var dx = (double)comparisonBall.Pos.X - referenceBall.Pos.X;
                var dy = (double)comparisonBall.Pos.Y - referenceBall.Pos.Y;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (double.IsFinite(distance) && distance <= distanceGateMeters)
                {
                    candidates.Add(new BallCandidate(referenceIndex, comparisonIndex, distance));
                }
            }
        }

        var matches = AssignBalls(reference.Count, comparison.Count, candidates);
        var matchedReferenceIndices = matches.Select(match => match.ReferenceIndex).ToHashSet();
        var matchedComparisonIndices = matches.Select(match => match.ComparisonIndex).ToHashSet();
        var ballMatches = matches
            .OrderBy(match => match.ReferenceIndex)
            .ThenBy(match => match.ComparisonIndex)
            .Select(match =>
            {
                var referenceBall = reference[match.ReferenceIndex];
                var comparisonBall = comparison[match.ComparisonIndex];
                return new TrackerBallMatch(
                    match.ReferenceIndex,
                    match.ComparisonIndex,
                    match.DistanceMeters,
                    new Vector3Difference(
                        comparisonBall.Pos.X - referenceBall.Pos.X,
                        comparisonBall.Pos.Y - referenceBall.Pos.Y,
                        comparisonBall.Pos.Z - referenceBall.Pos.Z),
                    referenceBall.Vel is not null && comparisonBall.Vel is not null
                        ? new Vector3Difference(
                            comparisonBall.Vel.X - referenceBall.Vel.X,
                            comparisonBall.Vel.Y - referenceBall.Vel.Y,
                            comparisonBall.Vel.Z - referenceBall.Vel.Z)
                        : null);
            })
            .ToArray();

        return new TrackerBallDifferences(
            ballMatches,
            Enumerable.Range(0, reference.Count).Where(index => !matchedReferenceIndices.Contains(index)).ToArray(),
            Enumerable.Range(0, comparison.Count).Where(index => !matchedComparisonIndices.Contains(index)).ToArray());
    }

    private static IReadOnlyList<BallAssignment> AssignBalls(
        int referenceCount,
        int comparisonCount,
        IReadOnlyList<BallCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var source = 0;
        var referenceBase = 1;
        var comparisonBase = referenceBase + referenceCount;
        var sink = comparisonBase + comparisonCount;
        var graph = Enumerable.Range(0, sink + 1).Select(_ => new List<FlowEdge>()).ToArray();
        for (var index = 0; index < referenceCount; index++)
        {
            AddEdge(graph, source, referenceBase + index, AssignmentCost.Zero);
        }

        for (var index = 0; index < comparisonCount; index++)
        {
            AddEdge(graph, comparisonBase + index, sink, AssignmentCost.Zero);
        }

        var assignmentEdges = new List<(BallCandidate Candidate, FlowEdge Edge)>(candidates.Count);
        for (var rank = 0; rank < candidates.Count; rank++)
        {
            var candidate = candidates[rank];
            var lexicographicWeight = BigInteger.One << (candidates.Count - rank - 1);
            var cost = new AssignmentCost(candidate.DistanceMeters, -lexicographicWeight);
            var edge = AddEdge(
                graph,
                referenceBase + candidate.ReferenceIndex,
                comparisonBase + candidate.ComparisonIndex,
                cost);
            assignmentEdges.Add((candidate, edge));
        }

        while (TryFindShortestAugmentingPath(graph, source, sink, out var previousNode, out var previousEdge))
        {
            for (var node = sink; node != source; node = previousNode[node])
            {
                var edge = graph[previousNode[node]][previousEdge[node]];
                edge.Capacity--;
                graph[node][edge.ReverseIndex].Capacity++;
            }
        }

        return assignmentEdges
            .Where(item => item.Edge.Capacity == 0)
            .Select(item => new BallAssignment(
                item.Candidate.ReferenceIndex,
                item.Candidate.ComparisonIndex,
                item.Candidate.DistanceMeters))
            .ToArray();
    }

    private static bool TryFindShortestAugmentingPath(
        IReadOnlyList<List<FlowEdge>> graph,
        int source,
        int sink,
        out int[] previousNode,
        out int[] previousEdge)
    {
        var distances = new AssignmentCost?[graph.Count];
        previousNode = Enumerable.Repeat(-1, graph.Count).ToArray();
        previousEdge = Enumerable.Repeat(-1, graph.Count).ToArray();
        distances[source] = AssignmentCost.Zero;

        for (var iteration = 0; iteration < graph.Count - 1; iteration++)
        {
            var changed = false;
            for (var from = 0; from < graph.Count; from++)
            {
                if (distances[from] is not { } distance)
                {
                    continue;
                }

                for (var edgeIndex = 0; edgeIndex < graph[from].Count; edgeIndex++)
                {
                    var edge = graph[from][edgeIndex];
                    if (edge.Capacity == 0)
                    {
                        continue;
                    }

                    var candidateDistance = distance + edge.Cost;
                    if (distances[edge.To] is { } current && candidateDistance.CompareTo(current) >= 0)
                    {
                        continue;
                    }

                    distances[edge.To] = candidateDistance;
                    previousNode[edge.To] = from;
                    previousEdge[edge.To] = edgeIndex;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        return previousNode[sink] >= 0;
    }

    private static FlowEdge AddEdge(
        IReadOnlyList<List<FlowEdge>> graph,
        int from,
        int to,
        AssignmentCost cost)
    {
        var forward = new FlowEdge(to, graph[to].Count, 1, cost);
        var reverse = new FlowEdge(from, graph[from].Count, 0, -cost);
        graph[from].Add(forward);
        graph[to].Add(reverse);
        return forward;
    }

    private static bool IsFinitePosition(TrackedBall ball) =>
        double.IsFinite(ball.Pos.X) && double.IsFinite(ball.Pos.Y);

    private static double ShortestAngleDelta(double reference, double comparison)
    {
        var fullCircle = 2 * Math.PI;
        var delta = (comparison - reference) % fullCircle;
        if (delta > Math.PI)
        {
            delta -= fullCircle;
        }
        else if (delta < -Math.PI)
        {
            delta += fullCircle;
        }

        return delta;
    }

    private readonly record struct RobotKey(Team Team, uint Id)
    {
        public static RobotKey From(TrackedRobot robot) => new(robot.RobotId.Team, robot.RobotId.Id);
    }

    private sealed record BallCandidate(int ReferenceIndex, int ComparisonIndex, double DistanceMeters);

    private sealed record BallAssignment(int ReferenceIndex, int ComparisonIndex, double DistanceMeters);

    private sealed class FlowEdge(int to, int reverseIndex, int capacity, AssignmentCost cost)
    {
        public int To { get; } = to;

        public int ReverseIndex { get; } = reverseIndex;

        public int Capacity { get; set; } = capacity;

        public AssignmentCost Cost { get; } = cost;
    }

    private readonly record struct AssignmentCost(double Distance, BigInteger Lexicographic)
        : IComparable<AssignmentCost>
    {
        public static AssignmentCost Zero => new(0, BigInteger.Zero);

        public int CompareTo(AssignmentCost other)
        {
            var distanceComparison = Distance.CompareTo(other.Distance);
            return distanceComparison != 0 ? distanceComparison : Lexicographic.CompareTo(other.Lexicographic);
        }

        public static AssignmentCost operator +(AssignmentCost left, AssignmentCost right) =>
            new(left.Distance + right.Distance, left.Lexicographic + right.Lexicographic);

        public static AssignmentCost operator -(AssignmentCost value) =>
            new(-value.Distance, -value.Lexicographic);
    }
}

public sealed record TrackerDifferenceResult(
    IReadOnlyList<TrackerRobotDifference> Robots,
    TrackerBallDifferences Balls);

public enum TrackerRobotDifferenceStatus
{
    Matched,
    MissingInReference,
    MissingInComparison,
}

public sealed record TrackerRobotDifference(
    Team Team,
    uint RobotId,
    TrackerRobotDifferenceStatus Status,
    Vector2Difference? PositionDelta,
    Vector2Difference? VelocityDelta,
    double? OrientationDeltaRadians,
    double? AngularVelocityDeltaRadiansPerSecond);

public sealed record TrackerBallDifferences(
    IReadOnlyList<TrackerBallMatch> Matches,
    IReadOnlyList<int> UnmatchedReferenceIndices,
    IReadOnlyList<int> UnmatchedComparisonIndices);

public sealed record TrackerBallMatch(
    int ReferenceIndex,
    int ComparisonIndex,
    double DistanceMeters,
    Vector3Difference PositionDelta,
    Vector3Difference? VelocityDelta);

public readonly record struct Vector2Difference(double X, double Y);

public readonly record struct Vector3Difference(double X, double Y, double Z);
