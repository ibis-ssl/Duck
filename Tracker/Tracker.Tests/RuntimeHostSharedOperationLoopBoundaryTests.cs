using Tracker.Core;

namespace Tracker.Tests;

/// <summary>
/// RUNTIME-HOST-005 の Core shared operation loop 境界を固定する。
/// </summary>
public class RuntimeHostSharedOperationLoopBoundaryTests
{
    /// <summary>
    /// 何を確認しているか: Core runtime source が DebugHost / UI / diagnostics / capture writer 境界を参照しないこと。
    /// </summary>
    [Fact]
    public void CoreRuntimeSource_DoesNotReferenceDebugHostUiDiagnosticsOrCaptureWriters()
    {
        var runtimeRoot = RepositoryPath("Tracker", "Tracker.Core", "Runtime");

        Assert.True(
            Directory.Exists(runtimeRoot),
            "RUNTIME-HOST-005 requires Tracker.Core/Runtime as the shared runtime boundary.");

        var sourceText = ReadSourceText(runtimeRoot);
        var forbiddenTokens = new[]
        {
            "Tracker.DebugHost",
            "Microsoft.AspNetCore.Components",
            "Blazor",
            "diagnostics/capture writer",
            "TrackerDiagnosticsLogReader",
            "TrackerDiagnosticsOptions",
            "VisionPacketCaptureSession",
            "TrackerRenderSnapshot",
            "TrackerRenderSnapshotCaptureWriter",
            "TrackerPacketSnapshotLog",
            "TrackerPacketSnapshotLogWriter",
            "TrackerPacketSnapshotLogReader",
            "TrackerSnapshotAlignmentLog",
            "TrackerSnapshotAlignmentLogWriter",
            "TrackerSnapshotAlignmentLogReader",
        };
        var hits = forbiddenTokens
            .Where(token => sourceText.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            hits.Length == 0,
            $"Core runtime source must stay UI independent. Found forbidden references: {string.Join(", ", hits)}");
    }

    /// <summary>
    /// 何を確認しているか: shared coordinator type が Tracker.Core assembly / namespace に存在すること。
    /// </summary>
    [Fact]
    public void SharedCoordinatorType_LivesInTrackerCoreAssemblyAndNamespace()
    {
        Assert.Same(typeof(TrackerCoreAssemblyMarker).Assembly, typeof(TrackerCoordinator).Assembly);
        Assert.Equal("Tracker.Core", typeof(TrackerCoordinator).Namespace);
    }

