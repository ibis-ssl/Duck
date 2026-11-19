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
    /// <summary>DI で構成した実 decode 経路が同じ診断 ledger と HTTP 応答へ反映されることを確認します。</summary>
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
        Assert.False(health.Response.TrackerReceiverEnabled);
        Assert.Equal("NoObservation", Assert.Single(health.Response.TrackerReceiveSources).Reason);

        var trackedPacket = new TrackerWrapperPacket
        {
            SourceName = "TIGERs",
            Uuid = "tigers-uuid",
            TrackedFrame = new TrackedFrame { FrameNumber = 9 },
        };
        diagnostics.RecordTrackerPacket(
            diagnostics.BeginTrackerReceive(),
            new TrackerPacketAdapter(trackedPacket),
            new IPEndPoint(IPAddress.Parse("172.20.0.4"), 11010));
        var trackerSnapshot = Assert.Single(RuntimeVisionDiagnosticsHttpService.GetHealth(
            diagnostics, configuration).Response.TrackerReceiveSources);
        Assert.Equal(ledger.Identity, trackerSnapshot.Identity);
        Assert.Equal("TIGERs", trackerSnapshot.SourceName);
        Assert.Equal("172.20.0.4:11010", trackerSnapshot.RemoteEndpoint);
        Assert.Equal(1, trackerSnapshot.SuccessfullyDecodedCount);
        using var healthJson = JsonDocument.Parse(JsonSerializer.Serialize(
            RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration).Response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.True(healthJson.RootElement.TryGetProperty("trackerDiagnostics", out _));
        Assert.False(healthJson.RootElement.GetProperty("trackerReceiverEnabled").GetBoolean());
    }

    /// <summary>開発用 HTTP の到達性と未帰属の受信観測を、所有元付き準備完了と混同しないことを確認します。</summary>
    [Fact]
    public void DevelopmentDiagnosticsReportUnattributedDetectionWithoutOwnerReadiness()
    {
        var clock = new ManualClock();
        var ledger = new ServiceReceiveDiagnostics(timeProvider: clock);
        var diagnostics = new RuntimeVisionReceiverDiagnostics(ledger);
        var buffer = new RuntimeVisionPacketBuffer();
        var configuration = HostRuntimeConfig();

        var health = RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration);
        Assert.Equal(200, health.StatusCode);
        Assert.False(health.Response.IsReady);
        Assert.Null(health.Response.Diagnostics.Identity);
        Assert.False(health.Response.Diagnostics.IsReady);
        Assert.Equal("NoObservation", health.Response.Diagnostics.Reason);

        var geometryOnly = new SSL_WrapperPacket { Geometry = new SSL_GeometryData() }.ToByteArray();
        Assert.True(RuntimeVisionReceiverService.ProcessDatagram(geometryOnly, DateTimeOffset.UtcNow,
            buffer, diagnostics, NullLogger.Instance));
        health = RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration);
        Assert.Equal(200, health.StatusCode);
        Assert.Equal(1, health.Response.VisionPacketsReceived);
        Assert.Equal(0, diagnostics.VisionPacketsReceivedTotal);
        Assert.Equal(0, health.Response.Diagnostics.SuccessfullyDecodedCount);
        Assert.False(health.Response.IsReady);
        Assert.Null(health.Response.Diagnostics.Identity);
        Assert.False(health.Response.Diagnostics.IsReady);

        Assert.False(RuntimeVisionReceiverService.ProcessDatagram([0xff, 0xff], DateTimeOffset.UtcNow,
            buffer, diagnostics, NullLogger.Instance));
        health = RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration);
        Assert.Equal(200, health.StatusCode);
        Assert.False(health.Response.IsReady);
        Assert.Equal(1, health.Response.Diagnostics.DecodeFailureCount);

        Assert.True(RuntimeVisionReceiverService.ProcessDatagram(Detection(), DateTimeOffset.UtcNow,
            buffer, diagnostics, NullLogger.Instance));
        health = RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration);
        Assert.Equal(200, health.StatusCode);
        Assert.False(health.Response.IsReady);
        Assert.Equal(1, diagnostics.VisionPacketsReceivedTotal);
        Assert.Equal(1, health.Response.Diagnostics.SuccessfullyDecodedCount);
        Assert.Null(health.Response.Diagnostics.Identity);
        Assert.False(health.Response.Diagnostics.IsReady);
        Assert.Equal("Unattributed", health.Response.Diagnostics.Reason);

        clock.Advance(ServiceReceiveDiagnostics.FreshnessWindow);
        Assert.Equal(200, RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration).StatusCode);
        clock.Advance(TimeSpan.FromTicks(1));
        health = RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration);
        Assert.Equal(200, health.StatusCode);
        Assert.False(health.Response.IsReady);
        Assert.False(health.Response.Diagnostics.IsReady);

        Assert.True(RuntimeVisionReceiverService.ProcessDatagram(Detection(), DateTimeOffset.UtcNow,
            buffer, diagnostics, NullLogger.Instance));
        Assert.Equal(200, RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration).StatusCode);
        Assert.False(RuntimeVisionReceiverService.ProcessDatagram([0xff, 0xff], DateTimeOffset.UtcNow,
            buffer, diagnostics, NullLogger.Instance));
        health = RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration);
        Assert.Equal(200, health.StatusCode);
        Assert.False(health.Response.IsReady);
        Assert.Equal(DiagnosticDecodeStatus.Failed, health.Response.Diagnostics.DecodeStatus);
    }

    /// <summary>health 応答に Tracker の publish 成功・失敗カウンターが含まれることを確認します。</summary>
    [Fact]
    public void HealthIncludesTrackerPublicationCounters()
    {
        var diagnostics = new RuntimeVisionReceiverDiagnostics();
        var store = new Tracker.Core.TrackedSnapshotStore("sim");
        store.RecordPublishSuccess();
        store.RecordPublishFailure();

        var output = RuntimeVisionDiagnosticsHttpService.GetHealth(
            diagnostics, Config(container: false), trackerSnapshotStore: store).Response.TrackerOutput;

        Assert.NotNull(output);
        Assert.Equal("sim", output.ActiveProfileName);
        Assert.Equal(1, output.PublishSuccessCount);
        Assert.Equal(1, output.PublishFailureCount);
    }

    /// <summary>Tracker 受信 health が observer 無効と観測欠落を区別することを確認します。</summary>
    /// <param name="receiverEnabled">Tracker の受信観測を有効にするかどうかを指定します。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrackerReceiveHealthDistinguishesDisabledObserverFromMissingObservation(bool receiverEnabled)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Testing:HostRuntimeProfile"] = "true",
            ["Tracker:Receive:Enabled"] = receiverEnabled.ToString(),
        }).Build();
        var diagnostics = new RuntimeVisionReceiverDiagnostics();

        var health = RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration).Response;

        Assert.Equal(receiverEnabled, health.TrackerReceiverEnabled);
        Assert.Equal("NoObservation", Assert.Single(health.TrackerReceiveSources).Reason);
    }

    /// <summary>所有元付き受信では識別元の置換時に準備完了をリセットし、再受信後に回復することを確認します。</summary>
    [Fact]
    public void OwnedReadinessResetsWhenDiagnosticIdentityIsReplaced()
    {
        var clock = new ManualClock();
        var ledger = new ServiceReceiveDiagnostics(Identity(), clock);
        var diagnostics = new RuntimeVisionReceiverDiagnostics(ledger);
        var buffer = new RuntimeVisionPacketBuffer();
        var configuration = Config(container: true);

        for (var i = 0; i < 2; i++)
            Assert.True(RuntimeVisionReceiverService.ProcessDatagram(Detection(), DateTimeOffset.UtcNow,
                buffer, diagnostics, NullLogger.Instance));
        Assert.Equal(200, RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration).StatusCode);

        var nextIdentity = new ResourceDiagnosticIdentity("run", "duck-host-runtime", new string('b', 64), 2);
        diagnostics.ReplaceIdentity(nextIdentity);
        var health = RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration);
        Assert.Equal(503, health.StatusCode);
        Assert.False(health.Response.IsReady);
        Assert.Equal(nextIdentity, health.Response.Diagnostics.Identity);
        Assert.False(health.Response.Diagnostics.IsReady);
        Assert.Null(health.Response.Diagnostics.DecodeAgeUpperBound);

        for (var i = 0; i < 2; i++)
            Assert.True(RuntimeVisionReceiverService.ProcessDatagram(Detection(), DateTimeOffset.UtcNow,
                buffer, diagnostics, NullLogger.Instance));
        health = RuntimeVisionDiagnosticsHttpService.GetHealth(diagnostics, configuration);
        Assert.Equal(200, health.StatusCode);
        Assert.True(health.Response.IsReady);
        Assert.Equal(nextIdentity, health.Response.Diagnostics.Identity);
    }

    /// <summary>geometry と空 wrapper は raw storage に保持される一方、detection 件数を増やさないことを確認します。</summary>
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

    /// <summary>identity handoff 前に decode された packet が新世代の受信件数へ加算されないことを確認します。</summary>
    [Fact]
    public void TrackerPacketDecodedBeforeIdentityHandoffCannotCountInNewGeneration()
    {
        var oldIdentity = new ResourceDiagnosticIdentity("run-1", "duck", new string('a', 64), 1);
        var newIdentity = new ResourceDiagnosticIdentity("run-1", "duck", new string('b', 64), 2);
        var ledger = new ServiceReceiveDiagnostics(oldIdentity);
        var diagnostics = new RuntimeVisionReceiverDiagnostics(ledger);
        var deserializer = new RuntimeTrackerPacketDeserializer(diagnostics);
        var packetBytes = new TrackerWrapperPacket
        {
            SourceName = "TIGERs",
            Uuid = "tigers-uuid",
            TrackedFrame = new TrackedFrame { FrameNumber = 1 },
        }.ToByteArray();

        Assert.True(deserializer.TryDeserialize(packetBytes, out var decoded));
        Assert.NotNull(decoded);
        diagnostics.ReplaceIdentity(newIdentity);
        deserializer.RecordPacket(decoded, new IPEndPoint(IPAddress.Parse("172.20.0.4"), 11010));

        var snapshot = Assert.Single(ledger.GetSnapshots("tracker"));
        Assert.Equal(newIdentity, snapshot.Identity);
        Assert.Equal(0, snapshot.SuccessfullyDecodedCount);
    }

    /// <summary>Tracker packet の decode 失敗が現在の受信 epoch の診断へ現れることを確認します。</summary>
    [Fact]
    public void TrackerPacketDecodeFailureIsVisibleInTheCurrentReceiveEpoch()
    {
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics(
            new ResourceDiagnosticIdentity("run-1", "duck", new string('c', 64), 1)));
        var deserializer = new RuntimeTrackerPacketDeserializer(diagnostics);
        var packetBytes = new TrackerWrapperPacket
        {
            SourceName = "TIGERs",
            Uuid = "tigers-uuid",
            TrackedFrame = new TrackedFrame { FrameNumber = 1 },
        }.ToByteArray();

        Assert.True(deserializer.TryDeserialize(packetBytes, out var packet));
        deserializer.RecordPacket(packet!, new IPEndPoint(IPAddress.Parse("172.20.0.4"), 11010));
        Assert.False(deserializer.TryDeserialize([0xff, 0xff], out _));

        var snapshot = Assert.Single(diagnostics.TrackerSnapshots);
        Assert.Equal(1, snapshot.DecodeFailureCount);
        Assert.Equal(1, snapshot.SuccessfullyDecodedCount);
        Assert.Equal(DiagnosticDecodeStatus.Failed, snapshot.DecodeStatus);
    }

    /// <summary>parse 遅延と停止した入力の状態が HTTP health に反映されることを確認します。</summary>
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

    /// <summary>Debug Vision が実 parser を使い、geometry を detection 件数から除外することを確認します。</summary>
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

    /// <summary>Debug 比較モードが入力元を分離し、実際の decode callback を使うことを確認します。</summary>
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

    /// <summary>owner がない比較モードでは従来 status を維持しつつ、strict health を通過できないことを確認します。</summary>
    /// <param name="container">コンテナーモードを有効にするかどうかを指定します。</param>
    /// <param name="expectedStatus">HTTP ヘルス応答に期待する状態コードです。</param>
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

    /// <summary>strict 比較が UUID fallback を維持し、役割 identity の競合を拒否することを確認します。</summary>
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

    /// <summary>Debug Tracker の epoch 切替後に遅れて届いた decode packet を拒否することを確認します。</summary>
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

    /// <summary>HTTP 機能を opt-in しない従来の起動動作が維持されることを確認します。</summary>
    /// <param name="container">コンテナーモードを有効にするかどうかを指定します。</param>
    /// <param name="comparison">比較モードを有効にするかどうかを指定します。</param>
    /// <param name="enabled">対象機能を明示的に有効にするかどうかを指定します。</param>
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

    /// <summary>owner 帰属情報の欠落を厳格に扱うのは明示的な container mode に限ることを確認します。</summary>
    /// <param name="container">コンテナーモードを有効にするかどうかを指定します。</param>
    /// <param name="expectedStatus">HTTP ヘルス応答に期待する状態コードです。</param>
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

    /// <summary>実 HTTP pipeline が container 用 HTTP と従来の HTTPS redirect を維持することを確認します。</summary>
    /// <param name="container">コンテナーモードを有効にするかどうかを指定します。</param>
    /// <returns>非同期試験の完了を表します。</returns>
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

    private static IConfiguration HostRuntimeConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Testing:ContainerMode"] = "false",
            ["Testing:HostRuntimeProfile"] = "true",
        }).Build();

    /// <summary>診断時刻を決定的に進めるための制御可能な時計です。</summary>
    private sealed class ManualClock : TimeProvider
    {
        private long ticks = TimeSpan.TicksPerSecond;
        /// <summary>テスト用時計のタイムスタンプが 1 秒あたり何 tick 進むかを示します。</summary>
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        /// <summary>制御可能な時計の現在のタイムスタンプを取得します。</summary>
        /// <returns>手動で進めた時計の tick 値です。</returns>
        public override long GetTimestamp() => ticks;
        /// <summary>制御可能な時計を指定時間だけ進めます。</summary>
        /// <param name="duration">時計を進める時間です。</param>
        public void Advance(TimeSpan duration) => ticks += duration.Ticks;
    }
}
