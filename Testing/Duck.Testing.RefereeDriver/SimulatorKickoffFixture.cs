using Google.Protobuf;

namespace Duck.Testing.RefereeDriver;

internal static class SimulatorKickoffFixture
{
    internal static byte[] CreateOwnHalfSetupCommand(bool blueTeamOnPositiveHalf)
    {
        var control = new SimulatorControl
        {
            TeleportBall = new TeleportBall { X = 0, Y = 0, Z = 0, Vx = 0, Vy = 0, Vz = 0 },
        };
        var yellow = new (uint Id, float X, float Y)[]
        {
            (0, 4.2f, 0), (1, 1.2f, -2), (2, 1.2f, -1), (3, 1.2f, 0), (4, 1.2f, 1),
            (5, 1.2f, 2), (6, 2.6f, -2), (7, 2.6f, -1), (8, 2.6f, 0), (9, 2.6f, 1), (10, 2.6f, 2),
        };
        var yellowPositiveHalf = !blueTeamOnPositiveHalf;
        var yellowSign = yellowPositiveHalf ? 1f : -1f;
        var blueSign = -yellowSign;
        var yellowOrientation = yellowPositiveHalf ? MathF.PI : 0f;
        var blueOrientation = blueSign > 0 ? MathF.PI : 0f;
        foreach (var (id, x, y) in yellow)
            control.TeleportRobot.Add(CreateStoppedRobot(id, Team.Yellow, yellowSign * x, y, yellowOrientation));
        foreach (var (id, x, y) in yellow)
            control.TeleportRobot.Add(CreateStoppedRobot(id, Team.Blue, blueSign * x, y, blueOrientation));
        return new SimulatorCommand { Control = control }.ToByteArray();
    }

    private static TeleportRobot CreateStoppedRobot(uint id, Team team, float x, float y, float orientation) =>
        new()
        {
            Id = new RobotId { Id = id, Team = team },
            X = x,
            Y = y,
            Orientation = orientation,
            VX = 0,
            VY = 0,
            VAngular = 0,
            ByForce = false,
        };
}
