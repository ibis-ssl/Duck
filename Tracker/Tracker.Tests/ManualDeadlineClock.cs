namespace Tracker.Tests;

/// <summary>単発の期限タイマーを実時間に依存せず進める。</summary>
internal sealed class ManualDeadlineClock : TimeProvider
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

    public int Advance(TimeSpan elapsed)
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
        return callbacks.Count;
    }

    private sealed class ReadTimer(ManualDeadlineClock clock, TimerCallback callback, object? state) : ITimer
    {
        private long due = long.MaxValue;
        private bool disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (period != Timeout.InfiniteTimeSpan)
                throw new NotSupportedException("ManualDeadlineClock supports only one-shot timers.");
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
