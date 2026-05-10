using Tracker.Core;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public class TrackerEngineBallTrackingContractTests : TrackerEngineContractTestBase, IClassFixture<TrackerContractFixture>
{
    public TrackerEngineBallTrackingContractTests(TrackerContractFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Update_MergesSameBallAcrossCamerasIntoSingleTrackedBall()
    {
        // 何を確認しているか: 複数 camera の同一 ball 観測が 1 つの tracked ball に統合されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

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
        // 何を確認しているか: visibility と stable sort により primary / secondary ball の順序が決まることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        TrackerUpdateResult result = new();
        for (var frameIndex = 0; frameIndex < 3; frameIndex++)
        {
            result = engine.Update(
                packet: TrackerContractTestData.CreateDetectionPacket(
                    frameNumber: (uint)(10 + frameIndex),
                    cameraId: 1,
                    balls:
                    [
                        TrackerContractTestData.CreateBall(x: 100, y: 100, confidence: 0.60f),
                        TrackerContractTestData.CreateBall(x: 300, y: 300, confidence: 0.95f),
                        TrackerContractTestData.CreateBall(x: 200, y: 200, confidence: 0.80f),
                    ],
                    captureTimeSeconds: 1.000 + (frameIndex * 0.100)),
                settings: settings);
        }

        var committedFrame = Assert.Single(result.CommittedFrames);

        Assert.Equal(committedFrame.Balls[0].InternalTrackId, committedFrame.PrimaryBallTrackId);
        Assert.Equal(300, committedFrame.Balls[0].XMm, precision: 3);
        Assert.Equal(200, committedFrame.Balls[1].XMm, precision: 3);
        Assert.Equal(100, committedFrame.Balls[2].XMm, precision: 3);
    }

    [Fact]
    public void Update_TracksBallVelocityAcrossFrames()
    {
        // 何を確認しているか: 連続 frame から ball velocity が算出されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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

        Assert.InRange(trackedBall.VXMmPerS, 290, 310);
        Assert.InRange(trackedBall.VYMmPerS, 385, 415);
        Assert.InRange(trackedBall.VZMmPerS, 290, 310);
    }

    [Fact]
    public void Update_AppliesBallKalmanMeasurementNoiseInsteadOfOverwritingObservation()
    {
        // 何を確認しているか: ball の measurement noise が Kalman 更新へ反映され、観測値で状態を直接上書きしないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            ballTracker: new TrackerBallTrackerOverrides
            {
                MeasurementNoise = 1_000_000d,
            });

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
                balls: [TrackerContractTestData.CreateBall(x: 180, y: 260, z: 70, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            settings: settings);

        var trackedBall = Assert.Single(Assert.Single(secondResult.CommittedFrames).Balls);

        Assert.True(trackedBall.XMm > 100.0);
        Assert.True(trackedBall.XMm < 180.0);
        Assert.True(trackedBall.YMm > 200.0);
        Assert.True(trackedBall.YMm < 260.0);
        Assert.True(trackedBall.ZMm > 10.0);
        Assert.True(trackedBall.ZMm < 70.0);
    }

    [Fact]
    public void Update_UsesConfiguredBallProcessNoiseWhenUpdatingAfterPredictionOnlyFrame()
    {
        // 何を確認しているか: prediction-only frame 後の更新で BallProcessNoise 設定が不確かさと追従に反映されることを確認する。
        var lowProcessEngine = Fixture.CreateEngine();
        var highProcessEngine = Fixture.CreateEngine();
        var lowProcessSettings = Fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            ballTracker: new TrackerBallTrackerOverrides
            {
                ProcessNoise = 0.001d,
                MeasurementNoise = 10_000d,
            });
        var highProcessSettings = Fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            ballTracker: new TrackerBallTrackerOverrides
            {
                ProcessNoise = 10_000d,
                MeasurementNoise = 10_000d,
            });

        _ = lowProcessEngine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: lowProcessSettings);
        _ = lowProcessEngine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 300, y: 400, orientation: 0.5f)],
                captureTimeSeconds: 1.200),
            settings: lowProcessSettings);
        var lowProcessResult = lowProcessEngine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 200, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.300),
            settings: lowProcessSettings);

        _ = highProcessEngine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            settings: highProcessSettings);
        _ = highProcessEngine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                robotsBlue: [TrackerContractTestData.CreateRobot(robotId: 2, x: 300, y: 400, orientation: 0.5f)],
                captureTimeSeconds: 1.200),
            settings: highProcessSettings);
        var highProcessResult = highProcessEngine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 200, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.300),
            settings: highProcessSettings);

        var lowProcessBall = Assert.Single(Assert.Single(lowProcessResult.CommittedFrames).Balls);
        var highProcessBall = Assert.Single(Assert.Single(highProcessResult.CommittedFrames).Balls);

        Assert.True(highProcessBall.XMm > lowProcessBall.XMm);
    }

    [Fact]
    public void Update_KeepsBallTrackAliveAcrossOneMissingFrameWithDecayedVisibility()
    {
        // 何を確認しているか: 1 frame 欠測した ball track が visibility を減衰させながら短期保持されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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
        // 何を確認しているか: BallTrackLifetime 設定に従って prediction のみの ball track が期限切れになることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
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
    public void Update_DoesNotEmitBallTrackAfterOutputVisibilityFallsBelowThreshold()
    {
        // 何を確認しているか: ball の output visibility が閾値未満まで落ちたら tracked frame へ出さないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
            reorderWindowNs: 0,
            mergeWindowNs: 0,
            ballTracker: new TrackerBallTrackerOverrides
            {
                VisibilityHalfLifeSeconds = 1.0d,
                OutputVisibilityThreshold = 0.5d,
                TrackLifetimeNs = 10_000_000_000,
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
                captureTimeSeconds: 2.100),
            settings: settings);

        Assert.Empty(Assert.Single(secondResult.CommittedFrames).Balls);
    }

    [Fact]
    public void Update_DoesNotEmitSingleFrameSecondaryBallGhost()
    {
        // 何を確認しているか: 1 frame だけ現れた secondary ball ghost を tracked frame へ出さないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        _ = engine.Update(
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
                balls:
                [
                    TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f),
                    TrackerContractTestData.CreateBall(x: -3173.6f, y: -4397.0f, confidence: 1.0f),
                ],
                captureTimeSeconds: 1.100),
            settings: settings);

        var thirdResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.200),
            settings: settings);

        var secondBall = Assert.Single(Assert.Single(secondResult.CommittedFrames).Balls);
        var thirdBall = Assert.Single(Assert.Single(thirdResult.CommittedFrames).Balls);
        Assert.Equal(0, secondBall.XMm, precision: 3);
        Assert.Equal(0, thirdBall.XMm, precision: 3);
    }

    [Fact]
    public void Update_DoesNotEmitStaleGrownUpSecondaryBall()
    {
        // 何を確認しているか: 成長済み secondary ball が fresh observation を失った後に出続けないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        // 成長済み secondary ball でも、fresh observation を失った後は外部出力しないことを確認する。
        TrackerUpdateResult result = new();
        for (var frameIndex = 0; frameIndex < 3; frameIndex++)
        {
            result = engine.Update(
                packet: TrackerContractTestData.CreateDetectionPacket(
                    frameNumber: (uint)(10 + frameIndex),
                    cameraId: 1,
                    balls:
                    [
                        TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f),
                        TrackerContractTestData.CreateBall(x: 300, y: 0, confidence: 1.0f),
                    ],
                    captureTimeSeconds: 1.000 + (frameIndex * 0.100)),
                settings: settings);
        }

        Assert.Equal(2, Assert.Single(result.CommittedFrames).Balls.Count);

        var staleResult = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.300),
            settings: settings);

        var ball = Assert.Single(Assert.Single(staleResult.CommittedFrames).Balls);
        Assert.Equal(0, ball.XMm, precision: 3);
    }

    [Fact]
    public void Update_UsesConfiguredBallVisibilityHalfLifeWhenPredictingTrack()
    {
        // 何を確認しているか: BallVisibilityHalfLifeSeconds 設定が欠測時の ball visibility 減衰へ反映されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
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
    public void Update_DampsStationaryBallMeasurementJitter()
    {
        // 何を確認しているか: 停止中に近い ball の measurement jitter が表示位置へそのまま出ないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        // 停止している ball の raw detection が小さく揺れても、tracked 出力が同じ幅で振動しないことを確認する。
        TrackerUpdateResult result = new();
        for (var frameIndex = 0; frameIndex < 12; frameIndex++)
        {
            var jitterXMm = frameIndex % 2 == 0 ? 10 : -10;
            result = engine.Update(
                packet: TrackerContractTestData.CreateDetectionPacket(
                    frameNumber: (uint)(10 + frameIndex),
                    cameraId: 1,
                    balls: [TrackerContractTestData.CreateBall(x: jitterXMm, y: 0, confidence: 1.0f)],
                    captureTimeSeconds: 1.000 + (frameIndex * 0.016)),
                settings: settings);
        }

        var trackedBall = Assert.Single(Assert.Single(result.CommittedFrames).Balls);
        Assert.InRange(Math.Abs(trackedBall.XMm), 0, 5);
        Assert.InRange(Math.Abs(trackedBall.VXMmPerS), 0, 600);
    }

    [Fact]
    public void Update_UsesConfiguredBallGateForTrackMatchingAcrossFrames()
    {
        // 何を確認しているか: BallGate 設定により frame 間の ball track matching 可否が決まることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(
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
        // 何を確認しているか: visible camera が切り替わっても同一 ball track の identity が保たれることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

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
        // 何を確認しているか: stale ball track が離れた 2 つの fresh ball を誤って bridge しないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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

        _ = engine.Update(
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

        _ = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 30,
                cameraId: 2,
                balls:
                [
                    TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f),
                    TrackerContractTestData.CreateBall(x: 200, y: 0, confidence: 1.0f),
                ],
                captureTimeSeconds: 2.100),
            settings: settings);

        var result = engine.Update(
            packet: TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 40,
                cameraId: 2,
                balls:
                [
                    TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f),
                    TrackerContractTestData.CreateBall(x: 200, y: 0, confidence: 1.0f),
                ],
                captureTimeSeconds: 2.200),
            settings: settings);

        var committedFrame = Assert.Single(result.CommittedFrames);
        Assert.Equal(2, committedFrame.Balls.Count);
        var sortedBallX = committedFrame.Balls.Select(ball => ball.XMm).OrderBy(x => x).ToArray();
        Assert.InRange(sortedBallX[0], -5, 5);
        Assert.InRange(sortedBallX[1], 195, 205);
    }

    [Fact]
    public void Update_MergesBallsUsingUncertaintyWeightedPositions()
    {
        // 何を確認しているか: 複数 ball 観測の merge で uncertainty-weighted position が使われることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

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
        Assert.InRange(mergedBall.XMm, 100, 115);
    }

    [Fact]
    public void Update_ReusesSameCameraBallTrackAcrossSequentialDetectionsInOneCommittedGroup()
    {
        // 何を確認しているか: 同一 committed group 内の同一 camera 連続 detection で同じ ball track を再利用することを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

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
        Assert.InRange(committedBall.XMm, 100, 110);
        Assert.Equal(committedBall.InternalTrackId, flushResult.CommittedFrames[0].PrimaryBallTrackId);
    }

    [Fact]
    public void Update_KeepsNearbyDistinctBallsFromSameCameraSeparated()
    {
        // 何を確認しているか: 同一 camera の近接しているが別物の ball を 1 つに潰さないことを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

        TrackerUpdateResult result = new();
        for (var frameIndex = 0; frameIndex < 3; frameIndex++)
        {
            result = engine.Update(
                packet: TrackerContractTestData.CreateDetectionPacket(
                    frameNumber: (uint)(10 + frameIndex),
                    cameraId: 1,
                    balls:
                    [
                        TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f),
                        TrackerContractTestData.CreateBall(x: 80, y: 80, confidence: 0.9f),
                    ],
                    captureTimeSeconds: 1.000 + (frameIndex * 0.100)),
                settings: settings);
        }

        var committedFrame = Assert.Single(result.CommittedFrames);
        Assert.Equal(2, committedFrame.Balls.Count);
        Assert.Equal([0d, 80d], committedFrame.Balls.Select(ball => ball.XMm).OrderBy(x => x));
    }

    [Fact]
    public void Update_PreservesMergedBallIdentityAcrossLargeCommittedFrameJumpWhenIntermediateDetectionsSustainVelocity()
    {
        // 何を確認しているか: 中間 detection が速度を支える場合、大きな frame jump 後も merged ball identity が保たれることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

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

        Assert.InRange(secondFrameBall.XMm, 95, 105);
        Assert.InRange(thirdFrameBall.XMm, 250, 310);
        Assert.Equal(secondFrameBall.InternalTrackId, thirdFrameBall.InternalTrackId);
    }

    [Fact]
    public void Update_MergesThreeCameraBallChainIntoSingleCluster()
    {
        // 何を確認しているか: 3 camera の chain 状 ball 観測が同一 cluster として merge されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

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
}
