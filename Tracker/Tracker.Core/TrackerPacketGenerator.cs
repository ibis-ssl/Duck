namespace Tracker.Core;

public sealed class TrackerPacketGenerator
{
    private static readonly Capability[] Capabilities =
    [
        Capability.DetectKickedBalls,
        Capability.DetectFlyingBalls,
        Capability.DetectMultipleBalls,
    ];

    public TrackerPacketGenerator(string sourceName, string uuid)
    {
        SourceName = sourceName;
        Uuid = uuid;
    }

    public string SourceName { get; }

    public string Uuid { get; }

    public TrackerWrapperPacket Generate(TrackerFrame frame)
    {
        var trackedFrame = new TrackedFrame
        {
            FrameNumber = frame.FrameNumber,
            Timestamp = ToSeconds(frame.DataTimestampNs),
        };

        trackedFrame.Balls.AddRange(OrderBalls(frame).Select(CreateTrackedBall));
        trackedFrame.Robots.AddRange(frame.Robots
            .OrderBy(robot => GetRobotSortKey(robot.Team))
            .ThenBy(robot => robot.RobotId)
            .Select(CreateTrackedRobot));
        trackedFrame.Capabilities.AddRange(Capabilities);

        if (frame.KickedBall is { IsStillMoving: true } kickedBall)
        {
            trackedFrame.KickedBall = CreateKickedBall(kickedBall);
        }

        return new TrackerWrapperPacket
        {
            Uuid = Uuid,
            SourceName = SourceName,
            TrackedFrame = trackedFrame,
        };
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

    private static TrackedBall CreateTrackedBall(TrackedBallState state)
    {
        return new TrackedBall
        {
            Pos = new Vector3
            {
                X = ToMeters(state.XMm),
                Y = ToMeters(state.YMm),
                Z = ToMeters(state.ZMm),
            },
            Vel = new Vector3
            {
                X = ToMeters(state.VXMmPerS),
                Y = ToMeters(state.VYMmPerS),
                Z = ToMeters(state.VZMmPerS),
            },
            Visibility = state.Visibility,
        };
    }

    private static TrackedRobot CreateTrackedRobot(TrackedRobotState state)
    {
        return new TrackedRobot
        {
            RobotId = new RobotId
            {
                Id = state.RobotId,
                Team = ToProtoTeam(state.Team),
            },
            Pos = new Vector2
            {
                X = ToMeters(state.XMm),
                Y = ToMeters(state.YMm),
            },
            Orientation = (float)state.OrientationRad,
            Vel = new Vector2
            {
                X = ToMeters(state.VXMmPerS),
                Y = ToMeters(state.VYMmPerS),
            },
            VelAngular = (float)state.AngularVelocityRadPerS,
            Visibility = state.Visibility,
        };
    }

    private static KickedBall CreateKickedBall(KickEventState state)
    {
        var kickedBall = new KickedBall
        {
            Pos = new Vector2
            {
                X = ToMeters(state.StartXMm),
                Y = ToMeters(state.StartYMm),
            },
            Vel = new Vector3
            {
                X = ToMeters(state.InitialVelocityXMmPerS),
                Y = ToMeters(state.InitialVelocityYMmPerS),
                Z = ToMeters(state.InitialVelocityZMmPerS),
            },
            StartTimestamp = ToSeconds(state.StartTimestampNs),
        };

        if (state.StopTimestampNs is { } stopTimestampNs)
        {
            kickedBall.StopTimestamp = ToSeconds(stopTimestampNs);
        }

        if (state.StopXMm is { } stopXMm && state.StopYMm is { } stopYMm)
        {
            kickedBall.StopPos = new Vector2
            {
                X = ToMeters(stopXMm),
                Y = ToMeters(stopYMm),
            };
        }

        if (state.KickerRobotId is { } robotId)
        {
            kickedBall.RobotId = new RobotId
            {
                Id = robotId,
            };
        }

        return kickedBall;
    }

    private static Team ToProtoTeam(TrackerTeam team)
    {
        return team switch
        {
            TrackerTeam.Yellow => Team.Yellow,
            TrackerTeam.Blue => Team.Blue,
            _ => Team.Unknown,
        };
    }

    private static int GetRobotSortKey(TrackerTeam team)
    {
        return team switch
        {
            TrackerTeam.Yellow => 0,
            TrackerTeam.Blue => 1,
            _ => 2,
        };
    }

    private static float ToMeters(double millimeters)
    {
        return (float)(millimeters / 1000.0);
    }

    private static double ToSeconds(long nanoseconds)
    {
        return nanoseconds / 1_000_000_000.0;
    }
}
