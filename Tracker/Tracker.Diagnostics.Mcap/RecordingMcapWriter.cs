using System.Text.Json;
using System.Runtime.ExceptionServices;
using Fizzy.McapSharp;

namespace Tracker.Diagnostics.Mcap;

/// <summary>
/// 個別セッションの録画データを、単体で確定する MCAP へ書き込む。
/// </summary>
public sealed class RecordingMcapWriter : IDisposable
{
    internal const string ManifestTopic = "/recording/manifest";
    internal const string RawVisionTopic = "/ssl_vision/raw";
    internal const string EffectiveConfigurationTopic = "/tracker/effective_config";
    internal const string PacketContextTopic = "/recording/packet_context";
    internal const string ControlEventTopic = "/tracker/control_event";
    internal const int CurrentSchemaVersion = 2;

    private static readonly string[] RequiredTopics =
    [
        ManifestTopic,
        RawVisionTopic,
        EffectiveConfigurationTopic,
        PacketContextTopic,
        ControlEventTopic,
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object gate = new();
    private readonly string completedPath;
    private readonly string partialPath;
    private readonly string recordingId;
    private readonly string initialConfigurationGenerationId;
    private string lastConfigurationGenerationId;
    private readonly DateTimeOffset startedAtUtc;
    private readonly ushort manifestChannel;
    private readonly ushort rawVisionChannel;
    private readonly ushort effectiveConfigurationChannel;
    private readonly ushort packetContextChannel;
    private readonly ushort controlEventChannel;
    private readonly Action<McapWriter> flushToDisk;
    private readonly Action<McapWriter> closeWriter;
    private readonly HashSet<string> configurationGenerationIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> effectiveConfigurations = new(StringComparer.Ordinal);
    private readonly List<RecordingMcapEffectiveConfiguration> configurationRecords = [];
    private readonly HashSet<ulong> rawPacketSequences = [];
    private readonly List<RecordingMcapRawPacket> rawPackets = [];
    private readonly Dictionary<ulong, RecordingMcapPacketContext> packetContexts = [];
    private readonly List<RecordingMcapControlEvent> controlEvents = [];
    private McapWriter? writer;
    private uint sequence;
    private bool completionAttempted;
    private bool completed;
    private Exception? writeFailure;

    /// <summary>
    /// 新しい録画用 writer を開始し、開始時の有効設定を記録する。
    /// </summary>
    public RecordingMcapWriter(
        string completedPath,
        string recordingId,
        DateTimeOffset startedAtUtc,
        JsonElement initialEffectiveConfiguration,
        string configurationGenerationId,
        byte[]? initialGeometryPayload = null)
        : this(completedPath, recordingId, startedAtUtc, initialEffectiveConfiguration, configurationGenerationId,
            path => new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read),
            initialGeometryPayload)
    {
    }

