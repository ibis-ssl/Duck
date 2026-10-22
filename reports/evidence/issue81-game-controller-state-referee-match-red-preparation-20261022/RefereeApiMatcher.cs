namespace Duck.Testing.RefereeDriver;

/// <summary>同じローカル観測contextのUDP指令とAPI状態を、有限窓で照合する境界。</summary>
internal sealed class RefereeApiMatcher
{
    internal RefereeApiMatcher(RefereeReceiveService receiver, GameControllerStateObserver observer)
    {
        _ = receiver;
        _ = observer;
    }

    /// <summary>先行試験用の未実装入口。受信やAPI取得、照合は行わない。</summary>
    internal Task<RefereeApiMatchResult> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new RefereeApiMatchResult(false, "NoObservation", null, null));

    /// <summary>先行試験用の未実装入口。既存入力を準備完了として返さない。</summary>
    internal RefereeApiMatchResult GetCurrent() => new(false, "NoObservation", null, null);
}

/// <summary>照合とそのローカル証跡。資源のDCP所有証明や外部サービスの受信診断ではない。</summary>
internal sealed record RefereeApiMatchResult(
    bool IsReady,
    string Reason,
    RefereeReceiveObservation? Udp,
    GameControllerApiObservation? Api);
