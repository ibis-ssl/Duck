using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using TrackerConnectionLib;

namespace Tracker.RuntimeHost;

/// <summary>Receives native tracker packets inside RuntimeHost's existing container network.</summary>
internal sealed class RuntimeTrackerReceiverService : IHostedService
{
    private readonly RuntimeTrackerReceiveOptions options;
    private readonly RuntimeVisionReceiverDiagnostics diagnostics;
    private readonly ILogger<RuntimeTrackerReceiverService> logger;
    private UdpTrackerReceiver<TrackerPacketAdapter>? receiver;
    private RuntimeTrackerPacketDeserializer? deserializer;

    public RuntimeTrackerReceiverService(
        IOptions<RuntimeTrackerReceiveOptions> options,
        RuntimeVisionReceiverDiagnostics diagnostics,
        ILogger<RuntimeTrackerReceiverService> logger)
    {
        this.options = options.Value;
        this.diagnostics = diagnostics;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled) return Task.CompletedTask;
        if (options.Port is <= 0 or > 65535)
            throw new InvalidOperationException("Tracker:Receive:Port must be a valid UDP port.");

        deserializer = new RuntimeTrackerPacketDeserializer(diagnostics);
        receiver = new UdpTrackerReceiver<TrackerPacketAdapter>(
            options.Port, options.MulticastAddress, deserializer, options.InterfaceAddress);
        receiver.PacketReceived += ProcessPacket;
        receiver.Start();
        logger.LogInformation(
            "RuntimeHost tracker-output observer listening at {MulticastAddress}:{Port}",
            options.MulticastAddress, options.Port);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (receiver is null) return;
        receiver.PacketReceived -= ProcessPacket;
        await receiver.StopAsync().ConfigureAwait(false);
        logger.LogInformation(
            "RuntimeHost tracker-output observer stopped. HandlerErrorCount={HandlerErrorCount}",
            receiver.HandlerErrorCount);
        receiver.Dispose();
        receiver = null;
    }

    private void ProcessPacket(TrackerPacketAdapter packet, System.Net.IPEndPoint endpoint)
    {
        deserializer?.RecordPacket(packet, endpoint);
    }
}

internal sealed class RuntimeTrackerPacketDeserializer(RuntimeVisionReceiverDiagnostics diagnostics)
    : ITrackerDeserializer<TrackerPacketAdapter>
{
    private readonly TrackerWrapperPacketDeserializer inner = new();
    private readonly ConditionalWeakTable<TrackerPacketAdapter, Duck.Testing.Diagnostics.ServiceReceiveDiagnostics.ReceiveAttempt> attempts = new();

    public bool TryDeserialize(ReadOnlySpan<byte> data, out TrackerPacketAdapter? packet)
    {
        var attempt = diagnostics.BeginTrackerReceive();
        if (!inner.TryDeserialize(data, out packet) || packet is null)
        {
            diagnostics.RecordTrackerDecodeFailure(attempt);
            return false;
        }

        attempts.Add(packet, attempt);
        return true;
    }

    internal void RecordPacket(TrackerPacketAdapter packet, System.Net.IPEndPoint endpoint)
    {
        if (!attempts.TryGetValue(packet, out var attempt)) return;
        attempts.Remove(packet);
        diagnostics.RecordTrackerPacket(attempt, packet, endpoint);
    }
}
