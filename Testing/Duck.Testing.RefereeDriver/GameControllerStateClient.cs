using System.Net.WebSockets;
using System.Text.Json;

namespace Duck.Testing.RefereeDriver;

/// <summary>注入された内部接続先から試合状態だけを読む観測専用clientの境界。</summary>
internal sealed class GameControllerStateClient
{
    private readonly Uri endpoint;
    private readonly Func<Uri, CancellationToken, Task<WebSocket>> connect;
    private readonly GameControllerStateReadOptions options;
    private readonly TimeProvider clock;

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
        this.options = options ?? new();
        clock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>完成messageごとに初期Outputを読み飛ばし、有限読取窓内のHALTを観測する。</summary>
    internal async Task<GameControllerMatchSnapshot?> ReadMatchStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.MaximumMessageBytes <= 0 || options.Timeout <= TimeSpan.Zero)
            return null;
        var started = clock.GetTimestamp();
        try
        {
            using var socket = await connect(endpoint, cancellationToken).ConfigureAwait(false);
            using var message = new MemoryStream();
            var buffer = new byte[4096];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ReadWindowExpired(started))
                    return null;
                var received = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (ReadWindowExpired(started) || received.MessageType != WebSocketMessageType.Text)
                    return null;
                if (message.Length + received.Count > options.MaximumMessageBytes)
                    return null;
                message.Write(buffer, 0, received.Count);
                if (!received.EndOfMessage)
                    continue;

                using var output = JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
                if (output.RootElement.ValueKind != JsonValueKind.Object)
                    return null;
                if (!output.RootElement.TryGetProperty("matchState", out var matchState) ||
                    matchState.ValueKind == JsonValueKind.Null)
                {
                    message.SetLength(0);
                    continue;
                }
                if (matchState.ValueKind != JsonValueKind.Object)
                    return null;

                if (IsHalt(matchState, "command") && IsHalt(matchState, "gameState"))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return ReadWindowExpired(started)
                        ? null
                        : new(GameControllerCommandKind.Halt, GameControllerMatchPhase.Halt);
                }
                return null;
            }
        }
        catch (JsonException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        catch (WebSocketException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    /// <summary>読取開始からの単調経過で期限到達と時計逆行を拒否し、遅い正常応答も成功にしない。</summary>
    private bool ReadWindowExpired(long started)
    {
        var elapsed = clock.GetElapsedTime(started, clock.GetTimestamp());
        return elapsed < TimeSpan.Zero || elapsed >= options.Timeout;
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
