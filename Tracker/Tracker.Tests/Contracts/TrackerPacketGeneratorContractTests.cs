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
