using Tracker.Core;
using Tracker.Server.Components.Vision;
using Tracker.Server.Tracking;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public class TrackedVisionViewStateTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public TrackedVisionViewStateTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void FromSnapshot_WithLatestFrame_MapsTrackedObjectsAndGeometryForViewer()
    {
        var latestFrame = fixture.CreateFrame(
            frameNumber: 7,
            dataTimestampNs: 2_500_000_000,
            balls:
            [
                fixture.CreateTrackedBall(trackId: 3, xMm: 1200, yMm: -400, zMm: 50),
                fixture.CreateTrackedBall(trackId: 8, xMm: -800, yMm: 600),
            ],
            robots:
            [
                new TrackedRobotState
                {
                    Team = TrackerTeam.Yellow,
                    RobotId = 2,
                    XMm = 1000,
                    YMm = 500,
                    OrientationRad = 0.5,
                    HasBallContact = true,
                },
                new TrackedRobotState
                {
                    Team = TrackerTeam.Blue,
                    RobotId = 4,
                    XMm = -900,
                    YMm = -300,
                    OrientationRad = -0.25,
                },
            ],
            primaryBallTrackId: 3);
        latestFrame = new TrackerFrame
        {
            FrameNumber = latestFrame.FrameNumber,
            DataTimestampNs = latestFrame.DataTimestampNs,
            ProcessedAtNs = latestFrame.ProcessedAtNs,
            Balls = latestFrame.Balls,
            Robots = latestFrame.Robots,
            PrimaryBallTrackId = latestFrame.PrimaryBallTrackId,
            KickedBall = latestFrame.KickedBall,
            GeometrySnapshot = new TrackerGeometrySnapshot
            {
                FieldLengthMm = 12000,
                FieldWidthMm = 9000,
                GoalWidthMm = 1800,
                GoalDepthMm = 200,
                BoundaryWidthMm = 300,
                BoundaryWidthGoalLineMm = 350,
                LineThicknessMm = 10,
            },
            Metadata = new TrackerFrameMetadata
            {
                ProfileName = "fast",
            },
        };

        var snapshot = new TrackedSnapshot(
            latestFrame,
            new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero),
            "default",
            11,
            1);

        var viewState = TrackedVisionViewState.FromSnapshot(snapshot);

        Assert.True(viewState.HasFrame);
        Assert.Equal("fast", viewState.ProfileName);
        Assert.Equal("7", viewState.FrameLabel);
        Assert.Equal(2, viewState.Balls.Count);
        Assert.Equal(1200, viewState.Balls[0].X);
        Assert.Equal((uint)2, Assert.Single(viewState.RobotsYellow).RobotId);
        Assert.Equal((uint)4, Assert.Single(viewState.RobotsBlue).RobotId);
        Assert.NotNull(viewState.Geometry);
        Assert.Equal(12000, viewState.Geometry.Field.FieldLength);
        Assert.Equal(1800, viewState.Geometry.Field.GoalWidth);
        Assert.Equal(300, viewState.Geometry.Field.BoundaryWidth);
        Assert.Equal(11, viewState.PublishSuccessCount);
        Assert.Equal(1, viewState.PublishFailureCount);
    }

    [Fact]
    public void FromSnapshot_WithoutLatestFrame_UsesActiveProfileAndReturnsEmptyViewerState()
    {
        var snapshot = new TrackedSnapshot(
            LatestFrame: null,
            ReceivedAt: null,
            ActiveProfileName: "lab",
            PublishSuccessCount: 3,
            PublishFailureCount: 2);

        var viewState = TrackedVisionViewState.FromSnapshot(snapshot);

        Assert.False(viewState.HasFrame);
        Assert.Equal("lab", viewState.ProfileName);
        Assert.Equal("-", viewState.FrameLabel);
        Assert.Null(viewState.Geometry);
        Assert.Empty(viewState.Balls);
        Assert.Empty(viewState.RobotsYellow);
        Assert.Empty(viewState.RobotsBlue);
        Assert.Equal(3, viewState.PublishSuccessCount);
        Assert.Equal(2, viewState.PublishFailureCount);
    }
}
