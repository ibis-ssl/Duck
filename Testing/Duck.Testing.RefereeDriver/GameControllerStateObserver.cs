namespace Duck.Testing.RefereeDriver;

/// <summary>一回のAPI読取と現在有効な観測contextを管理する境界。UDPや資源所有は判定しない。</summary>
internal sealed class GameControllerStateObserver
{
    private readonly object gate = new();
    private readonly Func<Uri, CancellationToken, Task<GameControllerMatchSnapshot?>> read;
    private readonly TimeSpan observationWindow;
    private readonly TimeProvider clock;
    private Uri endpoint;
    private long endpointGeneration = 1;
    private long requestSequence;
    private long? timestampHighWaterMark;
    private long clockVersion;
    private GameControllerApiObservation? current;

    internal GameControllerStateObserver(
        Uri endpoint,
        Func<Uri, CancellationToken, Task<GameControllerMatchSnapshot?>> read,
        TimeSpan observationWindow,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(read);
        if (observationWindow <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(observationWindow));
        this.endpoint = endpoint;
        this.read = read;
        this.observationWindow = observationWindow;
        clock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>開始前に要求番号と接続先世代を固定し、最新要求の有効な結果だけを公開する。</summary>
    internal async Task<GameControllerApiObservation?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri readEndpoint;
        long generation;
        long sequence;
        long started;
        long startedClockVersion;
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sequence = checked(++requestSequence);
            generation = endpointGeneration;
            readEndpoint = endpoint;
            current = null;
            if (!TryReadTimestamp(out started))
                return null;
            startedClockVersion = clockVersion;
        }

        GameControllerMatchSnapshot? state;
        try
        {
            state = await read(readEndpoint, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            // 結果を通常成功へ変換せず、当該要求がまだ最新なら保持値だけを解除して再送出する。
            lock (gate)
                if (sequence == requestSequence && generation == endpointGeneration)
                    current = null;
            throw;
        }

        lock (gate)
        {
            if (sequence != requestSequence || generation != endpointGeneration)
                return null;
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadTimestamp(out var completed) || startedClockVersion != clockVersion ||
                state is null || !TryGetAge(started, completed, out var age))
            {
                current = null;
                return null;
            }
            current = new(state, readEndpoint, generation, sequence, started, completed, age);
            return current;
        }
    }

    /// <summary>取得開始からの経過だけを増やし、再pollで観測の開始時点や期限を更新しない。</summary>
    internal GameControllerApiObservation? GetCurrent()
    {
        lock (gate)
        {
            if (!TryReadTimestamp(out var now) || current is null)
                return null;
            if (!TryGetAge(current.ReadStartedTimestamp, now, out var age))
            {
                current = null;
                return null;
            }
            return current with { AgeUpperBound = age };
        }
    }

    /// <summary>URL変更時に設定世代を進めて旧値を解除し、保留中の旧URL要求を公開できなくする。</summary>
    internal void Reconfigure(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        lock (gate)
        {
            if (this.endpoint == endpoint)
                return;
            var nextGeneration = checked(endpointGeneration + 1);
            this.endpoint = endpoint;
            endpointGeneration = nextGeneration;
            current = null;
        }
    }

    /// <summary>同じlock内で時計の高水位を保持し、逆行した区間と進行中の旧時計観測を失効させる。</summary>
    private bool TryReadTimestamp(out long now)
    {
        now = clock.GetTimestamp();
        if (timestampHighWaterMark is { } high && now < high)
        {
            clockVersion = checked(clockVersion + 1);
            current = null;
            return false;
        }
        timestampHighWaterMark = now;
        return true;
    }

    /// <summary>取得遅延を含む開始時点からの上界を求め、不明な区間や窓外の値は受理しない。</summary>
    private bool TryGetAge(long started, long now, out TimeSpan age)
    {
        age = default;
        if (now < started)
            return false;
        try
        {
            age = clock.GetElapsedTime(started, now);
            return age >= TimeSpan.Zero && age <= observationWindow;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}

/// <summary>同じ単調時計で採ったAPI観測context。世代は接続先設定のローカル世代で、所有証明ではない。</summary>
internal sealed record GameControllerApiObservation(
    GameControllerMatchSnapshot State,
    Uri Endpoint,
    long EndpointGeneration,
    long RequestSequence,
    long ReadStartedTimestamp,
    long ReadCompletedTimestamp,
    TimeSpan AgeUpperBound);
