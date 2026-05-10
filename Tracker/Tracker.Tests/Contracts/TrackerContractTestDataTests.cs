using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public class TrackerContractTestDataTests
{
    [Fact]
    public void CreateDetectionPacket_BuildsDetectionFrameWithProvidedObjects()
    {
        var ball = TrackerContractTestData.CreateBall(x: 120, y: -340, z: 10, confidence: 0.8f);
        var yellowRobot = TrackerContractTestData.CreateRobot(robotId: 2, x: 400, y: 500, orientation: 1.5f);
        var blueRobot = TrackerContractTestData.CreateRobot(robotId: 5, x: -400, y: -500, orientation: -1.5f);

        var packet = TrackerContractTestData.CreateDetectionPacket(
            frameNumber: 42,
            cameraId: 3,
            balls: [ball],
            robotsYellow: [yellowRobot],
            robotsBlue: [blueRobot],
            captureTimeSeconds: 12.5,
            sentTimeSeconds: 12.6);

        Assert.NotNull(packet.Detection);
        Assert.Equal((uint)42, packet.Detection.FrameNumber);
        Assert.Equal((uint)3, packet.Detection.CameraId);
        Assert.Equal(12.5, packet.Detection.TCapture, precision: 6);
        Assert.Equal(12.6, packet.Detection.TSent, precision: 6);
        Assert.Single(packet.Detection.Balls);
        Assert.Single(packet.Detection.RobotsYellow);
        Assert.Single(packet.Detection.RobotsBlue);
    }

    [Fact]
    public void CreateGeometryPacket_BuildsFieldGeometryWithConfiguredDimensions()
    {
        var packet = TrackerContractTestData.CreateGeometryPacket(
            fieldLength: 9000,
            fieldWidth: 6000,
            goalWidth: 1200,
            goalDepth: 200,
            boundaryWidth: 250);

        Assert.NotNull(packet.Geometry);
        Assert.Equal(9000, packet.Geometry.Field.FieldLength);
        Assert.Equal(6000, packet.Geometry.Field.FieldWidth);
        Assert.Equal(1200, packet.Geometry.Field.GoalWidth);
        Assert.Equal(200, packet.Geometry.Field.GoalDepth);
        Assert.Equal(250, packet.Geometry.Field.BoundaryWidth);
        Assert.Equal(1000, packet.Geometry.Field.PenaltyAreaDepth);
        Assert.Equal(2000, packet.Geometry.Field.PenaltyAreaWidth);
        Assert.Equal(500, packet.Geometry.Field.CenterCircleRadius);
    }
}
