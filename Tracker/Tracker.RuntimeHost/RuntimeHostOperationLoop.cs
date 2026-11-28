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
    private readonly RuntimeMatchRecordingSession? recordingSession;

    /// <summary>
    /// packet buffer、coordinator、resolved options を受け取り operation loop を作成する。
    /// </summary>
    public RuntimeHostOperationLoop(
        RuntimeVisionPacketBuffer packetBuffer,
        TrackerCoordinator trackerCoordinator,
        TrackerRuntimeResolvedOptions resolvedOptions)
        : this(packetBuffer, trackerCoordinator, resolvedOptions, recordingSession: null)
    {
    }

    internal RuntimeHostOperationLoop(
        RuntimeVisionPacketBuffer packetBuffer,
        TrackerCoordinator trackerCoordinator,
        TrackerRuntimeResolvedOptions resolvedOptions,
        RuntimeMatchRecordingSession? recordingSession)
    {
        this.packetBuffer = packetBuffer;
        this.trackerCoordinator = trackerCoordinator;
        this.resolvedOptions = resolvedOptions;
        this.recordingSession = recordingSession;
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
            try
            {
                var result = trackerCoordinator.ProcessPacket(packet.Packet, packet.ReceivedAt);
                recordingSession?.WriteProcessed(packet.RecordingPacket, packet.Packet, result, packet.ReceivedAt);
            }
            catch
            {
                recordingSession?.WriteUntracked(
                    packet.RecordingPacket,
                    decodeSucceeded: true,
                    "tracker-processing-failed",
                    packet.ReceivedAt);
                throw;
            }
        }

        return true;
    }
}
