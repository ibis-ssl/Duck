namespace Tracker.Tests;

public sealed class RobotMotionAcceptanceVerifierTests
{
    [Fact]
    public void FindMovement_RequiresTheSameTeamAndRobotIdentity()
    {
        var verifier = new RobotMotionAcceptanceVerifier(minimumDistanceMm: 100);
        verifier.CaptureBaseline(CreateFrame(yellow: [(2, 100, 200)], blue: [(2, -500, 0)]));

        var movement = verifier.FindMovement(CreateFrame(
            yellow: [(3, 500, 500)],
            blue: [(2, 1000, 0)]));

        Assert.Null(movement);
    }

    [Fact]
    public void FindMovement_RequiresTheMinimumDistanceAndReportsObservedMovement()
    {
        var verifier = new RobotMotionAcceptanceVerifier(minimumDistanceMm: 100);
        verifier.CaptureBaseline(CreateFrame(yellow: [(4, 100, 200)], blue: []));

        Assert.Null(verifier.FindMovement(CreateFrame(yellow: [(4, 159, 280)], blue: [])));

        var movement = verifier.FindMovement(CreateFrame(yellow: [(4, 160, 280)], blue: []));

        Assert.NotNull(movement);
        Assert.Equal("yellow", movement.Team);
        Assert.Equal(4u, movement.RobotId);
        Assert.Equal(100, movement.DistanceMm, precision: 3);
    }

    private static SSL_DetectionFrame CreateFrame(
        (uint Id, float X, float Y)[] yellow,
        (uint Id, float X, float Y)[] blue)
    {
        var frame = new SSL_DetectionFrame
        {
            FrameNumber = 1,
            TCapture = 1,
            TSent = 1,
            CameraId = 0,
        };

        foreach (var robot in yellow)
        {
            frame.RobotsYellow.Add(CreateRobot(robot));
        }

        foreach (var robot in blue)
        {
            frame.RobotsBlue.Add(CreateRobot(robot));
        }

        return frame;
    }

    private static SSL_DetectionRobot CreateRobot((uint Id, float X, float Y) value) => new()
    {
        RobotId = value.Id,
        Confidence = 1,
        X = value.X,
        Y = value.Y,
        Orientation = 0,
        PixelX = 0,
        PixelY = 0,
    };
}
