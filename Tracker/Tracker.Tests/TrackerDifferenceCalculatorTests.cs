using Tracker.DebugHost.Tracking;

namespace Tracker.Tests;

public sealed class TrackerDifferenceCalculatorTests
{
    [Fact]
    public void Calculate_RobotsMatchesByTeamAndIdAndReportsMissingWithoutZeroDelta()
    {
        var reference = Frame(
            robots:
            [
                Robot(Team.Blue, 3, x: 1, y: 2, orientation: Math.PI - 0.01, vx: 3, vy: 4, angularVelocity: 0.5),
                Robot(Team.Yellow, 3, x: 10, y: 20),
            ]);
        var comparison = Frame(
            robots:
            [
                Robot(Team.Blue, 3, x: 4, y: 6, orientation: -Math.PI + 0.01, vx: 8, vy: 10, angularVelocity: 0.25),
                Robot(Team.Yellow, 4, x: 100, y: 200),
            ]);

        var result = TrackerDifferenceCalculator.Calculate(reference, comparison, ballDistanceGateMeters: 0.5);

        var matched = Assert.Single(result.Robots, item => item.Team == Team.Blue && item.RobotId == 3);
        Assert.Equal(TrackerRobotDifferenceStatus.Matched, matched.Status);
        Assert.Equal(new Vector2Difference(3, 4), matched.PositionDelta);
        Assert.Equal(new Vector2Difference(5, 6), matched.VelocityDelta);
        Assert.Equal(0.02, matched.OrientationDeltaRadians!.Value, 6);
        Assert.Equal(-0.25, matched.AngularVelocityDeltaRadiansPerSecond);

        var missing = Assert.Single(result.Robots, item => item.Team == Team.Yellow && item.RobotId == 3);
        Assert.Equal(TrackerRobotDifferenceStatus.MissingInComparison, missing.Status);
        Assert.Null(missing.PositionDelta);
        Assert.Null(missing.VelocityDelta);

        var unmatched = Assert.Single(result.Robots, item => item.Team == Team.Yellow && item.RobotId == 4);
        Assert.Equal(TrackerRobotDifferenceStatus.MissingInReference, unmatched.Status);
        Assert.Null(unmatched.PositionDelta);
    }

    [Fact]
    public void Calculate_BallsMaximizesPairCountThenMinimizesTotalDistance()
    {
        var result = TrackerDifferenceCalculator.Calculate(
            Frame(balls: [Ball(0), Ball(4)]),
            Frame(balls: [Ball(3), Ball(7)]),
            ballDistanceGateMeters: 10);

        Assert.Equal([(0, 0), (1, 1)], result.Balls.Matches.Select(match => (match.ReferenceIndex, match.ComparisonIndex)));
        Assert.Equal([3d, 3d], result.Balls.Matches.Select(match => match.DistanceMeters));
        Assert.Empty(result.Balls.UnmatchedReferenceIndices);
        Assert.Empty(result.Balls.UnmatchedComparisonIndices);

        result = TrackerDifferenceCalculator.Calculate(
            Frame(balls: [Ball(0), Ball(5)]),
            Frame(balls: [Ball(0), Ball(-5)]),
            ballDistanceGateMeters: 5);
        Assert.Equal([(0, 1), (1, 0)], result.Balls.Matches.Select(match => (match.ReferenceIndex, match.ComparisonIndex)));

        result = TrackerDifferenceCalculator.Calculate(
            Frame(balls: [Ball(0), Ball(4)]),
            Frame(balls: [Ball(3)]),
            ballDistanceGateMeters: 10);
        Assert.Equal([(1, 0)], result.Balls.Matches.Select(match => (match.ReferenceIndex, match.ComparisonIndex)));
        Assert.Equal([0], result.Balls.UnmatchedReferenceIndices);

        result = TrackerDifferenceCalculator.Calculate(
            Frame(balls: [Ball(3)]),
            Frame(balls: [Ball(0), Ball(4)]),
            ballDistanceGateMeters: 10);
        Assert.Equal([(0, 1)], result.Balls.Matches.Select(match => (match.ReferenceIndex, match.ComparisonIndex)));
        Assert.Equal([0], result.Balls.UnmatchedComparisonIndices);
    }

    [Fact]
    public void Calculate_BallEqualCostAssignmentsUseLexicographicallySmallestIndexPairs()
    {
        var result = TrackerDifferenceCalculator.Calculate(
            Frame(balls: [Ball(0), Ball(2)]),
            Frame(balls: [Ball(1), Ball(1)]),
            ballDistanceGateMeters: 2);

        Assert.Equal([(0, 0), (1, 1)], result.Balls.Matches.Select(match => (match.ReferenceIndex, match.ComparisonIndex)));
    }

    [Fact]
    public void Calculate_BallGateIsInclusiveAndNonFiniteCoordinatesAreUnmatched()
    {
        var result = TrackerDifferenceCalculator.Calculate(
            Frame(balls: [Ball(0), Ball(10), Ball(double.NaN)]),
            Frame(balls: [Ball(1), Ball(12)]),
            ballDistanceGateMeters: 1);

        Assert.Equal([(0, 0)], result.Balls.Matches.Select(match => (match.ReferenceIndex, match.ComparisonIndex)));
        Assert.Equal([1, 2], result.Balls.UnmatchedReferenceIndices);
        Assert.Equal([1], result.Balls.UnmatchedComparisonIndices);
    }

    [Fact]
    public void Calculate_BallDifferencesContainPositionAndVelocityDelta()
    {
        var result = TrackerDifferenceCalculator.Calculate(
            Frame(balls: [Ball(1, y: 2, z: 3, vx: 4, vy: 5, vz: 6)]),
            Frame(balls: [Ball(2, y: 4, z: 6, vx: 8, vy: 10, vz: 12)]),
            ballDistanceGateMeters: 10);

        var match = Assert.Single(result.Balls.Matches);
        Assert.Equal(new Vector3Difference(1, 2, 3), match.PositionDelta);
        Assert.Equal(new Vector3Difference(4, 5, 6), match.VelocityDelta);
    }

    private static TrackedFrame Frame(
        IReadOnlyList<TrackedBall>? balls = null,
        IReadOnlyList<TrackedRobot>? robots = null)
    {
        var frame = new TrackedFrame();
        if (balls is not null)
        {
            frame.Balls.Add(balls);
        }

        if (robots is not null)
        {
            frame.Robots.Add(robots);
        }

        return frame;
    }

    private static TrackedBall Ball(double x, double y = 0, double z = 0, double vx = 0, double vy = 0, double vz = 0) =>
        new()
        {
            Pos = new Vector3 { X = (float)x, Y = (float)y, Z = (float)z },
            Vel = new Vector3 { X = (float)vx, Y = (float)vy, Z = (float)vz },
        };

    private static TrackedRobot Robot(
        Team team,
        uint id,
        double x,
        double y,
        double orientation = 0,
        double vx = 0,
        double vy = 0,
        double angularVelocity = 0) =>
        new()
        {
            RobotId = new RobotId { Team = team, Id = id },
            Pos = new Vector2 { X = (float)x, Y = (float)y },
            Orientation = (float)orientation,
            Vel = new Vector2 { X = (float)vx, Y = (float)vy },
            VelAngular = (float)angularVelocity,
        };
}
