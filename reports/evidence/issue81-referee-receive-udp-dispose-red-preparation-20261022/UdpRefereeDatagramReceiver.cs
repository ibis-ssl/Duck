using System.Net;
using System.Net.Sockets;
using Duck.Testing.Diagnostics;

namespace Duck.Testing.RefereeDriver;

/// <summary>単一pumpへUDP受信を渡す境界。先行テスト用で実受信と資源破棄は未実装。</summary>
internal sealed class UdpRefereeDatagramReceiver : IRefereeDatagramReceiver, IAsyncDisposable
{
    internal UdpRefereeDatagramReceiver(UdpClient client)
    {
    }

    /// <summary>呼出し可能な最小scaffoldとして空の受信結果を返す。</summary>
    public ValueTask<RefereeReceivedDatagram> ReceiveAsync(
        Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new RefereeReceivedDatagram(
            ReadOnlyMemory<byte>.Empty, new IPEndPoint(IPAddress.None, 0), captureReceive()));

    /// <summary>資源破棄の実装前に呼出し境界だけを用意する。</summary>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
