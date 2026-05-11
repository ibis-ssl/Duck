using Tracker.Core;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TrackerEngine の detection buffering、flush ordering、late packet contract を検証する。
/// </summary>
public class TrackerEngineBufferingContractTests : TrackerEngineContractTestBase, IClassFixture<TrackerContractFixture>
{
    public TrackerEngineBufferingContractTests(TrackerContractFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// 何を確認しているか: 到着順が event time と異なる場合でも、確定 frame が event time 昇順で flush されることを確認する。
    /// </summary>
    [Fact]
    public void Update_FlushesBufferedDetectionsInEventTimeOrder_WhenArrivalOrderDiffers()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 100_000_000, mergeWindowNs: 20_000_000);

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

    /// <summary>
    /// 何を確認しているか: 観測時刻の差が MergeWindow を超えた場合、同一 flush 内でも別 frame として分割されることを確認する。
    /// </summary>
    [Fact]
    public void Update_SplitsFrames_WhenObservationsExceedMergeWindow()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 10_000_000);

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

    /// <summary>
    /// 何を確認しているか: ReorderWindow 内で buffer 中は 0 frame を返し、複数 group が閉じた時だけまとめて確定されることを確認する。
    /// </summary>
    [Fact]
    public void Update_CanReturnZeroFramesWhileBuffering_AndMultipleFramesWhenSeveralGroupsFlush()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 1_500_000_000, mergeWindowNs: 20_000_000);

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

    /// <summary>
    /// 何を確認しているか: すでに確定済み時刻より古い packet が後続 flush の tracked frame を汚染しないことを確認する。
    /// </summary>
    [Fact]
    public void Update_DropsLatePacketsAndDoesNotLetThemContaminateLaterFlushes()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

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

    /// <summary>
    /// 何を確認しているか: 複数 frame が同時に flush される場合、各 committed frame に対応する event が flush 順で発行されることを確認する。
    /// </summary>
    [Fact]
    public void Update_EmitsWorldFrameCommittedForEachCommittedFrameInFlushOrder()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 1_500_000_000, mergeWindowNs: 10_000_000);

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

    /// <summary>
    /// 何を確認しているか: capture time が欠落した detection では sent time を DataTimestampNs として使う契約を確認する。
    /// </summary>
    [Fact]
    public void Update_UsesSentTimeWhenCaptureTimeIsMissing()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);

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

    /// <summary>
    /// 何を確認しているか: 確定済み MergeWindow 内に後着した packet を late として捨て、既存 frame を再構成しないことを確認する。
    /// </summary>
    [Fact]
    public void Update_DropsLatePacketsThatFallInsideAnAlreadyCommittedMergeWindow()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

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

    /// <summary>
    /// 何を確認しているか: 最古 group の MergeWindow が閉じるまで flush せず、早すぎる確定を避けることを確認する。
    /// </summary>
    [Fact]
    public void Update_WaitsForTheOldestGroupMergeWindowToCloseBeforeFlushingIt()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 50_000_000, mergeWindowNs: 20_000_000);

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

    /// <summary>
    /// 何を確認しているか: local processing time から ProcessedAtNs が設定され、data timestamp と独立していることを確認する。
    /// </summary>
    [Fact]
    public void Update_PopulatesProcessedAtNsFromLocalProcessingTime()
    {
        var engine = Fixture.CreateEngine();
        var settings = Fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0);
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
}
