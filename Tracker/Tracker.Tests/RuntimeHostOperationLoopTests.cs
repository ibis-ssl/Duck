using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tracker.Core;
using Tracker.RuntimeHost;
using Tracker.Tests.Contracts;

namespace Tracker.Tests;

/// <summary>
/// RUNTIME-HOST-009 の RuntimeHost operation loop と publish normal path を固定する。
/// </summary>
public class RuntimeHostOperationLoopTests
{
    /// <summary>
    /// 何を確認しているか: RuntimeHost operation loop の tick source が RuntimeHost 設定値を使うことを確認する。
    /// </summary>
    [Fact]
    public void RuntimeHostTickSource_UsesConfiguredOperationLoopInterval()
    {
        using var host = BuildHost(
        [
            KeyValuePair.Create<string, string?>(
                "RuntimeHost:OperationLoopIntervalMilliseconds",
                "25"),
        ]);

        var tickSource = Assert.IsType<RuntimeHostPeriodicTickSource>(
            host.Services.GetRequiredService<IRuntimeHostTickSource>());

        Assert.Equal(TimeSpan.FromMilliseconds(25), tickSource.Interval);
    }

    /// <summary>
    /// 何を確認しているか: RuntimeHost の Tracker section が official publish 宛先と packet metadata に binding されることを確認する。
    /// </summary>
    [Fact]
    public void RuntimeHostTrackerOptions_BindsPublishDestinationAndMetadata()
    {
        using var host = BuildHost(
        [
            KeyValuePair.Create<string, string?>("Tracker:PublishUdp", "false"),
            KeyValuePair.Create<string, string?>("Tracker:SourceName", "runtime-source"),
            KeyValuePair.Create<string, string?>("Tracker:Uuid", "runtime-uuid"),
            KeyValuePair.Create<string, string?>("Tracker:ActiveProfileName", "simulation"),
            KeyValuePair.Create<string, string?>("Tracker:Profiles:simulation:Publish:MulticastAddress", "239.10.20.30"),
            KeyValuePair.Create<string, string?>("Tracker:Profiles:simulation:Publish:Port", "12030"),
            KeyValuePair.Create<string, string?>("Tracker:Profiles:simulation:Engine:MergeWindowNs", "5000000"),
        ]);

        var resolvedOptions = host.Services.GetRequiredService<TrackerRuntimeResolvedOptions>();

        Assert.True(resolvedOptions.Enabled);
        Assert.False(resolvedOptions.PublisherOptions.PublishUdp);
        Assert.Equal("239.10.20.30", resolvedOptions.PublisherOptions.MulticastAddress);
        Assert.Equal(12030, resolvedOptions.PublisherOptions.Port);
        Assert.Equal("runtime-source", resolvedOptions.PublisherOptions.SourceName);
        Assert.Equal("runtime-uuid", resolvedOptions.PublisherOptions.Uuid);
        Assert.Equal("simulation", resolvedOptions.EngineSettings.ProfileName);
        Assert.Equal(5_000_000, resolvedOptions.EngineSettings.MergeWindowNs);
    }

    /// <summary>
    /// 何を確認しているか: RuntimeHost の active profile が missing の場合は default profile へ fallback せず明示失敗することを確認する。
    /// </summary>
    [Fact]
    public void RuntimeHostTrackerOptions_WithMissingActiveProfile_Throws()
    {
        using var host = BuildHost(
        [
            KeyValuePair.Create<string, string?>("Tracker:ActiveProfileName", "missing"),
        ]);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            host.Services.GetRequiredService<TrackerRuntimeResolvedOptions>());

