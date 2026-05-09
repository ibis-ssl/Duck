using Microsoft.Extensions.Logging.Abstractions;
using Tracker.Core;
using Tracker.Server.Tracking;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public class TrackerCoordinatorTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public TrackerCoordinatorTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void ProcessPacket_WithCommittedFrame_UpdatesTrackedSnapshotAndPublishesTrackerPacket()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingTrackerPacketPublisher();
        var observer = new RecordingTrackerObserver(snapshotStore);
        var coordinator = CreateCoordinator(snapshotStore, publisher, observer);
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
    public void ProcessPacket_WhenGeometryResetOccurs_ClearsTrackedSnapshotBeforeNotifyingObserver()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingTrackerPacketPublisher();
        var observer = new RecordingTrackerObserver(snapshotStore);
        var coordinator = CreateCoordinator(
            snapshotStore,
            publisher,
            [observer],
            fixture.CreateSettings(
                reorderWindowNs: 100_000_000,
                mergeWindowNs: 20_000_000,
                geometryResetFieldLengthThresholdMm: 100,
                geometryResetFieldWidthThresholdMm: 100));
        var receivedAt = new DateTimeOffset(2026, 5, 10, 8, 35, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateGeometryPacket(fieldLength: 9000, fieldWidth: 6000),
            receivedAt);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100)],
                captureTimeSeconds: 1.000),
            receivedAt);

        observer.Events.Clear();

        _ = coordinator.ProcessPacket(
            new SSL_WrapperPacket
            {
                Geometry = TrackerContractTestData.CreateGeometryPacket(fieldLength: 9400, fieldWidth: 6400).Geometry,
                Detection = TrackerContractTestData.CreateDetectionPacket(
                    frameNumber: 20,
                    cameraId: 1,
                    balls: [TrackerContractTestData.CreateBall(x: 200)],
                    captureTimeSeconds: 2.000).Detection,
            },
            receivedAt.AddSeconds(1));

        var snapshot = snapshotStore.GetSnapshot();

        Assert.True(observer.LatestFrameWasClearedAtGeometryReset);
        Assert.Equal(
            ["geometry-reset", "world-frame:1"],
            observer.Events);
        Assert.Equal((uint)1, Assert.IsType<TrackerFrame>(snapshot.LatestFrame).FrameNumber);
        Assert.Equal(receivedAt.AddSeconds(1), snapshot.ReceivedAt);
        Assert.Equal(1, snapshot.PublishSuccessCount);
    }

    [Fact]
    public void ProcessPacket_WhenDerivedEventsExist_NotifiesObserverInEmittedOrder()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingTrackerPacketPublisher();
        var observer = new RecordingTrackerObserver(snapshotStore);
        var coordinator = CreateCoordinator(snapshotStore, publisher, observer);
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

    private TrackerCoordinator CreateCoordinator(
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher,
        params ITrackerObserver[] observers)
    {
        return CreateCoordinator(
            snapshotStore,
            publisher,
            observers,
            fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0));
    }

    private TrackerCoordinator CreateCoordinator(
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher,
        IReadOnlyList<ITrackerObserver> observers,
        TrackerEngineSettings settings)
    {
        return new TrackerCoordinator(
            fixture.CreateEngine(),
            fixture.CreatePacketGenerator(),
            settings,
            snapshotStore,
            publisher,
            observers,
            NullLogger<TrackerCoordinator>.Instance);
    }

    private sealed class RecordingTrackerPacketPublisher : ITrackerPacketPublisher
    {
        public List<TrackerWrapperPacket> Packets { get; } = [];

        public void Publish(TrackerWrapperPacket packet)
        {
            Packets.Add(packet.Clone());
        }
    }

    private sealed class RecordingTrackerObserver : ITrackerObserver
    {
        private readonly TrackedSnapshotStore snapshotStore;

        public RecordingTrackerObserver(TrackedSnapshotStore snapshotStore)
        {
            this.snapshotStore = snapshotStore;
        }

        public List<string> Events { get; } = [];

        public bool LatestFrameWasClearedAtGeometryReset { get; private set; }

        public void OnProfileSwitched(string profileName)
        {
            Events.Add($"profile:{profileName}");
        }

        public void OnGeometryReset()
        {
            LatestFrameWasClearedAtGeometryReset = snapshotStore.GetSnapshot().LatestFrame is null;
            Events.Add("geometry-reset");
        }

        public void OnWorldFrameCommitted(TrackerFrame frame)
        {
            Events.Add($"world-frame:{frame.FrameNumber}");
        }

        public void OnKickDetected(KickEventState kick, TrackerFrame frame)
        {
            Events.Add($"kick:{frame.FrameNumber}");
        }

        public void OnContactChanged(TrackerFrame frame)
        {
            Events.Add($"contact:{frame.FrameNumber}");
        }

        public void OnBallLeftField(BallLeftFieldState state, TrackerFrame frame)
        {
            Events.Add($"left-field:{frame.FrameNumber}");
        }
    }
}
