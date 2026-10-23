using System.Net;
using System.Text.Json;
using Duck.Testing.Diagnostics;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Tracker.RuntimeHost;
using TrackerConnectionLib;

namespace Tracker.Tests;

/// <summary>実ホスト構成と受信処理の試験。BCL の補助試験へ混ぜず、標準 CI で実行する。</summary>
public sealed class HostDiagnosticsCompositionTests
{
    [Fact]
    public void RuntimeDiConnectsActualDecodeToTheSameLedgerAndHttpResponse()
    {
        var configuration = Config(container: true);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRuntimeHost(configuration);
        using var provider = services.BuildServiceProvider();
        var diagnostics = provider.GetRequiredService<RuntimeVisionReceiverDiagnostics>();
        var ledger = provider.GetRequiredService<ServiceReceiveDiagnostics>();
        var buffer = new RuntimeVisionPacketBuffer();
        Assert.Equal(503, RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration).StatusCode);
        for (var i = 0; i < 2; i++)
            Assert.True(RuntimeVisionReceiverService.ProcessDatagram(Detection(), DateTimeOffset.UtcNow,
                buffer, diagnostics, NullLogger.Instance));
        Assert.Equal(2, Assert.Single(ledger.GetSnapshots("vision")).SuccessfullyDecodedCount);
        var health = RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration);
        Assert.Equal(200, health.StatusCode);
        Assert.Equal(2, health.Response.VisionPacketsReceived);
        Assert.Equal(ledger.Identity, health.Response.Diagnostics.Identity);
    }

    [Fact]
    public void RuntimeGeometryAndEmptyWrappersPreserveRawStorageWithoutDetectionProgress()
    {
        var clock = new ManualClock();
        var ledger = new ServiceReceiveDiagnostics(Identity(), clock);
        var diagnostics = new RuntimeVisionReceiverDiagnostics(ledger);
        var buffer = new RuntimeVisionPacketBuffer();
        var geometry = new SSL_WrapperPacket { Geometry = new SSL_GeometryData() };
        Assert.True(RuntimeVisionReceiverService.ProcessDatagram(geometry.ToByteArray(), DateTimeOffset.UtcNow,
            buffer, diagnostics, NullLogger.Instance));
        Assert.True(buffer.TryTakeLatestBatch(out var packets));
        Assert.NotNull(Assert.Single(packets).Packet.Geometry);
        Assert.True(RuntimeVisionReceiverService.ProcessDatagram([], DateTimeOffset.UtcNow,
            buffer, diagnostics, NullLogger.Instance));
        Assert.Equal(0, diagnostics.VisionPacketsReceivedTotal);
        Assert.Equal(2, diagnostics.DecodedWrapperPacketsReceivedTotal);
        Assert.Equal(2, RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, Config(false)).Response.VisionPacketsReceived);
        Assert.Equal(0, RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, Config(true)).Response.VisionPacketsReceived);
        Assert.Null(diagnostics.Snapshot.LastReceivedAt);
        Assert.False(RuntimeVisionReceiverService.ProcessDatagram([0xff, 0xff], DateTimeOffset.UtcNow,
            buffer, diagnostics, NullLogger.Instance));
        Assert.Equal(1, diagnostics.Snapshot.DecodeFailureCount);
    }

    [Fact]
    public void RuntimeParseDelayAndStoppedInputRemainVisibleToHttpHealth()
    {
        var clock = new ManualClock();
        var ledger = new ServiceReceiveDiagnostics(Identity(), clock);
        var diagnostics = new RuntimeVisionReceiverDiagnostics(ledger);
        var buffer = new RuntimeVisionPacketBuffer();
        RuntimeVisionReceiverService.ProcessDatagram(Detection(), DateTimeOffset.UtcNow,
            buffer, diagnostics, NullLogger.Instance);
        var attempt = diagnostics.BeginReceive();
        clock.Advance(TimeSpan.FromSeconds(2));
        RuntimeVisionReceiverService.ProcessDatagram(Detection(), DateTimeOffset.UtcNow,
            buffer, diagnostics, NullLogger.Instance, attempt);
        var health = RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, Config(true));
        Assert.Equal(TimeSpan.FromSeconds(2), health.Response.Diagnostics.DecodeAgeUpperBound);
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(503, RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, Config(true)).StatusCode);
    }

    [Fact]
    public void DebugVisionUsesActualParserAndKeepsGeometryOutOfDetectionCount()
    {
        var ledger = new ServiceReceiveDiagnostics(Identity());
        var epoch = ledger.BeginEpoch("vision");
        var store = new VisionPacketStore();
        var endpoint = new IPEndPoint(IPAddress.Loopback, 10020);
        var geometry = new SSL_WrapperPacket { Geometry = new SSL_GeometryData() };
        Assert.NotNull(VisionReceiverService.DecodeDatagram(geometry.ToByteArray(), endpoint,
            DateTimeOffset.UtcNow, store, ledger, ledger.BeginReceive(epoch)));
        Assert.Equal(1, store.GetSnapshot().PacketCount);
        Assert.NotNull(store.GetSnapshot().Geometry);
        Assert.Equal(0, Assert.Single(ledger.GetSnapshots("vision")).SuccessfullyDecodedCount);
        for (var i = 0; i < 2; i++)
            Assert.NotNull(VisionReceiverService.DecodeDatagram(Detection(), endpoint,
                DateTimeOffset.UtcNow, store, ledger, ledger.BeginReceive(epoch)));
        Assert.Equal(200, Status(DebugHostHealthEndpoints.GetVisionHealthResult(ledger, Config(true))));
        Assert.Null(VisionReceiverService.DecodeDatagram([0xff, 0xff], endpoint, DateTimeOffset.UtcNow,
            store, ledger, ledger.BeginReceive(epoch)));
        Assert.Equal(3, store.GetSnapshot().PacketCount);
        Assert.Equal(503, Status(DebugHostHealthEndpoints.GetVisionHealthResult(ledger, Config(true))));
    }

    [Fact]
    public void DebugComparisonKeepsIndependentSourcesAndUsesTheActualDecodeCallback()
    {
        var clock = new ManualClock();
        var ledger = new ServiceReceiveDiagnostics(Identity(), clock);
        var deserializer = new DiagnosticTrackerDeserializer(ledger);
        var manager = new MultiTrackerManager<TrackerPacketAdapter>();
        using var store = new ExternalTrackerSnapshotStore(manager);
        var vision = new VisionPacketStore();
        var configuration = Config(true, comparison: true);
        for (var i = 0; i < 2; i++)
        {
            Feed("ibis", "duck-id", 1);
            Feed("TIGERs", "tigers-id", 2);
            Feed("ER-FORCE", "erforce-id", 3);
        }
        var result = DebugHostHealthEndpoints.GetComparisonHealthResult(store, vision, configuration, ledger);
        Assert.Equal(200, Status(result));
        var response = Assert.IsAssignableFrom<IValueHttpResult>(result).Value!;
        Assert.Equal(3, Assert.IsAssignableFrom<IEnumerable<TrackerDifferencePairResult>>(
            response.GetType().GetProperty("Pairs")!.GetValue(response)).Count());
        Assert.Equal(3, ledger.GetSnapshots("tracker").Count);
        Assert.True(deserializer.TryDeserialize(Packet("ibis", "duck-id").ToByteArray(), out var duplicate));
        TrackerConnectionLibReceiverHostedService.ProcessDecodedPacket(duplicate!,
            new IPEndPoint(IPAddress.Loopback, 12001), manager, deserializer);
        Assert.Equal(3, ledger.GetSnapshots("tracker").Count);
        Assert.Equal(200, Status(DebugHostHealthEndpoints.GetComparisonHealthResult(store, vision, configuration, ledger)));

        Assert.False(deserializer.TryDeserialize([0xff, 0xff], out _));
        Assert.Equal(503, Status(DebugHostHealthEndpoints.GetComparisonHealthResult(store, vision, configuration, ledger)));
        Feed("ibis", "duck-id", 1);
        Assert.Equal(503, Status(DebugHostHealthEndpoints.GetComparisonHealthResult(store, vision, configuration, ledger)));

        void Feed(string name, string uuid, int endpoint)
        {
            Assert.True(deserializer.TryDeserialize(Packet(name, uuid).ToByteArray(), out var decoded));
            TrackerConnectionLibReceiverHostedService.ProcessDecodedPacket(decoded!,
                new IPEndPoint(IPAddress.Loopback, 11010 + endpoint), manager, deserializer);
        }
    }

    [Theory]
    [InlineData(false, 200)]
    [InlineData(true, 503)]
    public void ComparisonWithoutOwnerKeepsLegacyStatusButCannotPassStrictHealth(bool container, int expectedStatus)
    {
        var ledger = new ServiceReceiveDiagnostics();
        var deserializer = new DiagnosticTrackerDeserializer(ledger);
        var manager = new MultiTrackerManager<TrackerPacketAdapter>();
        using var store = new ExternalTrackerSnapshotStore(manager);
        foreach (var name in new[] { "ibis", "TIGERs", "ER-FORCE" })
            for (var i = 0; i < 2; i++)
            {
                Assert.True(deserializer.TryDeserialize(Packet(name, name).ToByteArray(), out var packet));
                TrackerConnectionLibReceiverHostedService.ProcessDecodedPacket(packet!,
                    new IPEndPoint(IPAddress.Loopback, 11010), manager, deserializer);
            }
        Assert.Equal(expectedStatus, Status(DebugHostHealthEndpoints.GetComparisonHealthResult(
            store, new VisionPacketStore(), Config(container, true), ledger)));
        Assert.All(ledger.GetSnapshots("tracker"), source => Assert.False(source.IsReady));
    }

    [Fact]
    public void StrictComparisonRetainsUuidFallbackAndRejectsConflictingRoleIdentities()
    {
        var ledger = new ServiceReceiveDiagnostics(Identity());
        var deserializer = new DiagnosticTrackerDeserializer(ledger);
        var manager = new MultiTrackerManager<TrackerPacketAdapter>();
        using var store = new ExternalTrackerSnapshotStore(manager);
        var names = new[] { "ibis", "TIGERs", "ER-FORCE" };
        for (var role = 0; role < names.Length; role++)
            Feed(names[role], 11011 + role);
        Assert.Equal(200, Status(DebugHostHealthEndpoints.GetComparisonHealthResult(
            store, new VisionPacketStore(), Config(true, true), ledger)));
        Feed("ibis", 12011);
        Assert.Equal(503, Status(DebugHostHealthEndpoints.GetComparisonHealthResult(
            store, new VisionPacketStore(), Config(true, true), ledger)));

        void Feed(string name, int port)
        {
            for (var i = 0; i < 2; i++)
            {
                Assert.True(deserializer.TryDeserialize(Packet(name, "").ToByteArray(), out var packet));
                TrackerConnectionLibReceiverHostedService.ProcessDecodedPacket(packet!,
                    new IPEndPoint(IPAddress.Loopback, port), manager, deserializer);
            }
        }
    }

    [Fact]
    public void DebugTrackerEpochRejectsLateDecodedPackets()
    {
        var ledger = new ServiceReceiveDiagnostics(Identity());
        var deserializer = new DiagnosticTrackerDeserializer(ledger);
        var manager = new MultiTrackerManager<TrackerPacketAdapter>();
        Assert.True(deserializer.TryDeserialize(Packet("ibis", "id").ToByteArray(), out var old));
        deserializer.BeginEpoch();
        TrackerConnectionLibReceiverHostedService.ProcessDecodedPacket(old!,
            new IPEndPoint(IPAddress.Loopback, 11010), manager, deserializer);
        Assert.Equal(0, Assert.Single(ledger.GetSnapshots("tracker")).SuccessfullyDecodedCount);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void RuntimeHttpOptInPreservesLegacyStartup(bool container, bool comparison, bool enabled)
    {
        var configuration = Config(container, comparison);
        Assert.Equal(enabled, RuntimeVisionDiagnosticsHttpService.IsEnabled(configuration));
        Assert.Equal(container ? "http://+:39108/" : "http://127.0.0.1:39108/",
            RuntimeVisionDiagnosticsHttpService.ListenerPrefix(configuration));
    }

    [Theory]
    [InlineData(true, 503)]
    [InlineData(false, 200)]
    public void MissingOwnerAttributionIsStrictOnlyInExplicitContainerMode(bool container, int expectedStatus)
    {
        var ledger = new ServiceReceiveDiagnostics();
        var diagnostics = new RuntimeVisionReceiverDiagnostics(ledger);
        var epoch = ledger.BeginEpoch("vision");
        ledger.RecordSuccess(ledger.BeginReceive(epoch));
        ledger.RecordSuccess(ledger.BeginReceive(epoch));
        var health = RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, Config(container));
        Assert.Equal(expectedStatus, health.StatusCode);
        Assert.False(health.Response.IsReady);
        Assert.Equal(expectedStatus, Status(DebugHostHealthEndpoints.GetVisionHealthResult(ledger, Config(container))));
        Assert.False(Assert.Single(ledger.GetSnapshots("vision")).IsReady);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RealHttpPipelinePreservesContainerHttpAndLegacyHttpsRedirect(bool container)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Testing:ContainerMode"] = container.ToString(),
        });
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddHttpsRedirection(options => options.HttpsPort = 44443);
        DebugHostHealthEndpoints.ConfigureInternalHttp(builder);
        if (container) Assert.Equal("http://0.0.0.0:8080", builder.Configuration["urls"]);
        // CI の試験ではループバックの動的ポートを使い、製品の受信機は起動しない。
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        DebugHostHealthEndpoints.UseTransportMiddleware(app);
        var ledger = new ServiceReceiveDiagnostics();
        app.MapGet("/", () => Results.Text("transport fixture"));
        app.MapGet("/health/vision", () => DebugHostHealthEndpoints.GetVisionHealthResult(ledger, app.Configuration));
        app.MapBlazorHub();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
            using var page = await client.GetAsync("/");
            using var health = await client.GetAsync("/health/vision");
            using var negotiate = await client.PostAsync("/_blazor/negotiate?negotiateVersion=1", new StringContent(""));
            if (container)
            {
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
                Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);
                using var json = JsonDocument.Parse(await negotiate.Content.ReadAsStringAsync());
                Assert.True(json.RootElement.TryGetProperty("connectionToken", out _));
                Assert.Null(page.Headers.Location);
            }
            else
            {
                Assert.Equal(HttpStatusCode.TemporaryRedirect, page.StatusCode);
                Assert.Equal(HttpStatusCode.TemporaryRedirect, health.StatusCode);
                Assert.Equal(HttpStatusCode.TemporaryRedirect, negotiate.StatusCode);
                Assert.Equal("https", page.Headers.Location!.Scheme);
            }
        }
        finally { await app.StopAsync(); }
    }

    private static int? Status(IResult result) => Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode;
    private static byte[] Detection() =>
        new SSL_WrapperPacket { Detection = new SSL_DetectionFrame() }.ToByteArray();
    private static TrackerWrapperPacket Packet(string name, string uuid) =>
        new() { SourceName = name, Uuid = uuid, TrackedFrame = new TrackedFrame { FrameNumber = 1, Timestamp = 1 } };
    private static ResourceDiagnosticIdentity Identity() => new("run", "debug-host", new string('a', 64), 1);
    private static IConfiguration Config(bool container, bool comparison = false) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Testing:ContainerMode"] = container.ToString(),
            ["Testing:Mode"] = comparison ? "comparison" : "base",
            ["Testing:Diagnostics:RunId"] = "run",
            ["Testing:Diagnostics:ResourceId"] = "duck",
            ["Testing:Diagnostics:ContainerId"] = new string('a', 64),
            ["Testing:Diagnostics:Generation"] = "1",
        }).Build();

    private sealed class ManualClock : TimeProvider
    {
        private long ticks = TimeSpan.TicksPerSecond;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public void Advance(TimeSpan duration) => ticks += duration.Ticks;
    }
}
