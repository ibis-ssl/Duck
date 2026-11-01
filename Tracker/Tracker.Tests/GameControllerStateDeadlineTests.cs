using System.Net.WebSockets;
using System.Text;
using Duck.Testing.RefereeDriver;

namespace Tracker.Tests;

public sealed class GameControllerStateDeadlineTests
{
    /// <summary>接続待ち、無入力受信待ち、初期Output後の待ちを、応答なしでも読取窓の満了で終端する。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task DeadlineTerminatesCooperativePendingOperationWithoutAnotherFrame(int stage)
    {
        var clock = new ManualDeadlineClock();
        using var caller = new CancellationTokenSource();
        using var pendingConnect = new PendingOperation<WebSocket>();
        using var socket = new PendingSocket(initialOutput: stage == 2);
        var options = stage == 0 ? null : new GameControllerStateReadOptions
        {
            Timeout = TimeSpan.FromSeconds(2),
        };
        var window = options?.Timeout ?? TimeSpan.FromSeconds(3);
        var client = new GameControllerStateClient(new Uri("ws://game-controller:8082/api/control"),
            (_, token) => stage == 0 ? pendingConnect.Wait(token) : Task.FromResult<WebSocket>(socket),
            options, clock);
        var read = client.ReadMatchStateAsync(caller.Token);
        try
        {
            var pending = stage == 0 ? pendingConnect.Entered.Task : socket.Pending.Entered.Task;
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(window - TimeSpan.FromTicks(1));
            Assert.False(read.IsCompleted);
            clock.Advance(TimeSpan.FromTicks(1));

            // 内部期限の取消を先にassertし、実時間の待機失敗を期限の証拠にしない。
            Assert.False(caller.IsCancellationRequested);
            var token = stage == 0 ? pendingConnect.Token : socket.Pending.Token;
            Assert.True(token.IsCancellationRequested, "手動時計の満了で、保留中の処理へ内部取消を伝える。");
            GameControllerMatchSnapshot? observed = null;
            var error = await Record.ExceptionAsync(async () => observed = await read.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Null(error);
            Assert.Null(observed);
            Assert.Equal(stage == 0 ? 0 : 1, socket.DisposeCalls);
            Assert.Equal(0, socket.SendCalls);
            Assert.Equal(stage == 0 ? 0 : stage, socket.ReceiveCalls);
        }
        finally
        {
            await ReapAsync(caller, read);
        }
    }

    /// <summary>接続中と受信中の呼出取消は、内部期限の空観測へ変換しない。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CallerCancellationRemainsCancellationBeforeDeadline(bool duringConnect)
    {
        var clock = new ManualDeadlineClock();
        using var caller = new CancellationTokenSource();
        using var pendingConnect = new PendingOperation<WebSocket>();
        using var socket = new PendingSocket();
        var client = new GameControllerStateClient(new Uri("ws://game-controller:8082/api/control"),
            (_, token) => duringConnect ? pendingConnect.Wait(token) : Task.FromResult<WebSocket>(socket),
            timeProvider: clock);
        var read = client.ReadMatchStateAsync(caller.Token);
        try
        {
            await (duringConnect ? pendingConnect.Entered.Task : socket.Pending.Entered.Task)
                .WaitAsync(TimeSpan.FromSeconds(5));
            await caller.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await read.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(duringConnect ? 0 : 1, socket.DisposeCalls);
            Assert.Equal(0, socket.SendCalls);
        }
        finally
        {
            await ReapAsync(caller, read);
        }
    }

    /// <summary>非正のbyte上限や、非正・無限・timer範囲外の読取窓は接続せず観測値なしにする。</summary>
    [Theory]
    [InlineData(0, 30000000L)]
    [InlineData(-1, 30000000L)]
    [InlineData(4096, 0L)]
    [InlineData(4096, -1L)]
    [InlineData(4096, -10000L)]
    [InlineData(4096, long.MaxValue)]
    public async Task InvalidOptionsReturnNoObservationWithoutConnecting(int maximumBytes, long timeoutTicks)
    {
        using var socket = new PendingSocket();
        var calls = 0;
        var client = new GameControllerStateClient(new Uri("ws://game-controller:8082/api/control"),
            (_, _) =>
            {
                calls++;
                throw new InvalidOperationException("不正設定では接続を開始しない。");
            },
            new GameControllerStateReadOptions
            {
                MaximumMessageBytes = maximumBytes,
                Timeout = TimeSpan.FromTicks(timeoutTicks),
            },
            new ManualDeadlineClock());
        GameControllerMatchSnapshot? observed = null;
        var error = await Record.ExceptionAsync(async () =>
            observed = await client.ReadMatchStateAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Null(error);
        Assert.Null(observed);
        Assert.Equal(0, calls);
        Assert.Equal(0, socket.SendCalls);
    }

    /// <summary>assertion失敗時も呼出取消で保留処理を解放し、fixtureのTaskを必ず回収する。</summary>
    private static async Task ReapAsync(CancellationTokenSource caller, Task read)
    {
        await caller.CancelAsync();
        _ = await Record.ExceptionAsync(async () => await read.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(read.IsCompleted, "期限fixtureのTaskを回収する。");
    }

    /// <summary>取消登録を同期実行し、時計進行とTask終端の間に実時間の待機条件を持ち込まない。</summary>
    private sealed class PendingOperation<T> : IDisposable
    {
        private readonly TaskCompletionSource<T> completion = new();
        private CancellationTokenRegistration registration;
        public TaskCompletionSource Entered { get; } = new();
        public CancellationToken Token { get; private set; }

        public Task<T> Wait(CancellationToken token)
        {
            Token = token;
            registration = token.Register(() => completion.TrySetCanceled(token));
            Entered.TrySetResult();
            return completion.Task;
        }

        public void Dispose() => registration.Dispose();
    }

    /// <summary>応答を生成せず取消へ協調する受信口。任意で初期gcStateだけを一度返す。</summary>
    private sealed class PendingSocket(bool initialOutput = false) : WebSocket
    {
        private bool initial = initialOutput;
        private bool disposed;
        private WebSocketState state = WebSocketState.Open;
        public PendingOperation<ValueWebSocketReceiveResult> Pending { get; } = new();
        public int ReceiveCalls { get; private set; }
        public int SendCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => state;
        public override string? SubProtocol => null;

        public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceiveCalls++;
            if (initial)
            {
                initial = false;
                var bytes = Encoding.UTF8.GetBytes("""{"gcState":{}}""");
                bytes.CopyTo(buffer.Span);
                return ValueTask.FromResult(new ValueWebSocketReceiveResult(bytes.Length,
                    WebSocketMessageType.Text, true));
            }
            return new(Pending.Wait(cancellationToken));
        }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            var result = await ReceiveAsync(buffer.AsMemory(), cancellationToken);
            return new(result.Count, result.MessageType, result.EndOfMessage);
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken cancellationToken)
        {
            SendCalls++;
            throw new InvalidOperationException("観測専用fixtureで書込みを行わない。");
        }

        public override void Abort() => state = WebSocketState.Aborted;
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken)
        {
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
            Pending.Dispose();
        }
    }

    /// <summary>読取期限用の単発timerを実時間に依存せず進める。</summary>
    private sealed class ManualDeadlineClock : TimeProvider
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

        private sealed class ReadTimer(ManualDeadlineClock clock, TimerCallback callback, object? state) : ITimer
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
