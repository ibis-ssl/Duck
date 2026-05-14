using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tracker.Core;
using Tracker.DebugHost.Tracking;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

public class TrackerProfileRequestServiceTests : IClassFixture<TrackerContractFixture>
{
    private readonly TrackerContractFixture fixture;

    public TrackerProfileRequestServiceTests(TrackerContractFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// 何を確認しているか: profile switch request が named profile を解決し coordinator 経由で反映されること。
    /// </summary>
    [Fact]
    public void RequestProfileSwitch_ResolvesNamedProfileAndAppliesItThroughCoordinator()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingTrackerPacketPublisher();
        var coordinator = new TrackerCoordinator(
            fixture.CreateEngine(),
            fixture.CreatePacketGenerator(),
            fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0),
            fixture.CreatePublisherOptions(port: 10010),
            new TrackerDiagnosticsOptions(),
            snapshotStore,
            publisher,
            [],
            NullLogger<TrackerCoordinator>.Instance);
        var service = new TrackerProfileRequestService(
            Options.Create(new TrackerOptions
            {
                ActiveProfileName = "default",
                Profiles = new Dictionary<string, TrackerProfileOptions>(StringComparer.OrdinalIgnoreCase)
                {
                    ["default"] = new(),
                    ["fast"] = new()
                    {
                        Publish = new TrackerPublishProfileOptions
                        {
                            MulticastAddress = "239.1.2.3",
                            Port = 12000,
                        },
                        Engine = new TrackerEngineProfileOptions
                        {
                            ReorderWindowNs = 0,
                            MergeWindowNs = 0,
                            GeometryResetFieldLengthThresholdMm = 500,
                            GeometryResetFieldWidthThresholdMm = 500,
                        },
                    },
                },
            }),
            coordinator);

        service.RequestProfileSwitch("fast", new DateTimeOffset(2026, 5, 10, 9, 10, 0, TimeSpan.Zero));

        var snapshot = snapshotStore.GetSnapshot();

        Assert.Equal("fast", snapshot.ActiveProfileName);
        Assert.Equal(12000, publisher.CurrentOptions.Port);
        Assert.Equal("239.1.2.3", publisher.CurrentOptions.MulticastAddress);
    }

    /// <summary>
    /// 何を確認しているか: unknown profile への切り替え要求が profile 名付きで失敗すること。
    /// </summary>
    [Fact]
    public void RequestProfileSwitch_WithUnknownProfile_Throws()
    {
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingTrackerPacketPublisher();
        var coordinator = new TrackerCoordinator(
            fixture.CreateEngine(),
            fixture.CreatePacketGenerator(),
            fixture.CreateSettings(profileName: "default", reorderWindowNs: 0, mergeWindowNs: 0),
            fixture.CreatePublisherOptions(port: 10010),
            new TrackerDiagnosticsOptions(),
            snapshotStore,
            publisher,
            [],
            NullLogger<TrackerCoordinator>.Instance);
        var service = new TrackerProfileRequestService(
            Options.Create(new TrackerOptions()),
            coordinator);

        var ex = Assert.Throws<InvalidOperationException>(() => service.RequestProfileSwitch("missing"));

        Assert.Contains("missing", ex.Message);
    }

    private sealed class RecordingTrackerPacketPublisher : ITrackerPacketPublisher
    {
        public TrackerPublisherOptions CurrentOptions { get; private set; } = new();

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
        }
    }
}
