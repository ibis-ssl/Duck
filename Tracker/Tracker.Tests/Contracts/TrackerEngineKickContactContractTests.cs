using Tracker.Core;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public class TrackerEngineKickContactContractTests : TrackerEngineContractTestBase, IClassFixture<TrackerContractFixture>
{
    public TrackerEngineKickContactContractTests(TrackerContractFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Update_PopulatesCurrentBallContactAndMarksContactingRobot()
    {
        // 何を確認しているか: ball contact が成立した frame で current contact と robot 側 contact marker が設定されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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
        // 何を確認しているか: ContactMarginMm 設定が ball contact 判定距離に反映されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
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
        // 何を確認しているか: contact が終了した後も last toucher 情報が保持されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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
        // 何を確認しているか: recent contact 後の ball 加速を kick として検出し、kick event が contact change より先に発行されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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
        // 何を確認しているか: KickSpeedThreshold 設定により kick 検出の有無が変わることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
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
        // 何を確認しているか: primary ball track が別物になった場合に last toucher を持ち越さないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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
        // 何を確認しているか: vertical velocity noise が chip threshold 未満なら flat kick と分類されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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
        // 何を確認しているか: ChipHeightThreshold 設定により chip / flat 分類が変わることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
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
}
