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
    TrackedDiagnosticsViewState Diagnostics,
    TrackedKickViewState Kick,
    TrackedContactViewState Contact,
    TrackedFieldStateViewState FieldState,
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
                new TrackedDiagnosticsViewState(0, 0, null, null),
                new TrackedKickViewState(false, false, null, null),
                new TrackedContactViewState(false, TrackerTeam.Unknown, null, TrackerTeam.Unknown, null),
                new TrackedFieldStateViewState(false, null),
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
            new TrackedDiagnosticsViewState(
                latestFrame.Balls.Count,
                latestFrame.Robots.Count,
                latestFrame.DataTimestampNs,
                latestFrame.ProcessedAtNs),
            CreateKick(latestFrame.KickedBall),
            CreateContact(latestFrame.LatestContact),
            CreateFieldState(latestFrame.BallLeftField),
            snapshot.PublishSuccessCount,
            snapshot.PublishFailureCount);
    }

    private static TrackedKickViewState CreateKick(KickEventState? kick)
    {
        return kick is null
            ? new TrackedKickViewState(false, false, null, null)
            : new TrackedKickViewState(true, kick.IsStillMoving, kick.KickerRobotId, kick.KickKind);
    }

    private static TrackedContactViewState CreateContact(BallContactState? contact)
    {
        return contact is null
            ? new TrackedContactViewState(false, TrackerTeam.Unknown, null, TrackerTeam.Unknown, null)
            : new TrackedContactViewState(
                contact.IsInContact,
                contact.ContactingTeam,
                contact.ContactingRobotId,
                contact.LastTeam,
                contact.LastRobotId);
    }

    private static TrackedFieldStateViewState CreateFieldState(BallLeftFieldState? fieldState)
    {
        return fieldState is null
            ? new TrackedFieldStateViewState(false, null)
            : new TrackedFieldStateViewState(fieldState.IsOutOfField, fieldState.BoundaryName);
    }

    private static SSL_GeometryData? CreateGeometry(TrackerGeometrySnapshot? geometrySnapshot)
    {
        if (geometrySnapshot is null)
        {
            return null;
        }

        var field = new SSL_GeometryFieldSize
        {
            FieldLength = geometrySnapshot.FieldLengthMm,
            FieldWidth = geometrySnapshot.FieldWidthMm,
            GoalWidth = geometrySnapshot.GoalWidthMm,
            GoalDepth = geometrySnapshot.GoalDepthMm,
            BoundaryWidth = geometrySnapshot.BoundaryWidthMm,
            BoundaryWidthGoalLine = geometrySnapshot.BoundaryWidthGoalLineMm,
            PenaltyAreaDepth = geometrySnapshot.PenaltyAreaDepthMm,
            PenaltyAreaWidth = geometrySnapshot.PenaltyAreaWidthMm,
            CenterCircleRadius = geometrySnapshot.CenterCircleRadiusMm,
            LineThickness = geometrySnapshot.LineThicknessMm,
        };
        field.FieldLines.AddRange(geometrySnapshot.FieldLines.Select(CreateFieldLine));
        field.FieldArcs.AddRange(geometrySnapshot.FieldArcs.Select(CreateFieldArc));

        return new SSL_GeometryData { Field = field };
    }

    private static SSL_FieldLineSegment CreateFieldLine(TrackerGeometryLineSegment line)
    {
        return new SSL_FieldLineSegment
        {
            Name = line.Name,
            P1 = new Vector2f { X = (float)line.P1XMm, Y = (float)line.P1YMm },
            P2 = new Vector2f { X = (float)line.P2XMm, Y = (float)line.P2YMm },
            Thickness = (float)line.ThicknessMm,
            Type = line.Type,
        };
    }

    private static SSL_FieldCircularArc CreateFieldArc(TrackerGeometryCircularArc arc)
    {
        return new SSL_FieldCircularArc
        {
            Name = arc.Name,
            Center = new Vector2f { X = (float)arc.CenterXMm, Y = (float)arc.CenterYMm },
            Radius = (float)arc.RadiusMm,
            A1 = (float)arc.A1Rad,
            A2 = (float)arc.A2Rad,
            Thickness = (float)arc.ThicknessMm,
            Type = arc.Type,
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

public sealed record TrackedDiagnosticsViewState(
    int BallCount,
    int RobotCount,
    long? DataTimestampNs,
    long? ProcessedAtNs);

public sealed record TrackedKickViewState(
    bool IsDetected,
    bool IsStillMoving,
    uint? KickerRobotId,
    string? KickKind);

public sealed record TrackedContactViewState(
    bool IsInContact,
    TrackerTeam ContactingTeam,
    uint? ContactingRobotId,
    TrackerTeam LastTeam,
    uint? LastRobotId);

public sealed record TrackedFieldStateViewState(
    bool IsOutOfField,
    string? BoundaryName);
