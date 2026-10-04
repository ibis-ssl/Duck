namespace Tracker.Tests;

internal sealed class RobotMotionAcceptanceVerifier(float minimumDistanceMm)
{
    public void CaptureBaseline(SSL_DetectionFrame frame)
    {
    }

    public RobotMotionDelta? FindMovement(SSL_DetectionFrame frame) => null;
}

internal sealed record RobotMotionDelta(string Team, uint RobotId, float DistanceMm);