        Assert.Contains("missing", ex.Message);
    }

    /// <summary>
    /// 何を確認しているか: blank active profile は default 扱いのまま、default profile がない場合は明示失敗することを確認する。
    /// </summary>
    [Fact]
    public void RuntimeHostTrackerOptions_WithBlankActiveProfileAndMissingDefaultProfile_Throws()
    {
        var options = new RuntimeTrackerOptions
        {
            ActiveProfileName = " ",
            Profiles = new Dictionary<string, RuntimeTrackerProfileOptions>(StringComparer.OrdinalIgnoreCase),
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RuntimeTrackerConfigurationResolver.Resolve(options));

        Assert.Contains("default", ex.Message);
    }

    /// <summary>
    /// 何を確認しているか: fake SSL-Vision input が coordinator、publisher、latest snapshot store へ到達することを確認する。
    /// </summary>
    [Fact]
    public void ProcessLatestPacket_WithFakeSslVisionInput_UpdatesCoordinatorPublisherAndSnapshotStore()
    {
        var packet = TrackerContractTestData.CreateDetectionPacket(
            frameNumber: 77,
            balls: [TrackerContractTestData.CreateBall(x: 100, y: 200)]);
        var receivedAt = new DateTimeOffset(2026, 5, 14, 19, 50, 0, TimeSpan.Zero);
        var committedFrame = new TrackerFrame
        {
            FrameNumber = 77,
            DataTimestampNs = 1_000_000_000,
            Metadata = new TrackerFrameMetadata
            {
                ProfileName = "default",
            },
        };
        var resolvedOptions = new TrackerRuntimeResolvedOptions
        {
            Enabled = true,
            EngineSettings = new TrackerEngineSettings
            {
                ProfileName = "default",
            },
            PublisherOptions = new TrackerPublisherOptions
            {
                PublishUdp = true,
                MulticastAddress = "224.5.23.2",
                Port = 10010,
                SourceName = "runtime-source",
                Uuid = "runtime-uuid",
            },
        };
        var engine = new RecordingEngine(new TrackerUpdateResult
        {
            CommittedFrames = [committedFrame],
            EmittedEvents =
            [
                new TrackerEvent
                {
                    Kind = TrackerEventKind.WorldFrameCommitted,
                    FrameNumber = committedFrame.FrameNumber,
                },
            ],
        });
        var snapshotStore = new TrackedSnapshotStore();
        var publisher = new RecordingPublisher();
        var coordinator = new TrackerCoordinator(
            engine,
            new TrackerPacketGenerator("runtime-source", "runtime-uuid"),
            resolvedOptions,
            snapshotStore,
            publisher,
            []);
        var packetBuffer = new RuntimeVisionPacketBuffer();
        var operationLoop = new RuntimeHostOperationLoop(packetBuffer, coordinator, resolvedOptions);

        packetBuffer.StorePacket(packet, receivedAt);
        var processed = operationLoop.ProcessLatestPacket();

        var snapshot = snapshotStore.GetSnapshot();
        var engineCall = Assert.Single(engine.Calls);
        var publishedPacket = Assert.Single(publisher.Packets);

        Assert.True(processed);
        Assert.NotNull(engineCall.Packet);
        Assert.Equal(77u, engineCall.Packet!.Detection.FrameNumber);
        Assert.Same(committedFrame, snapshot.LatestFrame);
        Assert.Equal(receivedAt, snapshot.ReceivedAt);
        Assert.Equal(1, snapshot.PublishSuccessCount);
        Assert.Equal((uint)77, publishedPacket.TrackedFrame.FrameNumber);
        Assert.Equal("runtime-source", publishedPacket.SourceName);
        Assert.Equal("runtime-uuid", publishedPacket.Uuid);
    }

    private static IHost BuildHost(IEnumerable<KeyValuePair<string, string?>> configurationValues)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = "Testing",
        });
        builder.Configuration.AddInMemoryCollection(configurationValues);
        builder.Services.AddRuntimeHost(builder.Configuration);
        return builder.Build();
    }

    private sealed class RecordingEngine(TrackerUpdateResult result) : ITrackerEngine
    {
        public List<EngineCall> Calls { get; } = [];

        public TrackerUpdateResult Update(
            SSL_WrapperPacket? packet,
            TrackerEngineSettings settings,
            TrackerProfileSwitchRequest? profileSwitchRequest = null)
        {
            Calls.Add(new EngineCall(packet?.Clone()));
            return result;
        }
    }

    private sealed record EngineCall(SSL_WrapperPacket? Packet);

    private sealed class RecordingPublisher : ITrackerPacketPublisher
    {
        public List<TrackerWrapperPacket> Packets { get; } = [];

        public void ApplyConfiguration(TrackerPublisherOptions options)
        {
        }

        public void Publish(TrackerWrapperPacket packet)
        {
            Packets.Add(packet.Clone());
        }
    }
}
