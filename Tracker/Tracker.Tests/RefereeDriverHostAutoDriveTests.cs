using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
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

namespace Tracker.Tests;

/// <summary>明示制御だけを既存Driver・制御adapterへ接続し、既定観測と受信所有を保つ。</summary>
public sealed class RefereeDriverHostAutoDriveTests
{
    [Theory]
    [InlineData(global::Referee.Types.Command.Stop, "FORCE_START", 4)]
    [InlineData(global::Referee.Types.Command.PrepareKickoffBlue, "NORMAL_START", 4)]
    [InlineData(global::Referee.Types.Command.ForceStart, null, 3)]
    public async Task ExplicitAutoDriveUsesExistingDriverAndControlAdapter(
        global::Referee.Types.Command afterNext, string? secondAction, int decodedCount)
    {
        await using var fixture = Fixture.Build(autoDrive: "true", afterNext: afterNext);
        await fixture.App.StartAsync();

        Assert.IsType<GameControllerWebSocketControlClient>(
            fixture.App.Services.GetService<IGameControllerControlClient>());
        Assert.NotNull(fixture.App.Services.GetService<RefereeDriverFixture>());
        await fixture.Transports.Receiver.AtCall(decodedCount + 1).WaitAsync(TimeSpan.FromSeconds(5));
        using var response = await fixture.WaitForHealthyAsync();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(decodedCount, document.RootElement.GetProperty("diagnostic")
            .GetProperty("successfullyDecodedCount").GetInt64());
        Assert.Equal(secondAction is null ? 1 : 2, fixture.Transports.Messages.Count);
        var types = fixture.Transports.Messages.Select(message => ReadAction(message.Json)).ToArray();
        var expectedTypes = secondAction is null ? new[] { "NEXT_COMMAND" } : new[] { "NEXT_COMMAND", secondAction };
        Assert.Equal(expectedTypes, types);
        Assert.All(fixture.Transports.Messages, message => Assert.Equal(Fixture.InternalUri, message.Endpoint));
        Assert.All(fixture.Transports.Sockets, socket => Assert.True(socket.SentOnlyAfterInitialOutput));
        Assert.Equal(1, fixture.Transports.CreateCalls);
        Assert.Equal(1, fixture.Transports.Receiver.MaxActive);
        Assert.Same(fixture.App.Services.GetRequiredService<RefereeReceiveService>(),
            fixture.App.Services.GetRequiredService<IRefereeCommandSource>());
        await fixture.App.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, fixture.Transports.Receiver.DisposeCalls);
        Assert.All(fixture.Transports.Sockets, socket => Assert.Equal(1, socket.DisposeCalls));
    }

    [Theory]
    [InlineData("true", null)]
    [InlineData("true", "false")]
    [InlineData("true", "not-a-boolean")]
    [InlineData("false", "true")]
    [InlineData(null, "true")]
    public async Task ObservationDefaultOrDisabledHostNeverStartsControl(string? enabled, string? autoDrive)
    {
        await using var fixture = Fixture.Build(enabled: enabled, autoDrive: autoDrive);
        await fixture.App.StartAsync();
        var active = enabled == "true";
        if (active)
        {
            await fixture.Transports.Receiver.AtCall(3).WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.Transports.AtConnection(2).WaitAsync(TimeSpan.FromSeconds(5));
            using var response = await fixture.WaitForHealthyAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, fixture.Transports.CreateCalls);
            Assert.Equal(1, fixture.Transports.Receiver.MaxActive);
        }
        else
        {
            using var response = await fixture.Client.GetAsync("/health/referee");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(0, fixture.Transports.CreateCalls);
            Assert.Empty(fixture.Transports.ConnectedUris);
        }
        Assert.Empty(fixture.Transports.Messages);
    }

    [Fact]
    public async Task StopCancelsExplicitControlSendAndRetainsSingleReceiverOwnership()
    {
        await using var fixture = Fixture.Build(autoDrive: "true", pendingSend: true);
        await fixture.App.StartAsync();

        Assert.NotNull(fixture.App.Services.GetService<RefereeDriverFixture>());
        await fixture.Transports.PendingSendEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.App.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(fixture.Transports.Sockets.Any(socket => socket.SendCancellationObserved));
        Assert.Equal(0, fixture.Transports.Receiver.Active);
        Assert.Equal(1, fixture.Transports.Receiver.MaxActive);
        Assert.Equal(1, fixture.Transports.Receiver.DisposeCalls);
        Assert.Empty(fixture.Transports.Messages);
        Assert.All(fixture.Transports.Sockets, socket => Assert.Equal(1, socket.DisposeCalls));
        await fixture.App.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, fixture.Transports.Receiver.DisposeCalls);
    }

    [Theory]
    [InlineData("ws://localhost:8082/api/control")]
    [InlineData("ws://referee-driver:39110/api/control")]
    public void ExplicitAutoDriveCannotReintroduceContainerSelfDestination(string endpoint)
    {
        using var transports = new Transports();
        var builder = Fixture.CreateBuilder(transports, "true", "true", endpoint);
        Assert.Throws<ArgumentException>(() => RefereeDriverApplication.Build(builder));
        Assert.Equal(0, transports.CreateCalls);
        Assert.Empty(transports.ConnectedUris);
        Assert.Empty(transports.Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ControlTransportReadsWholeInitialOutputBeforeSendingAndDisposesSocket(bool fragmented)
    {
        using var transports = new Transports(fragmented: fragmented);
        var adapter = new RefereeDriverWebSocketTransport(transports.ConnectAsync);
        var client = new GameControllerWebSocketControlClient(adapter, Fixture.InternalUri);
        await client.SendContinueActionAsync(GameControllerContinueAction.NextCommand, CancellationToken.None);

        Assert.Single(transports.ConnectedUris);
        var sent = Assert.Single(transports.Messages);
        Assert.Equal(Fixture.InternalUri, sent.Endpoint);
        Assert.Equal("{\"continue_action\":{\"type\":\"NEXT_COMMAND\",\"for_team\":\"UNKNOWN\"}}", sent.Json);
        var socket = Assert.Single(transports.Sockets);
        Assert.Equal(fragmented ? 2 : 1, socket.ReadCalls);
        Assert.True(socket.SentOnlyAfterInitialOutput);
        Assert.Equal(1, socket.DisposeCalls);
    }

    [Fact]
    public async Task ExistingExplicitTeamNameMessageUsesInjectedUriWithoutImplicitTeamChanges()
    {
        using var transports = new Transports();
        var adapter = new RefereeDriverWebSocketTransport(transports.ConnectAsync);
        var client = new GameControllerWebSocketControlClient(adapter, Fixture.InternalUri);
        await client.SendYellowTeamNameAsync("crane", CancellationToken.None);

        var sent = Assert.Single(transports.Messages);
        Assert.Equal(Fixture.InternalUri, sent.Endpoint);
        using var document = JsonDocument.Parse(sent.Json);
        var change = document.RootElement.GetProperty("change").GetProperty("update_team_state_change");
        Assert.Equal("YELLOW", change.GetProperty("for_team").GetString());
        Assert.Equal("crane", change.GetProperty("team_name").GetString());
        Assert.Equal(1, Assert.Single(transports.Sockets).DisposeCalls);
    }

    [Fact]
    public async Task CallerCancellationInterruptsInitialOutputWithoutSendingAndDisposesSocket()
    {
        using var transports = new Transports(pendingInitial: true);
        var adapter = new RefereeDriverWebSocketTransport(transports.ConnectAsync);
        using var cancellation = new CancellationTokenSource();
        var send = adapter.SendJsonAsync(Fixture.InternalUri, "{}", cancellation.Token);
        try
        {
            Assert.Single(transports.ConnectedUris);
            await transports.PendingInitialEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
            Assert.Empty(transports.Messages);
            Assert.Equal(1, Assert.Single(transports.Sockets).DisposeCalls);
        }
        finally
        {
            cancellation.Cancel();
            try { await send.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
    }

    private static string ReadAction(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("continue_action").GetProperty("type").GetString()!;
    }

    /// <summary>本番Build/DI/worker/HTTPを使い、通信生成と時計だけを試験内へ隔離する。</summary>
    private sealed class Fixture(WebApplication app, Transports transports) : IAsyncDisposable
    {
        internal static readonly Uri InternalUri = new("ws://game-controller:8082/api/control");
        internal WebApplication App { get; } = app;
        internal Transports Transports { get; } = transports;
        private HttpClient? client;
        internal HttpClient Client => client ??= new HttpClient
        {
            BaseAddress = new Uri(App.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single()),
            Timeout = TimeSpan.FromSeconds(5),
        };
        internal static Fixture Build(string? enabled = "true", string? autoDrive = null,
            global::Referee.Types.Command afterNext = global::Referee.Types.Command.Stop, bool pendingSend = false)
        {
            var transports = new Transports(afterNext: afterNext, pendingSend: pendingSend);
            try
            {
                var app = RefereeDriverApplication.Build(CreateBuilder(transports, enabled, autoDrive));
                app.Urls.Clear();
                app.Urls.Add("http://127.0.0.1:0");
                return new(app, transports);
            }
            catch { transports.Dispose(); throw; }
        }
        internal static WebApplicationBuilder CreateBuilder(Transports transports, string? enabled,
            string? autoDrive, string? endpoint = "ws://game-controller:8082/api/control")
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            { Args = [], EnvironmentName = Environments.Production });
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RefereeDriver:Enabled"] = enabled,
                ["RefereeDriver:AutoDriveToActive"] = autoDrive,
                ["Testing:ContainerMode"] = "true",
                ["RefereeDriver:GameControllerUri"] = endpoint,
                ["Testing:Diagnostics:RunId"] = "autodrive-test",
                ["Testing:Diagnostics:ResourceId"] = "referee-driver",
                ["Testing:Diagnostics:ContainerId"] = new string('c', 64),
                ["Testing:Diagnostics:Generation"] = "2",
            });
            builder.Services.AddSingleton<TimeProvider>(transports.Clock);
            builder.Services.AddSingleton(transports.Factories);
            return builder;
        }
        internal async Task<HttpResponseMessage> WaitForHealthyAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (true)
            {
                var response = await Client.GetAsync("/health/referee", timeout.Token);
                if (response.IsSuccessStatusCode) return response;
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
                finally { Transports.Dispose(); }
            }
            Assert.Equal(0, Transports.Receiver.Active);
            Assert.Equal(0, Transports.Sockets.Sum(socket => socket.Active));
        }
    }

    private sealed record Sent(Uri Endpoint, string Json);

    /// <summary>制御の送信に応じて入力だけを進め、実Driverとadapterの制御順序を置換しない。</summary>
    private sealed class Transports : IDisposable
    {
        internal TimeProvider Clock { get; } = new StaticClock();
        internal Receiver Receiver { get; } = new();
        internal ConcurrentQueue<Sent> Messages { get; } = new();
        internal ConcurrentBag<Uri> ConnectedUris { get; } = [];
        internal ConcurrentBag<Socket> Sockets { get; } = [];
        internal TaskCompletionSource PendingSendEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource PendingInitialEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal RefereeDriverTransportFactories Factories { get; }
        internal int CreateCalls => Volatile.Read(ref createCalls);
        private int createCalls, connections, currentCommand = (int)global::Referee.Types.Command.Halt;
        private readonly ConcurrentDictionary<int, TaskCompletionSource> connected = new();
        private readonly global::Referee.Types.Command afterNext;
        private readonly bool pendingSend, fragmented, pendingInitial;

        internal Transports(global::Referee.Types.Command afterNext = global::Referee.Types.Command.Stop,
            bool pendingSend = false, bool fragmented = false, bool pendingInitial = false)
        {
            this.afterNext = afterNext;
            this.pendingSend = pendingSend;
            this.fragmented = fragmented;
            this.pendingInitial = pendingInitial;
            Receiver.Supply(Packet(global::Referee.Types.Command.Halt, 10));
            Receiver.Supply(Packet(global::Referee.Types.Command.Halt, 10));
            Factories = new(_ =>
            {
                Interlocked.Increment(ref createCalls);
                return new(Receiver, Receiver);
            }, ConnectAsync);
        }
        internal Task AtConnection(int count) => connected.GetOrAdd(count,
            _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        internal Task<WebSocket> ConnectAsync(Uri endpoint, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ConnectedUris.Add(endpoint);
            var socket = new Socket(this, endpoint);
            Sockets.Add(socket);
            connected.GetOrAdd(Interlocked.Increment(ref connections),
                _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            return Task.FromResult<WebSocket>(socket);
        }
        private byte[] Output()
        {
            var command = (global::Referee.Types.Command)Volatile.Read(ref currentCommand);
            var type = command switch
            {
                global::Referee.Types.Command.Stop => "STOP",
                global::Referee.Types.Command.ForceStart => "FORCE_START",
                global::Referee.Types.Command.NormalStart => "NORMAL_START",
                _ => "HALT",
            };
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                matchState = new
                {
                    command = new { type, forTeam = "UNKNOWN" },
                    gameState = new { type = type is "FORCE_START" or "NORMAL_START" ? "RUNNING" : type },
                },
            });
        }
        private void RecordSend(Uri endpoint, string json)
        {
            Messages.Enqueue(new(endpoint, json));
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("continue_action", out var action)) return;
            var command = action.GetProperty("type").GetString() switch
            {
                "NEXT_COMMAND" => afterNext,
                "FORCE_START" => global::Referee.Types.Command.ForceStart,
                "NORMAL_START" => global::Referee.Types.Command.NormalStart,
                _ => throw new InvalidOperationException("未知の制御JSON。"),
            };
            Interlocked.Exchange(ref currentCommand, (int)command);
            Receiver.Supply(Packet(command, Messages.Count == 1 ? 11u : 12u));
        }
        internal void CancelPending()
        {
            Receiver.CancelPending();
            foreach (var socket in Sockets) socket.CancelPending();
        }
        public void Dispose()
        {
            CancelPending();
            Receiver.DisposeEmergency();
            foreach (var socket in Sockets) socket.DisposeEmergency();
        }

        internal sealed class Socket(Transports owner, Uri endpoint) : WebSocket
        {
            private readonly CancellationTokenSource emergency = new();
            private readonly TaskCompletionSource never = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int reads, disposed, active, cancelled;
            private bool completeOutput;
            internal int ReadCalls => reads;
            internal int DisposeCalls => Volatile.Read(ref disposed);
            internal int Active => Volatile.Read(ref active);
            internal bool SendCancellationObserved => Volatile.Read(ref cancelled) != 0;
            internal bool SentOnlyAfterInitialOutput { get; private set; } = true;
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
                    Interlocked.Increment(ref reads);
                    if (owner.pendingInitial)
                    {
                        owner.PendingInitialEntered.TrySetResult();
                        await never.Task.WaitAsync(linked.Token);
                    }
                    linked.Token.ThrowIfCancellationRequested();
                    var bytes = owner.fragmented ? Encoding.UTF8.GetBytes(ReadCalls == 1 ? "{" : "}") : owner.Output();
                    bytes.AsSpan().CopyTo(buffer.AsSpan());
                    completeOutput = !owner.fragmented || ReadCalls >= 2;
                    return new(bytes.Length, WebSocketMessageType.Text, completeOutput);
                }
                finally { Interlocked.Decrement(ref active); }
            }
            public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type,
                bool endOfMessage, CancellationToken token)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, emergency.Token);
                Interlocked.Increment(ref active);
                try
                {
                    if (owner.pendingSend)
                    {
                        owner.PendingSendEntered.TrySetResult();
                        await never.Task.WaitAsync(linked.Token);
                    }
                    linked.Token.ThrowIfCancellationRequested();
                    SentOnlyAfterInitialOutput &= completeOutput;
                    owner.RecordSend(endpoint, Encoding.UTF8.GetString(buffer.AsSpan()));
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested) Interlocked.Exchange(ref cancelled, 1);
                    throw;
                }
                finally { Interlocked.Decrement(ref active); }
            }
            public override void Abort() { }
            public override Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken token) =>
                Task.CompletedTask;
            public override Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken token) =>
                Task.CompletedTask;
            public override void Dispose() => Interlocked.Increment(ref disposed);
            internal void CancelPending() => emergency.Cancel();
            internal void DisposeEmergency() => emergency.Dispose();
        }
    }

    private sealed class Receiver : IRefereeDatagramReceiver, IAsyncDisposable
    {
        private readonly Channel<ReadOnlyMemory<byte>> packets = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> calls = new();
        private readonly CancellationTokenSource emergency = new();
        private int received, active, maximum, disposed;
        internal int Active => Volatile.Read(ref active);
        internal int MaxActive => Volatile.Read(ref maximum);
        internal int DisposeCalls => Volatile.Read(ref disposed);
        internal void Supply(ReadOnlyMemory<byte> packet) => Assert.True(packets.Writer.TryWrite(packet));
        internal Task AtCall(int count) => calls.GetOrAdd(count,
            _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> capture, CancellationToken token)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, emergency.Token);
            var current = Interlocked.Increment(ref active);
            Interlocked.Exchange(ref maximum, Math.Max(current, MaxActive));
            calls.GetOrAdd(Interlocked.Increment(ref received),
                _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            try
            {
                var bytes = await packets.Reader.ReadAsync(linked.Token);
                return new(bytes, new IPEndPoint(IPAddress.Parse("192.0.2.70"), 11003), capture());
            }
            finally { Interlocked.Decrement(ref active); }
        }
        public ValueTask DisposeAsync() { Interlocked.Increment(ref disposed); return ValueTask.CompletedTask; }
        internal void CancelPending() => emergency.Cancel();
        internal void DisposeEmergency() => emergency.Dispose();
    }

    private sealed class StaticClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 22, 22, 0, 0, TimeSpan.Zero);
    }

    private static ReadOnlyMemory<byte> Packet(global::Referee.Types.Command command, uint counter) =>
        new global::Referee
        {
            PacketTimestamp = 1, Stage = global::Referee.Types.Stage.NormalFirstHalf,
            Command = command, CommandCounter = counter, CommandTimestamp = 1,
            Yellow = Team("yellow"), Blue = Team("blue"),
        }.ToByteArray();
    private static global::Referee.Types.TeamInfo Team(string name) => new()
    {
        Name = name, Score = 0, RedCards = 0, YellowCards = 0,
        Timeouts = 0, TimeoutTime = 0, Goalkeeper = 0,
    };
}
