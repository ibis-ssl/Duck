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
    private int isRunning;
    private readonly object commandGate = new();
    private ReceivedCommand? latestCommand;
    private TaskCompletionSource commandChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

    /// <summary>同時Runを拒否して単一pumpを保ち、終端時に当該世代の準備と実行所有を解除する。</summary>
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
            throw new InvalidOperationException("この受信機は既に実行中です。");
        try
        {
            var epoch = diagnostics.BeginEpoch(InputId);
            ClearCommand();
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
                        if (diagnostics.RecordFailure(datagram.ReceiveAttempt))
                            ClearCommand();
                        continue;
                    }
                    if (!packet.HasCommand || !packet.HasCommandCounter || !Enum.IsDefined(packet.Command))
                    {
                        if (diagnostics.RecordFailure(datagram.ReceiveAttempt))
                            ClearCommand();
                        continue;
                    }
                    var endpoint = datagram.RemoteEndpoint.ToString();
                    if (diagnostics.RecordSuccess(
                        datagram.ReceiveAttempt,
                        sourceKey: endpoint,
                        remoteEndpoint: endpoint))
                    {
                        PublishCommand(new(packet.Command, packet.CommandCounter, packet.Yellow?.Name ?? ""),
                            endpoint, epoch.Number);
                    }
                }
            }
            finally
            {
                try { diagnostics.EndEpoch(epoch); }
                finally { ClearCommand(); }
            }
        }
        finally
        {
            Interlocked.Exchange(ref isRunning, 0);
        }
    }

    /// <summary>単一pumpの新しい正常commandを待つ。同counterでは完了せず、自身では受信しない。</summary>
    public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
        uint? previousCommandCounter,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task changed;
            lock (commandGate)
            {
                if (latestCommand is { } latest &&
                    (previousCommandCounter is null ||
                        latest.Snapshot.CommandCounter != previousCommandCounter.Value))
                {
                    var current = diagnostics.GetSnapshots(InputId).FirstOrDefault(snapshot =>
                        snapshot.SourceKey == latest.SourceKey &&
                        snapshot.ReceiverEpoch == latest.ReceiverEpoch);
                    if (current is { DecodeStatus: DiagnosticDecodeStatus.Succeeded } &&
                        current.DecodeAgeUpperBound is { } age &&
                        age >= TimeSpan.Zero && age <= ServiceReceiveDiagnostics.FreshnessWindow)
                    {
                        return latest.Snapshot;
                    }
                }
                changed = commandChanged.Task;
            }
            await changed.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>診断に受理されたcommandを保持し、購読へ変更の再確認を通知する。</summary>
    private void PublishCommand(RefereeCommandSnapshot snapshot, string sourceKey, long receiverEpoch)
    {
        TaskCompletionSource changed;
        lock (commandGate)
        {
            latestCommand = new(snapshot, sourceKey, receiverEpoch);
            changed = commandChanged;
            commandChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        changed.TrySetResult();
    }

    /// <summary>失敗や受信世代の終了で、保持中のcommandを返せないようにする。</summary>
    private void ClearCommand()
    {
        lock (commandGate)
            latestCommand = null;
    }

    private sealed record ReceivedCommand(
        RefereeCommandSnapshot Snapshot,
        string SourceKey,
        long ReceiverEpoch);

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
