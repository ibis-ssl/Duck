using System.Net.WebSockets;
using System.Text.Json;

namespace Duck.Testing.RefereeDriver;

/// <summary>注入された内部接続先から試合状態だけを読む観測専用clientの境界。</summary>
internal sealed class GameControllerStateClient
{
    private readonly Uri endpoint;
    private readonly Func<Uri, CancellationToken, Task<WebSocket>> connect;

    internal GameControllerStateClient(
        Uri endpoint,
        Func<Uri, CancellationToken, Task<WebSocket>> connect,
        GameControllerStateReadOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(connect);
        this.endpoint = endpoint;
        this.connect = connect;
        _ = options;
        _ = timeProvider;
    }

    /// <summary>初期の別Outputを読み飛ばし、公開matchStateのHALTを観測専用接続から読む。</summary>
    internal async Task<GameControllerMatchSnapshot?> ReadMatchStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var socket = await connect(endpoint, cancellationToken).ConfigureAwait(false);
        var buffer = new byte[4096];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var received = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (received.MessageType != WebSocketMessageType.Text || !received.EndOfMessage)
                return null;

            using var output = JsonDocument.Parse(buffer.AsMemory(0, received.Count));
            if (output.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            if (!output.RootElement.TryGetProperty("matchState", out var matchState) ||
                matchState.ValueKind == JsonValueKind.Null)
                continue;
            if (matchState.ValueKind != JsonValueKind.Object)
                return null;

            if (IsHalt(matchState, "command") && IsHalt(matchState, "gameState"))
                return new(GameControllerCommandKind.Halt, GameControllerMatchPhase.Halt);
            return null;
        }
    }

    /// <summary>APIのHALT文字列だけを明示確認し、別の列挙体系へ整数変換しない。</summary>
    private static bool IsHalt(JsonElement matchState, string propertyName) =>
        matchState.TryGetProperty(propertyName, out var state) &&
        state.ValueKind == JsonValueKind.Object &&
        state.TryGetProperty("type", out var type) &&
        type.ValueKind == JsonValueKind.String &&
        type.GetString() == "HALT";
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

/// <summary>一回の状態読取に明示注入する有限窓と、完成message全体のbyte上限。</summary>
internal sealed record GameControllerStateReadOptions
{
    internal int MaximumMessageBytes { get; init; } = 4096;
    internal TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);
}
