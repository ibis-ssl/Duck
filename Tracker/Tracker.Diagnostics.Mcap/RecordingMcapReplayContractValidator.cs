using Tracker.Core;

namespace Tracker.Diagnostics.Mcap;

/// <summary>Validates replay-required settings and ordered geometry reset events for a recording.</summary>
public static class RecordingMcapReplayContractValidator
{
    /// <summary>Rejects recordings that cannot reproduce their effective settings and geometry reset events.</summary>
    public static void Validate(RecordingMcapSnapshot recording)
    {
        ArgumentNullException.ThrowIfNull(recording);
        var settingsByGeneration = RecordingMcapSettingsValidator.ReadAll(recording);
        ValidateSettingsGenerationOrder(recording, settingsByGeneration);
        RecordingMcapReader.ValidateAssociatedGeometryUpdates(
            recording.RawPackets, recording.EffectiveConfigurations, recording.PacketContexts, recording.ControlEvents);
        ValidateGeometryResetEvents(recording, settingsByGeneration);
    }

    private static void ValidateSettingsGenerationOrder(
        RecordingMcapSnapshot recording,
        IReadOnlyDictionary<string, TrackerEngineSettings> settingsByGeneration)
    {
        var activeGeneration = recording.EffectiveConfigurations[0].GenerationId;
        var contextsByOrder = recording.PacketContexts.Where(item => item.EngineUpdateOrder is not null)
            .ToDictionary(item => item.EngineUpdateOrder!.Value);
        var eventsByOrder = recording.ControlEvents.GroupBy(item => item.EngineUpdateOrder)
            .ToDictionary(group => group.Key, group => group.ToArray());
        foreach (var order in contextsByOrder.Keys.Concat(eventsByOrder.Keys).Distinct().Order())
        {
            var eventsAtOrder = eventsByOrder.GetValueOrDefault(order, []);
            var switches = eventsAtOrder.Where(item => item.EventKind == "profile-switch-applied").ToArray();
            if (switches.Length > 1)
            {
                throw new InvalidDataException($"Recording MCAP has duplicate settings switches at update order {order}.");
            }

            if (switches.Length == 1)
            {
                activeGeneration = switches[0].ConfigurationGenerationId!;
                if (!settingsByGeneration.ContainsKey(activeGeneration))
                {
                    throw new InvalidDataException($"Recording MCAP applied settings switch at order {order} references missing generation '{activeGeneration}'.");
                }
            }

            var context = contextsByOrder.GetValueOrDefault(order);
            if (context is not null && !string.Equals(context.ConfigurationGenerationId, activeGeneration, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Recording MCAP packet at update order {order} references inactive settings generation '{context.ConfigurationGenerationId}'.");
            }

            if (eventsAtOrder.Any(item => !string.Equals(item.ConfigurationGenerationId, activeGeneration, StringComparison.Ordinal)))
            {
                throw new InvalidDataException($"Recording MCAP control event at update order {order} references an inactive settings generation.");
            }
        }
    }

    private static void ValidateGeometryResetEvents(
        RecordingMcapSnapshot recording,
        IReadOnlyDictionary<string, TrackerEngineSettings> settingsByGeneration)
    {
        var initialConfiguration = recording.EffectiveConfigurations[0];
        var initialSettings = GetSettings(settingsByGeneration, initialConfiguration.GenerationId);
        var engine = new TrackerEngine();
        if (initialConfiguration.InitialGeometryBase64 is { } initialGeometryBase64)
        {
            var initialGeometry = ParseGeometry(initialGeometryBase64, "initial geometry");
            _ = engine.Update(new SSL_WrapperPacket { Geometry = initialGeometry }, initialSettings);
        }

        var rawBySequence = recording.RawPackets.ToDictionary(packet => packet.Sequence);
        var contextsByOrder = recording.PacketContexts
            .Where(context => context.EngineUpdateOrder is not null)
            .ToDictionary(context => context.EngineUpdateOrder!.Value);
        var eventsByOrder = recording.ControlEvents
            .GroupBy(controlEvent => controlEvent.EngineUpdateOrder)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var profileSwitchRequestVersion = 0;

        foreach (var order in contextsByOrder.Keys.Concat(eventsByOrder.Keys).Distinct().Order())
        {
            var orderEvents = eventsByOrder.GetValueOrDefault(order, []);
            var context = contextsByOrder.GetValueOrDefault(order);
            var generationId = context?.ConfigurationGenerationId
                ?? orderEvents.FirstOrDefault()?.ConfigurationGenerationId
                ?? throw new InvalidDataException($"Recording MCAP update order {order} has no effective settings generation.");
            var settings = GetSettings(settingsByGeneration, generationId);

            SSL_WrapperPacket? packet = null;
            if (context is not null)
            {
                if (!context.DecodeSucceeded || !context.TrackingEnabled)
                {
                    throw new InvalidDataException($"Recording MCAP update order {order} has no decoded tracked input.");
                }

                var rawPayload = rawBySequence[context.PacketSequence].Payload;
                _ = SslVisionGeometryValidator.ExtractWrapperPacketGeometry(rawPayload);
                packet = SSL_WrapperPacket.Parser.ParseFrom(rawPayload);
            }
            else
            {
                var geometryUpdate = orderEvents.SingleOrDefault(item => item.EventKind == "geometry-updated");
                if (geometryUpdate is not null)
                {
                    packet = new SSL_WrapperPacket
                    {
                        Geometry = ParseGeometryUpdate(geometryUpdate),
                    };
                }
            }

            var profileSwitch = orderEvents.SingleOrDefault(item => item.EventKind == "profile-switch-applied");
            var switchRequest = profileSwitch is null
                ? null
                : new TrackerProfileSwitchRequest
                {
                    RequestVersion = checked(++profileSwitchRequestVersion),
                    ProfileName = settings.ProfileName,
                    ResolvedBaseSettings = settings,
                };
            var actualResetCount = engine.Update(packet, settings, switchRequest)
                .EmittedEvents.Count(item => item.Kind == TrackerEventKind.GeometryReset);
            var expectedResetCount = orderEvents.Count(item => item.EventKind == "geometry-reset");
            if (actualResetCount != expectedResetCount)
            {
                if (actualResetCount > 0 && context is not null
                    && orderEvents.All(item => item.EventKind != "geometry-updated"))
                {
                    throw new InvalidDataException($"Recording MCAP is missing geometry update for raw sequence {context.PacketSequence}.");
                }

                throw new InvalidDataException($"Recording MCAP geometry reset count differs at update order {order}: engine emitted {actualResetCount}, recording has {expectedResetCount}.");
            }
        }
    }

    private static TrackerEngineSettings GetSettings(
        IReadOnlyDictionary<string, TrackerEngineSettings> settingsByGeneration,
        string generationId) =>
        settingsByGeneration.TryGetValue(generationId, out var settings)
            ? settings
            : throw new InvalidDataException($"Recording MCAP has no settings for generation '{generationId}'.");

    private static SSL_GeometryData ParseGeometryUpdate(RecordingMcapControlEvent controlEvent)
    {
        if (controlEvent.Payload.ValueKind != System.Text.Json.JsonValueKind.Object
            || !controlEvent.Payload.TryGetProperty("protobufBase64", out var value)
            || value.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            throw new InvalidDataException("Recording MCAP geometry-updated event has no protobufBase64.");
        }

        return ParseGeometry(value.GetString()!, "geometry-updated event");
    }

    private static SSL_GeometryData ParseGeometry(string base64, string source)
    {
        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(base64);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException($"Recording MCAP {source} protobuf is invalid.", exception);
        }

        SslVisionGeometryValidator.Validate(payload);
        return SSL_GeometryData.Parser.ParseFrom(payload);
    }
}
