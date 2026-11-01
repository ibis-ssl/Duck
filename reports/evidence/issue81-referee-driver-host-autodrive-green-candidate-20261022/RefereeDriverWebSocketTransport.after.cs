using System.Net.WebSockets;
using System.Text;

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
        using var socket = await connect(endpoint, cancellationToken).ConfigureAwait(false);
        var buffer = new byte[4096];
        while (true)
        {
            var received = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (received.MessageType != WebSocketMessageType.Text)
                throw new InvalidOperationException("制御WebSocketの初期Outputがtextでないか、受信前に切断された。");
            if (received.EndOfMessage)
                break;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await socket.SendAsync(Encoding.UTF8.GetBytes(json).AsMemory(),
            WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
    }
}