    internal RecordingMcapWriter(
        string completedPath,
        string recordingId,
        DateTimeOffset startedAtUtc,
        JsonElement initialEffectiveConfiguration,
        string configurationGenerationId,
        Func<string, Stream> openPartialStream,
        byte[]? initialGeometryPayload = null,
        Action<McapWriter>? flushToDisk = null,
        Action<McapWriter>? closeWriter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(completedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationGenerationId);
        ArgumentNullException.ThrowIfNull(openPartialStream);
        if (initialEffectiveConfiguration.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Effective configuration must be a JSON object.", nameof(initialEffectiveConfiguration));
        }
        if (initialGeometryPayload is { Length: 0 })
        {
            throw new ArgumentException("Initial applied geometry payload cannot be empty.", nameof(initialGeometryPayload));
        }
        if (!Path.GetExtension(completedPath).Equals(".mcap", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("録画の完成先は .mcap ファイルである必要があります。", nameof(completedPath));
        }

        this.completedPath = Path.GetFullPath(completedPath);
        partialPath = this.completedPath + ".partial";
        this.recordingId = recordingId;
        initialConfigurationGenerationId = configurationGenerationId;
        lastConfigurationGenerationId = configurationGenerationId;
        this.startedAtUtc = startedAtUtc.ToUniversalTime();
        this.flushToDisk = flushToDisk ?? (currentWriter => currentWriter.FlushToDisk());
        this.closeWriter = closeWriter ?? (currentWriter => currentWriter.Dispose());

        var directory = Path.GetDirectoryName(this.completedPath)
            ?? throw new ArgumentException("録画の完成先に親ディレクトリが必要です。", nameof(completedPath));
        Directory.CreateDirectory(directory);
        Stream? output = null;
        var ownsPartialFile = false;
        try
        {
            output = openPartialStream(partialPath);
            ownsPartialFile = File.Exists(partialPath);
            writer = new McapWriter(output, new McapWriterOptions
            {
                UseChunks = false,
                EmitSummaryRecords = true,
                CalculateDataSectionCrc = true,
                CalculateSummarySectionCrc = true,
            });
            manifestChannel = writer.RegisterChannel(ManifestTopic, "json", 0, null);
            rawVisionChannel = writer.RegisterChannel(RawVisionTopic, "json", 0, null);
            effectiveConfigurationChannel = writer.RegisterChannel(EffectiveConfigurationTopic, "json", 0, null);
            packetContextChannel = writer.RegisterChannel(PacketContextTopic, "json", 0, null);
            controlEventChannel = writer.RegisterChannel(ControlEventTopic, "json", 0, null);
            WriteManifest(stoppedAtUtc: null);
            WriteEffectiveConfigurationCore(
                configurationGenerationId,
                initialEffectiveConfiguration.Clone(),
                this.startedAtUtc,
                initialGeometryPayload is null ? null : Convert.ToBase64String(initialGeometryPayload));
            configurationGenerationIds.Add(configurationGenerationId);
            effectiveConfigurations.Add(configurationGenerationId, initialEffectiveConfiguration.Clone());
            configurationRecords.Add(new RecordingMcapEffectiveConfiguration(
                configurationGenerationId,
                initialEffectiveConfiguration.Clone(),
                this.startedAtUtc,
                initialGeometryPayload is null ? null : Convert.ToBase64String(initialGeometryPayload)));
        }
        catch (Exception primaryException)
        {
            if (primaryException is AggregateException { InnerExceptions.Count: > 0 } aggregate)
            {
                primaryException = aggregate.InnerExceptions[0];
                foreach (var secondaryException in aggregate.InnerExceptions.Skip(1))
                {
                    Console.Error.WriteLine($"Secondary MCAP initialization cleanup failure: {secondaryException}");
                }
            }

            TryCleanup(writer is null ? null : () => writer.Dispose(), "MCAP writer");
            writer = null;
            TryCleanup(output is null ? null : () => output.Dispose(), "recording stream");
            if (ownsPartialFile)
            {
                TryCleanup(() => File.Delete(partialPath), "partial recording file");
            }

            ExceptionDispatchInfo.Capture(primaryException).Throw();
            throw;
        }
    }

    /// <summary>復号前の受信 payload 全体を、時刻・送信元・連番と一緒に記録する。</summary>
    public void WriteRawPacket(byte[] payload, DateTimeOffset receivedAtUtc, string source, ulong sequence)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(source);
        lock (gate)
        {
            EnsureWritable();
            try
            {
                if (!rawPacketSequences.Add(sequence))
                {
                    throw new InvalidDataException($"Duplicate raw packet sequence {sequence}.");
                }

                var rawPacket = new RecordingMcapRawPacket(
                    payload.ToArray(), receivedAtUtc.ToUniversalTime(), source, sequence);
                WriteJson(rawVisionChannel, receivedAtUtc, rawPacket);
                rawPackets.Add(rawPacket);
            }
            catch (Exception exception)
            {
                writeFailure ??= exception;
                throw;
            }
        }
    }

    /// <summary>実際に有効となった tracker 設定世代を記録する。</summary>
    public void WriteEffectiveConfiguration(
        string generationId,
        JsonElement configuration,
        DateTimeOffset effectiveAtUtc)
    {
        lock (gate)
        {
            try
            {
                EnsureWritable();
                ArgumentException.ThrowIfNullOrWhiteSpace(generationId);
                if (configuration.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("Effective configuration must be a JSON object.", nameof(configuration));
                }

                if (!configurationGenerationIds.Add(generationId))
                {
                    throw new InvalidDataException($"Duplicate configuration generation '{generationId}'.");
                }

                WriteEffectiveConfigurationCore(generationId, configuration.Clone(), effectiveAtUtc.ToUniversalTime());
                effectiveConfigurations.Add(generationId, configuration.Clone());
                configurationRecords.Add(new RecordingMcapEffectiveConfiguration(
                    generationId, configuration.Clone(), effectiveAtUtc.ToUniversalTime()));
                lastConfigurationGenerationId = generationId;
            }
            catch (Exception exception)
            {
                writeFailure ??= exception;
                throw;
            }
        }
    }

