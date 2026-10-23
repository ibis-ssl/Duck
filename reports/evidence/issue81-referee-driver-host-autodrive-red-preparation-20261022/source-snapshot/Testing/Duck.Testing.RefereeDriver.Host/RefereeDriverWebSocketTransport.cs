using System.Net.WebSockets;

namespace Duck.Testing.RefereeDriver.Hosting;

/// <summary>既存制御clientへ、接続先を注入したWebSocket送信口を渡すための先行宣言。</summary>
internal sealed class RefereeDriverWebSocketTransport : IGameControllerWebSocketTransport
{
    internal RefereeDriverWebSocketTransport(Func<Uri, CancellationToken, Task<WebSocket>> connect)
    {
        ArgumentNullException.ThrowIfNull(connect);
    }

    /// <summary>先行試験用の未実装境界。接続・初期Output受信・送信・破棄はまだ行わない。</summary>
    public Task SendJsonAsync(Uri endpoint, string json, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
