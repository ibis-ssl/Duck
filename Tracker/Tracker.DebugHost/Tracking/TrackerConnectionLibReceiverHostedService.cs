using System.Net;
using TrackerConnectionLib;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// TrackerConnectionLib の UDP receiver を application lifetime に接続する hosted service。
/// </summary>
public sealed class TrackerConnectionLibReceiverHostedService : IHostedService
{
    private readonly UdpTrackerReceiver<TrackerPacketAdapter> receiver;
    private readonly MultiTrackerManager<TrackerPacketAdapter> manager;
    private readonly TrackerConnectionLibSnapshotRecorder recorder;
    private readonly ILogger<TrackerConnectionLibReceiverHostedService> logger;

    /// <summary>
    /// live receiver、manager、snapshot recorder を常駐接続する。
    /// </summary>
    public TrackerConnectionLibReceiverHostedService(
        UdpTrackerReceiver<TrackerPacketAdapter> receiver,
        MultiTrackerManager<TrackerPacketAdapter> manager,
        TrackerConnectionLibSnapshotRecorder recorder,
        ILogger<TrackerConnectionLibReceiverHostedService> logger)
    {
        this.receiver = receiver;
        this.manager = manager;
        this.recorder = recorder;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = recorder;
        receiver.PacketReceived += ProcessPacket;
        receiver.Start();
        logger.LogInformation("Started live tracker receiver.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        receiver.PacketReceived -= ProcessPacket;
        await receiver.StopAsync();
        logger.LogInformation(
            "Stopped live tracker receiver. HandlerErrorCount={HandlerErrorCount}",
            receiver.HandlerErrorCount);
    }

    private void ProcessPacket(TrackerPacketAdapter packet, IPEndPoint remoteEndpoint)
    {
        manager.ProcessPacket(packet, remoteEndpoint, DateTimeOffset.UtcNow);
    }
}
