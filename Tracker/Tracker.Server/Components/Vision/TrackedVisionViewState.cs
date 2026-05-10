using System.Globalization;
using Tracker.Core;
using Tracker.Server.Tracking;

namespace Tracker.Server.Components.Vision;

public sealed record TrackedVisionViewState(
    bool HasFrame,
    string ProfileName,
    string FrameLabel,
    DateTimeOffset? ReceivedAt,
    SSL_GeometryData? Geometry,
    IReadOnlyList<SSL_DetectionBall> Balls,
    IReadOnlyList<SSL_DetectionRobot> RobotsYellow,
    IReadOnlyList<SSL_DetectionRobot> RobotsBlue,
    long PublishSuccessCount,
    long PublishFailureCount)
{
    public static TrackedVisionViewState FromSnapshot(TrackedSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var latestFrame = snapshot.LatestFrame;
        if (latestFrame is null)
        {
            return new TrackedVisionViewState(
                false,
                snapshot.ActiveProfileName,
                "-",
                snapshot.ReceivedAt,
                null,
                Array.Empty<SSL_DetectionBall>(),
                Array.Empty<SSL_DetectionRobot>(),
                Array.Empty<SSL_DetectionRobot>(),
                snapshot.PublishSuccessCount,
                snapshot.PublishFailureCount);
        }

        var profileName = latestFrame.Metadata.ProfileName ?? snapshot.ActiveProfileName;
        var yellowRobots = latestFrame.Robots
            .Where(robot => robot.Team == TrackerTeam.Yellow)
            .Select(CreateRobot)
            .ToArray();
        var blueRobots = latestFrame.Robots
            .Where(robot => robot.Team == TrackerTeam.Blue)
            .Select(CreateRobot)
            .ToArray();

        return new TrackedVisionViewState(
            true,
            profileName,
            latestFrame.FrameNumber.ToString(CultureInfo.InvariantCulture),
            snapshot.ReceivedAt,
            CreateGeometry(latestFrame.GeometrySnapshot),
            latestFrame.Balls.Select(CreateBall).ToArray(),
            yellowRobots,
            blueRobots,
            snapshot.PublishSuccessCount,
            snapshot.PublishFailureCount);
    }

    private static SSL_GeometryData? CreateGeometry(TrackerGeometrySnapshot? geometrySnapshot)
    {
        if (geometrySnapshot is null)
        {
            return null;
        }

        return new SSL_GeometryData
        {
            Field = new SSL_GeometryFieldSize
            {
                FieldLength = geometrySnapshot.FieldLengthMm,
                FieldWidth = geometrySnapshot.FieldWidthMm,
                GoalWidth = geometrySnapshot.GoalWidthMm,
                GoalDepth = geometrySnapshot.GoalDepthMm,
                BoundaryWidth = geometrySnapshot.BoundaryWidthMm,
                BoundaryWidthGoalLine = geometrySnapshot.BoundaryWidthGoalLineMm,
                LineThickness = geometrySnapshot.LineThicknessMm,
            },
        };
    }

    private static SSL_DetectionBall CreateBall(TrackedBallState ball)
    {
        return new SSL_DetectionBall
        {
            Confidence = ball.Visibility,
            X = (float)ball.XMm,
            Y = (float)ball.YMm,
            Z = (float)ball.ZMm,
        };
    }

    private static SSL_DetectionRobot CreateRobot(TrackedRobotState robot)
    {
        return new SSL_DetectionRobot
        {
            Confidence = robot.Visibility,
            RobotId = robot.RobotId,
            X = (float)robot.XMm,
            Y = (float)robot.YMm,
            Orientation = (float)robot.OrientationRad,
        };
    }
}
