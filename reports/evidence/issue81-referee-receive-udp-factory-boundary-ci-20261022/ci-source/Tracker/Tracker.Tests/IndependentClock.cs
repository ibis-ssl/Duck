namespace Issue81.IndependentSpecificationTests;

/// <summary>製品と既存テストに依存せず、時刻と経過時間を決定的に進める時計。</summary>
public sealed class IndependentClock : TimeProvider
{
    private DateTimeOffset utc = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    private long ticks;

    /// <summary>試験で指定した現在時刻を返す。</summary>
    public override DateTimeOffset GetUtcNow() => utc;

    /// <summary>独立した単調増加時計の周波数を返す。</summary>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>経過時間の基準値を返す。</summary>
    public override long GetTimestamp() => ticks;

    /// <summary>待機せずに両時計を同じ経過時間だけ進める。</summary>
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        utc += elapsed;
        ticks += elapsed.Ticks;
    }
}
