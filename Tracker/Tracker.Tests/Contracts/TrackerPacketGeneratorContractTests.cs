using Tracker.Core;

namespace Tracker.Tests;

public class TrackerPacketGeneratorContractTests
{
    [Fact]
    public void Generate_WithPrimaryAndSecondaryBalls_EmitsPrimaryFirstAndStableSortedSecondaryBalls()
    {
        var frame = CreateFrame(
            balls:
            [
                CreateBall(trackId: 30, xMm: 500, yMm: 600, visibility: 0.95f, lastVisibleTimestampNs: 9_000),
                CreateBall(trackId: 10, xMm: 100, yMm: 200, visibility: 0.60f, lastVisibleTimestampNs: 8_000),
                CreateBall(trackId: 20, xMm: 300, yMm: 400, visibility: 0.95f, lastVisibleTimestampNs: 7_000),
            ],
            primaryBallTrackId: 10);

        var packet = CreateGenerator().Generate(frame);
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
    public void Generate_ConvertsInternalUnitsAndUsesFrameDataTimestamp()
    {
        var frame = CreateFrame(
            dataTimestampNs: 12_500_000_000,
            balls:
            [
                CreateBall(
                    trackId: 10,
                    xMm: 1200,
                    yMm: -3400,
                    zMm: 150,
                    vxMmPerS: 2500,
                    vyMmPerS: -1250,
                    vzMmPerS: 300),
            ],
            primaryBallTrackId: 10);

        var packet = CreateGenerator().Generate(frame);
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
    public void Generate_EmitsExpectedCapabilitiesInStableOrder()
    {
        var frame = CreateFrame(
            balls:
            [
                CreateBall(trackId: 10, isFlying: true),
                CreateBall(trackId: 20),
            ],
            primaryBallTrackId: 10,
            kickedBall: CreateKick(isStillMoving: true));

        var packet = CreateGenerator().Generate(frame);
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
    public void Generate_OmitsKickedBall_WhenKickIsNoLongerMoving()
    {
        var frame = CreateFrame(
            balls:
            [
                CreateBall(trackId: 10, xMm: 100, yMm: 200),
            ],
            primaryBallTrackId: 10,
            kickedBall: CreateKick(isStillMoving: false));

        var packet = CreateGenerator().Generate(frame);
        var trackedFrame = AssertTrackedFrame(packet);

        Assert.Null(trackedFrame.KickedBall);
    }

    private static TrackerPacketGenerator CreateGenerator()
    {
        return new TrackerPacketGenerator(sourceName: "test-source", uuid: "test-uuid");
    }

    private static TrackedFrame AssertTrackedFrame(TrackerWrapperPacket packet)
    {
        return Assert.IsType<TrackedFrame>(packet.TrackedFrame);
    }

    private static TrackerFrame CreateFrame(
        long dataTimestampNs = 1_000_000_000,
        IReadOnlyList<TrackedBallState>? balls = null,
        int primaryBallTrackId = 1,
        KickEventState? kickedBall = null)
    {
        return new TrackerFrame
        {
            FrameNumber = 42,
            DataTimestampNs = dataTimestampNs,
            ProcessedAtNs = dataTimestampNs + 1_000_000,
            Balls = balls ?? [],
            Robots = [],
            PrimaryBallTrackId = primaryBallTrackId,
            KickedBall = kickedBall,
        };
    }

    private static TrackedBallState CreateBall(
        int trackId,
        double xMm = 0,
        double yMm = 0,
        double zMm = 0,
        double vxMmPerS = 0,
        double vyMmPerS = 0,
        double vzMmPerS = 0,
        float visibility = 1.0f,
        long lastVisibleTimestampNs = 1_000_000_000,
        bool isFlying = false)
    {
        return new TrackedBallState
        {
            InternalTrackId = trackId,
            XMm = xMm,
            YMm = yMm,
            ZMm = zMm,
            VXMmPerS = vxMmPerS,
            VYMmPerS = vyMmPerS,
            VZMmPerS = vzMmPerS,
            Visibility = visibility,
            LastVisibleTimestampNs = lastVisibleTimestampNs,
            IsFlying = isFlying,
        };
    }

    private static KickEventState CreateKick(bool isStillMoving)
    {
        return new KickEventState
        {
            StartXMm = 100,
            StartYMm = 200,
            StartTimestampNs = 9_500_000_000,
            InitialVelocityXMmPerS = 3000,
            InitialVelocityYMmPerS = 1500,
            InitialVelocityZMmPerS = 0,
            StopXMm = 900,
            StopYMm = 1000,
            StopTimestampNs = 12_000_000_000,
            IsStillMoving = isStillMoving,
            BallTrackId = 10,
        };
    }
}
