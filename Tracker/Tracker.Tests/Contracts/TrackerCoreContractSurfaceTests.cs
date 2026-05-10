using Tracker.Core;

namespace Tracker.Tests;

public class TrackerCoreContractSurfaceTests
{
    [Fact]
    public void TrackerFrame_ExposesTypedGeometryAndContactContracts()
    {
        var geometry = new TrackerGeometrySnapshot
        {
            FieldLengthMm = 12000,
            FieldWidthMm = 9000,
            GoalWidthMm = 1800,
            GoalDepthMm = 180,
            BoundaryWidthMm = 300,
            BoundaryWidthGoalLineMm = 350,
            PenaltyAreaDepthMm = 1000,
            PenaltyAreaWidthMm = 2000,
            CenterCircleRadiusMm = 500,
            LineThicknessMm = 10,
            FieldLines =
            [
                new TrackerGeometryLineSegment
                {
                    Name = "HalfwayLine",
                    P1YMm = -4500,
                    P2YMm = 4500,
                    Type = SSL_FieldShapeType.HalfwayLine,
                },
            ],
            FieldArcs =
            [
                new TrackerGeometryCircularArc
                {
                    Name = "CenterCircle",
                    RadiusMm = 500,
                    Type = SSL_FieldShapeType.CenterCircle,
                },
            ],
        };
        var contact = new BallContactState
        {
            IsInContact = true,
            ContactingRobotId = 7,
            ContactingTeam = TrackerTeam.Yellow,
            LastRobotId = 8,
            LastTeam = TrackerTeam.Blue,
            LastContactTimestampNs = 1_250_000_000,
        };

        var frame = new TrackerFrame
        {
            GeometrySnapshot = geometry,
            LatestContact = contact,
        };

        Assert.Equal(12000, frame.GeometrySnapshot.FieldLengthMm);
        Assert.Equal(1000, frame.GeometrySnapshot.PenaltyAreaDepthMm);
        Assert.Equal(2000, frame.GeometrySnapshot.PenaltyAreaWidthMm);
        Assert.Equal(500, frame.GeometrySnapshot.CenterCircleRadiusMm);
        Assert.Equal("HalfwayLine", Assert.Single(frame.GeometrySnapshot.FieldLines).Name);
        Assert.Equal("CenterCircle", Assert.Single(frame.GeometrySnapshot.FieldArcs).Name);
        Assert.True(frame.LatestContact.IsInContact);
        Assert.Equal((uint)7, frame.LatestContact.ContactingRobotId);
        Assert.Equal(TrackerTeam.Blue, frame.LatestContact.LastTeam);
    }

    [Fact]
    public void TrackerRuntimeOverrides_ExposesTypedSnapshotForProfileSwitchRequests()
    {
        var runtimeOverrides = new TrackerRuntimeOverrides
        {
            Publish = new TrackerPublishOverrides
            {
                MulticastAddress = "224.5.23.2",
                Port = 10010,
                SourceName = "tracker-source",
                Uuid = "tracker-uuid",
            },
            RobotTracker = new TrackerRobotTrackerOverrides
            {
                ProcessNoise = 1.5,
                MeasurementNoise = 0.8,
                OutputVisibilityThreshold = 0.2,
                Gate = 2.0,
                OutlierLimitMm = 300,
            },
            BallTracker = new TrackerBallTrackerOverrides
            {
                ProcessNoise = 1.7,
                MeasurementNoise = 0.6,
                OutputVisibilityThreshold = 0.4,
                Gate = 2.5,
                OutlierLimitMm = 450,
                TrackLifetimeNs = 2_000_000_000,
            },
            KickDetector = new TrackerKickDetectorOverrides
            {
                KickSpeedThresholdMmPerS = 2500,
                ChipHeightThresholdMm = 120,
                ContactMarginMm = 25,
            },
        };

        var request = new TrackerProfileSwitchRequest
        {
            RequestVersion = 3,
            ProfileName = "simulation",
            RuntimeOverrides = runtimeOverrides,
        };

        Assert.Equal("224.5.23.2", request.RuntimeOverrides.Publish.MulticastAddress);
        Assert.Equal(10010, request.RuntimeOverrides.Publish.Port);
        Assert.Equal(0.2, request.RuntimeOverrides.RobotTracker.OutputVisibilityThreshold);
        Assert.Equal(300, request.RuntimeOverrides.RobotTracker.OutlierLimitMm);
        Assert.Equal(0.4, request.RuntimeOverrides.BallTracker.OutputVisibilityThreshold);
        Assert.Equal(2_000_000_000, request.RuntimeOverrides.BallTracker.TrackLifetimeNs);
        Assert.Equal(25, request.RuntimeOverrides.KickDetector.ContactMarginMm);
    }

    [Fact]
    public void TrackerUpdateResult_AndObserverContracts_CanReferenceTypedContactAndFrameEvents()
    {
        var frame = new TrackerFrame
        {
            FrameNumber = 9,
            GeometrySnapshot = new TrackerGeometrySnapshot
            {
                FieldLengthMm = 12000,
                FieldWidthMm = 9000,
                PenaltyAreaDepthMm = 1000,
                PenaltyAreaWidthMm = 2000,
                CenterCircleRadiusMm = 500,
            },
            LatestContact = new BallContactState
            {
                IsInContact = false,
                LastRobotId = 3,
                LastTeam = TrackerTeam.Yellow,
                LastContactTimestampNs = 900_000_000,
            },
        };
        var result = new TrackerUpdateResult
        {
            CommittedFrames = [frame],
            EmittedEvents =
            [
                new TrackerEvent { Kind = TrackerEventKind.GeometryReset },
                new TrackerEvent { Kind = TrackerEventKind.ContactChanged, FrameNumber = frame.FrameNumber },
                new TrackerEvent { Kind = TrackerEventKind.WorldFrameCommitted, FrameNumber = frame.FrameNumber },
            ],
        };
        var observer = new RecordingObserver();

        observer.OnGeometryReset();
        observer.OnContactChanged(frame);
        observer.OnWorldFrameCommitted(frame);

        Assert.Equal(frame.FrameNumber, result.EmittedEvents[1].FrameNumber);
        Assert.True(observer.GeometryResetObserved);
        Assert.Same(frame, observer.LastContactFrame);
        Assert.Same(frame, observer.LastCommittedFrame);
    }

    private sealed class RecordingObserver : ITrackerObserver
    {
        public bool GeometryResetObserved { get; private set; }

        public TrackerFrame? LastCommittedFrame { get; private set; }

        public TrackerFrame? LastContactFrame { get; private set; }

        public void OnProfileSwitched(string profileName)
        {
        }

        public void OnGeometryReset()
        {
            GeometryResetObserved = true;
        }

        public void OnWorldFrameCommitted(TrackerFrame frame)
        {
            LastCommittedFrame = frame;
        }

        public void OnKickDetected(KickEventState kick, TrackerFrame frame)
        {
        }

        public void OnContactChanged(TrackerFrame frame)
        {
            LastContactFrame = frame;
        }

        public void OnBallLeftField(BallLeftFieldState state, TrackerFrame frame)
        {
        }
    }
}
