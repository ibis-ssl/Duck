using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using Tracker.DebugHost.Tracking;
using Xunit.Abstractions;

namespace Tracker.Tests;

public sealed class TrackerDifferenceSnapshotEvaluatorTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private const int MeasurementWarmupCalls = 500;
    private const int MeasurementIterations = 3_000;

    [Fact]
    public void Evaluate_UsesResolvedThreeTrackerSnapshotsAndPropagatesObjectDifferences()
    {
        var snapshots = new[]
        {
            Source("ibis", "duck-id", Frame(11, robotX: 1, ballX: 2)),
            Source("TIGERs", "tigers-id", Frame(12, robotX: 4, ballX: 6)),
            Source("ER-FORCE", "erforce-id", Frame(13, robotX: 7, ballX: 10)),
        };

        var result = TrackerDifferenceSnapshotEvaluator.Evaluate(
            snapshots,
            Now.AddMilliseconds(2),
            freshnessWindow: TimeSpan.FromSeconds(5),
            ballDistanceGateMeters: 10);

        Assert.True(result.Readiness.IsReady);
        Assert.Collection(
            result.Pairs,
            pair => AssertPair(pair, "Duck", 11, "TIGERs", 12, expectedRobotDeltaX: 3, expectedBallDeltaX: 4),
            pair => AssertPair(pair, "Duck", 11, "ER-Force", 13, expectedRobotDeltaX: 6, expectedBallDeltaX: 8),
            pair => AssertPair(pair, "TIGERs", 12, "ER-Force", 13, expectedRobotDeltaX: 3, expectedBallDeltaX: 4));
    }

    [Fact]
    public void Evaluate_UsesNewestPacketWhenOneResolvedIdentityHasSeveralEndpoints()
    {
        var newestTiger = Source("TIGERs", "tigers-id", Frame(22, robotX: 4, ballX: 6), endpointPort: 11011,
            receivedAt: Now.AddMilliseconds(1));
        var snapshots = new[]
        {
            Source("ibis", "duck-id", Frame(11, robotX: 1, ballX: 2)),
            Source("TIGERs", "tigers-id", Frame(21, robotX: 3, ballX: 5), endpointPort: 11010),
            newestTiger,
            Source("ER-FORCE", "erforce-id", Frame(13, robotX: 7, ballX: 10)),
        };

        var result = TrackerDifferenceSnapshotEvaluator.Evaluate(
            snapshots,
            Now.AddMilliseconds(2),
            freshnessWindow: TimeSpan.FromSeconds(5),
            ballDistanceGateMeters: 5);

        Assert.True(result.Readiness.IsReady);
        var duckTiger = Assert.Single(result.Pairs, pair => pair.ReferenceRole == "Duck" && pair.ComparisonRole == "TIGERs");
        Assert.Equal(22u, duckTiger.ComparisonFrameNumber);
        Assert.Equal(3d, Assert.Single(
            duckTiger.Difference!.Robots,
            robot => robot.Team == Team.Blue && robot.RobotId == 3).PositionDelta!.Value.X);
    }

    [Fact]
    public void Evaluate_DoesNotCalculatePairsWhenResolvedRoleIsMissing()
    {
        var result = TrackerDifferenceSnapshotEvaluator.Evaluate(
            [Source("ibis", "duck-id", Frame(11, robotX: 1, ballX: 2)),
             Source("TIGERs", "tigers-id", Frame(12, robotX: 4, ballX: 6))],
            Now,
            freshnessWindow: TimeSpan.FromSeconds(5),
            ballDistanceGateMeters: 1);

        Assert.False(result.Readiness.IsReady);
        Assert.Contains("ER-Force", result.Readiness.MissingRoles);
        Assert.Empty(result.Pairs);
    }

    [Fact]
    public void Evaluate_PropagatesMissingRobotsAndUnmatchedBallsFromResolvedPackets()
    {
        var duckFrame = Frame(11, robotX: 1, ballX: 2);
        duckFrame.Balls.Add(new TrackedBall { Pos = new Vector3 { X = 8, Y = 0, Z = 0 } });
        var tigerFrame = Frame(12, robotX: 4, ballX: 2);
        tigerFrame.Robots.RemoveAt(1);
        var result = TrackerDifferenceSnapshotEvaluator.Evaluate(
            [Source("ibis", "duck-id", duckFrame),
             Source("TIGERs", "tigers-id", tigerFrame),
             Source("ER-FORCE", "erforce-id", Frame(13, robotX: 7, ballX: 10))],
            Now,
            freshnessWindow: TimeSpan.FromSeconds(5),
            ballDistanceGateMeters: 1);

        var pair = Assert.Single(result.Pairs, item => item.ReferenceRole == "Duck" && item.ComparisonRole == "TIGERs");
        Assert.Equal(
            TrackerRobotDifferenceStatus.MissingInComparison,
            Assert.Single(pair.Difference!.Robots, robot => robot.RobotId == 4).Status);
        Assert.Equal([1], pair.Difference.Balls.UnmatchedReferenceIndices);
        Assert.Empty(pair.Difference.Balls.UnmatchedComparisonIndices);
    }

    [Fact]
    public void Evaluate_ReportsRepresentativeThreeSourceTimeAndAllocationCost()
    {
        var snapshots = new[]
        {
            Source("ibis", "duck-id", RepresentativeFrame(101, 0.00)),
            Source("TIGERs", "tigers-id", RepresentativeFrame(102, 0.05)),
            Source("ER-FORCE", "erforce-id", RepresentativeFrame(103, 0.10)),
        };
        TrackerDifferenceSnapshotResult? lastResult = null;

        for (var index = 0; index < MeasurementWarmupCalls; index++)
        {
            lastResult = TrackerDifferenceSnapshotEvaluator.Evaluate(
                snapshots,
                Now,
                TimeSpan.FromSeconds(5),
                ballDistanceGateMeters: 0.5);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var elapsedTicksByCall = new long[MeasurementIterations];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var totalStart = Stopwatch.GetTimestamp();
        for (var index = 0; index < MeasurementIterations; index++)
        {
            var callStart = Stopwatch.GetTimestamp();
            lastResult = TrackerDifferenceSnapshotEvaluator.Evaluate(
                snapshots,
                Now,
                TimeSpan.FromSeconds(5),
                ballDistanceGateMeters: 0.5);
            elapsedTicksByCall[index] = Stopwatch.GetTimestamp() - callStart;
        }

        var totalElapsedTicks = Stopwatch.GetTimestamp() - totalStart;
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Array.Sort(elapsedTicksByCall);
        var measuredCallsPerSecond = MeasurementIterations /
            (totalElapsedTicks / (double)Stopwatch.Frequency);
        var p50Microseconds = ToMicroseconds(elapsedTicksByCall[PercentileIndex(0.50)]);
        var p95Microseconds = ToMicroseconds(elapsedTicksByCall[PercentileIndex(0.95)]);
        const double ScenarioCallsPerSecond = 60;

        Assert.True(lastResult!.Readiness.IsReady);
        Assert.Equal(3, lastResult.Pairs.Count);
        output.WriteLine(
            $"snapshot difference measurement: sources=3; robots/source=12; balls/source=1; " +
            $"pairs/call=3; calls/s scenario={ScenarioCallsPerSecond:F0}; measured throughput={measuredCallsPerSecond:F0}/s; " +
            $"calls={MeasurementIterations}; warmup={MeasurementWarmupCalls}; " +
            $"p50={p50Microseconds:F2} us; p95={p95Microseconds:F2} us; " +
            $"allocated/call={allocatedBytes / (double)MeasurementIterations:F0} bytes; " +
            $"p95-at-60Hz-budget={p95Microseconds * ScenarioCallsPerSecond / 1_000_000:P3}; " +
            $"runtime={RuntimeInformation.FrameworkDescription}; os={RuntimeInformation.OSDescription}; " +
            $"arch={RuntimeInformation.ProcessArchitecture}; processors={Environment.ProcessorCount}");
    }

    private static void AssertPair(
        TrackerDifferencePairResult pair,
        string referenceRole,
        uint referenceFrame,
        string comparisonRole,
        uint comparisonFrame,
        double expectedRobotDeltaX,
        double expectedBallDeltaX)
    {
        Assert.Equal(referenceRole, pair.ReferenceRole);
        Assert.Equal(referenceFrame, pair.ReferenceFrameNumber);
        Assert.Equal(comparisonRole, pair.ComparisonRole);
        Assert.Equal(comparisonFrame, pair.ComparisonFrameNumber);
        Assert.Equal(TrackerDifferencePairStatus.Compared, pair.Status);
        Assert.NotNull(pair.Difference);
        Assert.Equal(
            expectedRobotDeltaX,
            Assert.Single(pair.Difference.Robots, robot => robot.Team == Team.Blue && robot.RobotId == 3)
                .PositionDelta!.Value.X);
        Assert.Equal(expectedBallDeltaX, Assert.Single(pair.Difference.Balls.Matches).PositionDelta.X);
        Assert.Empty(pair.Difference.Balls.UnmatchedReferenceIndices);
        Assert.Empty(pair.Difference.Balls.UnmatchedComparisonIndices);
    }

    private static ExternalTrackerReadSideSnapshot Source(
        string sourceName,
        string uuid,
        TrackedFrame frame,
        int endpointPort = 11010,
        DateTimeOffset? receivedAt = null)
    {
        return new ExternalTrackerReadSideSnapshot(
            new TrackerWrapperPacket { SourceName = sourceName, Uuid = uuid, TrackedFrame = frame },
            receivedAt ?? Now,
            sourceName == "ibis" ? "own" : "external",
            sourceName,
            sourceName,
            uuid,
            new IPEndPoint(IPAddress.Parse("192.0.2.10"), endpointPort).ToString());
    }

    private static TrackedFrame Frame(uint frameNumber, double robotX, double ballX)
    {
        var frame = new TrackedFrame { FrameNumber = frameNumber, Timestamp = frameNumber };
        frame.Robots.Add(Robot(Team.Blue, 3, robotX));
        frame.Robots.Add(Robot(Team.Yellow, 4, 100));
        frame.Balls.Add(new TrackedBall { Pos = new Vector3 { X = (float)ballX, Y = 0, Z = 0 } });
        return frame;
    }

    private static TrackedFrame RepresentativeFrame(uint frameNumber, double offset)
    {
        var frame = new TrackedFrame { FrameNumber = frameNumber, Timestamp = frameNumber };
        foreach (var team in new[] { Team.Blue, Team.Yellow })
        {
            for (uint robotId = 0; robotId < 6; robotId++)
            {
                frame.Robots.Add(Robot(
                    team,
                    robotId,
                    offset + robotId * 0.2,
                    team == Team.Blue ? 1 : -1));
            }
        }

        frame.Balls.Add(new TrackedBall
        {
            Pos = new Vector3 { X = (float)offset, Y = 0, Z = 0 },
        });
        return frame;
    }

    private static TrackedRobot Robot(Team team, uint id, double x) => new()
    {
        RobotId = new RobotId { Team = team, Id = id },
        Pos = new Vector2 { X = (float)x, Y = 0 },
        Orientation = 0,
    };

    private static TrackedRobot Robot(Team team, uint id, double x, float y) => new()
    {
        RobotId = new RobotId { Team = team, Id = id },
        Pos = new Vector2 { X = (float)x, Y = y },
        Orientation = 0,
    };

    private static int PercentileIndex(double percentile) =>
        Math.Clamp((int)Math.Ceiling(percentile * MeasurementIterations) - 1, 0, MeasurementIterations - 1);

    private static double ToMicroseconds(long stopwatchTicks) =>
        stopwatchTicks * (1_000_000d / Stopwatch.Frequency);
}
