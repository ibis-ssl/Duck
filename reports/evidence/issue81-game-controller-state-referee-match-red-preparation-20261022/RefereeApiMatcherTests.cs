using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class RefereeApiMatcherTests
{
    /// <summary>実serviceの復号と実client/Observerを通り、同packetの証跡と明示状態対応だけで一致する。</summary>
    [Theory]
    [InlineData("HALT", "HALT", global::Referee.Types.Command.Halt)]
    [InlineData("STOP", "STOP", global::Referee.Types.Command.Stop)]
    [InlineData("NORMAL_START", "RUNNING", global::Referee.Types.Command.NormalStart)]
    [InlineData("FORCE_START", "RUNNING", global::Referee.Types.Command.ForceStart)]
    public async Task MatchesFreshActualReceiveAndApiPaths(
        string apiCommand, string apiPhase, global::Referee.Types.Command command)
    {
        await using var fixture = await MatchFixture.CreateAsync(command);
        fixture.Api.Queue(new ApiSocket(apiCommand, apiPhase));
        var result = await fixture.ReadAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.IsReady);
        Assert.Equal("Matched", result.Reason);
        Assert.NotNull(result.Udp);
        Assert.NotNull(result.Api);
        Assert.Equal(command, result.Udp.Command.Command);
        Assert.Equal((uint)7, result.Udp.Command.CommandCounter);
        Assert.Equal(2, result.Udp.Diagnostic.SuccessfullyDecodedCount);
        Assert.Equal(1, result.Udp.Diagnostic.ReceiverEpoch);
        Assert.Equal(fixture.Identity, result.Udp.Diagnostic.Identity);
        Assert.Equal("192.0.2.70:11003", result.Udp.Diagnostic.SourceKey);
        Assert.Equal(1, result.Api.RequestSequence);
        Assert.Equal(1, result.Api.EndpointGeneration);
        Assert.Equal(MatchFixture.Endpoint, result.Api.Endpoint);
        Assert.Equal(0, fixture.Api.SendCalls);
        Assert.Equal(1, fixture.Api.DisposeCalls);
        Assert.Equal(3, fixture.Receiver.ReceiveCalls);
        Assert.Equal(1, fixture.Receiver.MaxActiveReceives);
        Assert.True(fixture.Matcher.GetCurrent().IsReady);
    }

    /// <summary>無入力、owner欠落、不明age、復号失敗は、GCと同じ名前の指令を与えても準備完了にしない。</summary>
    [Theory]
    [InlineData("NoInput")]
    [InlineData("MissingOwner")]
    [InlineData("UnknownAge")]
    [InlineData("DecodeFailure")]
    public async Task UnusableUdpDoesNotMatch(string condition)
    {
        await using var fixture = await MatchFixture.CreateAsync(
            global::Referee.Types.Command.Halt,
            withOwner: condition != "MissingOwner",
            initialPackets: condition == "NoInput" ? 0 : 2);
        if (condition == "UnknownAge")
        {
            fixture.Clock.Advance(TimeSpan.FromTicks(-1));
            Assert.Null(Assert.Single(fixture.Diagnostics.GetSnapshots(RefereeReceiveService.InputId)).DecodeAgeUpperBound);
        }
        if (condition == "DecodeFailure")
        {
            await fixture.SupplyBytesAsync(new byte[] { 0xff });
            Assert.Equal(DiagnosticDecodeStatus.Failed,
                Assert.Single(fixture.Diagnostics.GetSnapshots(RefereeReceiveService.InputId)).DecodeStatus);
        }
        var result = await fixture.ReadAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.IsReady);
        Assert.False(fixture.Matcher.GetCurrent().IsReady);
        Assert.Equal(0, fixture.Api.SendCalls);
        Assert.Equal(1, fixture.Receiver.MaxActiveReceives);
    }

    /// <summary>双方が正常で新しくても、HALTとSTOPの不一致を一致成功に変換しない。</summary>
    [Fact]
    public async Task DifferentApiCommandIsNotReady()
    {
        await using var fixture = await MatchFixture.CreateAsync();
        fixture.Api.Queue(new ApiSocket("STOP", "STOP"));
        Assert.False((await fixture.ReadAsync().WaitAsync(TimeSpan.FromSeconds(5))).IsReady);
        Assert.False(fixture.Matcher.GetCurrent().IsReady);
    }

    /// <summary>APIの取得失敗を新しい正常UDPだけで補完しない。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrExpiredApiReadIsNotReady(bool expires)
    {
        await using var fixture = await MatchFixture.CreateAsync();
        fixture.Api.Queue(new ApiSocket("HALT", "HALT", close: !expires,
            clock: fixture.Clock, advance: expires ? MatchFixture.ApiWindow : TimeSpan.Zero));
        Assert.False((await fixture.ReadAsync().WaitAsync(TimeSpan.FromSeconds(5))).IsReady);
        Assert.False(fixture.Matcher.GetCurrent().IsReady);
        Assert.Equal(1, fixture.Api.ConnectCalls);
        Assert.Equal(1, fixture.Api.DisposeCalls);
        Assert.Equal(0, fixture.Api.SendCalls);
    }

    /// <summary>pollでAPIやUDPのageを延ばさず、有限窓の境界と直後を区別する。</summary>
    [Fact]
    public async Task PollingDoesNotRefreshEitherEvidence()
    {
        await using var fixture = await MatchFixture.CreateAsync();
        Assert.True((await fixture.ReadAsync().WaitAsync(TimeSpan.FromSeconds(5))).IsReady);
        var receivedAt = Assert.Single(fixture.Diagnostics.GetSnapshots(RefereeReceiveService.InputId)).LastReceivedAt;
        fixture.Clock.Advance(MatchFixture.ApiWindow);
        Assert.True(fixture.Matcher.GetCurrent().IsReady);
        fixture.Clock.Advance(TimeSpan.FromTicks(1));
        Assert.False(fixture.Matcher.GetCurrent().IsReady);
        Assert.False(fixture.Matcher.GetCurrent().IsReady);
        var current = Assert.Single(fixture.Diagnostics.GetSnapshots(RefereeReceiveService.InputId));
        Assert.Equal(2, current.SuccessfullyDecodedCount);
        Assert.Equal(receivedAt, current.LastReceivedAt);
        Assert.Equal(1, fixture.Api.ConnectCalls);
        Assert.Equal(0, fixture.Api.SendCalls);
        Assert.Equal(3, fixture.Receiver.ReceiveCalls);
    }

    /// <summary>API取得中に新しいpacketを受理したら、同counterでも開始時の旧packetへ結果を対応付けない。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewUdpPacketDuringApiReadInvalidatesOldPair(bool changesCommand)
    {
        await using var fixture = await MatchFixture.CreateAsync();
        var socket = new ApiSocket("HALT", "HALT", pending: true);
        fixture.Api.Queue(socket);
        var read = fixture.ReadAsync();
        await RequireApiEntryAsync(socket, read);
        await fixture.SupplyAsync(changesCommand ? global::Referee.Types.Command.Stop : global::Referee.Types.Command.Halt,
            changesCommand ? 8u : 7u);
        Assert.Equal(3, Assert.Single(fixture.Diagnostics.GetSnapshots(RefereeReceiveService.InputId)).SuccessfullyDecodedCount);
        socket.Release.TrySetResult();

        Assert.False((await read.WaitAsync(TimeSpan.FromSeconds(5))).IsReady);
        Assert.False(fixture.Matcher.GetCurrent().IsReady);
        Assert.Equal(1, fixture.Receiver.MaxActiveReceives);
    }

    /// <summary>API待ちの間に受信機が再起動したら、同じ指令でも旧受信世代へ結果を公開しない。</summary>
    [Fact]
    public async Task RestartedReceiveEpochRejectsPendingApiPair()
    {
        await using var fixture = await MatchFixture.CreateAsync();
        var socket = new ApiSocket("HALT", "HALT", pending: true);
        fixture.Api.Queue(socket);
        var read = fixture.ReadAsync();
        await RequireApiEntryAsync(socket, read);
        await fixture.RestartAsync();
        var restarted = Assert.Single(fixture.Diagnostics.GetSnapshots(RefereeReceiveService.InputId));
        Assert.Equal(2, restarted.ReceiverEpoch);
        Assert.Equal(4, restarted.SuccessfullyDecodedCount);
        socket.Release.TrySetResult();

        Assert.False((await read.WaitAsync(TimeSpan.FromSeconds(5))).IsReady);
        Assert.False(fixture.Matcher.GetCurrent().IsReady);
    }

    /// <summary>接続先世代を変更した後に旧URL応答が届いても照合値を復活させない。</summary>
    [Fact]
    public async Task ChangedApiEndpointRejectsPendingPair()
    {
        await using var fixture = await MatchFixture.CreateAsync();
        var socket = new ApiSocket("HALT", "HALT", pending: true);
        fixture.Api.Queue(socket);
        var read = fixture.ReadAsync();
        await RequireApiEntryAsync(socket, read);
        fixture.Observer.Reconfigure(new Uri("ws://replacement-controller:8082/api/control"));
        socket.Release.TrySetResult();

        Assert.False((await read.WaitAsync(TimeSpan.FromSeconds(5))).IsReady);
        Assert.False(fixture.Matcher.GetCurrent().IsReady);
    }

    /// <summary>新packetと新requestのSTOP一致後、遅い旧HALT要求で新しい一致を解除も上書きもしない。</summary>
    [Fact]
    public async Task OlderApiCompletionCannotOverwriteNewPacketMatch()
    {
        await using var fixture = await MatchFixture.CreateAsync();
        var oldSocket = new ApiSocket("HALT", "HALT", pending: true);
        fixture.Api.Queue(oldSocket);
        var oldRead = fixture.ReadAsync();
        await RequireApiEntryAsync(oldSocket, oldRead);
        await fixture.SupplyAsync(global::Referee.Types.Command.Stop, 8);
        fixture.Api.Queue(new ApiSocket("STOP", "STOP"));
        var latest = await fixture.ReadAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(latest.IsReady);
        Assert.Equal(3, latest.Udp!.Diagnostic.SuccessfullyDecodedCount);
        Assert.Equal(2, latest.Api!.RequestSequence);
        oldSocket.Release.TrySetResult();

        Assert.False((await oldRead.WaitAsync(TimeSpan.FromSeconds(5))).IsReady);
        var current = fixture.Matcher.GetCurrent();
        Assert.True(current.IsReady);
        Assert.Equal(global::Referee.Types.Command.Stop, current.Udp!.Command.Command);
        Assert.Equal(3, current.Udp.Diagnostic.SuccessfullyDecodedCount);
        Assert.Equal(2, current.Api!.RequestSequence);
    }

    /// <summary>新packetの診断受理とcommand公開の隙間で、旧commandと新しいageを組み合わせない。</summary>
    [Fact]
    public async Task AtomicAccessorDoesNotMixCommandWithNextPacketDiagnostics()
    {
        await using var fixture = await MatchFixture.CreateAsync();
        using var release = new ManualResetEventSlim();
        var accepted = NewSignal();
        var before = fixture.Service.GetCurrentObservation();
        Assert.NotNull(before);
        Assert.Equal(2, before.Diagnostic.SuccessfullyDecodedCount);
        Assert.Equal(global::Referee.Types.Command.Halt, before.Command.Command);
        fixture.PublicationHook = boundary =>
        {
            if (boundary != RefereeReceiveBoundary.BeforeCommandPublish) return;
            accepted.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("公開境界fixtureの解放が届かなかった。");
        };
        try
        {
            fixture.Receiver.Supply(CreatePacket(8, global::Referee.Types.Command.Stop).ToByteArray());
            await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(3, Assert.Single(fixture.Diagnostics.GetSnapshots(RefereeReceiveService.InputId)).SuccessfullyDecodedCount);
            Assert.Null(fixture.Service.GetCurrentObservation());
        }
        finally
        {
            release.Set();
            fixture.PublicationHook = null;
        }
        await fixture.Receiver.AtReceiveCall(4).WaitAsync(TimeSpan.FromSeconds(5));
        var after = fixture.Service.GetCurrentObservation();
        Assert.NotNull(after);
        Assert.Equal(global::Referee.Types.Command.Stop, after.Command.Command);
        Assert.Equal(3, after.Diagnostic.SuccessfullyDecodedCount);
        Assert.Equal(before.Diagnostic.ReceiverEpoch, after.Diagnostic.ReceiverEpoch);
        Assert.Equal(before.Diagnostic.SourceKey, after.Diagnostic.SourceKey);
    }

    /// <summary>no-opの完了をtimeoutにせず、APIを開始していない具体assertionで識別する。</summary>
    private static async Task RequireApiEntryAsync(ApiSocket socket, Task operation)
    {
        await Task.WhenAny(socket.Entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(socket.Entered.Task.IsCompleted, "照合は注入されたAPI観測を開始する。");
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class MatchFixture : IAsyncDisposable
    {
        public static readonly Uri Endpoint = new("ws://game-controller:8082/api/control");
        public static readonly TimeSpan ApiWindow = TimeSpan.FromSeconds(3);
        public MatchClock Clock { get; } = new();
        public ResourceDiagnosticIdentity Identity { get; } =
            new("run-referee-match", "referee-driver", new string('b', 64), 1);
        public ServiceReceiveDiagnostics Diagnostics { get; }
        public MatchReceiver Receiver { get; } = new();
        public RefereeReceiveService Service { get; }
        public ApiSource Api { get; } = new();
        public GameControllerStateObserver Observer { get; }
        public RefereeApiMatcher Matcher { get; }
        public Action<RefereeReceiveBoundary>? PublicationHook { get; set; }
        private CancellationTokenSource runCancellation = new();
        private readonly CancellationTokenSource operationsCancellation = new();
        private readonly List<Task> operations = [];
        private Task run;

        private MatchFixture(bool withOwner)
        {
            Diagnostics = new(withOwner ? Identity : null, Clock);
            Service = new(Receiver, Diagnostics) { BoundaryObserver = boundary => PublicationHook?.Invoke(boundary) };
            Observer = new(Endpoint, (uri, token) =>
                new GameControllerStateClient(uri, Api.ConnectAsync, timeProvider: Clock).ReadMatchStateAsync(token),
                ApiWindow, Clock);
            Matcher = new(Service, Observer);
            run = Service.RunAsync(runCancellation.Token);
        }

        public static async Task<MatchFixture> CreateAsync(
            global::Referee.Types.Command command = global::Referee.Types.Command.Halt,
            bool withOwner = true, int initialPackets = 2)
        {
            var fixture = new MatchFixture(withOwner);
            try
            {
                for (var i = 0; i < initialPackets; i++)
                    fixture.Receiver.Supply(CreatePacket(7, command).ToByteArray());
                await fixture.Receiver.AtReceiveCall(initialPackets + 1).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(fixture.run.IsCompleted);
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public Task<RefereeApiMatchResult> ReadAsync()
        {
            var task = Matcher.ReadAsync(operationsCancellation.Token);
            operations.Add(task);
            return task;
        }

        public Task SupplyAsync(global::Referee.Types.Command command, uint counter) =>
            SupplyBytesAsync(CreatePacket(counter, command).ToByteArray());

        public async Task SupplyBytesAsync(ReadOnlyMemory<byte> bytes)
        {
            var next = Receiver.ReceiveCalls + 1;
            Receiver.Supply(bytes);
            await Receiver.AtReceiveCall(next).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
        }

        public async Task RestartAsync()
        {
            await runCancellation.CancelAsync();
            _ = await Record.ExceptionAsync(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(run.IsCompleted);
            runCancellation.Dispose();
            runCancellation = new();
            var target = Receiver.ReceiveCalls + 3;
            run = Service.RunAsync(runCancellation.Token);
            Receiver.Supply(CreatePacket(7, global::Referee.Types.Command.Halt).ToByteArray());
            Receiver.Supply(CreatePacket(7, global::Referee.Types.Command.Halt).ToByteArray());
            await Receiver.AtReceiveCall(target).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
        }

        public async ValueTask DisposeAsync()
        {
            Api.ReleaseAll();
            await operationsCancellation.CancelAsync();
            await runCancellation.CancelAsync();
            var all = Task.WhenAll(operations.Append(run));
            _ = await Record.ExceptionAsync(async () => await all.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(all.IsCompleted, "照合fixtureの受信/API全Taskを回収する。");
            await Service.DisposeAsync();
            Api.DisposeAll();
            runCancellation.Dispose();
            operationsCancellation.Dispose();
        }
    }

    private sealed class ApiSource
    {
        private readonly Queue<ApiSocket> queued = new();
        private readonly List<ApiSocket> sockets = [];
        public int ConnectCalls { get; private set; }
        public int SendCalls => sockets.Sum(s => s.SendCalls);
        public int DisposeCalls => sockets.Sum(s => s.DisposeCalls);
        public void Queue(ApiSocket socket) { queued.Enqueue(socket); sockets.Add(socket); }
        public Task<WebSocket> ConnectAsync(Uri endpoint, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ConnectCalls++;
            if (queued.TryDequeue(out var socket)) return Task.FromResult<WebSocket>(socket);
            socket = new ApiSocket("HALT", "HALT");
            sockets.Add(socket);
            return Task.FromResult<WebSocket>(socket);
        }
        public void ReleaseAll() { foreach (var socket in sockets) socket.Release.TrySetResult(); }
        public void DisposeAll() { foreach (var socket in sockets) socket.Dispose(); }
    }

    private sealed class ApiSocket : WebSocket
    {
        private readonly byte[] payload;
        private readonly bool close;
        private readonly MatchClock? clock;
        private readonly TimeSpan advance;
        private bool disposed;
        public TaskCompletionSource Entered { get; } = NewSignal();
        public TaskCompletionSource Release { get; } = NewSignal();
        public int SendCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public ApiSocket(string command, string phase, bool pending = false, bool close = false,
            MatchClock? clock = null, TimeSpan advance = default)
        {
            payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
            {
                matchState = new { command = new { type = command, forTeam = "UNKNOWN" }, gameState = new { type = phase } },
            });
            this.close = close;
            this.clock = clock;
            this.advance = advance;
            if (!pending) Release.TrySetResult();
        }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => disposed ? WebSocketState.Closed : WebSocketState.Open;
        public override string? SubProtocol => null;
        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Entered.TrySetResult();
            await Release.Task.WaitAsync(token);
            clock?.Advance(advance);
            token.ThrowIfCancellationRequested();
            if (close) return new(0, WebSocketMessageType.Close, true);
            payload.AsSpan().CopyTo(buffer.AsSpan());
            return new(payload.Length, WebSocketMessageType.Text, true);
        }
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type,
            bool endOfMessage, CancellationToken token)
        {
            SendCalls++;
            throw new InvalidOperationException("観測専用fixtureで書込みを行わない。");
        }
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken token) =>
            Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken token) =>
            Task.CompletedTask;
        public override void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DisposeCalls++;
        }
    }

    private static global::Referee CreatePacket(uint counter, global::Referee.Types.Command command) => new()
    {
        PacketTimestamp = 1,
        Stage = global::Referee.Types.Stage.NormalFirstHalf,
        Command = command,
        CommandCounter = counter,
        CommandTimestamp = 1,
        Yellow = CreateTeam("yellow"),
        Blue = CreateTeam("blue"),
    };

    private static global::Referee.Types.TeamInfo CreateTeam(string name) => new()
    {
        Name = name,
        Score = 0,
        RedCards = 0,
        YellowCards = 0,
        Timeouts = 0,
        TimeoutTime = 0,
        Goalkeeper = 0,
    };

    private sealed class MatchReceiver : IRefereeDatagramReceiver
    {
        private readonly Channel<ReadOnlyMemory<byte>> packets = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> calls = new();
        private int receiveCalls;
        private int activeReceives;
        private int maxActiveReceives;

        public int ReceiveCalls => Volatile.Read(ref receiveCalls);
        public int MaxActiveReceives => Volatile.Read(ref maxActiveReceives);

        public Task AtReceiveCall(int number) => GetSignal(number).Task;

        public void Supply(ReadOnlyMemory<byte> packet)
        {
            if (!packets.Writer.TryWrite(packet))
                throw new InvalidOperationException("競合fixtureへpacketを供給できなかった。");
        }

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
            CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref activeReceives);
            Interlocked.Exchange(ref maxActiveReceives, Math.Max(active, MaxActiveReceives));
            GetSignal(Interlocked.Increment(ref receiveCalls)).TrySetResult();
            try
            {
                var packet = await packets.Reader.ReadAsync(cancellationToken);
                return new(packet, new IPEndPoint(IPAddress.Parse("192.0.2.70"), 11003), captureReceive());
            }
            finally
            {
                Interlocked.Decrement(ref activeReceives);
            }
        }

        private TaskCompletionSource GetSignal(int number) => calls.GetOrAdd(number, _ => NewSignal());
    }

    /// <summary>読取期限用の単発timerを実時間に依存せず進める。</summary>
    private sealed class MatchClock : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<ReadTimer> timers = [];
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() { lock (gate) return timestamp; }
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromTicks(GetTimestamp());

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ReadTimer(this, callback, state);
            lock (gate) timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan elapsed)
        {
            List<Action> callbacks = [];
            lock (gate)
            {
                timestamp += elapsed.Ticks;
                foreach (var timer in timers)
                    if (timer.TakeDueCallback(timestamp) is { } callback)
                        callbacks.Add(callback);
            }
            foreach (var callback in callbacks) callback();
        }

        private sealed class ReadTimer(MatchClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long due = long.MaxValue;
            private bool disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (period != Timeout.InfiniteTimeSpan)
                    throw new NotSupportedException("読取期限fixtureは単発timerだけを使用する。");
                lock (clock.gate)
                {
                    if (disposed) return false;
                    due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock.timestamp + dueTime.Ticks;
                    return true;
                }
            }
            public Action? TakeDueCallback(long now)
            {
                if (disposed || now < due) return null;
                due = long.MaxValue;
                return () => callback(state);
            }
            public void Dispose() { lock (clock.gate) { disposed = true; due = long.MaxValue; } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
