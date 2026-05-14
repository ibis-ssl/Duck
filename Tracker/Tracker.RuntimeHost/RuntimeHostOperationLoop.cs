using Tracker.Core;

namespace Tracker.RuntimeHost;

/// <summary>
/// RuntimeHost の周期 tick ごとに latest SSL-Vision packet を tracker coordinator へ投入する。
/// </summary>
public sealed class RuntimeHostOperationLoop
{
    private readonly RuntimeVisionPacketBuffer packetBuffer;
    private readonly TrackerCoordinator trackerCoordinator;
    private readonly TrackerRuntimeResolvedOptions resolvedOptions;

    /// <summary>
    /// packet buffer、coordinator、resolved options を受け取り operation loop を作成する。
    /// </summary>
    public RuntimeHostOperationLoop(
        RuntimeVisionPacketBuffer packetBuffer,
        TrackerCoordinator trackerCoordinator,
        TrackerRuntimeResolvedOptions resolvedOptions)
    {
        this.packetBuffer = packetBuffer;
        this.trackerCoordinator = trackerCoordinator;
        this.resolvedOptions = resolvedOptions;
    }

    /// <summary>
    /// 未処理の camera ごとの latest packet があれば tracker coordinator に渡し、処理したかどうかを返す。
    /// </summary>
    public bool ProcessLatestPacket()
    {
        if (!resolvedOptions.Enabled)
        {
            return false;
        }

        if (!packetBuffer.TryTakeLatestBatch(out var packets))
        {
            return false;
        }

        foreach (var packet in packets)
        {
            _ = trackerCoordinator.ProcessPacket(packet.Packet, packet.ReceivedAt);
        }

        return true;
    }
}
