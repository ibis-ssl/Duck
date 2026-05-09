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

        Assert.Empty(secondResult.CommittedFrames);
        Assert.Equal(
            [1_000_000_000L, 2_000_000_000L],
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
        var settings = fixture.CreateSettings(reorderWindowNs: 100_000_000, mergeWindowNs: 20_000_000);

        var firstResult = engine.Update(
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

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 300)],
                captureTimeSeconds: 3.0),
            settings: settings);

        Assert.Empty(firstResult.CommittedFrames);
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
    public void Update_WithControlOnlyProfileSwitch_EmitsOnlyProfileSwitched()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(profileName: "default");

        var result = engine.Update(
            packet: null,
            settings: settings,
            profileSwitchRequest: fixture.CreateProfileSwitchRequest(requestVersion: 2, profileName: "fast"));

        Assert.Empty(result.CommittedFrames);
        Assert.Equal([TrackerEventKind.ProfileSwitched], result.EmittedEvents.Select(emitted => emitted.Kind));
    }

    [Fact]
    public void Update_OrdersProfileSwitchBeforeWorldFrameCommitted_WhenSwitchAndFrameShareAResult()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.0),
            settings: settings,
            profileSwitchRequest: fixture.CreateProfileSwitchRequest(requestVersion: 3, profileName: "fast"));

        Assert.Equal(
            [TrackerEventKind.ProfileSwitched, TrackerEventKind.WorldFrameCommitted],
            result.EmittedEvents.Select(emitted => emitted.Kind).Take(2));
    }

    [Fact]
    public void Update_PreservesFrameNumberContinuityAcrossProfileSwitch()
    {
        var engine = fixture.CreateEngine();
        var settings = fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0);

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
            profileSwitchRequest: fixture.CreateProfileSwitchRequest(requestVersion: 3, profileName: "fast"));

        var firstCommittedFrame = Assert.Single(firstResult.CommittedFrames);
        var switchedCommittedFrame = Assert.Single(switchResult.CommittedFrames);

        Assert.Equal(firstCommittedFrame.FrameNumber + 1, switchedCommittedFrame.FrameNumber);
        Assert.Equal(
            [TrackerEventKind.ProfileSwitched, TrackerEventKind.WorldFrameCommitted],
            switchResult.EmittedEvents.Select(emitted => emitted.Kind).Take(2));
    }
}
