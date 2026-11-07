namespace Duck.Testing.RefereeDriver;

/// <summary>一回のAPI読取と現在有効な観測contextを管理する境界。UDPや資源所有は判定しない。</summary>
internal sealed class GameControllerStateObserver
{
    internal GameControllerStateObserver(
        Uri endpoint,
        Func<Uri, CancellationToken, Task<GameControllerMatchSnapshot?>> read,
        TimeSpan observationWindow,
        TimeProvider? timeProvider = null)
    {
        _ = endpoint;
        _ = read;
        _ = observationWindow;
        _ = timeProvider;
    }

    /// <summary>先行試験用の未実装入口。現時点では取得や公開を行わない。</summary>
    internal Task<GameControllerApiObservation?> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult<GameControllerApiObservation?>(null);

    /// <summary>先行試験用の未実装入口。現時点では有効な観測を持たない。</summary>
    internal GameControllerApiObservation? GetCurrent() => null;

    /// <summary>接続先世代を切り替えるための未実装入口。</summary>
    internal void Reconfigure(Uri endpoint) { }
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
