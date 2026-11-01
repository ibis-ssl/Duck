using System.Net.WebSockets;
using System.Text;
using Duck.Testing.RefereeDriver;

namespace Tracker.Tests;

public sealed class GameControllerStateObserverTests
{
    private static readonly Uri InitialUri = new("ws://game-controller:8082/api/control");
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);
    private static readonly GameControllerMatchSnapshot Halt =
        new(GameControllerCommandKind.Halt, GameControllerMatchPhase.Halt);
    private static readonly GameControllerMatchSnapshot Stop =
        new(GameControllerCommandKind.Stop, GameControllerMatchPhase.Stop);

    /// <summary>実clientの取得前後を同じ単調時計で記録し、読取遅延を鮮度へ含め、pollで期限を延ばさない。</summary>
    [Fact]
    public async Task CapturesReadContextAndExpiresFromStartWithoutPollingRenewal()
    {
        var clock = new ManualObservationClock();
        clock.Advance(TimeSpan.FromSeconds(10));
        using var socket = new AdvancingSocket(clock, TimeSpan.FromSeconds(2));
        var endpoints = new List<Uri>();
        var observer = new GameControllerStateObserver(InitialUri, (uri, token) =>
        {
            endpoints.Add(uri);
            return new GameControllerStateClient(uri, (_, _) => Task.FromResult<WebSocket>(socket),
                timeProvider: clock).ReadMatchStateAsync(token);
        }, Window, clock);

        var observed = await observer.ReadAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(observed);
        Assert.Equal(Halt, observed.State);
        Assert.Equal(InitialUri, observed.Endpoint);
        Assert.Equal(1L, observed.EndpointGeneration);
        Assert.Equal(1L, observed.RequestSequence);
        Assert.Equal(TimeSpan.FromSeconds(10).Ticks, observed.ReadStartedTimestamp);
        Assert.Equal(TimeSpan.FromSeconds(12).Ticks, observed.ReadCompletedTimestamp);
        Assert.Equal(TimeSpan.FromSeconds(2), observed.AgeUpperBound);
        Assert.Equal(InitialUri, Assert.Single(endpoints));
        Assert.Equal(0, socket.SendCalls);
        Assert.Equal(1, socket.DisposeCalls);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(Window, Assert.IsType<GameControllerApiObservation>(observer.GetCurrent()).AgeUpperBound);
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.Null(observer.GetCurrent());
        Assert.Null(observer.GetCurrent());
    }

    /// <summary>逐次取得の番号を単調増加させ、新しい取得値とその開始時点を区別する。</summary>
    [Fact]
    public async Task SequentialReadsHaveIncreasingRequestSequences()
    {
        var clock = new ManualObservationClock();
        var count = 0;
        var observer = new GameControllerStateObserver(InitialUri,
            (_, _) => Task.FromResult<GameControllerMatchSnapshot?>(++count == 1 ? Halt : Stop), Window, clock);

        var first = await observer.ReadAsync(CancellationToken.None);
        Assert.NotNull(first);
        clock.Advance(TimeSpan.FromSeconds(1));
        var second = await observer.ReadAsync(CancellationToken.None);
        Assert.NotNull(second);
        Assert.Equal(1L, first.RequestSequence);
        Assert.Equal(2L, second.RequestSequence);
        Assert.Equal(1L, second.EndpointGeneration);
        Assert.Equal(Stop, second.State);
        Assert.Equal(TimeSpan.FromSeconds(1).Ticks, second.ReadStartedTimestamp);
        Assert.Equal(second, observer.GetCurrent());
    }

    /// <summary>新しいrequestが先に完了した後、遅い旧requestの値で上書きしない。</summary>
    [Fact]
    public async Task OlderRequestCannotOverwriteNewerCompletedObservation()
    {
        using var caller = new CancellationTokenSource();
        var reader = new ControlledReader();
        var observer = CreateObserver(reader, new ManualObservationClock());
        var older = observer.ReadAsync(caller.Token);
        Task<GameControllerApiObservation?>? newer = null;
        try
        {
            var oldRequest = await reader.RequireRequestAsync(0, older);
            newer = observer.ReadAsync(caller.Token);
            var newRequest = await reader.RequireRequestAsync(1, newer);
            newRequest.Complete(Stop);
            var accepted = await newer.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(accepted);
            Assert.Equal(2L, accepted.RequestSequence);
            oldRequest.Complete(Halt);

            Assert.Null(await older.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(Stop, Assert.IsType<GameControllerApiObservation>(observer.GetCurrent()).State);
            Assert.Equal(accepted, observer.GetCurrent());
        }
        finally
        {
            await ReapAsync(caller, reader, older, newer);
        }
    }

    /// <summary>URL変更で既存値を無効にし、旧URLの保留応答を拒否して新URLの世代だけを公開する。</summary>
    [Fact]
    public async Task EndpointChangeInvalidatesCurrentAndRejectsOldUrlResult()
    {
        using var caller = new CancellationTokenSource();
        var reader = new ControlledReader();
        var observer = CreateObserver(reader, new ManualObservationClock());
        var initial = observer.ReadAsync(caller.Token);
        Task<GameControllerApiObservation?>? oldPending = null;
        Task<GameControllerApiObservation?>? current = null;
        try
        {
            (await reader.RequireRequestAsync(0, initial)).Complete(Halt);
            Assert.NotNull(await initial.WaitAsync(TimeSpan.FromSeconds(5)));
            oldPending = observer.ReadAsync(caller.Token);
            var oldRequest = await reader.RequireRequestAsync(1, oldPending);
            var nextUri = new Uri("ws://replacement-controller:8082/api/control");
            observer.Reconfigure(nextUri);
            Assert.Null(observer.GetCurrent());
            current = observer.ReadAsync(caller.Token);
            var newRequest = await reader.RequireRequestAsync(2, current);
            Assert.Equal(InitialUri, oldRequest.Endpoint);
            Assert.Equal(nextUri, newRequest.Endpoint);
            newRequest.Complete(Stop);
            var accepted = await current.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(accepted);
            Assert.Equal(nextUri, accepted.Endpoint);
            Assert.Equal(2L, accepted.EndpointGeneration);
            Assert.Equal(3L, accepted.RequestSequence);
            oldRequest.Complete(Halt);

            Assert.Null(await oldPending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(accepted, observer.GetCurrent());
        }
        finally
        {
            await ReapAsync(caller, reader, initial, oldPending, current);
        }
    }

    /// <summary>最新取得の失敗で旧値を解除し、さらに古い成功応答が後から来ても復活させない。</summary>
    [Fact]
    public async Task FailedNewestRequestDoesNotPermitOlderSuccessToReviveCurrent()
    {
        using var caller = new CancellationTokenSource();
        var reader = new ControlledReader();
        var observer = CreateObserver(reader, new ManualObservationClock());
        var initial = observer.ReadAsync(caller.Token);
        Task<GameControllerApiObservation?>? oldPending = null;
        Task<GameControllerApiObservation?>? newest = null;
        try
        {
            (await reader.RequireRequestAsync(0, initial)).Complete(Halt);
            Assert.NotNull(await initial.WaitAsync(TimeSpan.FromSeconds(5)));
            oldPending = observer.ReadAsync(caller.Token);
            var oldRequest = await reader.RequireRequestAsync(1, oldPending);
            newest = observer.ReadAsync(caller.Token);
            (await reader.RequireRequestAsync(2, newest)).Complete(null);
            Assert.Null(await newest.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Null(observer.GetCurrent());
            oldRequest.Complete(Halt);
            Assert.Null(await oldPending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Null(observer.GetCurrent());
        }
        finally
        {
            await ReapAsync(caller, reader, initial, oldPending, newest);
        }
    }

    /// <summary>窓を過ぎて返った値と時計が逆行した取得を、新しい観測として公開しない。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidReadTimingDoesNotPublishAnObservation(bool clockRegresses)
    {
        var clock = new ManualObservationClock();
        clock.Advance(TimeSpan.FromSeconds(10));
        var count = 0;
        var observer = new GameControllerStateObserver(InitialUri, (_, _) =>
        {
            count++;
            clock.Advance(clockRegresses ? TimeSpan.FromSeconds(-1) : Window + TimeSpan.FromTicks(1));
            return Task.FromResult<GameControllerMatchSnapshot?>(Halt);
        }, Window, clock);

        var observed = await observer.ReadAsync(CancellationToken.None);
        Assert.Equal(1, count);
        Assert.Null(observed);
        Assert.Null(observer.GetCurrent());
    }

    /// <summary>実clientの内部期限で終わった読取はcontextを作らず、書込みなしでsocketを破棄する。</summary>
    [Fact]
    public async Task ExpiredRealClientDoesNotPublishContextAndDisposesSocket()
    {
        var clock = new ManualObservationClock();
        using var socket = new AdvancingSocket(clock, Window);
        var observer = new GameControllerStateObserver(InitialUri,
            (uri, token) => new GameControllerStateClient(uri, (_, _) => Task.FromResult<WebSocket>(socket),
                timeProvider: clock).ReadMatchStateAsync(token), Window, clock);

        var observed = await observer.ReadAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(observed);
        Assert.Null(observer.GetCurrent());
        Assert.Equal(0, socket.SendCalls);
        Assert.Equal(1, socket.DisposeCalls);
    }

    /// <summary>呼出し前からの明示取消を空の正常観測へ変換せず、取得も開始しない。</summary>
    [Fact]
    public async Task AlreadyCancelledCallerDoesNotStartARead()
    {
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();
        var calls = 0;
        var observer = new GameControllerStateObserver(InitialUri, (_, _) =>
        {
            calls++;
            return Task.FromResult<GameControllerMatchSnapshot?>(Halt);
        }, Window, new ManualObservationClock());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observer.ReadAsync(caller.Token));
        Assert.Equal(0, calls);
        Assert.Null(observer.GetCurrent());
    }

    private static GameControllerStateObserver CreateObserver(ControlledReader reader, TimeProvider clock) =>
        new(InitialUri, reader.ReadAsync, Window, clock);

    /// <summary>失敗時も全応答を解放し、呼出取消後に試験が所有するTaskを回収する。</summary>
    private static async Task ReapAsync(CancellationTokenSource caller, ControlledReader reader,
        params Task<GameControllerApiObservation?>?[] tasks)
    {
        await caller.CancelAsync();
        reader.ReleaseAll();
        foreach (var task in tasks)
        {
            if (task is null) continue;
            _ = await Record.ExceptionAsync(async () => await task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(task.IsCompleted, "context fixtureのTaskを回収する。");
        }
    }

    /// <summary>要求の開始順と応答順を独立に制御し、未開始のno-opも実assertionで識別する。</summary>
    private sealed class ControlledReader
    {
        private readonly object gate = new();
        private readonly List<Request> requests = [];
        private TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<GameControllerMatchSnapshot?> ReadAsync(Uri uri, CancellationToken token)
        {
            var request = new Request(uri, token);
            TaskCompletionSource notify;
            lock (gate)
            {
                requests.Add(request);
                notify = changed;
                changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            notify.TrySetResult();
            return request.Result;
        }
        public async Task<Request> RequireRequestAsync(int index, Task operation)
        {
            Task signal;
            lock (gate)
            {
                if (requests.Count > index) return requests[index];
                signal = changed.Task;
            }
            await Task.WhenAny(signal, operation).WaitAsync(TimeSpan.FromSeconds(5));
            lock (gate)
            {
                Assert.True(requests.Count > index, "観測処理が注入されたreaderを開始する。");
                return requests[index];
            }
        }
        public void ReleaseAll()
        {
            lock (gate)
                foreach (var request in requests) request.Complete(null);
        }
    }

    private sealed class Request(Uri endpoint, CancellationToken token)
    {
        private readonly TaskCompletionSource<GameControllerMatchSnapshot?> result =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Uri Endpoint { get; } = endpoint;
        public Task<GameControllerMatchSnapshot?> Result => result.Task.WaitAsync(token);
        public void Complete(GameControllerMatchSnapshot? state) => result.TrySetResult(state);
    }

    /// <summary>受信中に同じ時計だけを進め、完了した公開HALTを返す。実ネットワークへ接続しない。</summary>
    private sealed class AdvancingSocket(ManualObservationClock clock, TimeSpan elapsed) : WebSocket
    {
        private bool disposed;
        public int DisposeCalls { get; private set; }
        public int SendCalls { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => disposed ? WebSocketState.Closed : WebSocketState.Open;
        public override string? SubProtocol => null;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            clock.Advance(elapsed);
            token.ThrowIfCancellationRequested();
            var bytes = Encoding.UTF8.GetBytes("""{"matchState":{"command":{"type":"HALT"},"gameState":{"type":"HALT"}}}""");
            bytes.AsSpan().CopyTo(buffer.AsSpan());
            return Task.FromResult(new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true));
        }
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType,
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

    /// <summary>読取期限用の単発timerを実時間に依存せず進める。</summary>
    private sealed class ManualObservationClock : TimeProvider
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

        private sealed class ReadTimer(ManualObservationClock clock, TimerCallback callback, object? state) : ITimer
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