    /// <summary>Record how a captured raw packet crossed decode and tracking processing.</summary>
    public void WritePacketContext(RecordingMcapPacketContext packetContext, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(packetContext);
        lock (gate)
        {
            try
            {
                EnsureWritable();
                if (!rawPacketSequences.Contains(packetContext.PacketSequence)
                    || packetContexts.ContainsKey(packetContext.PacketSequence)
                    || string.IsNullOrWhiteSpace(packetContext.ConfigurationGenerationId)
                    || !configurationGenerationIds.Contains(packetContext.ConfigurationGenerationId)
                    || (packetContext.TrackingEnabled != (packetContext.EngineUpdateOrder is not null))
                    || packetContext.EngineUpdateOrder is <= 0
                    || (!packetContext.DecodeSucceeded && packetContext.TrackingEnabled)
                    || (!packetContext.DecodeSucceeded && string.IsNullOrWhiteSpace(packetContext.Reason)))
                {
                    throw new InvalidDataException("Packet context must uniquely match an admitted raw packet and effective generation.");
                }

                if (packetContext.EngineUpdateOrder is { } updateOrder
                    && packetContexts.Values.Any(existing => existing.EngineUpdateOrder == updateOrder))
                {
                    throw new InvalidDataException($"Duplicate engine update order {updateOrder} for packet contexts.");
                }

                WriteJson(packetContextChannel, timestamp, packetContext);
                packetContexts.Add(packetContext.PacketSequence, packetContext);
            }
            catch (Exception exception)
            {
                writeFailure ??= exception;
                throw;
            }
        }
    }

    /// <summary>Record ordered engine control changes such as profile switches and geometry resets.</summary>
    public void WriteControlEvent(RecordingMcapControlEvent controlEvent, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(controlEvent);
        lock (gate)
        {
            try
            {
                EnsureWritable();
                if (controlEvent.EngineUpdateOrder <= 0
                    || string.IsNullOrWhiteSpace(controlEvent.EventKind)
                    || string.IsNullOrWhiteSpace(controlEvent.ConfigurationGenerationId)
                    || controlEvent.Payload.ValueKind != JsonValueKind.Object
                    || (controlEvent.PacketSequence is { } packetSequence && !rawPacketSequences.Contains(packetSequence))
                    || !configurationGenerationIds.Contains(controlEvent.ConfigurationGenerationId))
                {
                    throw new InvalidDataException("Control event must have a positive order and valid payload references.");
                }

                var eventsAtOrder = controlEvents.Where(item => item.EngineUpdateOrder == controlEvent.EngineUpdateOrder).ToArray();
                if (eventsAtOrder.Any(item => item.PacketSequence != controlEvent.PacketSequence
                    || !string.Equals(item.ConfigurationGenerationId, controlEvent.ConfigurationGenerationId, StringComparison.Ordinal)
                    || string.Equals(item.EventKind, controlEvent.EventKind, StringComparison.Ordinal)))
                {
                    throw new InvalidDataException($"Control event order {controlEvent.EngineUpdateOrder} has conflicting or duplicate events.");
                }

                WriteJson(controlEventChannel, timestamp, controlEvent);
                controlEvents.Add(controlEvent);
            }
            catch (Exception exception)
            {
                writeFailure ??= exception;
                throw;
            }
        }
    }

