using System.Net.Sockets;
using Duck.Testing.Diagnostics;

namespace Duck.Testing.RefereeDriver;

/// <summary>注入されたUDP socketの受信結果と復号前の時計を単一pumpへ渡す。</summary>
internal sealed class UdpRefereeDatagramReceiver : IRefereeDatagramReceiver, IAsyncDisposable
{
    private readonly UdpClient client;
    private readonly bool ownsClient;
    private int disposed;

    internal UdpRefereeDatagramReceiver(UdpClient client, bool ownsClient = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
        this.ownsClient = ownsClient;
    }

    /// <summary>取消可能な受信の直後に時計を採り、payloadと実送信元を保持する。</summary>
    public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
        Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        try
        {
            var datagram = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            var attempt = captureReceive();
            return new(datagram.Buffer, datagram.RemoteEndPoint, attempt);
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    /// <summary>明示的に所有するsocketだけを一度破棄する。</summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0 && ownsClient)
            client.Dispose();
        return ValueTask.CompletedTask;
    }
}
