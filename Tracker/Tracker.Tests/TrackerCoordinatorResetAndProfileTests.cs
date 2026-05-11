using Microsoft.Extensions.Logging.Abstractions;
using Tracker.Core;
using Tracker.Server.Tracking;
using Tracker.Server.Vision;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TrackerCoordinator の geometry reset、profile switch、runtime tuning 反映 contract を検証する。
/// </summary>
public class TrackerCoordinatorResetAndProfileTests : IClassFixture<TrackerContractFixture>
{
    public TrackerCoordinatorResetAndProfileTests(TrackerContractFixture fixture)
    {
        Fixture = fixture;
        Factory = new TrackerCoordinatorTestFactory(fixture);
    }

    private TrackerContractFixture Fixture { get; }

    private TrackerCoordinatorTestFactory Factory { get; }

    /// <summary>
    /// 何を確認しているか: geometry reset 時に observer 通知前へ tracked snapshot が clear されることを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WhenGeometryResetOccurs_ClearsTrackedSnapshotBeforeNotifyingObserver()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new TrackerCoordinatorRecordingTrackerPacketPublisher();
        var observer = new TrackerCoordinatorRecordingTrackerObserver(snapshotStore);
        var coordinator = Factory.CreateCoordinator(
            snapshotStore,
            publisher,
            [observer],
            Fixture.CreateSettings(
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

    /// <summary>
    /// 何を確認しているか: packet を伴わない profile switch が control-only update を drain し、observer 通知前に snapshot を clear することを確認する。
    /// </summary>
    [Fact]
    public void RequestProfileSwitch_WithoutPacket_DrainsControlOnlyUpdateAndClearsSnapshotBeforeObserverNotification()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new TrackerCoordinatorRecordingTrackerPacketPublisher();
        var observer = new TrackerCoordinatorRecordingTrackerObserver(snapshotStore);
        var initialPublisherOptions = Fixture.CreatePublisherOptions(port: 10010);
        var coordinator = Factory.CreateCoordinator(
            snapshotStore,
            publisher,
            [observer],
            Fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0),
            initialPublisherOptions);
        var receivedAt = new DateTimeOffset(2026, 5, 10, 9, 0, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            receivedAt);

        observer.Events.Clear();

        coordinator.RequestProfileSwitch(
            Fixture.CreateResolvedOptions(
                Fixture.CreateSettings(profileName: "fast", reorderWindowNs: 0, mergeWindowNs: 0),
                Fixture.CreatePublisherOptions(port: 12000)),
            receivedAt.AddMilliseconds(50));

        var snapshot = snapshotStore.GetSnapshot();

        Assert.Equal("fast", snapshot.ActiveProfileName);
        Assert.Null(snapshot.LatestFrame);
        Assert.Null(snapshot.ReceivedAt);
        Assert.True(observer.LatestFrameWasClearedAtProfileSwitch);
        Assert.Equal(["profile:fast"], observer.Events);
        Assert.Equal(12000, publisher.CurrentOptions.Port);
    }

    /// <summary>
    /// 何を確認しているか: pending profile switch が packet 処理前に適用され、新 profile context で frame publish されることを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WithPendingProfileSwitch_PublishesCommittedFrameAfterApplyingNewProfileContext()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new TrackerCoordinatorRecordingTrackerPacketPublisher();
        var observer = new TrackerCoordinatorRecordingTrackerObserver(snapshotStore);
        var initialPublisherOptions = Fixture.CreatePublisherOptions(port: 10010);
        var coordinator = Factory.CreateCoordinator(
            snapshotStore,
            publisher,
            [observer],
            Fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0),
            initialPublisherOptions);
        var receivedAt = new DateTimeOffset(2026, 5, 10, 9, 5, 0, TimeSpan.Zero);

        coordinator.RequestProfileSwitch(
            Fixture.CreateResolvedOptions(
                Fixture.CreateSettings(profileName: "fast", reorderWindowNs: 0, mergeWindowNs: 0),
                Fixture.CreatePublisherOptions(port: 12000)),
            receivedAt);

        observer.Events.Clear();

        var result = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 150, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            receivedAt.AddMilliseconds(100));

        var committedFrame = Assert.Single(result.CommittedFrames);
        var snapshot = snapshotStore.GetSnapshot();

        Assert.Equal("fast", committedFrame.Metadata.ProfileName);
        Assert.Equal("fast", snapshot.ActiveProfileName);
        Assert.Equal((uint)1, Assert.IsType<TrackerFrame>(snapshot.LatestFrame).FrameNumber);
        Assert.Equal(12000, publisher.PublishedPorts.Single());
        Assert.Equal(["world-frame:1"], observer.Events);
    }

    /// <summary>
    /// 何を確認しているか: 同じ profile 名でも runtime tuning 差分が engine settings に反映されることを確認する。
    /// </summary>
    [Fact]
    public void RequestProfileSwitch_WithSameProfileButDifferentRuntimeTuning_AppliesNewEngineSettings()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new TrackerCoordinatorRecordingTrackerPacketPublisher();
        var coordinator = Factory.CreateCoordinator(
            snapshotStore,
            publisher,
            [],
            Fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0),
            Fixture.CreatePublisherOptions(port: 10010));
        var receivedAt = new DateTimeOffset(2026, 5, 10, 9, 15, 0, TimeSpan.Zero);

        coordinator.RequestProfileSwitch(
            Fixture.CreateResolvedOptions(
                Fixture.CreateSettings(
                    profileName: "default",
                    reorderWindowNs: 0,
                    mergeWindowNs: 0,
                    kickDetector: new TrackerKickDetectorOverrides
                    {
                        ContactMarginMm = 0d,
                    }),
                Fixture.CreatePublisherOptions(port: 10010)),
            receivedAt,
            new TrackerRuntimeOverrides
            {
                KickDetector = new TrackerKickDetectorOverrides
                {
                    ContactMarginMm = 0d,
                },
            });

        var result = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 0, y: 0, confidence: 1.0f)],
                robotsYellow: [TrackerContractTestData.CreateRobot(robotId: 4, x: 130, y: 0, orientation: 0.0f)],
                captureTimeSeconds: 1.000),
            receivedAt.AddMilliseconds(100));

        var committedFrame = Assert.Single(result.CommittedFrames);

        Assert.Null(committedFrame.LatestContact);
        Assert.Equal([TrackerEventKind.WorldFrameCommitted], result.EmittedEvents.Select(emitted => emitted.Kind));
    }
}
