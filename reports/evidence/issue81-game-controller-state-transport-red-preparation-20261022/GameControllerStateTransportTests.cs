using System.Net.WebSockets;
using System.Text;
using Duck.Testing.RefereeDriver;

namespace Tracker.Tests;

public sealed class GameControllerStateTransportTests
{
    private const string HaltJson =
        """{"matchState":{"command":{"type":"HALT","forTeam":"UNKNOWN"},"gameState":{"type":"HALT"}}}""";

    /// <summary>UTF8文字の途中で分かれたTextも、完成messageを組み立ててから正規化する。</summary>
    [Fact]
    public async Task FragmentedUtf8MatchStateReturnsHalt()
    {
        var payload = Encoding.UTF8.GetBytes(HaltJson[..^1] + ""","note":"審判"}""");
        var cut = Array.IndexOf(payload, (byte)0xe5) + 1;
        Assert.True(cut > 0 && cut < payload.Length - 1);
        using var socket = new ScriptedSocket(
            new Frame(payload[..cut], End: false),
            new Frame(payload[cut..(cut + 1)], End: false),
            new Frame(payload[(cut + 1)..]));
        var client = CreateClient(socket);

        var observed = await client.ReadMatchStateAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(observed);
        Assert.Equal(GameControllerCommandKind.Halt, observed.Command);
        Assert.Equal(GameControllerMatchPhase.Halt, observed.Phase);
        Assert.Equal(3, socket.ReceiveCalls);
        AssertClosedWithoutWrites(socket);
    }

    /// <summary>分割された初期Outputの後も別messageとしてmatchStateを読み、前messageのbyteを混ぜない。</summary>
    [Fact]
    public async Task FragmentedInitialOutputDoesNotEndMatchStateRead()
    {
        using var socket = new ScriptedSocket(
            new Frame(Encoding.UTF8.GetBytes("""{"gcSta"""), End: false),
            new Frame(Encoding.UTF8.GetBytes("""te":{}}""")),
            Text(HaltJson));
        var observed = await CreateClient(socket).ReadMatchStateAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(observed);
        Assert.Equal(GameControllerCommandKind.Halt, observed.Command);
        Assert.Equal(3, socket.ReceiveCalls);
        AssertClosedWithoutWrites(socket);
    }

