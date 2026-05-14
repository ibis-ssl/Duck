using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tracker.Core;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: TrackerCoordinator test が coordinator と依存 object を同じ fixture 設定で生成できることを支える。
/// </summary>
internal sealed class TrackerCoordinatorTestFactory
{
    private readonly TrackerContractFixture fixture;

    public TrackerCoordinatorTestFactory(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    public TrackerCoordinator CreateCoordinator(
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher,
        params ITrackerObserver[] observers)
    {
        return CreateCoordinator(
            snapshotStore,
            publisher,
            observers,
            fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0),
            fixture.CreatePublisherOptions());
    }

    public TrackerCoordinator CreateCoordinator(
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher,
        IReadOnlyList<ITrackerObserver> observers,
        TrackerEngineSettings settings)
    {
        return CreateCoordinator(
            snapshotStore,
            publisher,
            observers,
            settings,
            fixture.CreatePublisherOptions());
    }

    public TrackerCoordinator CreateCoordinator(
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher,
        IReadOnlyList<ITrackerObserver> observers,
        TrackerEngineSettings settings,
        TrackerPublisherOptions publisherOptions)
    {
        return CreateCoordinator(
            snapshotStore,
            publisher,
            observers,
            settings,
            publisherOptions,
            new TrackerDiagnosticsOptions());
    }

    public TrackerCoordinator CreateCoordinator(
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher,
        IReadOnlyList<ITrackerObserver> observers,
        TrackerEngineSettings settings,
        TrackerPublisherOptions publisherOptions,
        TrackerDiagnosticsOptions diagnosticsOptions,
        VisionPacketCaptureSession? packetCaptureSession = null)
    {
        return CreateCoordinator(
            snapshotStore,
            publisher,
            observers,
            settings,
            publisherOptions,
            diagnosticsOptions,
            packetCaptureSession,
            renderSnapshotCaptureWriter: null);
    }

    public TrackerCoordinator CreateCoordinator(
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher,
        IReadOnlyList<ITrackerObserver> observers,
        TrackerEngineSettings settings,
        TrackerPublisherOptions publisherOptions,
        TrackerDiagnosticsOptions diagnosticsOptions,
        VisionPacketCaptureSession? packetCaptureSession,
        TrackerRenderSnapshotCaptureWriter? renderSnapshotCaptureWriter)
    {
        return new TrackerCoordinator(
            fixture.CreateEngine(),
            fixture.CreatePacketGenerator(),
            new TrackerRuntimeResolvedOptions
            {
                Enabled = true,
                EngineSettings = settings,
                PublisherOptions = publisherOptions,
            },
            snapshotStore,
            publisher,
            observers);
    }

    public VisionPacketCaptureSession CreateCaptureSession(
        string captureDirectory,
        bool enabled = true,
        VisionPacketCaptureRuntimeControl? runtimeControl = null)
    {
        return new VisionPacketCaptureSession(
            Options.Create(new VisionReceiverOptions
            {
                PacketCapture = new VisionPacketCaptureOptions
                {
                    Enabled = enabled,
                    DirectoryPath = captureDirectory,
                    FilePrefix = "test-vision",
                    FlushEachPacket = true,
                },
            }),
            Options.Create(new TrackerOptions { ActiveProfileName = "sim" }),
            fixture.CreateResolvedOptions(fixture.CreateSettings(profileName: "sim")),
            NullLogger<VisionPacketCaptureSession>.Instance,
            runtimeControl);
    }
}
