using Microsoft.Extensions.Logging.Abstractions;
using Tracker.Core;
using Tracker.Server.Tracking;
using Tracker.Server.Vision;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public class TrackerCoordinatorFrameFlowTests : IClassFixture<TrackerContractFixture>
{
    public TrackerCoordinatorFrameFlowTests(TrackerContractFixture fixture)
    {
        Fixture = fixture;
        Factory = new TrackerCoordinatorTestFactory(fixture);
    }

    private TrackerContractFixture Fixture { get; }

    private TrackerCoordinatorTestFactory Factory { get; }

    [Fact]
    public void ProcessPacket_WithCommittedFrame_UpdatesTrackedSnapshotAndPublishesTrackerPacket()
    {
        // 何を確認しているか: committed frame が snapshot と tracker packet publish に反映され、observer が world-frame を受けることを確認する。
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new TrackerCoordinatorRecordingTrackerPacketPublisher();
        var observer = new TrackerCoordinatorRecordingTrackerObserver(snapshotStore);
        var coordinator = Factory.CreateCoordinator(snapshotStore, publisher, observer);
        var receivedAt = new DateTimeOffset(2026, 5, 10, 8, 30, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateGeometryPacket(fieldLength: 12000, fieldWidth: 9000),
            receivedAt);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 50, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            receivedAt);

        var snapshot = snapshotStore.GetSnapshot();
        var publishedPacket = Assert.Single(publisher.Packets);
        var trackedFrame = Assert.IsType<TrackedFrame>(publishedPacket.TrackedFrame);
        var latestFrame = Assert.IsType<TrackerFrame>(snapshot.LatestFrame);

        Assert.Equal("default", snapshot.ActiveProfileName);
        Assert.Equal(receivedAt, snapshot.ReceivedAt);
        Assert.Equal(1, snapshot.PublishSuccessCount);
        Assert.Equal(0, snapshot.PublishFailureCount);
        Assert.Equal((uint)1, latestFrame.FrameNumber);
        Assert.Equal((long)1_000_000_000, latestFrame.DataTimestampNs);
        Assert.Equal((uint)1, trackedFrame.FrameNumber);
        Assert.Equal(0.1, trackedFrame.Balls[0].Pos.X, precision: 6);
        Assert.Equal(
            ["world-frame:1"],
            observer.Events);
    }

    [Fact]
    public void ProcessPacket_WhenDerivedEventsExist_NotifiesObserverInEmittedOrder()
    {
        // 何を確認しているか: derived event がある場合も observer へ engine の event 順で通知されることを確認する。
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new TrackerCoordinatorRecordingTrackerPacketPublisher();
        var observer = new TrackerCoordinatorRecordingTrackerObserver(snapshotStore);
        var coordinator = Factory.CreateCoordinator(snapshotStore, publisher, observer);
        var receivedAt = new DateTimeOffset(2026, 5, 10, 8, 40, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f)],
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 80, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.000),
            receivedAt);

        observer.Events.Clear();

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 110, y: 0, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            receivedAt.AddMilliseconds(100));

        Assert.Equal(
            ["world-frame:2", "kick:2", "contact:2"],
            observer.Events);
    }
}
