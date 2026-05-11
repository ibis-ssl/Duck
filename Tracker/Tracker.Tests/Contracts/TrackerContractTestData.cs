namespace Tracker.Tests.Contracts;

/// <summary>
/// 何を確認しているか: Tracker contract test 用の SSL-Vision packet と detection object を一貫した既定値で生成する。
/// </summary>
public static class TrackerContractTestData
{
    public static SSL_DetectionBall CreateBall(
        float x = 0,
        float y = 0,
        float z = 0,
        float confidence = 1.0f)
    {
        return new SSL_DetectionBall
        {
            X = x,
            Y = y,
            Z = z,
            Confidence = confidence,
        };
    }

    public static SSL_DetectionRobot CreateRobot(
        uint robotId,
        float x = 0,
        float y = 0,
        float orientation = 0,
        float confidence = 1.0f)
    {
        return new SSL_DetectionRobot
        {
            RobotId = robotId,
            X = x,
            Y = y,
            Orientation = orientation,
            Confidence = confidence,
        };
    }

    public static SSL_WrapperPacket CreateDetectionPacket(
        uint frameNumber = 1,
        uint cameraId = 0,
        IEnumerable<SSL_DetectionBall>? balls = null,
        IEnumerable<SSL_DetectionRobot>? robotsYellow = null,
        IEnumerable<SSL_DetectionRobot>? robotsBlue = null,
        double captureTimeSeconds = 1.0,
        double sentTimeSeconds = 1.0)
    {
        var packet = new SSL_WrapperPacket
        {
            Detection = new SSL_DetectionFrame
            {
                FrameNumber = frameNumber,
                CameraId = cameraId,
                TCapture = captureTimeSeconds,
                TSent = sentTimeSeconds,
            },
        };

        if (balls is not null)
        {
            packet.Detection.Balls.AddRange(balls);
        }

        if (robotsYellow is not null)
        {
            packet.Detection.RobotsYellow.AddRange(robotsYellow);
        }

        if (robotsBlue is not null)
        {
            packet.Detection.RobotsBlue.AddRange(robotsBlue);
        }

        return packet;
    }

    public static SSL_WrapperPacket CreateGeometryPacket(
        int fieldLength = 12000,
        int fieldWidth = 9000,
        int goalWidth = 1800,
        int goalDepth = 180,
        int boundaryWidth = 300,
        int boundaryWidthGoalLine = 300,
        int penaltyAreaDepth = 1000,
        int penaltyAreaWidth = 2000,
        int centerCircleRadius = 500,
        int lineThickness = 10)
    {
        return new SSL_WrapperPacket
        {
            Geometry = new SSL_GeometryData
            {
                Field = new SSL_GeometryFieldSize
                {
                    FieldLength = fieldLength,
                    FieldWidth = fieldWidth,
                    GoalWidth = goalWidth,
                    GoalDepth = goalDepth,
                    BoundaryWidth = boundaryWidth,
                    BoundaryWidthGoalLine = boundaryWidthGoalLine,
                    PenaltyAreaDepth = penaltyAreaDepth,
                    PenaltyAreaWidth = penaltyAreaWidth,
                    CenterCircleRadius = centerCircleRadius,
                    LineThickness = lineThickness,
                },
            },
        };
    }
}
