using System.Net.WebSockets;

namespace Duck.Testing.RefereeDriver.Hosting;

/// <summary>既存制御clientから渡されたJSONを、注入先のWebSocketへ送る。</summary>
internal sealed class RefereeDriverWebSocketTransport : IGameControllerWebSocketTransport
{
    private readonly Func<Uri, CancellationToken, Task<WebSocket>> connect;

    internal RefereeDriverWebSocketTransport(Func<Uri, CancellationToken, Task<WebSocket>> connect)
    {
        ArgumentNullException.ThrowIfNull(connect);
        this.connect = connect;
    }

    /// <summary>初期Output全体の受信後に一度だけ送信し、正常・失敗・取消の全経路でsocketを破棄する。</summary>
    public async Task SendJsonAsync(Uri endpoint, string json, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await GameControllerWebSocketTransportOperations.SendJsonAsync(
            connect, endpoint, json, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool?> ReadAutoContinueAsync(Uri endpoint, CancellationToken cancellationToken) =>
        GameControllerWebSocketTransportOperations.ReadAutoContinueAsync(connect, endpoint, cancellationToken);

    public Task<bool> SendConfigDeltaAndWaitForAutoContinueAsync(
        Uri endpoint,
        string json,
        bool expectedAutoContinue,
        CancellationToken cancellationToken) =>
        GameControllerWebSocketTransportOperations.SendConfigDeltaAndWaitForAutoContinueAsync(
            connect, endpoint, json, expectedAutoContinue, cancellationToken);
}
