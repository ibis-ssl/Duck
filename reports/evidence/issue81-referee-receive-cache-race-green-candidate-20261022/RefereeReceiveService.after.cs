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
    private readonly IAsyncDisposable? ownedReceiver;
    private readonly object lifecycleGate = new();
    private readonly CancellationTokenSource stop = new();
    private Task? activeRun;
    private Task? disposal;
    private readonly object commandGate = new();
    private ReceivedCommand? latestCommand;
    private TaskCompletionSource commandChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>競合試験が既存処理の境界を観測する。未指定時は処理へ介入しない。</summary>
    internal Action<RefereeReceiveBoundary>? BoundaryObserver { get; init; }

    internal RefereeReceiveService(
        IRefereeDatagramReceiver receiver,
        ServiceReceiveDiagnostics diagnostics,
        Func<ReadOnlyMemory<byte>, global::Referee>? decode = null,
        IAsyncDisposable? ownedReceiver = null)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(diagnostics);
        this.receiver = receiver;
        this.diagnostics = diagnostics;
        this.ownedReceiver = ownedReceiver;
        this.decode = decode ?? (payload => global::Referee.Parser.ParseFrom(payload.Span));
    }

    /// <summary>同時Runを拒否して単一pumpを保ち、終端時に当該世代の準備と実行所有を解除する。</summary>
    internal Task RunAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource start;
        Task run;
        lock (lifecycleGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(disposal is not null, this);
            if (activeRun is { IsCompleted: false })
                throw new InvalidOperationException("この受信機は既に実行中です。");
            start = new(TaskCreationOptions.RunContinuationsAsynchronously);
            activeRun = run = RunCoreAsync(cancellationToken, start.Task);
        }
        start.TrySetResult();
        return run;
    }

    /// <summary>Runの公開後に受信を開始し、呼出し元または内部停止で同じ世代を解除する。</summary>
    private async Task RunCoreAsync(CancellationToken cancellationToken, Task start)
    {
        await start.ConfigureAwait(false);
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stop.Token);
        var receiveCancellation = runCancellation.Token;
        var epoch = diagnostics.BeginEpoch(InputId);
        ClearCommand();
        try
        {
            while (true)
            {
                receiveCancellation.ThrowIfCancellationRequested();
                var datagram = await receiver.ReceiveAsync(
                    () => diagnostics.BeginReceive(epoch),
                    receiveCancellation);
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
                    out var acceptedCount,
                    sourceKey: endpoint,
                    remoteEndpoint: endpoint))
                {
                    PublishCommand(new(packet.Command, packet.CommandCounter, packet.Yellow?.Name ?? ""),
                        endpoint, epoch.Number, acceptedCount);
                }
            }
        }
        finally
        {
            try { diagnostics.EndEpoch(epoch); }
            finally { ClearCommand(); }
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
                    BoundaryObserver?.Invoke(RefereeReceiveBoundary.BeforeCommandDiagnosticRead);
                    var current = diagnostics.GetSnapshots(InputId).FirstOrDefault(snapshot =>
                        snapshot.SourceKey == latest.SourceKey &&
                        snapshot.ReceiverEpoch == latest.ReceiverEpoch);
                    if (current is { DecodeStatus: DiagnosticDecodeStatus.Succeeded } &&
                        current.SuccessfullyDecodedCount == latest.AcceptedCount &&
                        current.DecodeAgeUpperBound is { } age &&
                        age >= TimeSpan.Zero && age <= ServiceReceiveDiagnostics.FreshnessWindow)
                    {
                        return latest.Snapshot;
                    }
                }
                changed = commandChanged.Task;
            }
            BoundaryObserver?.Invoke(RefereeReceiveBoundary.BeforeCommandWait);
            await changed.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>同packetの受理件数とcommandを保持し、別packetの新しさを旧値へ流用しない。</summary>
    private void PublishCommand(RefereeCommandSnapshot snapshot, string sourceKey, long receiverEpoch, long acceptedCount)
    {
        BoundaryObserver?.Invoke(RefereeReceiveBoundary.BeforeCommandPublish);
        TaskCompletionSource changed;
        lock (commandGate)
        {
            latestCommand = new(snapshot, sourceKey, receiverEpoch, acceptedCount);
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
        long ReceiverEpoch,
        long AcceptedCount);

    /// <summary>一度だけ内部停止を開始し、明示所有資源の破棄と実Runの終端を待つ。</summary>
    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completed;
        Task? run;
        lock (lifecycleGate)
        {
            if (disposal is not null)
                return new(disposal);
            completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            disposal = completed.Task;
            run = activeRun;
        }
        _ = CompleteDisposeAsync(run, completed);
        return new(completed.Task);
    }

    /// <summary>取消と資源破棄の失敗時もRunを回収し、失敗を同じDispose結果へ伝える。</summary>
    private async Task CompleteDisposeAsync(Task? run, TaskCompletionSource completed)
    {
        Exception? failure = null;
        try
        {
            try
            {
                await stop.CancelAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (ownedReceiver is not null)
                        await ownedReceiver.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    if (run is not null)
                    {
                        try { await run.ConfigureAwait(false); }
                        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                    }
                }
            }
        }
        catch (Exception error)
        {
            failure = error;
        }
        finally
        {
            stop.Dispose();
        }
        if (failure is null)
            completed.TrySetResult();
        else
            completed.TrySetException(failure);
    }
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

/// <summary>競合試験で既存の順序を固定するための内部観測点。</summary>
internal enum RefereeReceiveBoundary
{
    BeforeCommandDiagnosticRead,
    BeforeCommandPublish,
    BeforeCommandWait,
}