    /// <summary>
    /// MCAP を完了・disk flush・close した後、既存ファイルを上書きせず完成名へ移す。
    /// </summary>
    public void Complete(DateTimeOffset? stoppedAtUtc = null)
    {
        lock (gate)
        {
            if (completed)
            {
                return;
            }

            if (completionAttempted)
            {
                throw new InvalidOperationException("録画 MCAP の確定処理は既に失敗しています。", writeFailure);
            }

            completionAttempted = true;
            try
            {
                if (writeFailure is not null)
                {
                    throw new InvalidOperationException("録画 MCAP の書き込みに失敗しました。", writeFailure);
                }

                ValidateRecordedAssociations();

                var finalTime = (stoppedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
                if (finalTime < startedAtUtc)
                {
                    throw new InvalidOperationException("録画の停止時刻を開始時刻より前にはできません。");
                }

                var pendingSnapshot = new RecordingMcapSnapshot(
                    new RecordingMcapManifest("recording", recordingId, CurrentSchemaVersion, startedAtUtc,
                        finalTime, RequiredTopics, []),
                    rawPackets.ToArray(),
                    configurationRecords.ToArray(),
                    packetContexts.Values.OrderBy(item => item.PacketSequence).ToArray(),
                    controlEvents.ToArray());
                RecordingMcapReader.ValidateAssociatedGeometryUpdates(
                    rawPackets, configurationRecords, pendingSnapshot.PacketContexts, controlEvents);
                RecordingMcapReplayContractValidator.Validate(pendingSnapshot);

                WriteManifest(finalTime);
                var currentWriter = writer ?? throw new ObjectDisposedException(nameof(RecordingMcapWriter));
                currentWriter.Complete();
                flushToDisk(currentWriter);
                closeWriter(currentWriter);
                writer = null;
                _ = RecordingMcapReader.ValidateCompletedPartial(partialPath);
                File.Move(partialPath, completedPath);
                completed = true;
            }
            catch (Exception exception)
            {
                writeFailure ??= exception;
                throw;
            }
        }
    }

    /// <summary>未確定の writer を閉じ、完成名へは公開しない。</summary>
    public void Dispose()
    {
        lock (gate)
        {
            writer?.Dispose();
            writer = null;
        }
    }

    private void WriteManifest(DateTimeOffset? stoppedAtUtc)
    {
        WriteJson(manifestChannel, stoppedAtUtc ?? startedAtUtc, new RecordingMcapManifest(
            "recording",
            recordingId,
            CurrentSchemaVersion,
            startedAtUtc,
            stoppedAtUtc,
            RequiredTopics,
            []));
    }

    private void WriteEffectiveConfigurationCore(
        string generationId,
        JsonElement configuration,
        DateTimeOffset effectiveAtUtc,
        string? initialGeometryBase64 = null) =>
        WriteJson(effectiveConfigurationChannel, effectiveAtUtc, new RecordingMcapEffectiveConfiguration(
            generationId,
            configuration,
            effectiveAtUtc,
            initialGeometryBase64));

    private void ValidateRecordedAssociations()
    {
        if (packetContexts.Count != rawPacketSequences.Count)
        {
            throw new InvalidDataException("Every admitted raw packet must have exactly one packet context before recording completion.");
        }

        var packetUpdateOrders = packetContexts.Values
            .Where(context => context.EngineUpdateOrder is not null)
            .Select(context => context.EngineUpdateOrder!.Value)
            .ToHashSet();
        foreach (var controlOnlyOrder in controlEvents
            .Where(controlEvent => controlEvent.PacketSequence is null)
            .Select(controlEvent => controlEvent.EngineUpdateOrder)
            .Distinct())
        {
            if (packetUpdateOrders.Contains(controlOnlyOrder))
            {
                throw new InvalidDataException("A control-only update order cannot also identify a packet update.");
            }
        }

        foreach (var controlEvent in controlEvents.Where(controlEvent => controlEvent.PacketSequence is not null))
        {
            var context = packetContexts[controlEvent.PacketSequence!.Value];
            if (context.EngineUpdateOrder != controlEvent.EngineUpdateOrder
                || !string.Equals(context.ConfigurationGenerationId, controlEvent.ConfigurationGenerationId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Control event does not match its packet context and effective generation.");
            }
        }

        foreach (var profileSwitch in controlEvents.Where(controlEvent => controlEvent.EventKind == "profile-switch-applied"))
        {
            if (!effectiveConfigurations.TryGetValue(profileSwitch.ConfigurationGenerationId!, out var configuration))
            {
                throw new InvalidDataException("Applied profile-switch event references missing effective settings.");
            }

            RecordingMcapSettingsValidator.ValidateAppliedProfileSwitch(profileSwitch, configuration);
        }

        var appliedSettingsGenerations = controlEvents
            .Where(controlEvent => controlEvent.EventKind == "profile-switch-applied")
            .Select(controlEvent => controlEvent.ConfigurationGenerationId)
            .ToHashSet(StringComparer.Ordinal);
        if (configurationGenerationIds.Any(generationId => generationId != initialConfigurationGenerationId
            && !appliedSettingsGenerations.Contains(generationId)))
        {
            throw new InvalidDataException("Every non-initial configuration generation must have a matching applied profile-switch event.");
        }

        var lastSettingsEvent = controlEvents
            .Where(controlEvent => controlEvent.EventKind == "profile-switch-applied")
            .MaxBy(controlEvent => controlEvent.EngineUpdateOrder);
        if (lastSettingsEvent is not null
            && !string.Equals(lastSettingsEvent.ConfigurationGenerationId,
                lastConfigurationGenerationId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The latest applied settings event must match the latest effective configuration generation.");
        }

        if (controlEvents.Any(controlEvent => controlEvent.EventKind is not
                ("profile-switch-applied" or "geometry-updated" or "geometry-reset")))
        {
            throw new InvalidDataException("Recording MCAP contains an unsupported control event kind.");
        }

    }

    private void WriteJson<T>(ushort channel, DateTimeOffset timestamp, T value)
    {
        try
        {
            var unixNanoseconds = ToUnixNanoseconds(timestamp);
            var payload = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            var header = new McapMessageHeader(channel, sequence++, unixNanoseconds, unixNanoseconds);
            (writer ?? throw new ObjectDisposedException(nameof(RecordingMcapWriter))).WriteToKnownChannel(in header, payload);
        }
        catch (Exception exception)
        {
            writeFailure ??= exception;
            throw;
        }
    }

    private void EnsureWritable()
    {
        if (completionAttempted || completed)
        {
            throw new InvalidOperationException("確定処理を開始した録画 MCAP には追記できません。");
        }

        if (writeFailure is not null)
        {
            throw new InvalidOperationException("録画 MCAP の書き込みに失敗しています。", writeFailure);
        }
    }

    private static ulong ToUnixNanoseconds(DateTimeOffset timestamp)
    {
        var ticks = timestamp.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks;
        if (ticks < 0)
        {
            throw new InvalidOperationException("録画時刻に1970年より前の値は使用できません。");
        }

        return checked((ulong)ticks * 100);
    }

    private static void TryCleanup(Action? cleanup, string resource)
    {
        if (cleanup is null)
        {
            return;
        }

        try
        {
            cleanup();
        }
        catch (Exception cleanupException)
        {
            Console.Error.WriteLine($"Failed to clean up {resource}: {cleanupException}");
        }
    }
}

/// <summary>録画ファイル内の manifest payload。</summary>
public sealed record RecordingMcapManifest(
    string ArtifactKind,
    string RecordingId,
    int SchemaVersion,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? StoppedAtUtc,
    IReadOnlyList<string> RequiredChannels,
    IReadOnlyList<string> OptionalChannels);

/// <summary>復号前の SSL-Vision datagram。</summary>
public sealed record RecordingMcapRawPacket(
    byte[] Payload,
    DateTimeOffset ReceivedAtUtc,
    string Source,
    ulong Sequence);

/// <summary>実効 tracker 設定の一世代。</summary>
public sealed record RecordingMcapEffectiveConfiguration(
    string GenerationId,
    JsonElement Configuration,
    DateTimeOffset EffectiveAtUtc,
    string? InitialGeometryBase64 = null);

/// <summary>Association between a raw packet and its actual engine processing boundary.</summary>
public sealed record RecordingMcapPacketContext(
    ulong PacketSequence,
    long? EngineUpdateOrder,
    string ConfigurationGenerationId,
    bool DecodeSucceeded,
    bool TrackingEnabled,
    string? Reason);

/// <summary>An ordered control event observed at an engine update boundary.</summary>
public sealed record RecordingMcapControlEvent(
    long EngineUpdateOrder,
    string EventKind,
    ulong? PacketSequence,
    string? ConfigurationGenerationId,
    JsonElement Payload);

/// <summary>録画専用 MCAP を読み戻した結果。</summary>
public sealed record RecordingMcapSnapshot(
    RecordingMcapManifest Manifest,
    IReadOnlyList<RecordingMcapRawPacket> RawPackets,
    IReadOnlyList<RecordingMcapEffectiveConfiguration> EffectiveConfigurations,
    IReadOnlyList<RecordingMcapPacketContext> PacketContexts,
    IReadOnlyList<RecordingMcapControlEvent> ControlEvents);
