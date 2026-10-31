using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Duck.Testing.RefereeDriver.Hosting;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tracker.Tests;

/// <summary>標準構成の起動・観測・停止を、通信境界だけを置換した実HTTPで確認する。</summary>
public sealed class RefereeDriverHostCompositionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("192.0.2.8")]
    public async Task EnabledHostComposesSingleReceiveApiAndHttpFromExplicitConfiguration(string? interfaceAddress)
    {
        await using var fixture = Fixture.Build(interfaceAddress: interfaceAddress);
        await fixture.App.StartAsync();

        Assert.Equal(1, fixture.Transports.CreateCalls);
        var options = Assert.Single(fixture.Transports.ReceiverOptions);
        Assert.Equal("224.5.23.1", options.MulticastAddress);
        Assert.Equal(11003, options.Port);
        Assert.Equal(interfaceAddress is null ? null : IPAddress.Parse(interfaceAddress), options.InterfaceAddress);
        Assert.Same(fixture.Transports.Clock, fixture.App.Services.GetRequiredService<TimeProvider>());
        var receive = fixture.App.Services.GetRequiredService<RefereeReceiveService>();
        Assert.Same(receive, fixture.App.Services.GetRequiredService<IRefereeCommandSource>());
        Assert.NotNull(fixture.App.Services.GetRequiredService<GameControllerStateObserver>());
        Assert.NotNull(fixture.App.Services.GetRequiredService<RefereeApiMatcher>());
        await fixture.Transports.Receiver.AtReceiveCall(3).WaitAsync(TimeSpan.FromSeconds(5));
        using var response = await fixture.WaitForHealthyAsync();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.True(root.GetProperty("isReady").GetBoolean());
        var diagnostic = root.GetProperty("diagnostic");
        Assert.Equal(new string('c', 64), diagnostic.GetProperty("identity").GetProperty("containerId").GetString());
        Assert.Equal(2, diagnostic.GetProperty("identity").GetProperty("generation").GetInt64());
        Assert.Equal(2, diagnostic.GetProperty("successfullyDecodedCount").GetInt64());
        Assert.Equal(fixture.Transports.Clock.GetUtcNow(), diagnostic.GetProperty("lastReceivedAt").GetDateTimeOffset());
        Assert.Equal(fixture.App.Services.GetRequiredService<ServiceReceiveDiagnostics>().Identity,
            receive.GetCurrentObservation()!.Diagnostic.Identity);
        Assert.All(fixture.Transports.ConnectedUris, uri => Assert.Equal(Fixture.InternalUri, uri));
        Assert.True(fixture.Transports.ConnectCalls >= 1);
        await fixture.Transports.AtConnectCall(2).WaitAsync(TimeSpan.FromSeconds(5));
        using var refreshed = await fixture.WaitForHealthyAsync();
        using var refreshedJson = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        Assert.True(refreshedJson.RootElement.GetProperty("api").GetProperty("requestSequence").GetInt64() >= 2);
        Assert.Equal(0, fixture.Transports.SendCalls);
        Assert.Empty(fixture.Control.Actions);
        Assert.Equal(1, fixture.Transports.Receiver.MaxActiveReceives);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("not-a-boolean")]
    public async Task DisabledDefaultStartsNoReceiverApiOrAutomaticControl(string? enabled)
    {
        await using var fixture = Fixture.Build(enabled: enabled, endpoint: null, containerId: null);
        await fixture.App.StartAsync();
        using var response = await fixture.Client.GetAsync("/health/referee");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, fixture.Transports.CreateCalls);
        Assert.Equal(0, fixture.Transports.ConnectCalls);
        Assert.Equal(0, fixture.Transports.SendCalls);
        Assert.Empty(fixture.Control.Actions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-uri")]
    [InlineData("http://game-controller:8082/api/control")]
    [InlineData("ws://localhost:8082/api/control")]
    [InlineData("ws://127.0.0.1:8082/api/control")]
    [InlineData("ws://[::1]:8082/api/control")]
    [InlineData("ws://referee-driver:39110/api/control")]
    public async Task InvalidOrSelfContainerUriIsRejectedWithoutStartingApi(string? endpoint)
    {
        var transports = new Transports();
        var control = new RecordingControl();
        WebApplication? app = null;
        try
        {
            var error = await Record.ExceptionAsync(async () =>
            {
                var builder = Fixture.CreateBuilder(transports, control, "true", true, endpoint, null, new string('c', 64));
                app = RefereeDriverApplication.Build(builder);
                app.Urls.Clear();
                app.Urls.Add("http://127.0.0.1:0");
                await app.StartAsync();
            });
            Assert.NotNull(error);
            Assert.True(error is ArgumentException or FormatException or OptionsValidationException,
                "不正な構成として拒否し、未実装や待機期限切れを理由にしない。");
            Assert.Equal(0, transports.ConnectCalls);
            Assert.Empty(control.Actions);
        }
        finally
        {
            transports.CancelPending();
            if (app is not null)
            {
                try { await app.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                finally { await app.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }
            }
            Assert.Equal(transports.CreateCalls, transports.Receiver.DisposeCalls);
            transports.Dispose();
        }
    }

    [Fact]
    public async Task NonContainerExplicitLoopbackUriRemainsUsable()
    {
        var endpoint = new Uri("ws://127.0.0.1:8082/api/control");
        await using var fixture = Fixture.Build(container: false, endpoint: endpoint.ToString());
        await fixture.App.StartAsync();

        Assert.Equal(1, fixture.Transports.CreateCalls);
        await fixture.Transports.Receiver.AtReceiveCall(3).WaitAsync(TimeSpan.FromSeconds(5));
        using var response = await fixture.WaitForHealthyAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(fixture.Transports.ConnectedUris, uri => Assert.Equal(endpoint, uri));
        Assert.Equal(0, fixture.Transports.SendCalls);
        Assert.Empty(fixture.Control.Actions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short-id")]
    public async Task StandardConfigurationCannotTurnUnattributedInputReady(string? containerId)
    {
        await using var fixture = Fixture.Build(containerId: containerId);
        await fixture.App.StartAsync();

        Assert.Equal(1, fixture.Transports.CreateCalls);
        await fixture.Transports.Receiver.AtReceiveCall(3).WaitAsync(TimeSpan.FromSeconds(5));
        using var response = await fixture.Client.GetAsync("/health/referee");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("isReady").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("diagnostic").GetProperty("identity").ValueKind);
        Assert.Equal(2, json.RootElement.GetProperty("diagnostic").GetProperty("successfullyDecodedCount").GetInt64());
        Assert.Equal(0, fixture.Transports.ConnectCalls);
        Assert.Equal(0, fixture.Transports.SendCalls);
    }

    [Fact]
    public async Task StopCancelsReceiveAndPendingApiAndDisposesOwnedReceiverOnce()
    {
        await using var fixture = Fixture.Build(pendingApi: true);
        await fixture.App.StartAsync();

        Assert.Equal(1, fixture.Transports.CreateCalls);
        await fixture.Transports.Receiver.AtReceiveCall(3).WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Transports.PendingApiEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.App.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(fixture.Transports.Receiver.CancellationObserved);
        Assert.True(fixture.Transports.ApiCancellationObserved);
        Assert.Equal(0, fixture.Transports.Receiver.ActiveReceives);
        Assert.Equal(0, fixture.Transports.ActiveApiReceives);
        Assert.Equal(1, fixture.Transports.Receiver.DisposeCalls);
        Assert.All(fixture.Transports.Sockets, socket => Assert.Equal(1, socket.DisposeCalls));
        Assert.False(fixture.App.Services.GetRequiredService<RefereeApiMatcher>().GetCurrent().IsReady);
        Assert.All(fixture.App.Services.GetRequiredService<ServiceReceiveDiagnostics>().GetSnapshots("referee"),
            snapshot => Assert.False(snapshot.IsReady));
        await fixture.App.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, fixture.Transports.Receiver.DisposeCalls);
        Assert.Equal(0, fixture.Transports.SendCalls);
        Assert.Empty(fixture.Control.Actions);
    }

    /// <summary>標準Buildを使い、試験だけがHTTPを動的loopbackへ置き換える。</summary>
    private sealed class Fixture(WebApplication app, Transports transports, RecordingControl control) : IAsyncDisposable
    {
        internal static readonly Uri InternalUri = new("ws://game-controller:8082/api/control");
        internal WebApplication App { get; } = app;
        internal Transports Transports { get; } = transports;
        internal RecordingControl Control { get; } = control;
        private HttpClient? client;
        internal HttpClient Client => client ??= new HttpClient
        {
            BaseAddress = new Uri(App.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single()),
            Timeout = TimeSpan.FromSeconds(5),
        };

        internal static Fixture Build(string? enabled = "true", bool container = true,
            string? endpoint = "ws://game-controller:8082/api/control", string? interfaceAddress = null,
            string? containerId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            bool pendingApi = false)
        {
            var transports = new Transports(pendingApi);
            var control = new RecordingControl();
            try
            {
                var builder = CreateBuilder(transports, control, enabled, container, endpoint, interfaceAddress, containerId);
                var app = RefereeDriverApplication.Build(builder);
                app.Urls.Clear();
                app.Urls.Add("http://127.0.0.1:0");
                return new(app, transports, control);
            }
            catch
            {
                transports.CancelPending();
                transports.Dispose();
                throw;
            }
        }

        internal static WebApplicationBuilder CreateBuilder(
            Transports transports, RecordingControl control, string? enabled, bool container,
            string? endpoint, string? interfaceAddress, string? containerId)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = [],
                EnvironmentName = Environments.Production,
            });
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RefereeDriver:Enabled"] = enabled,
                ["Testing:ContainerMode"] = container.ToString(),
                ["RefereeDriver:GameControllerUri"] = endpoint,
                ["RefereeDriver:InterfaceAddress"] = interfaceAddress,
                ["Testing:Diagnostics:RunId"] = "host-composition",
                ["Testing:Diagnostics:ResourceId"] = "referee-driver",
                ["Testing:Diagnostics:ContainerId"] = containerId,
                ["Testing:Diagnostics:Generation"] = "2",
            });
            builder.Services.AddSingleton<TimeProvider>(transports.Clock);
            builder.Services.AddSingleton(transports.Factories);
            builder.Services.AddSingleton<IGameControllerControlClient>(control);
            return builder;
        }

        /// <summary>後続のAPI周期処理を有界に待つ。現在のRed根拠は、この待機前の生成件数assertionとする。</summary>
        internal async Task<HttpResponseMessage> WaitForHealthyAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (true)
            {
                var response = await Client.GetAsync("/health/referee", timeout.Token);
                if (response.StatusCode == HttpStatusCode.OK) return response;
                response.Dispose();
                await Task.Delay(10, timeout.Token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            client?.Dispose();
            try { await App.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            finally
            {
                Transports.CancelPending();
                try { await App.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }
                finally
                {
                    Assert.Equal(0, Transports.Receiver.ActiveReceives);
                    Assert.Equal(0, Transports.ActiveApiReceives);
                    Transports.Dispose();
                }
            }
        }
    }

    /// <summary>標準組成には実処理を残し、OSの通信口の生成と接続だけを置換する。</summary>
    private sealed class Transports : IDisposable
    {
        internal StaticClock Clock { get; } = new();
        internal Receiver Receiver { get; } = new();
        internal ConcurrentBag<RefereeUdpReceiverOptions> ReceiverOptions { get; } = [];
        internal ConcurrentBag<Uri> ConnectedUris { get; } = [];
        internal ConcurrentBag<ApiSocket> Sockets { get; } = [];
        internal TaskCompletionSource PendingApiEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal RefereeDriverTransportFactories Factories { get; }
        internal int CreateCalls => Volatile.Read(ref createCalls);
        internal int ConnectCalls => Volatile.Read(ref connectCalls);
        internal Task AtConnectCall(int count) => connections.GetOrAdd(count,
            _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        internal int SendCalls => Sockets.Sum(s => s.SendCalls);
        internal bool ApiCancellationObserved => Sockets.Any(s => s.CancellationObserved);
        internal int ActiveApiReceives => Sockets.Sum(s => s.ActiveReceives);
        private int createCalls;
        private int connectCalls;
        private readonly ConcurrentDictionary<int, TaskCompletionSource> connections = new();

        internal Transports(bool pendingApi = false)
        {
            Receiver.Supply(Packet().ToByteArray());
            Receiver.Supply(Packet().ToByteArray());
            Factories = new(options =>
            {
                Interlocked.Increment(ref createCalls);
                ReceiverOptions.Add(options);
                return new(Receiver, Receiver);
            }, (uri, token) =>
            {
                token.ThrowIfCancellationRequested();
                ConnectedUris.Add(uri);
                var socket = new ApiSocket(pendingApi, PendingApiEntered);
                Sockets.Add(socket);
                connections.GetOrAdd(Interlocked.Increment(ref connectCalls),
                    _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
                return Task.FromResult<WebSocket>(socket);
            });
        }

        internal void CancelPending()
        {
            Receiver.CancelPending();
            foreach (var socket in Sockets) socket.CancelPending();
        }
        public void Dispose()
        {
            CancelPending();
            Receiver.DisposeEmergencyToken();
            foreach (var socket in Sockets) socket.DisposeEmergencyToken();
        }
    }

    private sealed class Receiver : IRefereeDatagramReceiver, IAsyncDisposable
    {
        private readonly Channel<ReadOnlyMemory<byte>> packets = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> calls = new();
        private readonly CancellationTokenSource emergency = new();
        private int received, active, maxActive, disposed, cancelled;
        internal int ActiveReceives => Volatile.Read(ref active);
        internal int MaxActiveReceives => Volatile.Read(ref maxActive);
        internal int DisposeCalls => Volatile.Read(ref disposed);
        internal bool CancellationObserved => Volatile.Read(ref cancelled) != 0;
        internal void Supply(ReadOnlyMemory<byte> bytes) => Assert.True(packets.Writer.TryWrite(bytes));
        internal Task AtReceiveCall(int count) => calls.GetOrAdd(count,
            _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> capture, CancellationToken token)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, emergency.Token);
            var current = Interlocked.Increment(ref active);
            Interlocked.Exchange(ref maxActive, Math.Max(current, MaxActiveReceives));
            calls.GetOrAdd(Interlocked.Increment(ref received),
                _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            try
            {
                var bytes = await packets.Reader.ReadAsync(linked.Token);
                return new(bytes, new IPEndPoint(IPAddress.Parse("192.0.2.70"), 11003), capture());
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested) Interlocked.Exchange(ref cancelled, 1);
                throw;
            }
            finally { Interlocked.Decrement(ref active); }
        }
        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref disposed);
            return ValueTask.CompletedTask;
        }
        internal void CancelPending() => emergency.Cancel();
        internal void DisposeEmergencyToken() => emergency.Dispose();
    }

    private sealed class ApiSocket(bool pending, TaskCompletionSource entered) : WebSocket
    {
        private readonly CancellationTokenSource emergency = new();
        private readonly TaskCompletionSource never = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            matchState = new { command = new { type = "HALT", forTeam = "UNKNOWN" }, gameState = new { type = "HALT" } },
        });
        private int active, cancelled, disposed, sends;
        internal int ActiveReceives => Volatile.Read(ref active);
        internal int DisposeCalls => Volatile.Read(ref disposed);
        internal int SendCalls => Volatile.Read(ref sends);
        internal bool CancellationObserved => Volatile.Read(ref cancelled) != 0;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => DisposeCalls == 0 ? WebSocketState.Open : WebSocketState.Closed;
        public override string? SubProtocol => null;
        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, emergency.Token);
            Interlocked.Increment(ref active);
            try
            {
                if (pending)
                {
                    entered.TrySetResult();
                    await never.Task.WaitAsync(linked.Token);
                }
                linked.Token.ThrowIfCancellationRequested();
                payload.AsSpan().CopyTo(buffer.AsSpan());
                return new(payload.Length, WebSocketMessageType.Text, true);
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested) Interlocked.Exchange(ref cancelled, 1);
                throw;
            }
            finally { Interlocked.Decrement(ref active); }
        }
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type,
            bool endOfMessage, CancellationToken token)
        {
            Interlocked.Increment(ref sends);
            throw new InvalidOperationException("観測用の接続から送信しない。");
        }
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken token) =>
            Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken token) =>
            Task.CompletedTask;
        public override void Dispose() => Interlocked.Increment(ref disposed);
        internal void CancelPending() => emergency.Cancel();
        internal void DisposeEmergencyToken() => emergency.Dispose();
    }

    private sealed class RecordingControl : IGameControllerControlClient
    {
        internal ConcurrentBag<GameControllerContinueAction> Actions { get; } = [];
        public Task SendContinueActionAsync(GameControllerContinueAction action, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }

    /// <summary>鮮度は同じ固定時計を使い、起動停止試験の待機timerだけはBCLの実時間で進める。</summary>
    private sealed class StaticClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 22, 21, 0, 0, TimeSpan.Zero);
    }

    private static global::Referee Packet() => new()
    {
        PacketTimestamp = 1, Stage = global::Referee.Types.Stage.NormalFirstHalf,
        Command = global::Referee.Types.Command.Halt, CommandCounter = 7, CommandTimestamp = 1,
        Yellow = Team("yellow"), Blue = Team("blue"),
    };
    private static global::Referee.Types.TeamInfo Team(string name) => new()
    {
        Name = name, Score = 0, RedCards = 0, YellowCards = 0,
        Timeouts = 0, TimeoutTime = 0, Goalkeeper = 0,
    };
}
