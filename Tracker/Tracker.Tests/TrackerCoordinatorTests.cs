using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tracker.Core;
using Tracker.Server.Tracking;
using Tracker.Server.Vision;
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

    [Fact]
    public void ProcessPacket_WhenDiagnosticsDisabled_DoesNotWriteConfiguredDiagnosticsFile()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingTrackerPacketPublisher();
        var diagnosticsFilePath = Path.Combine(
            Path.GetTempPath(),
            $"tracker-diagnostics-disabled-{Guid.NewGuid():N}.log");
        var coordinator = CreateCoordinator(
            snapshotStore,
            publisher,
            [],
            fixture.CreateSettings(reorderWindowNs: 0, mergeWindowNs: 0),
            fixture.CreatePublisherOptions(),
            new TrackerDiagnosticsOptions
            {
                Enabled = false,
                FileEnabled = true,
                FilePath = diagnosticsFilePath,
            });
        var receivedAt = new DateTimeOffset(2026, 5, 10, 9, 30, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(
            TrackerContractTestData.CreateDetectionPacket(
                frameNumber: 10,
                cameraId: 1,
                balls: [TrackerContractTestData.CreateBall(x: 100, y: 50, confidence: 1.0f)],
                captureTimeSeconds: 1.000),
            receivedAt);

        Assert.False(File.Exists(diagnosticsFilePath));
    }

    [Fact]
    public void RequestProfileSwitch_WithoutPacket_DrainsControlOnlyUpdateAndClearsSnapshotBeforeObserverNotification()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingTrackerPacketPublisher();
        var observer = new RecordingTrackerObserver(snapshotStore);
        var initialPublisherOptions = fixture.CreatePublisherOptions(port: 10010);
        var coordinator = CreateCoordinator(
            snapshotStore,
            publisher,
            [observer],
            fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0),
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
            fixture.CreateResolvedOptions(
                fixture.CreateSettings(profileName: "fast", reorderWindowNs: 0, mergeWindowNs: 0),
                fixture.CreatePublisherOptions(port: 12000)),
            receivedAt.AddMilliseconds(50));

        var snapshot = snapshotStore.GetSnapshot();

        Assert.Equal("fast", snapshot.ActiveProfileName);
        Assert.Null(snapshot.LatestFrame);
        Assert.Null(snapshot.ReceivedAt);
        Assert.True(observer.LatestFrameWasClearedAtProfileSwitch);
        Assert.Equal(["profile:fast"], observer.Events);
        Assert.Equal(12000, publisher.CurrentOptions.Port);
    }

    [Fact]
    public void ProcessPacket_WithPendingProfileSwitch_PublishesCommittedFrameAfterApplyingNewProfileContext()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingTrackerPacketPublisher();
        var observer = new RecordingTrackerObserver(snapshotStore);
        var initialPublisherOptions = fixture.CreatePublisherOptions(port: 10010);
        var coordinator = CreateCoordinator(
            snapshotStore,
            publisher,
            [observer],
            fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0),
            initialPublisherOptions);
        var receivedAt = new DateTimeOffset(2026, 5, 10, 9, 5, 0, TimeSpan.Zero);

        coordinator.RequestProfileSwitch(
            fixture.CreateResolvedOptions(
                fixture.CreateSettings(profileName: "fast", reorderWindowNs: 0, mergeWindowNs: 0),
                fixture.CreatePublisherOptions(port: 12000)),
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

    [Fact]
    public void ProcessPacket_WithPacketCaptureSession_WritesDiagnosticsLogSidecar()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-capture-diagnostics-{Guid.NewGuid():N}");
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingTrackerPacketPublisher();
        var captureSession = CreateCaptureSession(captureDirectory);
        using var renderSnapshotWriter = new TrackerRenderSnapshotCaptureWriter(
            captureSession,
            NullLogger<TrackerRenderSnapshotCaptureWriter>.Instance);
        var coordinator = CreateCoordinator(
            snapshotStore,
            publisher,
            [],
            fixture.CreateSettings(profileName: "sim", reorderWindowNs: 0, mergeWindowNs: 0),
            fixture.CreatePublisherOptions(),
            new TrackerDiagnosticsOptions
            {
                Enabled = true,
                FileEnabled = true,
            },
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

    [Fact]
    public void ProcessPacket_WithPacketCaptureSessionAndConfiguredDiagnosticsFile_WritesBothLogs()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"vision-capture-diagnostics-both-{Guid.NewGuid():N}");
        var configuredLogPath = Path.Combine(
            Path.GetTempPath(),
            $"tracker-diagnostics-configured-{Guid.NewGuid():N}.log");
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingTrackerPacketPublisher();
        var captureSession = CreateCaptureSession(captureDirectory);
        var coordinator = CreateCoordinator(
            snapshotStore,
            publisher,
            [],
            fixture.CreateSettings(profileName: "sim", reorderWindowNs: 0, mergeWindowNs: 0),
            fixture.CreatePublisherOptions(),
            new TrackerDiagnosticsOptions
            {
                Enabled = true,
                FileEnabled = true,
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

    [Fact]
    public void RequestProfileSwitch_WithSameProfileButDifferentRuntimeTuning_AppliesNewEngineSettings()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingTrackerPacketPublisher();
        var coordinator = CreateCoordinator(
            snapshotStore,
            publisher,
            [],
            fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0),
            fixture.CreatePublisherOptions(port: 10010));
        var receivedAt = new DateTimeOffset(2026, 5, 10, 9, 15, 0, TimeSpan.Zero);

        coordinator.RequestProfileSwitch(
            fixture.CreateResolvedOptions(
                fixture.CreateSettings(
                    profileName: "default",
                    reorderWindowNs: 0,
                    mergeWindowNs: 0,
                    kickDetector: new TrackerKickDetectorOverrides
                    {
                        ContactMarginMm = 0d,
                    }),
                fixture.CreatePublisherOptions(port: 10010)),
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

    private TrackerCoordinator CreateCoordinator(
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

    private TrackerCoordinator CreateCoordinator(
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

    private TrackerCoordinator CreateCoordinator(
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

    private TrackerCoordinator CreateCoordinator(
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

    private TrackerCoordinator CreateCoordinator(
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
            settings,
            publisherOptions,
            diagnosticsOptions,
            snapshotStore,
            publisher,
            observers,
            NullLogger<TrackerCoordinator>.Instance,
            packetCaptureSession,
            renderSnapshotCaptureWriter);
    }

    private VisionPacketCaptureSession CreateCaptureSession(string captureDirectory)
    {
        return new VisionPacketCaptureSession(
            Options.Create(new VisionReceiverOptions
            {
                PacketCapture = new VisionPacketCaptureOptions
                {
                    Enabled = true,
                    DirectoryPath = captureDirectory,
                    FilePrefix = "test-vision",
                    FlushEachPacket = true,
                },
            }),
            Options.Create(new TrackerOptions { ActiveProfileName = "sim" }),
            fixture.CreateResolvedOptions(fixture.CreateSettings(profileName: "sim")),
            NullLogger<VisionPacketCaptureSession>.Instance);
    }

    private sealed class RecordingTrackerPacketPublisher : ITrackerPacketPublisher
    {
        public TrackerPublisherOptions CurrentOptions { get; private set; } = new();

        public List<TrackerWrapperPacket> Packets { get; } = [];

        public List<int> PublishedPorts { get; } = [];

        public void ApplyConfiguration(TrackerPublisherOptions options)
        {
            CurrentOptions = new TrackerPublisherOptions
            {
                PublishUdp = options.PublishUdp,
                MulticastAddress = options.MulticastAddress,
                Port = options.Port,
                SourceName = options.SourceName,
                Uuid = options.Uuid,
            };
        }

        public void Publish(TrackerWrapperPacket packet)
        {
            Packets.Add(packet.Clone());
            PublishedPorts.Add(CurrentOptions.Port);
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

        public bool LatestFrameWasClearedAtProfileSwitch { get; private set; }

        public void OnProfileSwitched(string profileName)
        {
            LatestFrameWasClearedAtProfileSwitch = snapshotStore.GetSnapshot().LatestFrame is null;
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
