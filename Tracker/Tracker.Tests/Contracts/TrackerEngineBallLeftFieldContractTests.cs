using Tracker.Core;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public class TrackerEngineBallLeftFieldContractTests : TrackerEngineContractTestBase, IClassFixture<TrackerContractFixture>
{
    public TrackerEngineBallLeftFieldContractTests(TrackerContractFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public void Update_EmitsBallLeftFieldWhenPrimaryBallLeavesThroughTouchLine()
    {
        // 何を確認しているか: primary ball が touch line から field 外へ出た場合に BallLeftField event が発行されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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
        // 何を確認しているか: goal mouth 内から field 外へ出た ball が GoalInterior と分類されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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
        // 何を確認しているか: goal mouth 外の goal line 退出が GoalLine と分類されることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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
        // 何を確認しているか: corner 方向の退出で最初に交差した perimeter に基づく分類になることを確認する。
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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