    /// <summary>不正JSONやOutputではない構造を成功にせず、通常の入力失敗として観測値なしを返す。</summary>
    [Theory]
    [InlineData("{")]
    [InlineData("\"not-an-output\"")]
    [InlineData("{\"matchState\":[]}")]
    public async Task InvalidOutputReturnsNoObservation(string json)
    {
        using var socket = new ScriptedSocket(Text(json));
        GameControllerMatchSnapshot? observed = null;
        var error = await Record.ExceptionAsync(async () =>
            observed = await CreateClient(socket).ReadMatchStateAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Null(error);
        Assert.Null(observed);
        AssertClosedWithoutWrites(socket);
    }

    /// <summary>Closeやbinaryを状態JSONと解釈しない。</summary>
    [Theory]
    [InlineData(WebSocketMessageType.Close)]
    [InlineData(WebSocketMessageType.Binary)]
    public async Task NonTextMessageReturnsNoObservation(WebSocketMessageType type)
    {
        using var socket = new ScriptedSocket(new Frame(Encoding.UTF8.GetBytes(HaltJson), Type: type));
        var observed = await CreateClient(socket).ReadMatchStateAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(observed);
        AssertClosedWithoutWrites(socket);
    }

    /// <summary>受信中の切断を成功にせず、接続を破棄して観測値なしへ戻す。</summary>
    [Fact]
    public async Task ReceiveDisconnectReturnsNoObservation()
    {
        using var socket = new ScriptedSocket(new Frame(ReadOnlyMemory<byte>.Empty,
            Failure: new WebSocketException(WebSocketError.ConnectionClosedPrematurely)));
        GameControllerMatchSnapshot? observed = null;
        var error = await Record.ExceptionAsync(async () =>
            observed = await CreateClient(socket).ReadMatchStateAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Null(error);
        Assert.Null(observed);
        AssertClosedWithoutWrites(socket);
    }

    /// <summary>上限ちょうどの完成messageは受理し、正常JSONでも一byte超過すれば観測値を返さない。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LogicalMessageByteLimitHasAnInclusiveBoundary(bool oneByteOver)
    {
        var payload = Encoding.UTF8.GetBytes(HaltJson + (oneByteOver ? " " : ""));
        using var socket = new ScriptedSocket(new Frame(payload));
        var options = new GameControllerStateReadOptions
        {
            MaximumMessageBytes = Encoding.UTF8.GetByteCount(HaltJson),
            Timeout = TimeSpan.FromSeconds(2),
        };
        var observed = await CreateClient(socket, options).ReadMatchStateAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        if (oneByteOver)
            Assert.Null(observed);
        else
        {
            Assert.NotNull(observed);
            Assert.Equal(GameControllerCommandKind.Halt, observed.Command);
        }
        AssertClosedWithoutWrites(socket);
    }

    /// <summary>一fragmentごとではなく、完成messageの合計byte数で上限を確認する。</summary>
    [Fact]
    public async Task FragmentedMessageOverTotalByteLimitReturnsNoObservation()
    {
        var payload = Encoding.UTF8.GetBytes(HaltJson + " ");
        var split = payload.Length / 2;
        using var socket = new ScriptedSocket(new Frame(payload[..split], End: false), new Frame(payload[split..]));
        var options = new GameControllerStateReadOptions
        {
            MaximumMessageBytes = payload.Length - 1,
            Timeout = TimeSpan.FromSeconds(2),
        };
        var observed = await CreateClient(socket, options).ReadMatchStateAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(observed);
        AssertClosedWithoutWrites(socket);
    }

    /// <summary>明示した読取窓の満了後に解放された正常frameを、新しい成功にしない。</summary>
    [Fact]
    public async Task DeadlineRejectsValidFrameReleasedAtExpiry()
    {
        var clock = new ManualReadClock();
        var release = NewSignal();
        using var cancellation = new CancellationTokenSource();
        using var socket = new ScriptedSocket(new Frame(Encoding.UTF8.GetBytes(HaltJson), Release: release.Task));
        var options = new GameControllerStateReadOptions { Timeout = TimeSpan.FromSeconds(2) };
        var read = CreateClient(socket, options, clock).ReadMatchStateAsync(cancellation.Token);
        try
        {
            await socket.ReceiveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(options.Timeout);
            release.TrySetResult();
            GameControllerMatchSnapshot? observed = null;
            var error = await Record.ExceptionAsync(async () =>
                observed = await read.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Null(error);
            Assert.Null(observed);
            AssertClosedWithoutWrites(socket);
        }
        finally
        {
            release.TrySetResult();
            await cancellation.CancelAsync();
            _ = await Record.ExceptionAsync(async () => await read.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(read.IsCompleted, "期限fixtureの読取Taskを回収する。");
        }
    }

    /// <summary>呼出し元の明示取消は成功や通常の空観測に変換せず、読取と接続を終端する。</summary>
    [Fact]
    public async Task CallerCancellationDuringReceiveRemainsCancellation()
    {
        var release = NewSignal();
        using var cancellation = new CancellationTokenSource();
        using var socket = new ScriptedSocket(new Frame(Encoding.UTF8.GetBytes(HaltJson), Release: release.Task));
        var read = CreateClient(socket).ReadMatchStateAsync(cancellation.Token);
        try
        {
            await socket.ReceiveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await cancellation.CancelAsync();
            release.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await read.WaitAsync(TimeSpan.FromSeconds(5)));
            AssertClosedWithoutWrites(socket);
        }
        finally
        {
            release.TrySetResult();
            await cancellation.CancelAsync();
            _ = await Record.ExceptionAsync(async () => await read.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(read.IsCompleted, "取消fixtureの読取Taskを回収する。");
        }
    }

    private static GameControllerStateClient CreateClient(
        ScriptedSocket socket,
        GameControllerStateReadOptions? options = null,
        TimeProvider? clock = null) =>
        new(new Uri("ws://game-controller:8082/api/control"),
            (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult<WebSocket>(socket);
            }, options, clock);

    private static Frame Text(string json) => new Frame(Encoding.UTF8.GetBytes(json));

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void AssertClosedWithoutWrites(ScriptedSocket socket)
    {
        Assert.Equal(0, socket.SendCalls);
        Assert.Equal(1, socket.DisposeCalls);
        Assert.Equal(WebSocketState.Closed, socket.State);
    }

    private sealed record Frame(
        ReadOnlyMemory<byte> Payload,
        WebSocketMessageType Type = WebSocketMessageType.Text,
        bool End = true,
        Task? Release = null,
        Exception? Failure = null);

    private sealed class ScriptedSocket(params Frame[] frames) : WebSocket
    {
        private readonly Queue<Frame> frames = new(frames);
        private WebSocketState state = WebSocketState.Open;
        private bool disposed;
        public int ReceiveCalls { get; private set; }
        public int SendCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public TaskCompletionSource ReceiveEntered { get; } = NewSignal();
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => state;
        public override string? SubProtocol => null;

        public override async Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (frames.Count == 0)
                throw new InvalidOperationException("境界fixtureのframeを最後まで読み終えた。");
            var frame = frames.Dequeue();
            ReceiveCalls++;
            ReceiveEntered.TrySetResult();
            if (frame.Release is not null)
                await frame.Release.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (frame.Failure is not null)
                throw frame.Failure;
            if (frame.Payload.Length > buffer.Count)
                throw new InvalidOperationException("境界fixtureのframeを収める領域が不足した。");
            frame.Payload.Span.CopyTo(buffer.AsSpan());
            return new(frame.Payload.Length, frame.Type, frame.End);
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendCalls++;
            return Task.CompletedTask;
        }

        public override void Abort() => state = WebSocketState.Aborted;
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state = WebSocketState.Closed;
            return Task.CompletedTask;
        }
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken) => CloseAsync(closeStatus, statusDescription, cancellationToken);
        public override void Dispose()
        {
            if (disposed) return;
            disposed = true;
            state = WebSocketState.Closed;
            DisposeCalls++;
        }
    }

    /// <summary>読取期限用の単発timerを実時間に依存せず進める。</summary>
    private sealed class ManualReadClock : TimeProvider
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

        private sealed class ReadTimer(ManualReadClock clock, TimerCallback callback, object? state) : ITimer
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
