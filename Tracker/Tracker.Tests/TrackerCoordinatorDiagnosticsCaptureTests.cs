using Tracker.Core;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: RUNTIME-HOST-005 以降、Core coordinator が DebugHost diagnostics / capture sidecar を直接書かないことを検証する。
/// </summary>
public class TrackerCoordinatorDiagnosticsCaptureTests : IClassFixture<TrackerContractFixture>
{
    public TrackerCoordinatorDiagnosticsCaptureTests(TrackerContractFixture fixture)
    {
        Fixture = fixture;
        Factory = new TrackerCoordinatorTestFactory(fixture);
    }

    private TrackerContractFixture Fixture { get; }

    private TrackerCoordinatorTestFactory Factory { get; }

    /// <summary>
    /// 何を確認しているか: capture session を用意しても Core coordinator は diagnostics log / render snapshot sidecar を生成しないこと。
    /// </summary>
    [Fact]
    public void ProcessPacket_WithCaptureSession_DoesNotWriteDiagnosticsOrRenderSidecarsFromCoreCoordinator()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"runtime-host-005-core-boundary-{Guid.NewGuid():N}");
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new TrackerCoordinatorRecordingTrackerPacketPublisher();
        _ = Factory.CreateCaptureSession(captureDirectory);
        var coordinator = Factory.CreateCoordinator(
            snapshotStore,
            publisher,
            [],
            Fixture.CreateSettings(profileName: "sim", reorderWindowNs: 0, mergeWindowNs: 0),
            Fixture.CreatePublisherOptions(),
            new TrackerDiagnosticsOptions());
        var receivedAt = new DateTimeOffset(2026, 5, 10, 18, 30, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            receivedAt);

        Assert.False(
            Directory.Exists(captureDirectory),
            "Core TrackerCoordinator must not create DebugHost diagnostics or capture sidecars as part of the operation loop.");
        Assert.Single(publisher.Packets);
        Assert.Equal(1, snapshotStore.GetSnapshot().PublishSuccessCount);
    }

    /// <summary>
    /// 何を確認しているか: configured diagnostics file があっても Core coordinator は file logging を直接行わないこと。
    /// </summary>
    [Fact]
    public void ProcessPacket_WithConfiguredDiagnosticsFile_DoesNotWriteDiagnosticsFileFromCoreCoordinator()
    {
        var configuredLogPath = Path.Combine(
            Path.GetTempPath(),
            $"runtime-host-005-configured-diagnostics-{Guid.NewGuid():N}.log");
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new TrackerCoordinatorRecordingTrackerPacketPublisher();
        var coordinator = Factory.CreateCoordinator(
            snapshotStore,
            publisher,
            [],
            Fixture.CreateSettings(profileName: "sim", reorderWindowNs: 0, mergeWindowNs: 0),
            Fixture.CreatePublisherOptions(),
            new TrackerDiagnosticsOptions
            {
                FilePath = configuredLogPath,
            });
        var receivedAt = new DateTimeOffset(2026, 5, 10, 18, 40, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            receivedAt);

        Assert.False(
            File.Exists(configuredLogPath),
            "Core TrackerCoordinator must not own DebugHost diagnostics file logging.");
        Assert.Single(publisher.Packets);
        Assert.Equal(1, snapshotStore.GetSnapshot().PublishSuccessCount);
    }
}
