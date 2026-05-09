using Tracker.Core;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public class TrackerPacketGeneratorContractTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public TrackerPacketGeneratorContractTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Generate_WithPrimaryAndSecondaryBalls_EmitsPrimaryFirstAndStableSortedSecondaryBalls()
    {
        var frame = fixture.CreateFrame(
            balls:
            [
                fixture.CreateTrackedBall(trackId: 30, xMm: 500, yMm: 600, visibility: 0.95f, lastVisibleTimestampNs: 9_000),
                fixture.CreateTrackedBall(trackId: 10, xMm: 100, yMm: 200, visibility: 0.60f, lastVisibleTimestampNs: 8_000),
                fixture.CreateTrackedBall(trackId: 20, xMm: 300, yMm: 400, visibility: 0.95f, lastVisibleTimestampNs: 7_000),
            ],
            primaryBallTrackId: 10);

        var packet = fixture.CreatePacketGenerator().Generate(frame);
        var trackedFrame = AssertTrackedFrame(packet);

        Assert.Collection(
            trackedFrame.Balls,
            ball =>
            {
                Assert.Equal(0.1f, ball.Pos.X, 3);
                Assert.Equal(0.2f, ball.Pos.Y, 3);
            },
            ball =>
            {
                Assert.Equal(0.5f, ball.Pos.X, 3);
                Assert.Equal(0.6f, ball.Pos.Y, 3);
            },
            ball =>
            {
                Assert.Equal(0.3f, ball.Pos.X, 3);
                Assert.Equal(0.4f, ball.Pos.Y, 3);
            });
    }

    [Fact]
    public void Generate_WhenSecondaryBallsTieOnVisibilityAndTimestamp_UsesInternalTrackIdAsFinalTieBreaker()
    {
        var frame = fixture.CreateFrame(
            balls:
            [
                fixture.CreateTrackedBall(trackId: 40, xMm: 400, yMm: 400, visibility: 0.50f, lastVisibleTimestampNs: 5_000),
                fixture.CreateTrackedBall(trackId: 30, xMm: 300, yMm: 300, visibility: 0.80f, lastVisibleTimestampNs: 7_000),
                fixture.CreateTrackedBall(trackId: 10, xMm: 100, yMm: 100, visibility: 0.80f, lastVisibleTimestampNs: 7_000),
                fixture.CreateTrackedBall(trackId: 20, xMm: 200, yMm: 200, visibility: 0.80f, lastVisibleTimestampNs: 7_000),
            ],
            primaryBallTrackId: 40);

        var packet = fixture.CreatePacketGenerator().Generate(frame);
        var trackedFrame = AssertTrackedFrame(packet);

        Assert.Collection(
            trackedFrame.Balls,
            ball => Assert.Equal(0.4f, ball.Pos.X, 3),
            ball => Assert.Equal(0.1f, ball.Pos.X, 3),
            ball => Assert.Equal(0.2f, ball.Pos.X, 3),
            ball => Assert.Equal(0.3f, ball.Pos.X, 3));
    }

    [Fact]
    public void Generate_ConvertsInternalUnitsAndUsesFrameDataTimestamp()
    {
        var frame = fixture.CreateFrame(
            dataTimestampNs: 12_500_000_000,
            balls:
            [
                fixture.CreateTrackedBall(
                    trackId: 10,
                    xMm: 1200,
                    yMm: -3400,
                    zMm: 150,
                    vxMmPerS: 2500,
                    vyMmPerS: -1250,
                    vzMmPerS: 300),
            ],
            primaryBallTrackId: 10);

        var packet = fixture.CreatePacketGenerator().Generate(frame);
        var trackedFrame = AssertTrackedFrame(packet);
        var ball = Assert.Single(trackedFrame.Balls);

        Assert.Equal(12.5, trackedFrame.Timestamp, 6);
        Assert.Equal(1.2f, ball.Pos.X, 3);
        Assert.Equal(-3.4f, ball.Pos.Y, 3);
        Assert.Equal(0.15f, ball.Pos.Z, 3);
        Assert.Equal(2.5f, ball.Vel.X, 3);
        Assert.Equal(-1.25f, ball.Vel.Y, 3);
        Assert.Equal(0.3f, ball.Vel.Z, 3);
    }

    [Fact]
    public void Generate_EmitsWrapperMetadata()
    {
        var frame = fixture.CreateFrame(
            balls:
            [
                fixture.CreateTrackedBall(trackId: 10, xMm: 120, yMm: 240),
            ],
            primaryBallTrackId: 10);

        var packet = fixture.CreatePacketGenerator().Generate(frame);

        Assert.Equal(TrackerContractFixture.DefaultUuid, packet.Uuid);
        Assert.Equal(TrackerContractFixture.DefaultSourceName, packet.SourceName);
        Assert.NotNull(packet.TrackedFrame);
    }

    [Fact]
    public void Generate_EmitsRobotsInStableTeamAndIdOrder()
    {
        var frame = fixture.CreateFrame(
            robots:
            [
                new TrackedRobotState { Team = TrackerTeam.Blue, RobotId = 2, XMm = 200, YMm = 200 },
                new TrackedRobotState { Team = TrackerTeam.Yellow, RobotId = 5, XMm = 500, YMm = 500 },
                new TrackedRobotState { Team = TrackerTeam.Blue, RobotId = 1, XMm = 100, YMm = 100 },
                new TrackedRobotState { Team = TrackerTeam.Yellow, RobotId = 3, XMm = 300, YMm = 300 },
            ]);

        var packet = fixture.CreatePacketGenerator().Generate(frame);
        var trackedFrame = AssertTrackedFrame(packet);

        Assert.Collection(
            trackedFrame.Robots,
            robot =>
            {
                Assert.Equal(Team.Yellow, robot.RobotId.Team);
                Assert.Equal((uint)3, robot.RobotId.Id);
            },
            robot =>
            {
                Assert.Equal(Team.Yellow, robot.RobotId.Team);
                Assert.Equal((uint)5, robot.RobotId.Id);
            },
            robot =>
            {
                Assert.Equal(Team.Blue, robot.RobotId.Team);
                Assert.Equal((uint)1, robot.RobotId.Id);
            },
            robot =>
            {
                Assert.Equal(Team.Blue, robot.RobotId.Team);
                Assert.Equal((uint)2, robot.RobotId.Id);
            });
    }

    [Fact]
    public void Generate_EmitsExpectedCapabilitiesInStableOrder()
    {
        var frame = fixture.CreateFrame(
            balls:
            [
                fixture.CreateTrackedBall(trackId: 10, isFlying: true),
                fixture.CreateTrackedBall(trackId: 20),
            ],
            primaryBallTrackId: 10,
            kickedBall: fixture.CreateKick(isStillMoving: true));

        var packet = fixture.CreatePacketGenerator().Generate(frame);
        var trackedFrame = AssertTrackedFrame(packet);

        Assert.Equal(
            [
                Capability.DetectKickedBalls,
                Capability.DetectFlyingBalls,
                Capability.DetectMultipleBalls,
            ],
            trackedFrame.Capabilities);
    }

    [Fact]
    public void Generate_EmitsFixedCapabilitiesEvenWhenFrameDoesNotContainKickOrFlyingBall()
    {
        var frame = fixture.CreateFrame(
            balls:
            [
                fixture.CreateTrackedBall(trackId: 10, isFlying: false),
            ],
            primaryBallTrackId: 10,
            kickedBall: null);

        var packet = fixture.CreatePacketGenerator().Generate(frame);
        var trackedFrame = AssertTrackedFrame(packet);

        Assert.Equal(
            [
                Capability.DetectKickedBalls,
                Capability.DetectFlyingBalls,
                Capability.DetectMultipleBalls,
            ],
            trackedFrame.Capabilities);
    }

    [Fact]
    public void Generate_WithStillMovingKick_EmitsConvertedKickedBallPayload()
    {
        var frame = fixture.CreateFrame(
            balls:
            [
                fixture.CreateTrackedBall(trackId: 10, xMm: 100, yMm: 200),
            ],
            primaryBallTrackId: 10,
            kickedBall: fixture.CreateKick(
                isStillMoving: true,
                startXMm: 1200,
                startYMm: -3400,
                startTimestampNs: 12_500_000_000,
                initialVelocityXMmPerS: 2500,
                initialVelocityYMmPerS: -1250,
                initialVelocityZMmPerS: 300,
                stopXMm: 1800,
                stopYMm: -4000,
                stopTimestampNs: 13_500_000_000,
                kickerRobotId: 7));

        var packet = fixture.CreatePacketGenerator().Generate(frame);
        var trackedFrame = AssertTrackedFrame(packet);
        var kickedBall = Assert.IsType<KickedBall>(trackedFrame.KickedBall);

        Assert.Equal(1.2f, kickedBall.Pos.X, 3);
        Assert.Equal(-3.4f, kickedBall.Pos.Y, 3);
        Assert.Equal(2.5f, kickedBall.Vel.X, 3);
        Assert.Equal(-1.25f, kickedBall.Vel.Y, 3);
        Assert.Equal(0.3f, kickedBall.Vel.Z, 3);
        Assert.Equal(12.5, kickedBall.StartTimestamp, 6);
        Assert.Equal(13.5, kickedBall.StopTimestamp, 6);
        Assert.Equal(1.8f, kickedBall.StopPos.X, 3);
        Assert.Equal(-4.0f, kickedBall.StopPos.Y, 3);
        Assert.Equal((uint)7, kickedBall.RobotId.Id);
    }

    [Fact]
    public void Generate_OmitsKickedBall_WhenKickIsNoLongerMoving()
    {
        var frame = fixture.CreateFrame(
            balls:
            [
                fixture.CreateTrackedBall(trackId: 10, xMm: 100, yMm: 200),
            ],
            primaryBallTrackId: 10,
            kickedBall: fixture.CreateKick(isStillMoving: false));

        var packet = fixture.CreatePacketGenerator().Generate(frame);
        var trackedFrame = AssertTrackedFrame(packet);

        Assert.Null(trackedFrame.KickedBall);
    }

    private static TrackedFrame AssertTrackedFrame(TrackerWrapperPacket packet)
    {
        return Assert.IsType<TrackedFrame>(packet.TrackedFrame);
    }
}
