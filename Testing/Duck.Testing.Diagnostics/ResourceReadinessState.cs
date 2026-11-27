namespace Duck.Testing.Diagnostics;

public enum ResourceReadinessReason
{
    NoObservation,
    AwaitingProgress,
    Ready,
    NonServiceEvidence,
    DecodeUnsuccessful,
    InvalidCount,
    CountRegressed,
    UnknownAge,
    InvalidAge,
    Expired,
    InvalidClock,
}

/// <summary>診断上の準備条件の判定結果。所有証明やサービス全体の正常性は示さない。</summary>
public sealed record ResourceReadinessResult(bool IsReady, ResourceReadinessReason Reason);

/// <summary>
/// 一つの入力について、正規化された復号の進捗を判定する。
/// 通信や資源の所有者とは連携しない。同じ件数を再取得しても進捗の有効期間を延長しない。
/// </summary>
public sealed class ResourceReadinessState
{
    private readonly object gate = new();
    private readonly string inputId;
    private readonly TimeSpan freshnessWindow;
    private readonly TimeProvider timeProvider;
    private ResourceDiagnosticIdentity expectedIdentity;
    private long lastSequence;
    private long? lastCount;
    private long? timestampHighWaterMark;
    private bool hasProgress;
    private long progressTimestamp;
    private TimeSpan progressAge;
    private ResourceReadinessReason reason = ResourceReadinessReason.NoObservation;

    public ResourceReadinessState(
        ResourceDiagnosticIdentity expectedIdentity,
        string inputId,
        TimeSpan freshnessWindow,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputId);
        if (freshnessWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(freshnessWindow));
        }

        this.expectedIdentity = expectedIdentity;
        this.inputId = inputId;
        this.freshnessWindow = freshnessWindow;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// 同じ実行と資源の新しい起動世代へ切り替える。変更を許可するのは診断データではなく所有者。
    /// プロセスの再起動では、コンテナ ID が同じままの場合もある。
    /// </summary>
    public void AdvanceGeneration(ResourceDiagnosticIdentity nextIdentity)
    {
        ArgumentNullException.ThrowIfNull(nextIdentity);
        lock (gate)
        {
            if (nextIdentity.RunId != expectedIdentity.RunId ||
                nextIdentity.ResourceId != expectedIdentity.ResourceId ||
                nextIdentity.Generation <= expectedIdentity.Generation)
            {
                throw new ArgumentException("A newer generation of the same run and resource is required.", nameof(nextIdentity));
            }

            expectedIdentity = nextIdentity;
            lastSequence = 0;
            lastCount = null;
            timestampHighWaterMark = null;
            Invalidate(ResourceReadinessReason.NoObservation);
        }
    }

    /// <summary>
    /// 入力または識別子の不一致、非正・重複・過去の番号は false として拒否する。
    /// 拒否した観測で新しい状態を上書きしない。一致する新しい観測は、
    /// その内容で準備未完了へ戻る場合も、受理を表す true を返す。
    /// </summary>
    public bool Observe(ResourceDiagnosticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (gate)
        {
            if (snapshot.Identity != expectedIdentity ||
                !string.Equals(snapshot.InputId, inputId, StringComparison.Ordinal) ||
                snapshot.ObservationSequence <= lastSequence)
            {
                return false;
            }

            lastSequence = snapshot.ObservationSequence;
            if (!TryReadTimestamp(out var now))
            {
                return true;
            }

            if (snapshot.EvidenceKind != DiagnosticEvidenceKind.ServiceDecode)
            {
                Invalidate(ResourceReadinessReason.NonServiceEvidence);
                return true;
            }
            if (snapshot.DecodeStatus != DiagnosticDecodeStatus.Succeeded)
            {
                Invalidate(ResourceReadinessReason.DecodeUnsuccessful);
                return true;
            }
            if (snapshot.SuccessfullyDecodedCount < 0)
            {
                Invalidate(ResourceReadinessReason.InvalidCount);
                return true;
            }
            if (lastCount is { } previousCount && snapshot.SuccessfullyDecodedCount < previousCount)
            {
                Invalidate(ResourceReadinessReason.CountRegressed);
                return true;
            }

            var increased = lastCount is { } count && snapshot.SuccessfullyDecodedCount > count;
            lastCount = snapshot.SuccessfullyDecodedCount;
            if (snapshot.DecodeAgeUpperBound is not { } age)
            {
                Invalidate(ResourceReadinessReason.UnknownAge);
                return true;
            }
            if (age < TimeSpan.Zero)
            {
                Invalidate(ResourceReadinessReason.InvalidAge);
                return true;
            }
            if (age > freshnessWindow)
            {
                Invalidate(ResourceReadinessReason.Expired);
                return true;
            }

            if (increased)
            {
                hasProgress = true;
                progressTimestamp = now;
                progressAge = age;
            }

            reason = hasProgress ? ResourceReadinessReason.Ready : ResourceReadinessReason.AwaitingProgress;
            return true;
        }
    }

    public ResourceReadinessResult GetReadiness()
    {
        lock (gate)
        {
            if (!TryReadTimestamp(out var now) || !hasProgress)
            {
                return new(false, reason);
            }

            var elapsed = timeProvider.GetElapsedTime(progressTimestamp, now);
            if (elapsed < TimeSpan.Zero)
            {
                Invalidate(ResourceReadinessReason.InvalidClock);
                return new(false, reason);
            }

            // 大きな経過時間を足して桁あふれしないよう、差分を先に求めて比較する。
            if (elapsed > freshnessWindow || progressAge > freshnessWindow - elapsed)
            {
                Invalidate(ResourceReadinessReason.Expired);
                return new(false, reason);
            }

            return new(true, ResourceReadinessReason.Ready);
        }
    }

    // gate の保持中だけ呼ぶ。読取りと観測で時計の高水位を共有し、無効化しても保持する。
    // 時計が追いついただけでは進捗を復活させない。
    private bool TryReadTimestamp(out long now)
    {
        now = timeProvider.GetTimestamp();
        if (timestampHighWaterMark is { } highWaterMark && now < highWaterMark)
        {
            Invalidate(ResourceReadinessReason.InvalidClock);
            return false;
        }

        timestampHighWaterMark = now;
        return true;
    }

    private void Invalidate(ResourceReadinessReason failure)
    {
        hasProgress = false;
        reason = failure;
    }
}
