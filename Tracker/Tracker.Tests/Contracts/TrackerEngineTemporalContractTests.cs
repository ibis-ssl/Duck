using Tracker.Core;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public class TrackerEngineTemporalContractTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public TrackerEngineTemporalContractTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Update_FlushesBufferedDetectionsInEventTimeOrder_WhenArrivalOrderDiffers()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 100_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 2,
                balls: [TrackerContractTestData.CreateBall(x: 200)],
                captureTimeSeconds: 2.0),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 300)],
                captureTimeSeconds: 3.0),
            settings: settings);

        var secondCommittedFrame = Assert.Single(secondResult.CommittedFrames);
        Assert.Equal(1_000_000_000L, secondCommittedFrame.DataTimestampNs);
        Assert.Equal(
            [2_000_000_000L],
            flushResult.CommittedFrames.Select(frame => frame.DataTimestampNs));
    }

    [Fact]
    public void Update_SplitsFrames_WhenObservationsExceedMergeWindow()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 10_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 2,
                balls: [TrackerContractTestData.CreateBall(x: 200)],
                captureTimeSeconds: 1.025),
            settings: settings);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 12,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 300)],
                captureTimeSeconds: 2.000),
            settings: settings);

        Assert.Equal(2, flushResult.CommittedFrames.Count);
        Assert.Equal(
            [1_000_000_000L, 1_025_000_000L],
            flushResult.CommittedFrames.Select(frame => frame.DataTimestampNs));
    }

    [Fact]
    public void Update_CanReturnZeroFramesWhileBuffering_AndMultipleFramesWhenSeveralGroupsFlush()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 1_500_000_000, mergeWindowNs: 20_000_000);

        var firstResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 200)],
                captureTimeSeconds: 2.0),
            settings: settings);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 300)],
                captureTimeSeconds: 4.0),
            settings: settings);

        Assert.Empty(firstResult.CommittedFrames);
        Assert.Empty(secondResult.CommittedFrames);
        Assert.Collection(
            flushResult.CommittedFrames,
            frame => Assert.Equal(1_000_000_000L, frame.DataTimestampNs),
            frame => Assert.Equal(2_000_000_000L, frame.DataTimestampNs));
    }

    [Fact]
    public void Update_DropsLatePacketsAndDoesNotLetThemContaminateLaterFlushes()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 200)],
                captureTimeSeconds: 2.0),
            settings: settings);

        var lateResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 150)],
                captureTimeSeconds: 1.0),
            settings: settings);

        Assert.Empty(lateResult.CommittedFrames);
        Assert.Equal(1, lateResult.Diagnostics.LatePacketDropCount);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 300)],
                captureTimeSeconds: 3.0),
            settings: settings);

        Assert.Collection(
            flushResult.CommittedFrames,
            frame => Assert.Equal(2_000_000_000L, frame.DataTimestampNs));
    }

    [Fact]
    public void Update_EmitsWorldFrameCommittedForEachCommittedFrameInFlushOrder()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 1_500_000_000, mergeWindowNs: 10_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 200)],
                captureTimeSeconds: 2.000),
            settings: settings);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 300)],
                captureTimeSeconds: 4.000),
            settings: settings);

        Assert.Collection(
            result.EmittedEvents,
            emitted =>
            {
                Assert.Equal(TrackerEventKind.WorldFrameCommitted, emitted.Kind);
                Assert.Equal(result.CommittedFrames[0].FrameNumber, emitted.FrameNumber);
            },
            emitted =>
            {
                Assert.Equal(TrackerEventKind.WorldFrameCommitted, emitted.Kind);
                Assert.Equal(result.CommittedFrames[1].FrameNumber, emitted.FrameNumber);
            });
    }

    [Fact]
    public void Update_UsesSentTimeWhenCaptureTimeIsMissing()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 0.0,
                sentTimeSeconds: 1.25),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);
        Assert.Equal(1_250_000_000L, committedFrame.DataTimestampNs);
    }

    [Fact]
    public void Update_DropsLatePacketsThatFallInsideAnAlreadyCommittedMergeWindow()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 2,
                balls: [TrackerContractTestData.CreateBall(x: 110)],
                captureTimeSeconds: 1.015),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 200)],
                captureTimeSeconds: 2.000),
            settings: settings);

        var lateResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 12,
                cameraId: 3,
                balls: [TrackerContractTestData.CreateBall(x: 105)],
                captureTimeSeconds: 1.019),
            settings: settings);

        Assert.Empty(lateResult.CommittedFrames);
        Assert.Equal(1, lateResult.Diagnostics.LatePacketDropCount);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 300)],
                captureTimeSeconds: 3.000),
            settings: settings);

        Assert.Collection(
            flushResult.CommittedFrames,
            frame => Assert.Equal(2_000_000_000L, frame.DataTimestampNs));
    }

    [Fact]
    public void Update_WaitsForTheOldestGroupMergeWindowToCloseBeforeFlushingIt()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 2,
                balls: [TrackerContractTestData.CreateBall(x: 110)],
                captureTimeSeconds: 1.015),
            settings: settings);

        var thirdResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 12,
                cameraId: 3,
                balls: [TrackerContractTestData.CreateBall(x: 120)],
                captureTimeSeconds: 1.060),
            settings: settings);

        Assert.Empty(thirdResult.CommittedFrames);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 200)],
                captureTimeSeconds: 2.000),
            settings: settings);

        Assert.Collection(
            flushResult.CommittedFrames,
            frame =>
            {
                Assert.Equal(1_000_000_000L, frame.DataTimestampNs);
                var mergedBall = Assert.Single(frame.Balls);
                Assert.Equal(105, mergedBall.XMm, precision: 3);
                Assert.Equal([1u, 2u], mergedBall.SourceCameraIds.OrderBy(id => id));
            },
            frame => Assert.Equal(1_060_000_000L, frame.DataTimestampNs));
    }

    [Fact]
    public void Update_PopulatesProcessedAtNsFromLocalProcessingTime()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);
        var beforeNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var afterNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
        var committedFrame = Assert.Single(result.CommittedFrames);

        Assert.InRange(committedFrame.ProcessedAtNs, beforeNs, afterNs + 1_000_000L);
    }

    [Fact]
    public void Update_PreservesGoalLineBoundaryAndLineThicknessInGeometrySnapshot()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateGeometryPacket(
                fieldLength: 12000,
                fieldWidth: 9000,
                boundaryWidth: 300,
                boundaryWidthGoalLine: 350,
                lineThickness: 12),
            settings: settings);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);
        Assert.NotNull(committedFrame.GeometrySnapshot);
        Assert.Equal(350, committedFrame.GeometrySnapshot!.BoundaryWidthGoalLineMm);
        Assert.Equal(12, committedFrame.GeometrySnapshot.LineThicknessMm);
    }

    [Fact]
    public void Update_EmitsGeometryResetAndDropsPendingFramesFromOldGeometryGeneration()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(
            reorderWindowNs: 100_000_000,
            mergeWindowNs: 20_000_000,
            geometryResetFieldLengthThresholdMm: 100,
            geometryResetFieldWidthThresholdMm: 100);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateGeometryPacket(fieldLength: 9000, fieldWidth: 6000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings);

        var resetResult = engine.Update(
            packet: new SSL_WrapperPacket
            {
                Geometry = TrackerContractTestData.CreateGeometryPacket(fieldLength: 9400, fieldWidth: 6400).Geometry,
                Detection = TrackerContractTestData.CreateDetectionPacket(
                    frameNumber: 20,
                    cameraId: 1,
                    balls: [TrackerContractTestData.CreateBall(x: 200)],
                    captureTimeSeconds: 2.0).Detection,
            },
            settings: settings);

        Assert.Contains(resetResult.EmittedEvents, emitted => emitted.Kind == TrackerEventKind.GeometryReset);
        Assert.Single(resetResult.CommittedFrames);
        Assert.Equal(2_000_000_000L, resetResult.CommittedFrames[0].DataTimestampNs);
        Assert.Equal(
            [TrackerEventKind.GeometryReset, TrackerEventKind.WorldFrameCommitted],
            resetResult.EmittedEvents.Select(emitted => emitted.Kind).Take(2));
    }

    [Fact]
    public void Update_EmitsGeometryResetWhenGoalGeometryChanges()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(
            reorderWindowNs: 100_000_000,
            mergeWindowNs: 20_000_000,
            geometryResetFieldLengthThresholdMm: 500,
            geometryResetFieldWidthThresholdMm: 500);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateGeometryPacket(
                fieldLength: 9000,
                fieldWidth: 6000,
                goalWidth: 1800,
                goalDepth: 180),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings);

        var resetResult = engine.Update(
            packet: new SSL_WrapperPacket
            {
                Geometry = TrackerContractTestData.CreateGeometryPacket(
                    fieldLength: 9000,
                    fieldWidth: 6000,
                    goalWidth: 2000,
                    goalDepth: 240).Geometry,
                Detection = TrackerContractTestData.CreateDetectionPacket(
                    frameNumber: 20,
                    cameraId: 1,
                    balls: [TrackerContractTestData.CreateBall(x: 200)],
                    captureTimeSeconds: 2.0).Detection,
            },
            settings: settings);

        Assert.Contains(resetResult.EmittedEvents, emitted => emitted.Kind == TrackerEventKind.GeometryReset);
        Assert.Single(resetResult.CommittedFrames);
        Assert.Equal(2_000_000_000L, resetResult.CommittedFrames[0].DataTimestampNs);
    }

    [Fact]
    public void Update_WithControlOnlyProfileSwitch_EmitsOnlyProfileSwitched()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(profileName: "default");
        var switchedSettings = fixture.CreateSettings(profileName: "fast");

        var result = engine.Update(
            packet: null,
            settings: settings,
            profileSwitchRequest: fixture.CreateProfileSwitchRequest(
                requestVersion: 2,
                profileName: "fast",
                resolvedBaseSettings: switchedSettings));

        Assert.Empty(result.CommittedFrames);
        Assert.Equal([TrackerEventKind.ProfileSwitched], result.EmittedEvents.Select(emitted => emitted.Kind));
        Assert.Equal("fast", result.EmittedEvents[0].ProfileName);
    }

    [Fact]
    public void Update_OrdersProfileSwitchBeforeWorldFrameCommitted_WhenSwitchAndFrameShareAResult()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0);
        var switchedSettings = fixture.CreateSettings(profileName: "fast", reorderWindowNs: 0, mergeWindowNs: 0);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings,
            profileSwitchRequest: fixture.CreateProfileSwitchRequest(
                requestVersion: 3,
                profileName: "fast",
                resolvedBaseSettings: switchedSettings));

        Assert.Equal(
            [TrackerEventKind.ProfileSwitched, TrackerEventKind.WorldFrameCommitted],
            result.EmittedEvents.Select(emitted => emitted.Kind).Take(2));
        Assert.Equal("fast", Assert.Single(result.CommittedFrames).Metadata.ProfileName);
    }

    [Fact]
    public void Update_PreservesFrameNumberContinuityAcrossProfileSwitch()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0);
        var switchedSettings = fixture.CreateSettings(profileName: "fast", reorderWindowNs: 0, mergeWindowNs: 0);

        var firstResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings);

        var switchResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 200)],
                captureTimeSeconds: 2.0),
            settings: settings,
            profileSwitchRequest: fixture.CreateProfileSwitchRequest(
                requestVersion: 3,
                profileName: "fast",
                resolvedBaseSettings: switchedSettings));

        var firstCommittedFrame = Assert.Single(firstResult.CommittedFrames);
        var switchedCommittedFrame = Assert.Single(switchResult.CommittedFrames);

        Assert.Equal(firstCommittedFrame.FrameNumber + 1, switchedCommittedFrame.FrameNumber);
        Assert.Equal(
            [TrackerEventKind.ProfileSwitched, TrackerEventKind.WorldFrameCommitted],
            switchResult.EmittedEvents.Select(emitted => emitted.Kind).Take(2));
        Assert.Equal("fast", switchedCommittedFrame.Metadata.ProfileName);
    }

    [Fact]
    public void Update_ProfileSwitchClearsPendingBufferedDetectionsFromOldProfile()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(profileName: "default", reorderWindowNs: 1_500_000_000, mergeWindowNs: 20_000_000);
        var switchedSettings = fixture.CreateSettings(profileName: "fast", reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings);

        var switchResult = engine.Update(
            packet: null,
            settings: settings,
            profileSwitchRequest: fixture.CreateProfileSwitchRequest(
                requestVersion: 4,
                profileName: "fast",
                resolvedBaseSettings: switchedSettings));

        Assert.Empty(switchResult.CommittedFrames);
        Assert.Equal([TrackerEventKind.ProfileSwitched], switchResult.EmittedEvents.Select(emitted => emitted.Kind));

        var frameResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 200)],
                captureTimeSeconds: 2.0),
            settings: switchedSettings);

        var committedFrame = Assert.Single(frameResult.CommittedFrames);
        Assert.Equal(2_000_000_000L, committedFrame.DataTimestampNs);
        Assert.Equal("fast", committedFrame.Metadata.ProfileName);
    }

    [Fact]
    public void Update_MergesSameRobotAcrossCamerasIntoSingleTrackedRobot()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 100, y: 200, orientation: 0.2f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 2,
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 140, y: 240, orientation: 0.4f)],
                captureTimeSeconds: 1.010),
            settings: settings);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 8, x: 400, y: 500, orientation: 1.0f)],
                captureTimeSeconds: 2.000),
            settings: settings);

        var mergedFrame = flushResult.CommittedFrames[0];
        var mergedRobot = Assert.Single(mergedFrame.Robots);

        Assert.Equal(TrackerTeam.Yellow, mergedRobot.Team);
        Assert.Equal((uint)4, mergedRobot.RobotId);
        Assert.Equal(120, mergedRobot.XMm, precision: 3);
        Assert.Equal(220, mergedRobot.YMm, precision: 3);
        Assert.Equal(0.3, mergedRobot.OrientationRad, precision: 3);
    }

    [Fact]
    public void Update_TracksRobotVelocityAndUnwrappedAngularVelocityAcrossFrames()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 100, y: 200, orientation: 3.10f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 130, y: 240, orientation: -3.08f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var trackedRobot = Assert.Single(Assert.Single(secondResult.CommittedFrames).Robots);

        Assert.Equal(300, trackedRobot.VXMmPerS, precision: 3);
        Assert.Equal(400, trackedRobot.VYMmPerS, precision: 3);
        Assert.InRange(trackedRobot.AngularVelocityRadPerS, 0.9, 1.2);
    }

    [Fact]
    public void Update_UsesConfiguredRobotOutlierLimitWhenDerivingVelocity()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            robotTracker: new TrackerRobotTrackerOverrides
            {
                OutlierLimitMm = 50d,
            });

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 100, y: 200, orientation: 0.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 220, y: 200, orientation: 0.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var trackedRobot = Assert.Single(Assert.Single(secondResult.CommittedFrames).Robots);

        Assert.Equal(220, trackedRobot.XMm, precision: 3);
        Assert.Equal(0, trackedRobot.VXMmPerS, precision: 3);
        Assert.Equal(0, trackedRobot.VYMmPerS, precision: 3);
        Assert.Equal(0, trackedRobot.AngularVelocityRadPerS, precision: 3);
    }

    [Fact]
    public void Update_KeepsRobotTrackAliveAcrossOneMissingFrameWithDecayedVisibility()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        var firstResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 100, y: 200, orientation: 0.5f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 300)],
                captureTimeSeconds: 1.200),
            settings: settings);

        var firstRobot = Assert.Single(Assert.Single(firstResult.CommittedFrames).Robots);
        var predictedRobot = Assert.Single(Assert.Single(secondResult.CommittedFrames).Robots);

        Assert.Equal((uint)2, predictedRobot.RobotId);
        Assert.Equal(firstRobot.XMm, predictedRobot.XMm, precision: 3);
        Assert.Equal(firstRobot.YMm, predictedRobot.YMm, precision: 3);
        Assert.True(predictedRobot.Visibility < firstRobot.Visibility);
        Assert.True(predictedRobot.Visibility > 0);
    }

    [Fact]
    public void Update_DoesNotMergeStaleCameraPredictionWhenAnotherCameraHasFreshRobotObservation()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 100, y: 200, orientation: 0.2f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 2,
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 140, y: 240, orientation: 0.4f)],
                captureTimeSeconds: 1.010),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 8, x: 400, y: 500, orientation: 1.0f)],
                captureTimeSeconds: 2.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 130, y: 230, orientation: 0.3f)],
                captureTimeSeconds: 3.000),
            settings: settings);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 40,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 8, x: 410, y: 510, orientation: 1.1f)],
                captureTimeSeconds: 4.000),
            settings: settings);

        var trackedRobot = Assert.Single(
            Assert.Single(flushResult.CommittedFrames).Robots,
            robot => robot.Team == TrackerTeam.Yellow && robot.RobotId == 4);
        Assert.Equal(130, trackedRobot.XMm, precision: 3);
        Assert.Equal(230, trackedRobot.YMm, precision: 3);
        Assert.Equal(0.3, trackedRobot.OrientationRad, precision: 3);
    }

    [Fact]
    public void Update_MergesSameBallAcrossCamerasIntoSingleTrackedBall()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 200, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 2,
                balls: [TrackerContractTestData.CreateBall(x: 140, y: 240, confidence: 1.0f)],
                captureTimeSeconds: 1.010),
            settings: settings);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 400, y: 500, confidence: 1.0f)],
                captureTimeSeconds: 2.000),
            settings: settings);

        var mergedBall = Assert.Single(flushResult.CommittedFrames[0].Balls);
        Assert.Equal(120, mergedBall.XMm, precision: 3);
        Assert.Equal(220, mergedBall.YMm, precision: 3);
        Assert.Equal([1u, 2u], mergedBall.SourceCameraIds.OrderBy(id => id));
    }

    [Fact]
    public void Update_SelectsPrimaryBallByVisibilityAndStableSortsSecondaryBalls()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls:
                [
                    TrackerContractTestData.CreateBall(x: 100, y: 100, confidence: 0.60f),
                    TrackerContractTestData.CreateBall(x: 300, y: 300, confidence: 0.95f),
                    TrackerContractTestData.CreateBall(x: 200, y: 200, confidence: 0.80f),
                ],
                captureTimeSeconds: 1.000),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);

        Assert.Equal(committedFrame.Balls[0].InternalTrackId, committedFrame.PrimaryBallTrackId);
        Assert.Equal(300, committedFrame.Balls[0].XMm, precision: 3);
        Assert.Equal(200, committedFrame.Balls[1].XMm, precision: 3);
        Assert.Equal(100, committedFrame.Balls[2].XMm, precision: 3);
    }

    [Fact]
    public void Update_TracksBallVelocityAcrossFrames()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 200, z: 10, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 130, y: 240, z: 40, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var trackedBall = Assert.Single(Assert.Single(secondResult.CommittedFrames).Balls);

        Assert.Equal(300, trackedBall.VXMmPerS, precision: 3);
        Assert.Equal(400, trackedBall.VYMmPerS, precision: 3);
        Assert.Equal(300, trackedBall.VZMmPerS, precision: 3);
    }

    [Fact]
    public void Update_KeepsBallTrackAliveAcrossOneMissingFrameWithDecayedVisibility()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        var firstResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 200, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 300, y: 400, orientation: 0.5f)],
                captureTimeSeconds: 1.200),
            settings: settings);

        var firstBall = Assert.Single(Assert.Single(firstResult.CommittedFrames).Balls);
        var predictedBall = Assert.Single(Assert.Single(secondResult.CommittedFrames).Balls);

        Assert.Equal(firstBall.XMm, predictedBall.XMm, precision: 3);
        Assert.Equal(firstBall.YMm, predictedBall.YMm, precision: 3);
        Assert.True(predictedBall.Visibility < firstBall.Visibility);
        Assert.True(predictedBall.Visibility > 0);
    }

    [Fact]
    public void Update_UsesConfiguredBallTrackLifetimeToExpirePredictedTracks()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            ballTracker: new TrackerBallTrackerOverrides
            {
                TrackLifetimeNs = 100_000_000,
            });

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 200, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 300, y: 400, orientation: 0.5f)],
                captureTimeSeconds: 1.200),
            settings: settings);

        Assert.Empty(Assert.Single(secondResult.CommittedFrames).Balls);
    }

    [Fact]
    public void Update_UsesConfiguredBallVisibilityHalfLifeWhenPredictingTrack()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            ballTracker: new TrackerBallTrackerOverrides
            {
                VisibilityHalfLifeSeconds = 0.1d,
            });

        var firstResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 200, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 300, y: 400, orientation: 0.5f)],
                captureTimeSeconds: 1.200),
            settings: settings);

        var firstBall = Assert.Single(Assert.Single(firstResult.CommittedFrames).Balls);
        var predictedBall = Assert.Single(Assert.Single(secondResult.CommittedFrames).Balls);

        Assert.Equal(firstBall.Visibility * 0.25f, predictedBall.Visibility, precision: 3);
    }

    [Fact]
    public void Update_UsesConfiguredBallGateForTrackMatchingAcrossFrames()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            ballTracker: new TrackerBallTrackerOverrides
            {
                Gate = 1.5d,
            });

        var firstResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 150, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var firstBall = Assert.Single(Assert.Single(firstResult.CommittedFrames).Balls);
        var secondBall = Assert.Single(Assert.Single(secondResult.CommittedFrames).Balls);

        Assert.Equal(firstBall.InternalTrackId, secondBall.InternalTrackId);
    }

    [Fact]
    public void Update_PreservesBallTrackIdentityWhenVisibleCameraChanges()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 200, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 2,
                balls: [TrackerContractTestData.CreateBall(x: 102, y: 202, confidence: 1.0f)],
                captureTimeSeconds: 1.010),
            settings: settings);

        var firstResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 2,
                balls: [TrackerContractTestData.CreateBall(x: 105, y: 205, confidence: 1.0f)],
                captureTimeSeconds: 2.000),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 2,
                balls: [TrackerContractTestData.CreateBall(x: 110, y: 210, confidence: 1.0f)],
                captureTimeSeconds: 3.000),
            settings: settings);

        var firstBall = Assert.Single(Assert.Single(firstResult.CommittedFrames).Balls);
        var secondBall = Assert.Single(Assert.Single(secondResult.CommittedFrames).Balls);

        Assert.Equal(firstBall.InternalTrackId, secondBall.InternalTrackId);
    }

    [Fact]
    public void Update_DoesNotLetStaleBallTrackBridgeTwoFreshBalls()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 2,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.010),
            settings: settings);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 2,
                balls:
                [
                    TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f),
                    TrackerContractTestData.CreateBall(x: 200, y: 0, confidence: 1.0f),
                ],
                captureTimeSeconds: 2.000),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);
        Assert.Equal(2, committedFrame.Balls.Count);
        Assert.Equal([0d, 200d], committedFrame.Balls.Select(ball => ball.XMm).OrderBy(x => x));
    }

    [Fact]
    public void Update_MergesBallsUsingUncertaintyWeightedPositions()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 2,
                balls: [TrackerContractTestData.CreateBall(x: 160, y: 0, confidence: 0.25f)],
                captureTimeSeconds: 1.010),
            settings: settings);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 400, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 2.000),
            settings: settings);

        var mergedBall = Assert.Single(flushResult.CommittedFrames[0].Balls);
        Assert.Equal(112, mergedBall.XMm, precision: 3);
    }

    [Fact]
    public void Update_ReusesSameCameraBallTrackAcrossSequentialDetectionsInOneCommittedGroup()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 110, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.010),
            settings: settings);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 300, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 2.000),
            settings: settings);

        var committedBall = Assert.Single(flushResult.CommittedFrames[0].Balls);
        Assert.Equal(110, committedBall.XMm, precision: 3);
        Assert.Equal(committedBall.InternalTrackId, flushResult.CommittedFrames[0].PrimaryBallTrackId);
    }

    [Fact]
    public void Update_KeepsNearbyDistinctBallsFromSameCameraSeparated()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls:
                [
                    TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f),
                    TrackerContractTestData.CreateBall(x: 80, y: 80, confidence: 0.9f),
                ],
                captureTimeSeconds: 1.000),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);
        Assert.Equal(2, committedFrame.Balls.Count);
        Assert.Equal([0d, 80d], committedFrame.Balls.Select(ball => ball.XMm).OrderBy(x => x));
    }

    [Fact]
    public void Update_PreservesMergedBallIdentityAcrossLargeCommittedFrameJumpWhenIntermediateDetectionsSustainVelocity()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var secondFrameResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 180, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.180),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 31,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 300, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.190),
            settings: settings);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 40,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 500, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 2.000),
            settings: settings);

        var secondFrameBall = Assert.Single(Assert.Single(secondFrameResult.CommittedFrames).Balls);
        var thirdFrameBall = Assert.Single(Assert.Single(flushResult.CommittedFrames).Balls);

        Assert.Equal(100, secondFrameBall.XMm, precision: 3);
        Assert.Equal(300, thirdFrameBall.XMm, precision: 3);
        Assert.Equal(secondFrameBall.InternalTrackId, thirdFrameBall.InternalTrackId);
    }

    [Fact]
    public void Update_MergesThreeCameraBallChainIntoSingleCluster()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 2,
                balls: [TrackerContractTestData.CreateBall(x: 200, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.010),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 12,
                cameraId: 3,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.015),
            settings: settings);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 400, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 2.000),
            settings: settings);

        var mergedBall = Assert.Single(flushResult.CommittedFrames[0].Balls);
        Assert.Equal(100, mergedBall.XMm, precision: 3);
        Assert.Equal([1u, 2u, 3u], mergedBall.SourceCameraIds.OrderBy(id => id));
    }

    [Fact]
    public void Update_PopulatesCurrentBallContactAndMarksContactingRobot()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f)],
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 80, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);
        var trackedRobot = Assert.Single(committedFrame.Robots);
        var contact = Assert.IsType<BallContactState>(committedFrame.LatestContact);

        Assert.True(contact.IsInContact);
        Assert.Equal((uint)4, contact.ContactingRobotId);
        Assert.Equal(TrackerTeam.Yellow, contact.ContactingTeam);
        Assert.Equal((uint)4, contact.LastRobotId);
        Assert.True(trackedRobot.HasBallContact);
        Assert.Equal(
            [TrackerEventKind.WorldFrameCommitted, TrackerEventKind.ContactChanged],
            result.EmittedEvents.Select(emitted => emitted.Kind));
    }

    [Fact]
    public void Update_UsesConfiguredContactMarginForBallContactDetection()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            kickDetector: new TrackerKickDetectorOverrides
            {
                ContactMarginMm = 0d,
            });

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f)],
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 130, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);
        var trackedRobot = Assert.Single(committedFrame.Robots);

        Assert.Null(committedFrame.LatestContact);
        Assert.False(trackedRobot.HasBallContact);
        Assert.Equal([TrackerEventKind.WorldFrameCommitted], result.EmittedEvents.Select(emitted => emitted.Kind));
    }

    [Fact]
    public void Update_PreservesLastToucherAfterBallContactEnds()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f)],
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 80, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 30, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var committedFrame = Assert.Single(secondResult.CommittedFrames);
        var contact = Assert.IsType<BallContactState>(committedFrame.LatestContact);

        Assert.False(contact.IsInContact);
        Assert.Null(contact.ContactingRobotId);
        Assert.Equal((uint)4, contact.LastRobotId);
        Assert.Equal(TrackerTeam.Yellow, contact.LastTeam);
        Assert.Equal(
            [TrackerEventKind.WorldFrameCommitted, TrackerEventKind.ContactChanged],
            secondResult.EmittedEvents.Select(emitted => emitted.Kind));
    }

    [Fact]
    public void Update_DetectsKickFromRecentContactAndPublishesKickBeforeContactChange()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f)],
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 80, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var kickResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 110, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var committedFrame = Assert.Single(kickResult.CommittedFrames);
        var kick = Assert.IsType<KickEventState>(committedFrame.KickedBall);
        var contact = Assert.IsType<BallContactState>(committedFrame.LatestContact);

        Assert.Equal((uint)4, kick.KickerRobotId);
        Assert.Equal("flat", kick.KickKind);
        Assert.True(kick.IsStillMoving);
        Assert.Equal(committedFrame.PrimaryBallTrackId, kick.BallTrackId);
        Assert.False(contact.IsInContact);
        Assert.Equal((uint)4, contact.LastRobotId);
        Assert.Equal(
            [TrackerEventKind.WorldFrameCommitted, TrackerEventKind.KickDetected, TrackerEventKind.ContactChanged],
            kickResult.EmittedEvents.Select(emitted => emitted.Kind));
    }

    [Fact]
    public void Update_UsesConfiguredKickSpeedThresholdForKickDetection()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            kickDetector: new TrackerKickDetectorOverrides
            {
                KickSpeedThresholdMmPerS = 1200d,
            });

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f)],
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 80, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var kickResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 110, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var committedFrame = Assert.Single(kickResult.CommittedFrames);
        var contact = Assert.IsType<BallContactState>(committedFrame.LatestContact);

        Assert.Null(committedFrame.KickedBall);
        Assert.False(contact.IsInContact);
        Assert.Equal((uint)4, contact.LastRobotId);
        Assert.Equal(
            [TrackerEventKind.WorldFrameCommitted, TrackerEventKind.ContactChanged],
            kickResult.EmittedEvents.Select(emitted => emitted.Kind));
    }

    [Fact]
    public void Update_DoesNotCarryLastToucherToDifferentPrimaryBallTrack()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls:
                [
                    TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f),
                    TrackerContractTestData.CreateBall(x: 400, y: 0, confidence: 1.0f),
                ],
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 80, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var switchedPrimaryResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 500, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var committedFrame = Assert.Single(switchedPrimaryResult.CommittedFrames);

        Assert.NotEqual(1, committedFrame.PrimaryBallTrackId);
        Assert.Null(committedFrame.LatestContact);
        Assert.Null(committedFrame.KickedBall);
        Assert.Equal([TrackerEventKind.WorldFrameCommitted], switchedPrimaryResult.EmittedEvents.Select(emitted => emitted.Kind));
    }

    [Fact]
    public void Update_DetectsFlatKickWhenVerticalVelocityNoiseIsBelowChipThreshold()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, z: 0, confidence: 1.0f)],
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 80, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var kickResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 110, y: 0, z: 1, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var committedFrame = Assert.Single(kickResult.CommittedFrames);
        var kick = Assert.IsType<KickEventState>(committedFrame.KickedBall);

        Assert.Equal("flat", kick.KickKind);
        Assert.Equal((uint)4, kick.KickerRobotId);
    }

    [Fact]
    public void Update_UsesConfiguredChipHeightThresholdForChipClassification()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            kickDetector: new TrackerKickDetectorOverrides
            {
                ChipHeightThresholdMm = 60d,
            });

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, z: 0, confidence: 1.0f)],
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 80, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var kickResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 110, y: 0, z: 80, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var kick = Assert.IsType<KickEventState>(Assert.Single(kickResult.CommittedFrames).KickedBall);

        Assert.Equal("chip", kick.KickKind);
    }

    [Fact]
    public void Update_EmitsBallLeftFieldWhenPrimaryBallLeavesThroughTouchLine()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateGeometryPacket(
                fieldLength: 12000,
                fieldWidth: 9000,
                goalWidth: 1800,
                goalDepth: 180),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 4450, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 120, y: 4550, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);
        var leftField = Assert.IsType<BallLeftFieldState>(committedFrame.BallLeftField);

        Assert.True(leftField.IsOutOfField);
        Assert.Equal("touch-line", leftField.BoundaryName);
        Assert.Equal(4500, leftField.CrossingYMm, precision: 3);
        Assert.Equal(
            [TrackerEventKind.WorldFrameCommitted, TrackerEventKind.BallLeftField],
            result.EmittedEvents.Select(emitted => emitted.Kind));
    }

    [Fact]
    public void Update_ClassifiesGoalMouthExitAsGoalInterior()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateGeometryPacket(
                fieldLength: 12000,
                fieldWidth: 9000,
                goalWidth: 1800,
                goalDepth: 180),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 5950, y: 500, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 6050, y: 500, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);
        var leftField = Assert.IsType<BallLeftFieldState>(committedFrame.BallLeftField);

        Assert.True(leftField.IsOutOfField);
        Assert.Equal("goal-interior", leftField.BoundaryName);
        Assert.Equal(6000, leftField.CrossingXMm, precision: 3);
        Assert.Equal(
            [TrackerEventKind.WorldFrameCommitted, TrackerEventKind.BallLeftField],
            result.EmittedEvents.Select(emitted => emitted.Kind));
    }

    [Fact]
    public void Update_ClassifiesNonGoalMouthExitAsGoalLine()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateGeometryPacket(
                fieldLength: 12000,
                fieldWidth: 9000,
                goalWidth: 1800,
                goalDepth: 180),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 5950, y: 1400, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 6050, y: 1400, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);
        var leftField = Assert.IsType<BallLeftFieldState>(committedFrame.BallLeftField);

        Assert.True(leftField.IsOutOfField);
        Assert.Equal("goal-line", leftField.BoundaryName);
        Assert.Equal(6000, leftField.CrossingXMm, precision: 3);
    }

    [Fact]
    public void Update_ClassifiesCornerExitByFirstPerimeterCrossing()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateGeometryPacket(
                fieldLength: 12000,
                fieldWidth: 9000,
                goalWidth: 1800,
                goalDepth: 180),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 5980, y: 4460, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 6060, y: 4510, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);
        var leftField = Assert.IsType<BallLeftFieldState>(committedFrame.BallLeftField);

        Assert.True(leftField.IsOutOfField);
        Assert.Equal("goal-line", leftField.BoundaryName);
        Assert.Equal(6000, leftField.CrossingXMm, precision: 3);
        Assert.Equal(4472.5, leftField.CrossingYMm, precision: 3);
        Assert.Equal(
            [TrackerEventKind.WorldFrameCommitted, TrackerEventKind.BallLeftField],
            result.EmittedEvents.Select(emitted => emitted.Kind));
    }
}
