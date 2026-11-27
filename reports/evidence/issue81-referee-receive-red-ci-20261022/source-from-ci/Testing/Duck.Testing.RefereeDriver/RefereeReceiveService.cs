using System.Net;
using Duck.Testing.Diagnostics;

namespace Duck.Testing.RefereeDriver;

/// <summary>同じ受信処理で診断とcommandの購読を供給するための、03A先行試験用の契約。</summary>
internal sealed class RefereeReceiveService : IRefereeCommandSource, IAsyncDisposable
{
    internal const string InputId = "referee";

    internal RefereeReceiveService(
        IRefereeDatagramReceiver receiver,
        ServiceReceiveDiagnostics diagnostics,
        Func<ReadOnlyMemory<byte>, global::Referee>? decode = null)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(diagnostics);
    }

    /// <summary>受信処理は未実装。先行試験の実行時失敗を得るまでpacketを処理しない。</summary>
    internal Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
