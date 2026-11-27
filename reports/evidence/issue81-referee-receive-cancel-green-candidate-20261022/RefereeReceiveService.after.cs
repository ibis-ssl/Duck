using System.Net;
using Duck.Testing.Diagnostics;

namespace Duck.Testing.RefereeDriver;

/// <summary>単一の受信処理から、自身が復号したpacketの診断を更新する。</summary>
internal sealed class RefereeReceiveService : IRefereeCommandSource, IAsyncDisposable
{
    internal const string InputId = "referee";
    private readonly IRefereeDatagramReceiver receiver;
    private readonly ServiceReceiveDiagnostics diagnostics;
    private readonly Func<ReadOnlyMemory<byte>, global::Referee> decode;

    internal RefereeReceiveService(
        IRefereeDatagramReceiver receiver,
        ServiceReceiveDiagnostics diagnostics,
        Func<ReadOnlyMemory<byte>, global::Referee>? decode = null)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(diagnostics);
        this.receiver = receiver;
        this.diagnostics = diagnostics;
        this.decode = decode ?? (payload => global::Referee.Parser.ParseFrom(payload.Span));
    }

    /// <summary>正常受信と不正入力を同じpumpで記録し、終端時に当該世代の準備を解除する。</summary>
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var epoch = diagnostics.BeginEpoch(InputId);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var datagram = await receiver.ReceiveAsync(
                    () => diagnostics.BeginReceive(epoch),
                    cancellationToken);
                global::Referee packet;
                try
                {
                    packet = decode(datagram.Payload);
                }
                catch (Google.Protobuf.InvalidProtocolBufferException)
                {
                    diagnostics.RecordFailure(datagram.ReceiveAttempt);
                    continue;
                }
                if (!packet.HasCommand || !packet.HasCommandCounter || !Enum.IsDefined(packet.Command))
                {
                    diagnostics.RecordFailure(datagram.ReceiveAttempt);
                    continue;
                }
                var endpoint = datagram.RemoteEndpoint.ToString();
                diagnostics.RecordSuccess(
                    datagram.ReceiveAttempt,
                    sourceKey: endpoint,
                    remoteEndpoint: endpoint);
            }
        }
        finally
        {
            diagnostics.EndEpoch(epoch);
        }
    }

    /// <summary>command変更の購読は未実装であり、診断用の受信を別に開始しない。</summary>
    public ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
        uint? previousCommandCounter,
        CancellationToken cancellationToken) =>
        ValueTask.FromException<RefereeCommandSnapshot>(new NotImplementedException());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>実際の受信直後、復号前に時計を採取するtransportの境界。</summary>
internal interface IRefereeDatagramReceiver
{
    ValueTask<RefereeReceivedDatagram> ReceiveAsync(
        Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive,
        CancellationToken cancellationToken);
}

/// <summary>受信したpayload、観測endpoint、同じ受信時点の診断時計を一緒に渡す。</summary>
internal sealed record RefereeReceivedDatagram(
    ReadOnlyMemory<byte> Payload,
    IPEndPoint RemoteEndpoint,
    ServiceReceiveDiagnostics.ReceiveAttempt ReceiveAttempt);
