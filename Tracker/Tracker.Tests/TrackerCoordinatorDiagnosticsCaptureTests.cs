using Microsoft.Extensions.Logging.Abstractions;
using Tracker.Core;
using Tracker.Server.Tracking;
using Tracker.Server.Vision;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TrackerCoordinator の diagnostics capture と render snapshot sidecar 出力 contract を検証する。
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
    /// 何を確認しているか: packet capture session 有効時に diagnostics sidecar と render snapshot が capture directory へ出ることを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WithPacketCaptureSession_WritesDiagnosticsLogSidecar()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-capture-diagnostics-{Guid.NewGuid():N}");
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new TrackerCoordinatorRecordingTrackerPacketPublisher();
        var captureSession = Factory.CreateCaptureSession(captureDirectory);
        using var renderSnapshotWriter = new TrackerRenderSnapshotCaptureWriter(
            captureSession,
            NullLogger<TrackerRenderSnapshotCaptureWriter>.Instance);
        var coordinator = Factory.CreateCoordinator(
            snapshotStore,
            publisher,
            [],
            Fixture.CreateSettings(profileName: "sim", reorderWindowNs: 0, mergeWindowNs: 0),
            Fixture.CreatePublisherOptions(),
            new TrackerDiagnosticsOptions(),
            captureSession,
            renderSnapshotWriter);
        var receivedAt = new DateTimeOffset(2026, 5, 10, 18, 30, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            receivedAt);

        var logPath = Assert.Single(Directory.GetFiles(captureDirectory, "test-vision-*.tracker-diagnostics.log"));
        var metadataPath = Assert.Single(Directory.GetFiles(captureDirectory, "test-vision-*.metadata.json"));
        var renderSnapshotPath = Assert.Single(Directory.GetFiles(captureDirectory, "test-vision-*.render-snapshots.jsonl.gz"));
        var logText = File.ReadAllText(logPath);
        var metadataText = File.ReadAllText(metadataPath);

        Assert.Contains("Tracker diagnostics profile=sim", logText);
        Assert.Contains(logPath, metadataText);
        Assert.True(new FileInfo(renderSnapshotPath).Length > 0);
    }

    /// <summary>
    /// 何を確認しているか: capture を再有効化した後、新しい diagnostics sidecar に出力が切り替わることを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WhenCaptureIsReenabled_WritesDiagnosticsToNewSidecar()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-capture-diagnostics-reenabled-{Guid.NewGuid():N}");
        var runtimeControl = new VisionPacketCaptureRuntimeControl(initialEnabled: true);
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new TrackerCoordinatorRecordingTrackerPacketPublisher();
        var captureSession = Factory.CreateCaptureSession(captureDirectory, runtimeControl: runtimeControl);
        var coordinator = Factory.CreateCoordinator(
            snapshotStore,
            publisher,
            [],
            Fixture.CreateSettings(profileName: "sim", reorderWindowNs: 0, mergeWindowNs: 0),
            Fixture.CreatePublisherOptions(),
            new TrackerDiagnosticsOptions(),
            captureSession);
        var receivedAt = new DateTimeOffset(2026, 5, 10, 18, 32, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            receivedAt);

        runtimeControl.SetEnabled(false);
        captureSession.Stop();
        runtimeControl.SetEnabled(true);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 20,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 200, confidence: 1.0f)],
                captureTimeSeconds: 1.100),
            receivedAt.AddSeconds(2));

        var logPaths = Directory.GetFiles(captureDirectory, "test-vision-*.tracker-diagnostics.log");

        Assert.Equal(2, logPaths.Length);
        Assert.All(logPaths, logPath => Assert.Contains("Tracker diagnostics profile=sim", File.ReadAllText(logPath)));
    }

    /// <summary>
    /// 何を確認しているか: capture 無効時でも default diagnostics log が capture directory 配下に作られることを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WithCaptureDisabled_WritesDefaultDiagnosticsLogUnderCaptureDirectory()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-capture-default-diagnostics-{Guid.NewGuid():N}");
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new TrackerCoordinatorRecordingTrackerPacketPublisher();
        var captureSession = Factory.CreateCaptureSession(captureDirectory, enabled: false);
        var coordinator = Factory.CreateCoordinator(
            snapshotStore,
            publisher,
            [],
            Fixture.CreateSettings(profileName: "sim", reorderWindowNs: 0, mergeWindowNs: 0),
            Fixture.CreatePublisherOptions(),
            new TrackerDiagnosticsOptions(),
            captureSession);
        var receivedAt = new DateTimeOffset(2026, 5, 10, 18, 35, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            receivedAt);

        var logPath = Assert.Single(Directory.GetFiles(captureDirectory, "tracker-diagnostics-*.log"));
        var logText = File.ReadAllText(logPath);

        Assert.Contains("Tracker diagnostics profile=sim", logText);
        Assert.Empty(Directory.GetFiles(captureDirectory, "test-vision-*.jsonl.gz"));
    }

    /// <summary>
    /// 何を確認しているか: capture sidecar と configured diagnostics file の両方へ diagnostics が書かれることを確認する。
    /// </summary>
    [Fact]
    public void ProcessPacket_WithPacketCaptureSessionAndConfiguredDiagnosticsFile_WritesBothLogs()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-capture-diagnostics-both-{Guid.NewGuid():N}");
        var configuredLogPath = Path.Combine(
            Path.GetTempPath(),
            $"tracker-diagnostics-configured-{Guid.NewGuid():N}.log");
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new TrackerCoordinatorRecordingTrackerPacketPublisher();
        var captureSession = Factory.CreateCaptureSession(captureDirectory);
        var coordinator = Factory.CreateCoordinator(
            snapshotStore,
            publisher,
            [],
            Fixture.CreateSettings(profileName: "sim", reorderWindowNs: 0, mergeWindowNs: 0),
            Fixture.CreatePublisherOptions(),
            new TrackerDiagnosticsOptions
            {
                FilePath = configuredLogPath,
            },
            captureSession);
        var receivedAt = new DateTimeOffset(2026, 5, 10, 18, 40, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            receivedAt);

        var sidecarLogPath = Assert.Single(Directory.GetFiles(captureDirectory, "test-vision-*.tracker-diagnostics.log"));
        var sidecarLogText = File.ReadAllText(sidecarLogPath);
        var configuredLogText = File.ReadAllText(configuredLogPath);

        Assert.Contains("Tracker diagnostics profile=sim", sidecarLogText);
        Assert.Contains("Tracker diagnostics profile=sim", configuredLogText);
    }
}
