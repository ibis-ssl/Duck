using System.Text.Json;
using Fizzy.McapSharp;
using Tracker.Core;

namespace Tracker.Diagnostics.Mcap;

/// <summary>
/// 完成済みの録画 MCAP から manifest、raw datagram、実効設定を読み戻す。
/// </summary>
public static class RecordingMcapReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>録画用の完成済み MCAP を読み込む。partial や診断 MCAP は受け付けない。</summary>
    public static RecordingMcapSnapshot Read(string path)
    {
        return ReadCore(path, allowPartialPath: false);
    }

    internal static RecordingMcapSnapshot ValidateCompletedPartial(string path)
    {
        return ReadCore(path, allowPartialPath: true);
    }

    private static RecordingMcapSnapshot ReadCore(string path, bool allowPartialPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!allowPartialPath && path.EndsWith(".mcap.partial", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("未完成の .mcap.partial 録画は通常再生へ読み込めません。");
        }

        if ((!allowPartialPath || !path.EndsWith(".mcap.partial", StringComparison.OrdinalIgnoreCase))
            && !Path.GetExtension(path).Equals(".mcap", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("録画 MCAP の読取には完成名 .mcap を指定してください。");
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("指定された録画 MCAP がありません。", fullPath);
        }

        try
        {
            var factory = new McapReaderFactory(fullPath);
            _ = factory.Validate();
            var channelTopics = factory.ReadChannels()
                .Select(channel => channel.Topic)
                .ToHashSet(StringComparer.Ordinal);
            var manifestRecords = new List<RecordingMcapManifest>();
            var rawPackets = new List<RecordingMcapRawPacket>();
            var configurations = new List<RecordingMcapEffectiveConfiguration>();
            var packetContexts = new List<RecordingMcapPacketContext>();
            var controlEvents = new List<RecordingMcapControlEvent>();

            foreach (var message in factory.ReadMessages(new McapMessageFilter()))
            {
                switch (message.Channel.Topic)
                {
                    case RecordingMcapWriter.ManifestTopic:
                        RequireJsonProperties(message.Data,
                            "artifactKind", "recordingId", "schemaVersion", "startedAtUtc", "stoppedAtUtc",
                            "requiredChannels", "optionalChannels");
                        var manifest = JsonSerializer.Deserialize<RecordingMcapManifest>(message.Data, JsonOptions)
                            ?? throw new InvalidDataException("録画 MCAP の manifest が空です。");
                        manifestRecords.Add(manifest);
                        break;
                    case RecordingMcapWriter.RawVisionTopic:
                        RequireJsonProperties(message.Data, "payload", "receivedAtUtc", "source", "sequence");
                        var raw = JsonSerializer.Deserialize<RecordingMcapRawPacket>(message.Data, JsonOptions)
                            ?? throw new InvalidDataException("録画 MCAP の raw packet が空です。");
                        if (raw.Payload is null || raw.Source is null
                            || raw.ReceivedAtUtc < DateTimeOffset.UnixEpoch)
                        {
                            throw new InvalidDataException("録画 MCAP の raw packet に必須項目がありません。");
                        }

                        rawPackets.Add(raw);
                        break;
                    case RecordingMcapWriter.EffectiveConfigurationTopic:
                        RequireJsonProperties(message.Data, "generationId", "configuration", "effectiveAtUtc");
                        var configuration = JsonSerializer.Deserialize<RecordingMcapEffectiveConfiguration>(message.Data, JsonOptions)
                            ?? throw new InvalidDataException("録画 MCAP の effective configuration が空です。");
                        configurations.Add(configuration);
                        break;
                    case RecordingMcapWriter.PacketContextTopic:
                        RequireJsonProperties(message.Data,
                            "packetSequence", "engineUpdateOrder", "configurationGenerationId",
                            "decodeSucceeded", "trackingEnabled", "reason");
                        var packetContext = JsonSerializer.Deserialize<RecordingMcapPacketContext>(message.Data, JsonOptions)
                            ?? throw new InvalidDataException("録画 MCAP の packet context が空です。");
                        packetContexts.Add(packetContext);
                        break;
                    case RecordingMcapWriter.ControlEventTopic:
                        RequireJsonProperties(message.Data,
                            "engineUpdateOrder", "eventKind", "packetSequence", "configurationGenerationId", "payload");
                        var controlEvent = JsonSerializer.Deserialize<RecordingMcapControlEvent>(message.Data, JsonOptions)
                            ?? throw new InvalidDataException("録画 MCAP の control event が空です。");
                        controlEvents.Add(controlEvent);
                        break;
                }
            }

            if (manifestRecords.Count != 2)
            {
                throw new InvalidDataException("録画 MCAP には開始時と完了時の manifest が必要です。");
            }

            var startManifest = manifestRecords[0];
            var finalManifest = manifestRecords[1];
            if (!string.Equals(startManifest.ArtifactKind, "recording", StringComparison.Ordinal)
                || !string.Equals(finalManifest.ArtifactKind, "recording", StringComparison.Ordinal))
            {
                throw new InvalidDataException("指定された MCAP は録画 artifact ではありません。");
            }

            if (startManifest.SchemaVersion != RecordingMcapWriter.CurrentSchemaVersion
                || finalManifest.SchemaVersion != startManifest.SchemaVersion)
            {
                throw new InvalidDataException($"未対応または開始・終了間で不整合の録画 MCAP schema version: {finalManifest.SchemaVersion}。");
            }

            if (string.IsNullOrWhiteSpace(startManifest.RecordingId)
                || !string.Equals(startManifest.RecordingId, finalManifest.RecordingId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("録画 MCAP の開始・終了 manifest の recording ID が不整合です。");
            }

            if (startManifest.StoppedAtUtc is not null
                || string.IsNullOrWhiteSpace(startManifest.RecordingId)
                || startManifest.StartedAtUtc < DateTimeOffset.UnixEpoch
                || finalManifest.StartedAtUtc != startManifest.StartedAtUtc
                || finalManifest.StoppedAtUtc is null
                || finalManifest.StoppedAtUtc < startManifest.StartedAtUtc)
            {
                throw new InvalidDataException("録画 MCAP の開始・終了時刻が不整合か、正常完了を示す停止時刻がありません。");
            }

            var expectedRequiredChannels = new[]
            {
                RecordingMcapWriter.ManifestTopic,
                RecordingMcapWriter.RawVisionTopic,
                RecordingMcapWriter.EffectiveConfigurationTopic,
                RecordingMcapWriter.PacketContextTopic,
                RecordingMcapWriter.ControlEventTopic,
            };

            if (startManifest.RequiredChannels is null
                || finalManifest.RequiredChannels is null
                || !startManifest.RequiredChannels.SequenceEqual(finalManifest.RequiredChannels, StringComparer.Ordinal)
                || startManifest.RequiredChannels.Distinct(StringComparer.Ordinal).Count() != startManifest.RequiredChannels.Count
                || expectedRequiredChannels.Any(topic => !startManifest.RequiredChannels.Contains(topic, StringComparer.Ordinal))
                || startManifest.RequiredChannels.Any(topic => !channelTopics.Contains(topic)))
            {
                throw new InvalidDataException("録画 MCAP に manifest が要求する channel が揃っていません。");
            }

            if (!channelTopics.Contains(RecordingMcapWriter.ManifestTopic)
                || !channelTopics.Contains(RecordingMcapWriter.RawVisionTopic)
                || !channelTopics.Contains(RecordingMcapWriter.EffectiveConfigurationTopic)
                || !channelTopics.Contains(RecordingMcapWriter.PacketContextTopic)
                || !channelTopics.Contains(RecordingMcapWriter.ControlEventTopic)
                || configurations.Count == 0)
            {
                throw new InvalidDataException("録画 MCAP の必須 channel または初期設定がありません。");
            }

            var configurationGenerations = new HashSet<string>(StringComparer.Ordinal);
            var priorConfigurationTime = startManifest.StartedAtUtc;
            for (var index = 0; index < configurations.Count; index++)
            {
                var configuration = configurations[index];
                if (string.IsNullOrWhiteSpace(configuration.GenerationId)
                    || !configurationGenerations.Add(configuration.GenerationId)
                    || configuration.Configuration.ValueKind != JsonValueKind.Object
                    || configuration.EffectiveAtUtc < startManifest.StartedAtUtc
                    || configuration.EffectiveAtUtc > finalManifest.StoppedAtUtc
                    || configuration.EffectiveAtUtc < priorConfigurationTime
                    || (index == 0 && configuration.EffectiveAtUtc != startManifest.StartedAtUtc))
                {
                    throw new InvalidDataException("録画 MCAP の実効設定世代・時刻・必須設定が不整合です。");
                }

                if (configuration.InitialGeometryBase64 is { } initialGeometryBase64)
                {
                    ValidateGeometryBase64(initialGeometryBase64, "initial geometry");
                }

                priorConfigurationTime = configuration.EffectiveAtUtc;
            }

            if (rawPackets.Any(packet => packet.ReceivedAtUtc < startManifest.StartedAtUtc
                || packet.ReceivedAtUtc > finalManifest.StoppedAtUtc))
            {
                throw new InvalidDataException("録画 MCAP の raw packet 時刻が録画境界の外にあります。");
            }

            for (var index = 1; index < rawPackets.Count; index++)
            {
                var previousSequence = rawPackets[index - 1].Sequence;
                if (previousSequence == ulong.MaxValue || rawPackets[index].Sequence != previousSequence + 1)
                {
                    throw new InvalidDataException("録画 MCAP の raw packet 連番に欠落または重複があります。");
                }
            }

            if (startManifest.SchemaVersion == RecordingMcapWriter.CurrentSchemaVersion)
            {
                var rawSequences = rawPackets.Select(packet => packet.Sequence).ToHashSet();
                var contextSequences = new HashSet<ulong>();
                var packetUpdateOrders = new HashSet<long>();
                var generationIds = configurationGenerations;
                if (packetContexts.Count != rawPackets.Count)
                {
                    throw new InvalidDataException("録画 MCAP の各 raw packet に一意な packet context が必要です。");
                }

                foreach (var context in packetContexts)
                {
                    if (!rawSequences.Contains(context.PacketSequence)
                        || !contextSequences.Add(context.PacketSequence)
                        || string.IsNullOrWhiteSpace(context.ConfigurationGenerationId)
                        || !generationIds.Contains(context.ConfigurationGenerationId)
                        || (context.TrackingEnabled != (context.EngineUpdateOrder is not null))
                        || (context.EngineUpdateOrder is <= 0)
                        || (context.EngineUpdateOrder is { } updateOrder && !packetUpdateOrders.Add(updateOrder))
                        || (!context.DecodeSucceeded && context.TrackingEnabled)
                        || (string.IsNullOrWhiteSpace(context.Reason)
                            && (!context.DecodeSucceeded || !context.TrackingEnabled)))
                    {
                        throw new InvalidDataException("録画 MCAP の packet context が raw・設定世代・処理結果と整合しません。");
                    }
                }

                long priorControlOrder = 0;
                var updateOrderAssociations = new Dictionary<long, (ulong? PacketSequence, string? GenerationId)>();
                foreach (var controlEvent in controlEvents)
                {
                    if (controlEvent.EngineUpdateOrder <= 0
                        || controlEvent.EngineUpdateOrder < priorControlOrder
                        || string.IsNullOrWhiteSpace(controlEvent.EventKind)
                        || string.IsNullOrWhiteSpace(controlEvent.ConfigurationGenerationId)
                        || (controlEvent.PacketSequence is not null && !rawSequences.Contains(controlEvent.PacketSequence.Value))
                        || !generationIds.Contains(controlEvent.ConfigurationGenerationId)
                        || controlEvent.Payload.ValueKind != JsonValueKind.Object)
                    {
                        throw new InvalidDataException("録画 MCAP の control event の順序または参照先が不整合です。");
                    }

                    if (controlEvent.PacketSequence is { } packetSequence)
                    {
                        var context = packetContexts.SingleOrDefault(item => item.PacketSequence == packetSequence);
                        if (context is null
                            || context.EngineUpdateOrder != controlEvent.EngineUpdateOrder
                            || !string.Equals(context.ConfigurationGenerationId, controlEvent.ConfigurationGenerationId, StringComparison.Ordinal))
                        {
                            throw new InvalidDataException("録画 MCAP の control event が対応する raw packet context と一致しません。");
                        }
                    }

                    if (controlEvent.EventKind is not ("profile-switch-applied" or "geometry-updated" or "geometry-reset"))
                    {
                        throw new InvalidDataException($"録画 MCAP に未対応の control event '{controlEvent.EventKind}' があります。");
                    }

                    if (controlEvent.EventKind == "geometry-updated")
                    {
                        _ = ParseGeometryUpdatePayload(controlEvent);
                    }

                    if (controlEvent.EventKind == "profile-switch-applied")
                    {
                        var effectiveConfiguration = configurations.Single(configuration =>
                            string.Equals(configuration.GenerationId, controlEvent.ConfigurationGenerationId, StringComparison.Ordinal));
                        RecordingMcapSettingsValidator.ValidateAppliedProfileSwitch(controlEvent, effectiveConfiguration.Configuration);
                    }

                    var association = (controlEvent.PacketSequence, controlEvent.ConfigurationGenerationId);
                    if (updateOrderAssociations.TryGetValue(controlEvent.EngineUpdateOrder, out var priorAssociation)
                        && priorAssociation != association)
                    {
                        throw new InvalidDataException("同じ engine update order の control event に異なる raw packet または設定世代があります。");
                    }

                    updateOrderAssociations[controlEvent.EngineUpdateOrder] = association;

                    priorControlOrder = controlEvent.EngineUpdateOrder;
                }

                foreach (var controlOnlyOrder in updateOrderAssociations
                    .Where(entry => entry.Value.PacketSequence is null)
                    .Select(entry => entry.Key))
                {
                    if (packetUpdateOrders.Contains(controlOnlyOrder))
                    {
                        throw new InvalidDataException("control-only update order が packet update order と重複しています。");
                    }
                }

                var appliedSettingsGenerations = controlEvents
                    .Where(controlEvent => controlEvent.EventKind == "profile-switch-applied")
                    .Select(controlEvent => controlEvent.ConfigurationGenerationId)
                    .ToHashSet(StringComparer.Ordinal);
                if (configurations.Skip(1).Any(configuration => !appliedSettingsGenerations.Contains(configuration.GenerationId)))
                {
                    throw new InvalidDataException("追加の実効設定世代には対応する profile-switch-applied event が必要です。");
                }

                var lastSettingsEvent = controlEvents
                    .Where(controlEvent => controlEvent.EventKind == "profile-switch-applied")
                    .MaxBy(controlEvent => controlEvent.EngineUpdateOrder);
                if (lastSettingsEvent is not null
                    && !string.Equals(lastSettingsEvent.ConfigurationGenerationId,
                        configurations[^1].GenerationId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("最後の適用済み設定イベントと最新の設定世代が一致しません。");
                }

                ValidateAssociatedGeometryUpdates(rawPackets, configurations, packetContexts, controlEvents);
            }

            var snapshot = new RecordingMcapSnapshot(finalManifest, rawPackets, configurations, packetContexts, controlEvents);
            RecordingMcapReplayContractValidator.Validate(snapshot);
            return snapshot;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidDataException("録画 MCAP を読み取れません。必須データが破損している可能性があります。", exception);
        }
    }

    private static void RequireJsonProperties(byte[] payload, params string[] requiredProperties)
    {
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("録画 MCAP の JSON record は object である必要があります。");
        }

        foreach (var requiredProperty in requiredProperties)
        {
            if (!document.RootElement.EnumerateObject().Any(property =>
                    string.Equals(property.Name, requiredProperty, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException($"録画 MCAP の JSON record に必須項目 '{requiredProperty}' がありません。");
            }
        }
    }

    private static byte[] ParseGeometryUpdatePayload(RecordingMcapControlEvent controlEvent)
    {
        if (controlEvent.Payload.ValueKind != JsonValueKind.Object
            || !controlEvent.Payload.TryGetProperty("protobufBase64", out var geometryValue)
            || geometryValue.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("録画 MCAP の geometry-updated event に protobufBase64 がありません。");
        }

        var base64 = geometryValue.GetString()!;
        ValidateGeometryBase64(base64, "geometry-updated event");
        return Convert.FromBase64String(base64);
    }

    private static ulong? ReadSourcePacketSequence(RecordingMcapControlEvent controlEvent)
    {
        if (controlEvent.Payload.ValueKind != JsonValueKind.Object
            || !controlEvent.Payload.TryGetProperty("sourcePacketSequence", out var sequenceValue))
        {
            return null;
        }

        if (sequenceValue.ValueKind != JsonValueKind.Number || !sequenceValue.TryGetUInt64(out var sequence))
        {
            throw new InvalidDataException("Recording MCAP geometry event has an invalid sourcePacketSequence.");
        }

        return sequence;
    }

    private static void ValidateAssociatedGeometryUpdates(
        IReadOnlyList<RecordingMcapRawPacket> rawPackets,
        IReadOnlyList<RecordingMcapEffectiveConfiguration> configurations,
        IReadOnlyList<RecordingMcapPacketContext> packetContexts,
        IReadOnlyList<RecordingMcapControlEvent> controlEvents)
    {
        var rawBySequence = rawPackets.ToDictionary(packet => packet.Sequence);
        byte[]? lastGeometryPayload = configurations[0].InitialGeometryBase64 is { } initialGeometry
            ? Convert.FromBase64String(initialGeometry)
            : null;
        var contextsByOrder = packetContexts
            .Where(context => context.EngineUpdateOrder is not null)
            .ToDictionary(context => context.EngineUpdateOrder!.Value);
        var eventsByOrder = controlEvents
            .GroupBy(controlEvent => controlEvent.EngineUpdateOrder)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var order in contextsByOrder.Keys.Concat(eventsByOrder.Keys).Distinct().Order())
        {
            var orderEvents = eventsByOrder.GetValueOrDefault(order, []);
            var geometryUpdates = orderEvents.Where(item => item.EventKind == "geometry-updated").ToArray();
            if (geometryUpdates.Length > 1)
            {
                throw new InvalidDataException($"Recording MCAP has duplicate geometry updates at engine update order {order}.");
            }

            if (contextsByOrder.TryGetValue(order, out var context))
            {
                var sequence = context.PacketSequence;
                if (!context.DecodeSucceeded || !context.TrackingEnabled)
                {
                    throw new InvalidDataException($"geometry update at engine update order {order} has no decoded engine input.");
                }

                var rawGeometry = SslVisionGeometryValidator.ExtractWrapperPacketGeometry(rawBySequence[sequence].Payload);

                if (geometryUpdates.Length == 1)
                {
                    if (rawGeometry is null)
                    {
                        throw new InvalidDataException($"geometry-updated event for raw sequence {sequence} has no geometry in its raw packet.");
                    }

                    var recordedGeometry = ParseGeometryUpdatePayload(geometryUpdates[0]);
                    if (!recordedGeometry.AsSpan().SequenceEqual(rawGeometry))
                    {
                        throw new InvalidDataException($"geometry-updated event does not match raw sequence {sequence}.");
                    }

                    lastGeometryPayload = recordedGeometry;
                }
                else if (rawGeometry is not null
                    && (lastGeometryPayload is null || !lastGeometryPayload.AsSpan().SequenceEqual(rawGeometry)))
                {
                    throw new InvalidDataException($"Recording MCAP is missing geometry update for raw sequence {sequence}.");
                }
            }
            else
            {
                if (orderEvents.Any(item => item.PacketSequence is not null))
                {
                    throw new InvalidDataException($"Recording MCAP control event at update order {order} has no tracked raw packet.");
                }

                if (geometryUpdates.Length == 1)
                {
                    var recordedGeometry = ParseGeometryUpdatePayload(geometryUpdates[0]);
                    var sourceSequence = ReadSourcePacketSequence(geometryUpdates[0]);
                    if (sourceSequence is { } referencedSequence)
                    {
                        if (!rawBySequence.TryGetValue(referencedSequence, out var sourceRaw))
                        {
                            throw new InvalidDataException($"Recording MCAP geometry event references missing raw sequence {referencedSequence}.");
                        }

                        var sourceContext = packetContexts.SingleOrDefault(context => context.PacketSequence == referencedSequence);
                        var sourceGeometry = SslVisionGeometryValidator.ExtractWrapperPacketGeometry(sourceRaw.Payload);
                        if (sourceContext is null
                            || !sourceContext.DecodeSucceeded
                            || sourceContext.TrackingEnabled
                            || !string.Equals(sourceContext.Reason, "tracker-late-input-dropped", StringComparison.Ordinal)
                            || !string.Equals(sourceContext.ConfigurationGenerationId, geometryUpdates[0].ConfigurationGenerationId, StringComparison.Ordinal)
                            || sourceGeometry is null
                            || !sourceGeometry.AsSpan().SequenceEqual(recordedGeometry))
                        {
                            throw new InvalidDataException($"Recording MCAP geometry event does not match its excluded raw sequence {referencedSequence}.");
                        }

                        foreach (var resetEvent in orderEvents.Where(item => item.EventKind == "geometry-reset"))
                        {
                            if (ReadSourcePacketSequence(resetEvent) != referencedSequence)
                            {
                                throw new InvalidDataException($"Recording MCAP geometry reset at update order {order} does not match raw sequence {referencedSequence}.");
                            }
                        }
                    }
                    else if (lastGeometryPayload is not null
                        && lastGeometryPayload.AsSpan().SequenceEqual(recordedGeometry))
                    {
                        throw new InvalidDataException($"Recording MCAP repeats an unchanged control-only geometry at update order {order}.");
                    }

                    lastGeometryPayload = recordedGeometry;
                }
            }
        }
    }

    private static void ValidateGeometryBase64(string base64, string source)
    {
        try
        {
            var payload = Convert.FromBase64String(base64);
            if (payload.Length == 0)
            {
                throw new FormatException("Geometry protobuf is empty.");
            }

            SslVisionGeometryValidator.Validate(payload);
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException)
        {
            throw new InvalidDataException($"録画 MCAP の {source} protobuf が壊れています。", exception);
        }
    }
}
