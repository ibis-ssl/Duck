namespace Duck.Testing.Diagnostics;

/// <summary>
/// このプロセス自身の正常な復号の証跡を保持する。識別子は外部から明示的に渡されるもので、
/// ここでは探索・認証しない。UTC は診断表示用とし、新鮮度はローカルの単調時計で判定する。
/// </summary>
public sealed class ServiceReceiveDiagnostics(
    ResourceDiagnosticIdentity? identity = null,
    TimeProvider? timeProvider = null)
{
    private readonly object gate = new();
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly SampledClock readinessClock = new(timeProvider ?? TimeProvider.System);
    private readonly Dictionary<string, Input> inputs = new(StringComparer.Ordinal);
    private ResourceDiagnosticIdentity? currentIdentity = identity;
    private long? clockHighWaterMark;
    private long clockVersion;
    public ResourceDiagnosticIdentity? Identity
    {
        get { lock (gate) return currentIdentity; }
    }
    public static readonly TimeSpan FreshnessWindow = TimeSpan.FromSeconds(5);

    public static ResourceDiagnosticIdentity? ParseIdentity(
        string? runId, string? resourceId, string? containerId, string? generation)
    {
        if (!long.TryParse(generation, out var parsedGeneration))
            return null;
        try { return new(runId!, resourceId!, containerId!, parsedGeneration); }
        catch (ArgumentException) { return null; }
    }

    /// <summary>識別子を切り替え、旧受信試行を無効化した新しい受信世代を返す。</summary>
    public ReceiveEpoch ReplaceIdentityAndBeginEpoch(ResourceDiagnosticIdentity newIdentity, string inputId)
    {
        ArgumentNullException.ThrowIfNull(newIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputId);
        lock (gate)
        {
            if (currentIdentity == newIdentity && inputs.TryGetValue(inputId, out var existing) && existing.Epoch is not null)
                return existing.Epoch;

            currentIdentity = newIdentity;
            clockVersion = checked(clockVersion + 1);
            foreach (var input in inputs.Values)
            {
                input.Epoch = null;
                input.ErrorCount = 0;
                foreach (var source in input.Sources.Values)
                {
                    source.Count = 0;
                    source.Sequence = 0;
                    source.Name = null;
                    source.Uuid = null;
                    source.RemoteEndpoint = null;
                    source.Stamp = null;
                    source.LastReceivedAt = null;
                    source.Status = DiagnosticDecodeStatus.Unknown;
                    source.Readiness = CreateReadiness(input.Id, source.Key);
                }
            }

            if (!inputs.TryGetValue(inputId, out var target))
                inputs.Add(inputId, target = new Input(inputId));
            var epoch = new ReceiveEpoch(inputId, checked(++target.EpochNumber));
            target.Epoch = epoch;
            return epoch;
        }
    }

    /// <summary>受信機の再起動で有効な進捗を無効化する。累積件数はリセットしない。</summary>
    public ReceiveEpoch BeginEpoch(string inputId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputId);
        lock (gate)
        {
            if (!inputs.TryGetValue(inputId, out var input))
                inputs.Add(inputId, input = new Input(inputId));
            var epoch = new ReceiveEpoch(inputId, checked(++input.EpochNumber));
            input.Epoch = epoch;
            foreach (var source in input.Sources.Values)
            {
                source.Stamp = null;
                source.LastReceivedAt = null;
                source.Status = DiagnosticDecodeStatus.Unknown;
                source.Readiness = CreateReadiness(inputId, source.Key);
            }
            return epoch;
        }
    }

    /// <summary>一致する受信世代を終了し、累積件数と最終受信UTCを保ったまま準備を解除する。</summary>
    public bool EndEpoch(ReceiveEpoch epoch)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        lock (gate)
        {
            if (!inputs.TryGetValue(epoch.InputId, out var input) || !ReferenceEquals(input.Epoch, epoch))
                return false;
            input.Epoch = null;
            foreach (var source in input.Sources.Values)
            {
                source.Stamp = null;
                source.Status = DiagnosticDecodeStatus.Unknown;
                source.Readiness = CreateReadiness(input.Id, source.Key);
            }
            return true;
        }
    }

    /// <summary>ReceiveAsync の直後、復号や記録処理の前に呼ぶ。</summary>
    public ReceiveAttempt BeginReceive(ReceiveEpoch epoch)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        lock (gate)
        {
            var validClock = ReadClock(out var now);
            return new ReceiveAttempt(epoch, now, clock.GetUtcNow(), validClock, clockVersion);
        }
    }

    public bool RecordSuccess(ReceiveAttempt attempt, string sourceKey = "",
        string? sourceName = null, string? sourceUuid = null, string? remoteEndpoint = null) =>
        RecordSuccess(attempt, out _, sourceKey, sourceName, sourceUuid, remoteEndpoint);

    /// <summary>正常な復号を記録し、同じ診断lock内でこのpacketに割り当てた成功件数を返す。</summary>
    public bool RecordSuccess(ReceiveAttempt attempt, out long acceptedCount, string sourceKey = "",
        string? sourceName = null, string? sourceUuid = null, string? remoteEndpoint = null)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(sourceKey);
        acceptedCount = 0;
        lock (gate)
        {
            if (!TryInput(attempt, out var input)) return false;
            var validClock = ReadClock(out var now);
            if (!input.Sources.TryGetValue(sourceKey, out var source))
                input.Sources.Add(sourceKey, source = new Source(sourceKey, CreateReadiness(input.Id, sourceKey)));
            source.Count = checked(source.Count + 1);
            source.Name = sourceName;
            source.Uuid = sourceUuid;
            source.RemoteEndpoint = remoteEndpoint;
            source.Status = DiagnosticDecodeStatus.Succeeded;
            source.LastReceivedAt = attempt.ReceivedAt;
            source.Stamp = attempt.ClockWasValid && attempt.ClockVersion == clockVersion && validClock && now >= attempt.Timestamp ? attempt.Timestamp : null;
            Observe(input, source, now);
            acceptedCount = source.Count;
            return true;
        }
    }

    /// <summary>送信元を特定できない通信の失敗では、送信元を推測せず各送信元の進捗を無効化する。</summary>
    public bool RecordFailure(ReceiveAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        lock (gate)
        {
            if (!TryInput(attempt, out var input)) return false;
            ReadClock(out var now);
            input.ErrorCount = checked(input.ErrorCount + 1);
            foreach (var source in input.Sources.Values)
            {
                source.Status = DiagnosticDecodeStatus.Failed;
                Observe(input, source, now);
            }
            return true;
        }
    }

    public IReadOnlyList<ServiceReceiveSnapshot> GetSnapshots(string inputId)
    {
        lock (gate)
        {
            ReadClock(out var now);
            if (!inputs.TryGetValue(inputId, out var input) || input.Sources.Count == 0)
                return [new(Identity, inputId, input?.EpochNumber ?? 0, "", null, null, null, 0, input?.ErrorCount ?? 0,
                    null, null, DiagnosticDecodeStatus.Unknown, false, "NoObservation")];
            return input.Sources.Values.OrderBy(s => s.Key, StringComparer.Ordinal).Select(source =>
            {
                var age = Age(source.Stamp, now);
                var result = source.Readiness?.GetReadiness();
                var ready = age is { } knownAge && knownAge <= FreshnessWindow &&
                    source.Status == DiagnosticDecodeStatus.Succeeded && result?.IsReady == true;
                return new ServiceReceiveSnapshot(Identity, inputId, input.EpochNumber, source.Key, source.Name,
                    source.Uuid, source.RemoteEndpoint, source.Count, input.ErrorCount,
                    source.LastReceivedAt, age, source.Status, ready,
                    Identity is null ? "Unattributed" : age is null ? "UnknownAge" : result?.Reason.ToString() ?? "NoObservation");
            }).ToArray();
        }
    }

    private ResourceReadinessState? CreateReadiness(string input, string source) =>
        Identity is null ? null : new(Identity, input + "\u001f" + source, FreshnessWindow, readinessClock);

    private void Observe(Input input, Source source, long now)
    {
        if (source.Readiness is null || Identity is null) return;
        source.Readiness.Observe(new(Identity, input.Id + "\u001f" + source.Key,
            checked(++source.Sequence), DiagnosticEvidenceKind.ServiceDecode, source.Status,
            source.Count, Age(source.Stamp, now)));
    }

    private TimeSpan? Age(long? stamp, long now)
    {
        if (stamp is null || now < stamp.Value) return null;
        var elapsed = clock.GetElapsedTime(stamp.Value, now);
        return elapsed < TimeSpan.Zero ? null : elapsed;
    }

    private bool ReadClock(out long now)
    {
        now = clock.GetTimestamp();
        readinessClock.Timestamp = now;
        if (clockHighWaterMark is { } high && now < high)
        {
            clockVersion++;
            foreach (var source in inputs.Values.SelectMany(i => i.Sources.Values))
                source.Stamp = null;
            return false;
        }
        clockHighWaterMark = now;
        return true;
    }

    private bool TryInput(ReceiveAttempt attempt, out Input input)
    {
        if (inputs.TryGetValue(attempt.Epoch.InputId, out var found) &&
            ReferenceEquals(found.Epoch, attempt.Epoch))
        {
            input = found;
            return true;
        }
        input = null!;
        return false;
    }

    public sealed class ReceiveEpoch
    {
        internal ReceiveEpoch(string inputId, long number) => (InputId, Number) = (inputId, number);
        public long Number { get; }
        internal string InputId { get; }
    }

    public sealed class ReceiveAttempt
    {
        internal ReceiveAttempt(ReceiveEpoch epoch, long timestamp, DateTimeOffset receivedAt, bool clockWasValid, long clockVersion)
            => (Epoch, Timestamp, ReceivedAt, ClockWasValid, ClockVersion) = (epoch, timestamp, receivedAt, clockWasValid, clockVersion);
        internal ReceiveEpoch Epoch { get; }
        internal long Timestamp { get; }
        internal DateTimeOffset ReceivedAt { get; }
        internal bool ClockWasValid { get; }
        internal long ClockVersion { get; }
    }

    // gate の保持中、有効期間と渡す経過時間には同じ時計の採取時点を使う。
    private sealed class SampledClock(TimeProvider source) : TimeProvider
    {
        public long Timestamp { get; set; }
        public override long TimestampFrequency => source.TimestampFrequency;
        public override long GetTimestamp() => Timestamp;
    }

    private sealed class Input(string id)
    {
        public string Id { get; } = id;
        public ReceiveEpoch? Epoch { get; set; }
        public long ErrorCount { get; set; }
        public long EpochNumber { get; set; }
        public Dictionary<string, Source> Sources { get; } = new(StringComparer.Ordinal);
    }

    private sealed class Source(string key, ResourceReadinessState? readiness)
    {
        public string Key { get; } = key;
        public long Count;
        public long Sequence;
        public string? Name;
        public string? Uuid;
        public string? RemoteEndpoint;
        public long? Stamp;
        public DateTimeOffset? LastReceivedAt;
        public DiagnosticDecodeStatus Status;
        public ResourceReadinessState? Readiness = readiness;
    }
}

/// <summary>自身が報告する復号の証跡。資源の所有証明や送信側の正常性を示すものではない。</summary>
public sealed record ServiceReceiveSnapshot(
    ResourceDiagnosticIdentity? Identity, string InputId, long ReceiverEpoch, string SourceKey, string? SourceName,
    string? SourceUuid, string? RemoteEndpoint, long SuccessfullyDecodedCount, long DecodeFailureCount,
    DateTimeOffset? LastReceivedAt, TimeSpan? DecodeAgeUpperBound,
    DiagnosticDecodeStatus DecodeStatus, bool IsReady, string Reason);
