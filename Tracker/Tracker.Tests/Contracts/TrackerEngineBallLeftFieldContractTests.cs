using Tracker.Core;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TrackerEngine の ball left field event と退出分類 contract を検証する。
/// </summary>
public class TrackerEngineBallLeftFieldContractTests : TrackerEngineContractTestBase, IClassFixture<TrackerContractFixture>
{
    public TrackerEngineBallLeftFieldContractTests(TrackerContractFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// 何を確認しているか: primary ball が touch line から field 外へ出た場合に BallLeftField event が発行されることを確認する。
    /// </summary>
    [Fact]
    public void Update_EmitsBallLeftFieldWhenPrimaryBallLeavesThroughTouchLine()
    {
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

    /// <summary>
    /// 何を確認しているか: goal mouth 内から field 外へ出た ball が GoalInterior と分類されることを確認する。
    /// </summary>
    [Fact]
    public void Update_ClassifiesGoalMouthExitAsGoalInterior()
    {
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

    /// <summary>
    /// 何を確認しているか: goal mouth 外の goal line 退出が GoalLine と分類されることを確認する。
    /// </summary>
    [Fact]
    public void Update_ClassifiesNonGoalMouthExitAsGoalLine()
    {
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

    /// <summary>
    /// 何を確認しているか: corner 方向の退出で最初に交差した perimeter に基づく分類になることを確認する。
    /// </summary>
    [Fact]
    public void Update_ClassifiesCornerExitByFirstPerimeterCrossing()
    {
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
