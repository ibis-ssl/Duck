using Tracker.Core;
using Tracker.Server.Components.Vision;
using Tracker.Server.Tracking;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: tracked vision viewer 用 view state が snapshot と空状態を正しく表現することを検証する。
/// </summary>
public class TrackedVisionViewStateTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public TrackedVisionViewStateTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// 何を確認しているか: latest frame の object、geometry、diagnostics、event metadata が viewer 用 view state に写ることを確認する。
    /// </summary>
    [Fact]
    public void FromSnapshot_WithLatestFrame_MapsTrackedObjectsAndGeometryForViewer()
    {
        // Viewer が参照する geometry と event metadata を同じ snapshot に含め、統合 mapping の契約を固定する。
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
            GeometrySnapshot = new TrackerGeometrySnapshot
            {
                FieldLengthMm = 12000,
                FieldWidthMm = 9000,
                GoalWidthMm = 1800,
                GoalDepthMm = 200,
                BoundaryWidthMm = 300,
                BoundaryWidthGoalLineMm = 350,
                PenaltyAreaDepthMm = 1200,
                PenaltyAreaWidthMm = 2400,
                CenterCircleRadiusMm = 600,
                LineThicknessMm = 10,
                FieldLines =
                [
                    new TrackerGeometryLineSegment
                    {
                        Name = "HalfwayLine",
                        P1YMm = -4500,
                        P2YMm = 4500,
                        ThicknessMm = 10,
                        Type = SSL_FieldShapeType.HalfwayLine,
                    },
                ],
                FieldArcs =
                [
                    new TrackerGeometryCircularArc
                    {
                        Name = "CenterCircle",
                        RadiusMm = 600,
                        A1Rad = 0,
                        A2Rad = Math.PI,
                        ThicknessMm = 10,
                        Type = SSL_FieldShapeType.CenterCircle,
                    },
                ],
            },
            Metadata = new TrackerFrameMetadata
            {
                ProfileName = "fast",
            },
            KickedBall = new KickEventState
            {
                BallTrackId = 3,
                KickerRobotId = 2,
                KickKind = "straight",
                IsStillMoving = true,
            },
            LatestContact = new BallContactState
            {
                IsInContact = true,
                ContactingTeam = TrackerTeam.Yellow,
                ContactingRobotId = 2,
                LastTeam = TrackerTeam.Yellow,
                LastRobotId = 2,
                LastContactTimestampNs = 2_400_000_000,
            },
            BallLeftField = new BallLeftFieldState
            {
                IsOutOfField = true,
                BoundaryName = "TouchLineTop",
                CrossingTimestampNs = 2_450_000_000,
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

        // geometry: raw/tracked field 表示で必要な field shape が view state に残ることを確認する。
        Assert.NotNull(viewState.Geometry);
        Assert.Equal(12000, viewState.Geometry.Field.FieldLength);
        Assert.Equal(1800, viewState.Geometry.Field.GoalWidth);
        Assert.Equal(300, viewState.Geometry.Field.BoundaryWidth);
        Assert.Equal(1200, viewState.Geometry.Field.PenaltyAreaDepth);
        Assert.Equal(2400, viewState.Geometry.Field.PenaltyAreaWidth);
        Assert.Equal(600, viewState.Geometry.Field.CenterCircleRadius);
        var fieldLine = Assert.Single(viewState.Geometry.Field.FieldLines);
        Assert.Equal("HalfwayLine", fieldLine.Name);
        Assert.Equal(SSL_FieldShapeType.HalfwayLine, fieldLine.Type);
        var fieldArc = Assert.Single(viewState.Geometry.Field.FieldArcs);
        Assert.Equal("CenterCircle", fieldArc.Name);
        Assert.Equal(600, fieldArc.Radius);
        Assert.Equal(SSL_FieldShapeType.CenterCircle, fieldArc.Type);

        // diagnostics: frame timestamp と publish count が viewer diagnostics に反映されることを確認する。
        Assert.Equal(2, viewState.Diagnostics.BallCount);
        Assert.Equal(2, viewState.Diagnostics.RobotCount);
        Assert.Equal(2_500_000_000, viewState.Diagnostics.DataTimestampNs);
        Assert.Equal(2_501_000_000, viewState.Diagnostics.ProcessedAtNs);

        // event metadata: kick/contact/field-left の状態が diagnostics panel 用に保持されることを確認する。
        Assert.True(viewState.Kick.IsDetected);
        Assert.True(viewState.Kick.IsStillMoving);
        Assert.Equal((uint)2, viewState.Kick.KickerRobotId);
        Assert.Equal("straight", viewState.Kick.KickKind);
        Assert.True(viewState.Contact.IsInContact);
        Assert.Equal(TrackerTeam.Yellow, viewState.Contact.ContactingTeam);
        Assert.Equal((uint)2, viewState.Contact.ContactingRobotId);
        Assert.True(viewState.FieldState.IsOutOfField);
        Assert.Equal("TouchLineTop", viewState.FieldState.BoundaryName);
        Assert.Equal(11, viewState.PublishSuccessCount);
        Assert.Equal(1, viewState.PublishFailureCount);
    }

    /// <summary>
    /// 何を確認しているか: latest frame がない場合でも active profile と publish count を残し、viewer object は空になることを確認する。
    /// </summary>
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
        Assert.Equal(0, viewState.Diagnostics.BallCount);
        Assert.Equal(0, viewState.Diagnostics.RobotCount);
        Assert.False(viewState.Kick.IsDetected);
        Assert.False(viewState.Contact.IsInContact);
        Assert.False(viewState.FieldState.IsOutOfField);
        Assert.Equal(3, viewState.PublishSuccessCount);
        Assert.Equal(2, viewState.PublishFailureCount);
    }
}