    /// <summary>
    /// 何を確認しているか: committed frame が latest snapshot、official publish、observer 通知へ event 順で反映されること。
    /// </summary>
    [Fact]
    public void ProcessPacket_WithCommittedFrame_UpdatesSnapshotPublishesAndNotifiesObserversInEventOrder()
    {
        var frame = CreateFrame(10, "default");
        var engine = new QueueingTrackerEngine(new TrackerUpdateResult
        {
            CommittedFrames = [frame],
            EmittedEvents =
            [
                new TrackerEvent { Kind = TrackerEventKind.WorldFrameCommitted, FrameNumber = frame.FrameNumber },
                new TrackerEvent { Kind = TrackerEventKind.ContactChanged, FrameNumber = frame.FrameNumber },
            ],
        });
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingPublisher();
        var observer = new RecordingObserver(snapshotStore, publisher);
        var coordinator = CreateCoordinator(engine, snapshotStore, publisher, observer);
        var receivedAt = new DateTimeOffset(2026, 5, 14, 10, 0, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(packet: null, receivedAt);

        var snapshot = snapshotStore.GetSnapshot();

        Assert.Same(frame, snapshot.LatestFrame);
        Assert.Equal(receivedAt, snapshot.ReceivedAt);
        Assert.Equal(1, snapshot.PublishSuccessCount);
        Assert.Equal(0, snapshot.PublishFailureCount);
        Assert.Equal((uint)10, Assert.Single(publisher.Packets).TrackedFrame.FrameNumber);
        Assert.Equal(["world:10", "contact:10"], observer.Events);
    }

    /// <summary>
    /// 何を確認しているか: profile switch が control-only update で drain され、snapshot clear と publisher config 反映が observer 通知前に完了すること。
    /// </summary>
    [Fact]
    public void RequestProfileSwitch_DrainsControlOnlyUpdateAndAppliesStateBeforeObserverNotification()
    {
        var engine = new QueueingTrackerEngine(
            new TrackerUpdateResult
            {
                CommittedFrames = [CreateFrame(1, "default")],
                EmittedEvents = [new TrackerEvent { Kind = TrackerEventKind.WorldFrameCommitted, FrameNumber = 1 }],
            },
            new TrackerUpdateResult
            {
                EmittedEvents = [new TrackerEvent { Kind = TrackerEventKind.ProfileSwitched, ProfileName = "fast" }],
            });
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingPublisher();
        var observer = new RecordingObserver(snapshotStore, publisher);
        var coordinator = CreateCoordinator(engine, snapshotStore, publisher, observer);
        var receivedAt = new DateTimeOffset(2026, 5, 14, 10, 5, 0, TimeSpan.Zero);

        _ = coordinator.ProcessPacket(packet: null, receivedAt);
        observer.Events.Clear();

        coordinator.RequestProfileSwitch(
            new TrackerRuntimeResolvedOptions
            {
                Enabled = true,
                EngineSettings = new TrackerEngineSettings
                {
                    ProfileName = "fast",
                },
                PublisherOptions = new TrackerPublisherOptions
                {
                    Port = 12000,
                    MulticastAddress = "239.1.2.3",
                    SourceName = "fast-source",
                    Uuid = "fast-uuid",
                },
            },
            receivedAt.AddMilliseconds(1));

        var snapshot = snapshotStore.GetSnapshot();

        Assert.Equal("fast", snapshot.ActiveProfileName);
        Assert.Null(snapshot.LatestFrame);
        Assert.Null(snapshot.ReceivedAt);
        Assert.Equal(12000, publisher.CurrentOptions.Port);
        Assert.Equal(["profile:fast"], observer.Events);
        Assert.True(observer.ProfileNotificationSawClearedSnapshot);
        Assert.True(observer.ProfileNotificationSawPublisherConfig);
        Assert.Null(engine.Calls[1].Packet);
        Assert.Equal("fast", engine.Calls[1].ProfileSwitchRequest?.ProfileName);
    }

    /// <summary>
    /// 何を確認しているか: publisher 例外で loop が落ちず、publish failure count が増えること。
    /// </summary>
    [Fact]
    public void ProcessPacket_WhenPublisherThrows_RecordsFailureAndContinues()
    {
        var frame = CreateFrame(20, "default");
        var engine = new QueueingTrackerEngine(new TrackerUpdateResult
        {
            CommittedFrames = [frame],
            EmittedEvents = [new TrackerEvent { Kind = TrackerEventKind.WorldFrameCommitted, FrameNumber = frame.FrameNumber }],
        });
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new ThrowingPublisher();
        var observer = new RecordingObserver(snapshotStore, publisher);
        var coordinator = CreateCoordinator(engine, snapshotStore, publisher, observer);

        var result = coordinator.ProcessPacket(
            packet: null,
            new DateTimeOffset(2026, 5, 14, 10, 10, 0, TimeSpan.Zero));

        var snapshot = snapshotStore.GetSnapshot();

        Assert.Single(result.CommittedFrames);
        Assert.Same(frame, snapshot.LatestFrame);
        Assert.Equal(0, snapshot.PublishSuccessCount);
        Assert.Equal(1, snapshot.PublishFailureCount);
        Assert.Equal(["world:20"], observer.Events);
    }

    private static TrackerCoordinator CreateCoordinator(
        ITrackerEngine engine,
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher,
        ITrackerObserver observer)
    {
        return new TrackerCoordinator(
            engine,
            new TrackerPacketGenerator("ibis-test", "ibis-test"),
            new TrackerRuntimeResolvedOptions
            {
                Enabled = true,
                EngineSettings = new TrackerEngineSettings
                {
                    ProfileName = "default",
                },
                PublisherOptions = new TrackerPublisherOptions
                {
                    Port = 10010,
                    MulticastAddress = "224.5.23.2",
                    SourceName = "ibis-test",
                    Uuid = "ibis-test",
                },
            },
            snapshotStore,
            publisher,
            [observer]);
    }

    private static TrackerFrame CreateFrame(uint frameNumber, string profileName)
    {
        return new TrackerFrame
        {
            FrameNumber = frameNumber,
            DataTimestampNs = frameNumber * 1_000_000,
            Metadata = new TrackerFrameMetadata
            {
                ProfileName = profileName,
            },
        };
    }

    private static string ReadSourceText(string root)
    {
        var sourceFiles = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !NormalizePath(path).Contains("/bin/", StringComparison.Ordinal))
            .Where(path => !NormalizePath(path).Contains("/obj/", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        return string.Join(Environment.NewLine, sourceFiles.Select(File.ReadAllText));
    }

    private static string RepositoryPath(params string[] segments)
    {
        return Path.Combine([FindRepositoryRoot(), .. segments]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Tracker", "Tracker.Tests", "Tracker.Tests.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root containing Tracker/Tracker.Tests/Tracker.Tests.csproj was not found.");
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private sealed class QueueingTrackerEngine(params TrackerUpdateResult[] results) : ITrackerEngine
    {
        private readonly Queue<TrackerUpdateResult> queuedResults = new(results);

        public List<EngineCall> Calls { get; } = [];

        public TrackerUpdateResult Update(
            SSL_WrapperPacket? packet,
            TrackerEngineSettings settings,
            TrackerProfileSwitchRequest? profileSwitchRequest = null)
        {
            Calls.Add(new EngineCall(packet, settings, profileSwitchRequest));

            return queuedResults.Count == 0
                ? new TrackerUpdateResult()
                : queuedResults.Dequeue();
        }
    }

    private sealed record EngineCall(
        SSL_WrapperPacket? Packet,
        TrackerEngineSettings Settings,
        TrackerProfileSwitchRequest? ProfileSwitchRequest);

    private class RecordingPublisher : ITrackerPacketPublisher
    {
        public TrackerPublisherOptions CurrentOptions { get; private set; } = new();

        public List<TrackerWrapperPacket> Packets { get; } = [];

        public virtual void ApplyConfiguration(TrackerPublisherOptions options)
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

        public virtual void Publish(TrackerWrapperPacket packet)
        {
            Packets.Add(packet.Clone());
        }
    }

    private sealed class ThrowingPublisher : RecordingPublisher
    {
        public override void Publish(TrackerWrapperPacket packet)
        {
            throw new InvalidOperationException("publish failed for test");
        }
    }

    private sealed class RecordingObserver(
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher) : ITrackerObserver
    {
        public List<string> Events { get; } = [];

        public bool ProfileNotificationSawClearedSnapshot { get; private set; }

        public bool ProfileNotificationSawPublisherConfig { get; private set; }

        public void OnProfileSwitched(string profileName)
        {
            var snapshot = snapshotStore.GetSnapshot();
            ProfileNotificationSawClearedSnapshot = snapshot.LatestFrame is null && snapshot.ActiveProfileName == profileName;
            ProfileNotificationSawPublisherConfig = publisher is RecordingPublisher recording && recording.CurrentOptions.Port == 12000;
            Events.Add($"profile:{profileName}");
        }

        public void OnGeometryReset()
        {
            Events.Add("geometry-reset");
        }

        public void OnWorldFrameCommitted(TrackerFrame frame)
        {
            Events.Add($"world:{frame.FrameNumber}");
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
