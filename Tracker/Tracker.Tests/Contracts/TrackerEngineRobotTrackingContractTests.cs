using Tracker.Core;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public class TrackerEngineRobotTrackingContractTests : TrackerEngineContractTestBase, IClassFixture<TrackerContractFixture>
{
    public TrackerEngineRobotTrackingContractTests(TrackerContractFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Update_MergesSameRobotAcrossCamerasIntoSingleTrackedRobot()
    {
        // 何を確認しているか: 複数 camera の同一 team / robot ID 観測が 1 つの tracked robot に統合されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

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
        // 何を確認しているか: 連続 frame から robot の並進速度と unwrap 済み角速度が算出されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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

        Assert.InRange(trackedRobot.VXMmPerS, 290, 310);
        Assert.InRange(trackedRobot.VYMmPerS, 385, 415);
        Assert.InRange(trackedRobot.AngularVelocityRadPerS, 0.9, 1.2);
    }

    [Fact]
    public void Update_DampsStationaryRobotMeasurementJitter()
    {
        // 何を確認しているか: 停止中に近い robot の measurement jitter が表示位置へそのまま出ないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        // 停止している robot の raw detection が小さく揺れても、tracked 出力が同じ幅で振動しないことを確認する。
        TrackerUpdateResult result = new();
        for (var frameIndex = 0; frameIndex < 12; frameIndex++)
        {
            var jitterXMm = frameIndex % 2 == 0 ? 8 : -8;
            result = engine.Update(
                packet: TrackerContractTestData.CreateDetectionPacket(
                    frameNumber: (uint)(10 + frameIndex),
                    cameraId: 1,
                    robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: jitterXMm, y: 0, orientation: 0)],
                    captureTimeSeconds: 1.000 + (frameIndex * 0.016)),
                settings: settings);
        }

        var trackedRobot = Assert.Single(Assert.Single(result.CommittedFrames).Robots);
        Assert.InRange(Math.Abs(trackedRobot.XMm), 0, 5);
        Assert.InRange(Math.Abs(trackedRobot.VXMmPerS), 0, 600);
    }

    [Fact]
    public void Update_UsesPredictedRobotPositionForGateAfterVelocityIsLearned()
    {
        // 何を確認しているか: 速度学習後の robot gate が前回観測ではなく predicted position を基準にすることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);
        var narrowGateSettings = Fixture.CreateSettings(
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
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 0, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 100, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var thirdResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 200, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.200),
            settings: narrowGateSettings);

        var trackedRobot = Assert.Single(Assert.Single(thirdResult.CommittedFrames).Robots);

        Assert.True(trackedRobot.VXMmPerS > 500);
    }

    [Fact]
    public void Update_AppliesRobotKalmanMeasurementNoiseInsteadOfOverwritingObservation()
    {
        // 何を確認しているか: robot の measurement noise が Kalman 更新へ反映され、観測値で状態を直接上書きしないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            robotTracker: new TrackerRobotTrackerOverrides
            {
                MeasurementNoise = 1_000_000d,
            });

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 100, y: 200, orientation: 0.1f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 180, y: 260, orientation: 0.9f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var trackedRobot = Assert.Single(Assert.Single(secondResult.CommittedFrames).Robots);

        Assert.True(trackedRobot.XMm > 100.0);
        Assert.True(trackedRobot.XMm < 180.0);
        Assert.True(trackedRobot.YMm > 200.0);
        Assert.True(trackedRobot.YMm < 260.0);
        Assert.True(trackedRobot.OrientationRad > 0.1);
        Assert.True(trackedRobot.OrientationRad < 0.9);
    }

    [Fact]
    public void Update_UsesConfiguredRobotOutlierLimitWhenDerivingVelocity()
    {
        // 何を確認しているか: RobotOutlierLimit の設定が速度導出時の外れ値抑制に効くことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
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
    public void Update_DropsFarCameraRobotOutlierWhenAnotherCameraHasSameRobotNearTrack()
    {
        // 何を確認しているか: 別 camera に正常な同一 robot ID 観測がある場合、遠方 outlier を merge に混ぜないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 0,
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 1, x: 5160, y: -2000, orientation: 2.9f)],
                captureTimeSeconds: 1.000),
            settings: settings);
        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 1,
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 1, x: 5162, y: -1998, orientation: 2.9f)],
                captureTimeSeconds: 1.005),
            settings: settings);

        // 別 camera に正常な同一 robot ID 観測がある場合、遠方の誤 ID 観測で merged robot が瞬間移動しないことを確認する。
        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 0,
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 1, x: -5275, y: -2255, orientation: 3.2f)],
                captureTimeSeconds: 1.100),
            settings: settings);
        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 21,
                cameraId: 1,
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 1, x: 5161, y: -1997, orientation: 2.9f)],
                captureTimeSeconds: 1.105),
            settings: settings);
        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall()],
                captureTimeSeconds: 1.200),
            settings: settings);

        var trackedRobot = Assert.Single(Assert.Single(result.CommittedFrames).Robots);
        Assert.Equal(TrackerTeam.Yellow, trackedRobot.Team);
        Assert.Equal((uint)1, trackedRobot.RobotId);
        Assert.InRange(trackedRobot.XMm, 5100, 5200);
        Assert.InRange(trackedRobot.YMm, -2050, -1950);
    }

    [Fact]
    public void Update_KeepsRobotTrackAliveAcrossOneMissingFrameWithDecayedVisibility()
    {
        // 何を確認しているか: 1 frame 欠測した robot track が visibility を減衰させながら短期保持されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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
    public void Update_DoesNotEmitRobotTrackAfterOutputVisibilityFallsBelowThreshold()
    {
        // 何を確認しているか: robot の output visibility が閾値未満まで落ちたら tracked frame へ出さないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            robotTracker: new TrackerRobotTrackerOverrides
            {
                VisibilityHalfLifeSeconds = 1.0d,
                OutputVisibilityThreshold = 0.25d,
            });

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 11, x: 100, y: 200, orientation: 0.5f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        var secondResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 300)],
                captureTimeSeconds: 3.100),
            settings: settings);

        Assert.Empty(Assert.Single(secondResult.CommittedFrames).Robots);
    }

    [Fact]
    public void Update_IgnoresCloseDuplicateRobotIdsFromSameCameraAndTeam()
    {
        // 何を確認しているか: 同一 camera / team の近接 duplicate robot ID を抑制し、重複表示を避けることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                robotsYellow:
                [
                    TrackerContractTestData.CreateRobot(robotId: 1, x: -5539.6f, y: -4310.4f, orientation: -1.567f),
                    TrackerContractTestData.CreateRobot(robotId: 11, x: -5539.2f, y: -4310.4f, orientation: 0.013f),
                ],
                captureTimeSeconds: 1.000),
            settings: settings);

        var trackedRobot = Assert.Single(Assert.Single(result.CommittedFrames).Robots);
        Assert.Equal(TrackerTeam.Yellow, trackedRobot.Team);
        Assert.Equal((uint)1, trackedRobot.RobotId);
    }

    [Fact]
    public void Update_DoesNotApplyCloseDuplicateRobotFilterAcrossDetections()
    {
        // 何を確認しているか: duplicate 抑制が別 detection group の正当な robot 観測まで消さないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 1, x: 100, y: 100, orientation: 0.1f)],
                captureTimeSeconds: 1.000),
            settings: settings);

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 11,
                cameraId: 1,
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 11, x: 101, y: 100, orientation: 0.2f)],
                captureTimeSeconds: 1.010),
            settings: settings);

        var flushResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 2,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0)],
                captureTimeSeconds: 2.000),
            settings: settings);

        var robotIds = flushResult.CommittedFrames[0].Robots
            .Where(robot => robot.Team == TrackerTeam.Yellow)
            .Select(robot => robot.RobotId)
            .Order()
            .ToList();
        Assert.Equal([(uint)1, (uint)11], robotIds);
    }

    [Fact]
    public void Update_DoesNotMergeStaleCameraPredictionWhenAnotherCameraHasFreshRobotObservation()
    {
        // 何を確認しているか: fresh な別 camera 観測がある場合、stale camera prediction で robot 位置を引っ張らないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

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
}
