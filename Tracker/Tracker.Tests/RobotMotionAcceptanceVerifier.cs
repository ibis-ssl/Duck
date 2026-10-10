namespace Tracker.Tests;

internal sealed class RobotMotionAcceptanceVerifier
{
    private readonly float minimumDistanceMm;
    private readonly Dictionary<RobotIdentity, (float X, float Y)> baseline = [];

    public RobotMotionAcceptanceVerifier(float minimumDistanceMm)
    {
        if (!float.IsFinite(minimumDistanceMm) || minimumDistanceMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumDistanceMm));
        }

        this.minimumDistanceMm = minimumDistanceMm;
    }

    public void CaptureBaseline(SSL_DetectionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        baseline.Clear();
        Capture(frame.RobotsYellow, "yellow");

        void Capture(IEnumerable<SSL_DetectionRobot> robots, string team)
        {
            foreach (var robot in robots)
            {
                if (robot.HasRobotId)
                {
                    baseline[new(team, robot.RobotId)] = (robot.X, robot.Y);
                }
            }
        }
    }

    public RobotMotionDelta? FindMovement(SSL_DetectionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return Find(frame.RobotsYellow, "yellow");

        RobotMotionDelta? Find(IEnumerable<SSL_DetectionRobot> robots, string team)
        {
            foreach (var robot in robots)
            {
                if (!robot.HasRobotId || !baseline.TryGetValue(new(team, robot.RobotId), out var start))
                {
                    continue;
                }

                var distance = MathF.Sqrt(MathF.Pow(robot.X - start.X, 2) + MathF.Pow(robot.Y - start.Y, 2));
                if (distance >= minimumDistanceMm)
                {
                    return new(team, robot.RobotId, distance);
                }
            }

            return null;
        }
    }

    private readonly record struct RobotIdentity(string Team, uint RobotId);
}

internal sealed record RobotMotionDelta(string Team, uint RobotId, float DistanceMm);
