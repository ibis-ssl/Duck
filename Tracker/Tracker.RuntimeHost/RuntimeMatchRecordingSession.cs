using System.Text.Json;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Tracker.Core;
using Tracker.Diagnostics.Mcap;

namespace Tracker.RuntimeHost;

/// <summary>RuntimeHost の一回分の試合録画を所有し、記録失敗を追跡処理から隔離する。</summary>
internal sealed class RuntimeMatchRecordingSession(ILogger<RuntimeMatchRecordingSession> logger) : IDisposable
{
    private const string InitialGenerationId = "runtime-initial";
    private readonly object gate = new();
    private RecordingMcapWriter? writer;
    private string? recordingId;
    private long engineUpdateOrder;
    private ulong nextSequence;
    private bool failed;

    public bool IsRecording
    {
        get
        {
            lock (gate)
            {
                return writer is not null && !failed;
            }
        }
    }

    public void Start(string directoryPath, TrackerRuntimeResolvedOptions resolvedOptions, DateTimeOffset startedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(resolvedOptions);
        if (!resolvedOptions.Enabled)
        {
            return;
        }

        lock (gate)
        {
            if (writer is not null)
            {
                throw new InvalidOperationException("RuntimeHost match recording is already active.");
            }

            var started = startedAtUtc.ToUniversalTime();
            recordingId = Guid.NewGuid().ToString("N");
            engineUpdateOrder = 0;
            nextSequence = 0;
            failed = false;
            var completedPath = Path.Combine(
                directoryPath,
                $"match-{started:yyyyMMdd'T'HHmmssfff'Z'}-{recordingId}.mcap");
            try
            {
                writer = new RecordingMcapWriter(
                    completedPath,
                    recordingId,
                    started,
                    CreateEffectiveConfiguration(resolvedOptions.EngineSettings),
                    InitialGenerationId);
            }
            catch (Exception exception)
            {
                recordingId = null;
                logger.LogError(exception, "Could not start RuntimeHost match recording; tracking will continue without recording.");
            }
        }
    }

    public RuntimeMatchRecordingPacket? WriteRaw(byte[] payload, DateTimeOffset receivedAtUtc, string source)
    {
        lock (gate)
        {
            if (writer is null || failed)
            {
                return null;
            }

            var packetSequence = nextSequence++;
            try
            {
                writer.WriteRawPacket(payload, receivedAtUtc, source, packetSequence);
                return new RuntimeMatchRecordingPacket(recordingId!, packetSequence, InitialGenerationId);
            }
            catch (Exception exception)
            {
                Fail(exception);
                return null;
            }
        }
    }

    public void WriteUntracked(
        RuntimeMatchRecordingPacket? packet,
        bool decodeSucceeded,
        string reason,
        DateTimeOffset timestampUtc)
    {
        if (packet is null)
        {
            return;
        }

        lock (gate)
        {
            try
            {
                if (writer is null || failed || !string.Equals(recordingId, packet.RecordingId, StringComparison.Ordinal))
                {
                    return;
                }

                writer.WritePacketContext(
                    new RecordingMcapPacketContext(
                        packet.Sequence,
                        null,
                        packet.ConfigurationGenerationId,
                        decodeSucceeded,
                        false,
                        reason),
                    timestampUtc);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }
    }

    public void WriteProcessed(
        RuntimeMatchRecordingPacket? packetStamp,
        SSL_WrapperPacket packet,
        TrackerUpdateResult updateResult,
        DateTimeOffset receivedAtUtc)
    {
        if (packetStamp is null)
        {
            return;
        }

        lock (gate)
        {
            try
            {
                if (writer is null || failed || !string.Equals(recordingId, packetStamp.RecordingId, StringComparison.Ordinal))
                {
                    return;
                }

                var order = checked(++engineUpdateOrder);
                var wasLateDropped = updateResult.Diagnostics.LatePacketDropCount > 0;
                writer.WritePacketContext(
                    new RecordingMcapPacketContext(
                        packetStamp.Sequence,
                        wasLateDropped ? null : order,
                        packetStamp.ConfigurationGenerationId,
                        true,
                        !wasLateDropped,
                        wasLateDropped ? "tracker-late-input-dropped" : null),
                    receivedAtUtc);

                if (wasLateDropped)
                {
                    return;
                }

                if (packet.Geometry is { } geometry)
                {
                    writer.WriteControlEvent(
                        new RecordingMcapControlEvent(
                            order,
                            "geometry-updated",
                            packetStamp.Sequence,
                            packetStamp.ConfigurationGenerationId,
                            JsonSerializer.SerializeToElement(new { protobufBase64 = Convert.ToBase64String(geometry.ToByteArray()) })),
                        receivedAtUtc);
                }

                if (updateResult.EmittedEvents.Any(item => item.Kind == TrackerEventKind.GeometryReset))
                {
                    writer.WriteControlEvent(
                        new RecordingMcapControlEvent(
                            order,
                            "geometry-reset",
                            packetStamp.Sequence,
                            packetStamp.ConfigurationGenerationId,
                            JsonSerializer.SerializeToElement(new { })),
                        receivedAtUtc);
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }
    }

    public void Complete(DateTimeOffset stoppedAtUtc)
    {
        lock (gate)
        {
            if (writer is null)
            {
                return;
            }

            try
            {
                if (!failed)
                {
                    writer.Complete(stoppedAtUtc);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not complete RuntimeHost match recording; the partial file will not be published.");
            }
            finally
            {
                writer.Dispose();
                writer = null;
                recordingId = null;
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            writer?.Dispose();
            writer = null;
            recordingId = null;
        }
    }

    private static JsonElement CreateEffectiveConfiguration(TrackerEngineSettings settings)
    {
        var properties = JsonSerializer.SerializeToElement(settings)
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        properties["AreaSelection"] = JsonSerializer.SerializeToElement(new
        {
            Enabled = false,
            Scope = "all-field",
            CoordinateSystem = "ssl-field-center",
            Units = "mm",
        });
        return JsonSerializer.SerializeToElement(properties);
    }

    private void Fail(Exception exception)
    {
        failed = true;
        logger.LogError(exception, "RuntimeHost match recording failed; tracking will continue without a completed recording.");
        try
        {
            writer?.Dispose();
        }
        catch (Exception cleanupException)
        {
            logger.LogWarning(cleanupException, "Could not close the failed RuntimeHost match recording writer.");
        }
    }
}

internal sealed record RuntimeMatchRecordingPacket(string RecordingId, ulong Sequence, string ConfigurationGenerationId);
