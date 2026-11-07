using System.Net.WebSockets;

namespace Duck.Testing.RefereeDriver;

/// <summary>注入された内部接続先から試合状態だけを読む観測専用clientの境界。</summary>
internal sealed class GameControllerStateClient
{
    internal GameControllerStateClient(
        Uri endpoint,
        Func<Uri, CancellationToken, Task<WebSocket>> connect)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(connect);
    }

    /// <summary>公開matchStateを読む。先行試験用scaffoldではまだ観測値を供給しない。</summary>
    internal Task<GameControllerMatchSnapshot?> ReadMatchStateAsync(CancellationToken cancellationToken) =>
        Task.FromResult<GameControllerMatchSnapshot?>(null);
}

/// <summary>APIの数値列挙やUDPの整数値を直接使わない、明示的な指令区分。</summary>
internal enum GameControllerCommandKind
{
    Unknown,
    Halt,
}

/// <summary>公開matchStateから正規化する試合状態。未確認は成功状態にしない。</summary>
internal enum GameControllerMatchPhase
{
    Unknown,
    Halt,
}

/// <summary>API公開状態の正規化結果。UDP件数・受信時刻・所有証明は含めない。</summary>
internal sealed record GameControllerMatchSnapshot(
    GameControllerCommandKind Command,
    GameControllerMatchPhase Phase);
