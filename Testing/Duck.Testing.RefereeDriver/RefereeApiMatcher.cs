using Duck.Testing.Diagnostics;

namespace Duck.Testing.RefereeDriver;

/// <summary>同じローカル観測contextのUDP指令とAPI状態を、有限窓で照合する境界。</summary>
internal sealed class RefereeApiMatcher
{
    private readonly object gate = new();
    private readonly RefereeReceiveService receiver;
    private readonly GameControllerStateObserver observer;
    private long requestSequence;
    private RefereeApiMatchResult? current;
    private TaskCompletionSource observationCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal RefereeApiMatcher(RefereeReceiveService receiver, GameControllerStateObserver observer)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(observer);
        this.receiver = receiver;
        this.observer = observer;
    }

    /// <summary>開始時のUDP証跡を保持してAPIを取得し、完了時にも同じpacketと現在有効なAPI要求を照合する。</summary>
    internal async Task<RefereeApiMatchResult> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long sequence;
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sequence = checked(++requestSequence);
            current = null;
        }

        try
        {
            var initial = receiver.GetCurrentObservation();
            if (!IsUsable(initial))
                return Complete(sequence, new(false, "NoFreshUdp", initial, null));
            var api = await observer.ReadAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var latestUdp = receiver.GetCurrentObservation();
            var latestApi = observer.GetCurrent();
            var result = Evaluate(initial, latestUdp, api, latestApi);
            cancellationToken.ThrowIfCancellationRequested();
            return Complete(sequence, result);
        }
        catch
        {
            // 例外を通常成功へ変換せず、古い要求から新しい保持値も解除しない。
            lock (gate)
                if (sequence == requestSequence)
                    current = null;
            throw;
        }
    }

    /// <summary>保持中の対応を現在の両証跡で再検証するだけで、新規受信やAPI再取得を行わない。</summary>
    internal RefereeApiMatchResult GetCurrent()
    {
        long sequence;
        RefereeApiMatchResult? previous;
        lock (gate)
        {
            sequence = requestSequence;
            previous = current;
        }
        if (previous is null)
            return new(false, "NoObservation", null, null);

        var latestUdp = receiver.GetCurrentObservation();
        var latestApi = observer.GetCurrent();
        var result = Evaluate(previous.Udp, latestUdp, previous.Api, latestApi);
        lock (gate)
        {
            if (sequence != requestSequence || !ReferenceEquals(previous, current))
                return new(false, "Superseded", null, null);
            if (!result.IsReady)
                current = null;
            return result;
        }
    }

    /// <summary>単一観測の次の完了を期限付きで共有し、返却直前に現在の両証跡を再検証する。</summary>
    internal async Task<RefereeApiMatchResult> WaitForCurrentAsync(
        TimeSpan timeout, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        cancellationToken.ThrowIfCancellationRequested();
        var immediate = GetCurrent();
        if (immediate.IsReady)
            return immediate;

        using var deadline = new CancellationTokenSource(timeout, clock);
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            while (true)
            {
                waiting.Token.ThrowIfCancellationRequested();
                Task completed;
                // 状態の再検査より先に通知を確保し、両者の間の完了を取りこぼさない。
                lock (gate)
                    completed = observationCompleted.Task;
                var result = GetCurrent();
                waiting.Token.ThrowIfCancellationRequested();
                if (result.IsReady)
                    return result;
                await completed.WaitAsync(waiting.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (
            deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return GetCurrent() with { IsReady = false, Reason = "ObservationWaitTimedOut" };
        }
    }

    /// <summary>当該要求がまだ最新の場合だけ結果を保持し、遅い旧結果で新しい一致を上書きしない。</summary>
    private RefereeApiMatchResult Complete(long sequence, RefereeApiMatchResult result)
    {
        lock (gate)
        {
            if (sequence != requestSequence)
                return new(false, "Superseded", result.Udp, result.Api);
            current = result.IsReady ? result : null;
            var completed = observationCompleted;
            observationCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            completed.TrySetResult();
            return result;
        }
    }

    /// <summary>双方の有限な新しさと同一contextを確認した後にだけ、明示的な状態対応を評価する。</summary>
    private static RefereeApiMatchResult Evaluate(
        RefereeReceiveObservation? expectedUdp,
        RefereeReceiveObservation? latestUdp,
        GameControllerApiObservation? requestedApi,
        GameControllerApiObservation? latestApi)
    {
        if (!IsUsable(expectedUdp) || !IsUsable(latestUdp))
            return new(false, "NoFreshUdp", latestUdp, latestApi);
        if (!SamePacket(expectedUdp!, latestUdp!))
            return new(false, "UdpContextChanged", latestUdp, latestApi);
        if (requestedApi is null || latestApi is null)
            return new(false, "NoFreshApi", latestUdp, latestApi);
        if (!SameApiContext(requestedApi, latestApi))
            return new(false, "ApiContextChanged", latestUdp, latestApi);
        if (!GameControllerStateMapping.MatchesUdpCommand(latestApi.State, latestUdp!.Command.Command))
            return new(false, "CommandMismatch", latestUdp, latestApi);
        return new(true, "Matched", latestUdp, latestApi);
    }

    /// <summary>明示ownerと当該入力の正常復号・進捗・既知の受信ageが揃う場合だけ証跡を使用する。</summary>
    private static bool IsUsable(RefereeReceiveObservation? observation) =>
        observation is { Diagnostic: var diagnostic } &&
        diagnostic.Identity is not null &&
        diagnostic.InputId == RefereeReceiveService.InputId &&
        diagnostic.ReceiverEpoch > 0 &&
        !string.IsNullOrWhiteSpace(diagnostic.SourceKey) &&
        diagnostic.SuccessfullyDecodedCount > 0 &&
        diagnostic.DecodeStatus == DiagnosticDecodeStatus.Succeeded &&
        diagnostic.IsReady &&
        diagnostic.DecodeAgeUpperBound is { } age &&
        age >= TimeSpan.Zero && age <= ServiceReceiveDiagnostics.FreshnessWindow;

    /// <summary>API取得開始時の同packet証跡を後の取得で置き換えず、同counterの新packetも区別する。</summary>
    private static bool SamePacket(RefereeReceiveObservation expected, RefereeReceiveObservation actual) =>
        expected.Diagnostic.Identity == actual.Diagnostic.Identity &&
        expected.Diagnostic.SourceKey == actual.Diagnostic.SourceKey &&
        expected.Diagnostic.ReceiverEpoch == actual.Diagnostic.ReceiverEpoch &&
        expected.Diagnostic.SuccessfullyDecodedCount == actual.Diagnostic.SuccessfullyDecodedCount &&
        expected.Command.Command == actual.Command.Command &&
        expected.Command.CommandCounter == actual.Command.CommandCounter;

    /// <summary>新しいAPI要求やURL設定世代の証跡を、別の要求の結果へ流用しない。</summary>
    private static bool SameApiContext(GameControllerApiObservation expected, GameControllerApiObservation actual) =>
        expected.Endpoint == actual.Endpoint &&
        expected.EndpointGeneration == actual.EndpointGeneration &&
        expected.RequestSequence == actual.RequestSequence &&
        expected.ReadStartedTimestamp == actual.ReadStartedTimestamp &&
        expected.ReadCompletedTimestamp == actual.ReadCompletedTimestamp &&
        expected.State == actual.State;
}

/// <summary>照合とそのローカル証跡。資源のDCP所有証明や外部サービスの受信診断ではない。</summary>
internal sealed record RefereeApiMatchResult(
    bool IsReady,
    string Reason,
    RefereeReceiveObservation? Udp,
    GameControllerApiObservation? Api);
