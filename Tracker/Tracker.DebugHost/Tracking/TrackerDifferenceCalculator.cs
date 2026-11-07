using System.Numerics;
using Tracker.Core;

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

    /// <summary>Compares a Duck core frame with an externally received tracker packet frame.</summary>
    public static TrackerDifferenceResult Calculate(
        TrackerFrame reference,
        TrackedFrame comparison,
        double ballDistanceGateMeters) =>
        Calculate(ToPacketFrame(reference), comparison, ballDistanceGateMeters);

    /// <summary>Compares an externally received tracker packet frame with a Duck core frame.</summary>
    public static TrackerDifferenceResult Calculate(
        TrackedFrame reference,
        TrackerFrame comparison,
        double ballDistanceGateMeters) =>
        Calculate(reference, ToPacketFrame(comparison), ballDistanceGateMeters);

    /// <summary>Compares two Duck core frames after projecting them to official tracker units and ordering.</summary>
    public static TrackerDifferenceResult Calculate(
        TrackerFrame reference,
        TrackerFrame comparison,
        double ballDistanceGateMeters) =>
        Calculate(ToPacketFrame(reference), ToPacketFrame(comparison), ballDistanceGateMeters);

    private static TrackedFrame ToPacketFrame(TrackerFrame source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var frame = new TrackedFrame
        {
            FrameNumber = source.FrameNumber,
            Timestamp = source.DataTimestampNs / 1_000_000_000.0,
        };
        frame.Balls.AddRange(OrderBalls(source).Select(ball => new TrackedBall
        {
            Pos = new global::Vector3
            {
                X = ToMeters(ball.XMm),
                Y = ToMeters(ball.YMm),
                Z = ToMeters(ball.ZMm),
            },
            Vel = new global::Vector3
            {
                X = ToMeters(ball.VXMmPerS),
                Y = ToMeters(ball.VYMmPerS),
                Z = ToMeters(ball.VZMmPerS),
            },
            Visibility = ball.Visibility,
        }));
        frame.Robots.AddRange(source.Robots
            .OrderBy(robot => GetRobotSortKey(robot.Team))
            .ThenBy(robot => robot.RobotId)
            .Select(robot => new TrackedRobot
            {
                RobotId = new global::RobotId
                {
                    Id = robot.RobotId,
                    Team = ToProtoTeam(robot.Team),
                },
                Pos = new global::Vector2
                {
                    X = ToMeters(robot.XMm),
                    Y = ToMeters(robot.YMm),
                },
                Orientation = (float)robot.OrientationRad,
                Vel = new global::Vector2
                {
                    X = ToMeters(robot.VXMmPerS),
                    Y = ToMeters(robot.VYMmPerS),
                },
                VelAngular = (float)robot.AngularVelocityRadPerS,
                Visibility = robot.Visibility,
            }));
        return frame;
    }

    private static IEnumerable<TrackedBallState> OrderBalls(TrackerFrame frame)
    {
        if (frame.PrimaryBallTrackId is { } primaryTrackId)
        {
            var primaryBall = frame.Balls.FirstOrDefault(ball => ball.InternalTrackId == primaryTrackId);
            if (primaryBall is not null)
            {
                yield return primaryBall;
            }
        }

        foreach (var secondaryBall in frame.Balls
                     .Where(ball => ball.InternalTrackId != frame.PrimaryBallTrackId)
                     .OrderByDescending(ball => ball.Visibility)
                     .ThenByDescending(ball => ball.LastVisibleTimestampNs)
                     .ThenBy(ball => ball.InternalTrackId))
        {
            yield return secondaryBall;
        }
    }

    private static Team ToProtoTeam(TrackerTeam team) =>
        team switch
        {
            TrackerTeam.Yellow => Team.Yellow,
            TrackerTeam.Blue => Team.Blue,
            _ => Team.Unknown,
        };

    private static int GetRobotSortKey(TrackerTeam team) =>
        team switch
        {
            TrackerTeam.Yellow => 0,
            TrackerTeam.Blue => 1,
            _ => 2,
        };

    private static float ToMeters(double millimeters) => (float)(millimeters / 1000.0);

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
                        (double)comparisonRobot!.Pos.X - referenceRobot!.Pos.X,
                        (double)comparisonRobot.Pos.Y - referenceRobot.Pos.Y),
                    VelocityDelta: referenceRobot.Vel is not null && comparisonRobot!.Vel is not null
                        ? new Vector2Difference(
                            (double)comparisonRobot.Vel.X - referenceRobot.Vel.X,
                            (double)comparisonRobot.Vel.Y - referenceRobot.Vel.Y)
                        : null,
                    OrientationDeltaRadians: ShortestAngleDelta(
                        referenceRobot!.Orientation,
                        comparisonRobot!.Orientation),
                    AngularVelocityDeltaRadiansPerSecond:
                        referenceRobot.HasVelAngular && comparisonRobot.HasVelAngular
                            ? (double)comparisonRobot.VelAngular - referenceRobot.VelAngular
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
                        (double)comparisonBall.Pos.X - referenceBall.Pos.X,
                        (double)comparisonBall.Pos.Y - referenceBall.Pos.Y,
                        (double)comparisonBall.Pos.Z - referenceBall.Pos.Z),
                    referenceBall.Vel is not null && comparisonBall.Vel is not null
                        ? new Vector3Difference(
                            (double)comparisonBall.Vel.X - referenceBall.Vel.X,
                            (double)comparisonBall.Vel.Y - referenceBall.Vel.Y,
                            (double)comparisonBall.Vel.Z - referenceBall.Vel.Z)
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
        var lexicographicWeights = new BigInteger[referenceCount];
        var weight = BigInteger.One;
        var weightBase = new BigInteger(comparisonCount) + BigInteger.One;
        for (var index = referenceCount - 1; index >= 0; index--)
        {
            // A matched row contributes a negative digit; an unmatched row contributes zero.
            // Base comparisonCount + 1 makes every earlier row dominate all later rows.
            lexicographicWeights[index] = weight;
            weight *= weightBase;
        }

        for (var index = 0; index < referenceCount; index++)
        {
            AddEdge(graph, source, referenceBase + index, 0, -1, 0);
        }

        for (var index = 0; index < comparisonCount; index++)
        {
            AddEdge(graph, comparisonBase + index, sink, 0, -1, 0);
        }

        var assignmentEdges = new List<(BallCandidate Candidate, FlowEdge Edge)>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var edge = AddEdge(
                graph,
                referenceBase + candidate.ReferenceIndex,
                comparisonBase + candidate.ComparisonIndex,
                candidate.DistanceMeters,
                candidate.ReferenceIndex,
                candidate.ComparisonIndex - comparisonCount);
            assignmentEdges.Add((candidate, edge));
        }

        var potentials = InitializePotentials(
            referenceCount,
            comparisonCount,
            candidates,
            lexicographicWeights,
            graph.Length,
            sink);
        while (TryFindShortestAugmentingPath(
            graph, source, sink, lexicographicWeights, potentials, out var previousNode, out var previousEdge))
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
        IReadOnlyList<BigInteger> lexicographicWeights,
        AssignmentCost[] potentials,
        out int[] previousNode,
        out int[] previousEdge)
    {
        var distances = new AssignmentCost?[graph.Count];
        previousNode = Enumerable.Repeat(-1, graph.Count).ToArray();
        previousEdge = Enumerable.Repeat(-1, graph.Count).ToArray();
        distances[source] = AssignmentCost.Zero;
        var queue = new PriorityQueue<int, AssignmentCost>();
        queue.Enqueue(source, AssignmentCost.Zero);

        while (queue.TryDequeue(out var from, out var queuedDistance))
        {
            if (distances[from] is not { } distance || queuedDistance.CompareTo(distance) != 0)
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

                var reducedCost = edge.ToCost(lexicographicWeights) + potentials[from] - potentials[edge.To];
                if (reducedCost.CompareTo(AssignmentCost.Zero) < 0)
                {
                    // Floating-point path sums can invalidate reduced-cost non-negativity.
                    // Keep exact ordering semantics by falling back to Bellman-Ford for this augmentation.
                    return TryFindShortestAugmentingPathBellmanFord(
                        graph, source, sink, lexicographicWeights, potentials, out previousNode, out previousEdge);
                }

                var candidateDistance = distance + reducedCost;
                if (distances[edge.To] is { } current && candidateDistance.CompareTo(current) >= 0)
                {
                    continue;
                }

                distances[edge.To] = candidateDistance;
                previousNode[edge.To] = from;
                previousEdge[edge.To] = edgeIndex;
                queue.Enqueue(edge.To, candidateDistance);
            }
        }

        if (previousNode[sink] < 0)
        {
            return false;
        }

        for (var node = 0; node < graph.Count; node++)
        {
            if (distances[node] is { } distance)
            {
                potentials[node] = potentials[node] + distance;
            }
        }

        return true;
    }

    private static bool TryFindShortestAugmentingPathBellmanFord(
        IReadOnlyList<List<FlowEdge>> graph,
        int source,
        int sink,
        IReadOnlyList<BigInteger> lexicographicWeights,
        AssignmentCost[] potentials,
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

                    var reducedCost = edge.ToCost(lexicographicWeights) + potentials[from] - potentials[edge.To];
                    var candidateDistance = distance + reducedCost;
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

        if (previousNode[sink] < 0)
        {
            return false;
        }

        for (var node = 0; node < graph.Count; node++)
        {
            if (distances[node] is { } distance)
            {
                potentials[node] = potentials[node] + distance;
            }
        }

        return true;
    }

    private static AssignmentCost[] InitializePotentials(
        int referenceCount,
        int comparisonCount,
        IReadOnlyList<BallCandidate> candidates,
        IReadOnlyList<BigInteger> lexicographicWeights,
        int nodeCount,
        int sink)
    {
        var potentials = Enumerable.Repeat(AssignmentCost.Zero, nodeCount).ToArray();
        var comparisonBase = 1 + referenceCount;
        var hasIncomingCandidate = new bool[comparisonCount];
        foreach (var candidate in candidates)
        {
            var comparisonIndex = candidate.ComparisonIndex;
            var cost = new AssignmentCost(
                candidate.DistanceMeters,
                (candidate.ComparisonIndex - comparisonCount) * lexicographicWeights[candidate.ReferenceIndex]);
            if (hasIncomingCandidate[comparisonIndex] && cost.CompareTo(potentials[comparisonBase + comparisonIndex]) >= 0)
            {
                continue;
            }

            hasIncomingCandidate[comparisonIndex] = true;
            potentials[comparisonBase + comparisonIndex] = cost;
        }

        var hasSinkPath = false;
        for (var comparisonIndex = 0; comparisonIndex < comparisonCount; comparisonIndex++)
        {
            if (!hasIncomingCandidate[comparisonIndex])
            {
                continue;
            }

            var cost = potentials[comparisonBase + comparisonIndex];
            if (hasSinkPath && cost.CompareTo(potentials[sink]) >= 0)
            {
                continue;
            }

            hasSinkPath = true;
            potentials[sink] = cost;
        }

        return potentials;
    }

    private static FlowEdge AddEdge(
        IReadOnlyList<List<FlowEdge>> graph,
        int from,
        int to,
        double distanceCost,
        int lexicographicIndex,
        int lexicographicDelta)
    {
        var forward = new FlowEdge(to, graph[to].Count, 1, distanceCost, lexicographicIndex, lexicographicDelta);
        var reverse = new FlowEdge(from, graph[from].Count, 0, -distanceCost, lexicographicIndex, -lexicographicDelta);
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

    private sealed class FlowEdge(
        int to,
        int reverseIndex,
        int capacity,
        double distanceCost,
        int lexicographicIndex,
        int lexicographicDelta)
    {
        public int To { get; } = to;

        public int ReverseIndex { get; } = reverseIndex;

        public int Capacity { get; set; } = capacity;

        public double DistanceCost { get; } = distanceCost;

        public int LexicographicIndex { get; } = lexicographicIndex;

        public int LexicographicDelta { get; } = lexicographicDelta;

        public AssignmentCost ToCost(IReadOnlyList<BigInteger> lexicographicWeights) =>
            new(
                DistanceCost,
                LexicographicIndex < 0
                    ? BigInteger.Zero
                    : LexicographicDelta * lexicographicWeights[LexicographicIndex]);
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

        public static AssignmentCost operator -(AssignmentCost left, AssignmentCost right) =>
            new(left.Distance - right.Distance, left.Lexicographic - right.Lexicographic);
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
