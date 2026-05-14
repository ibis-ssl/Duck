namespace Tracker.RuntimeHost;

/// <summary>
/// RuntimeHost receiver が受信し、operation loop が周期的に取り出す latest SSL-Vision packet buffer。
/// </summary>
public sealed class RuntimeVisionPacketBuffer
{
    private readonly object gate = new();
    private readonly Dictionary<uint, RuntimeVisionPacket> latestDetectionPacketsByCamera = [];
    private RuntimeVisionPacket? latestNonDetectionPacket;

    /// <summary>
    /// latest SSL-Vision packet と受信時刻を保存する。
    /// </summary>
    public void StorePacket(SSL_WrapperPacket packet, DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(packet);

        lock (gate)
        {
            var runtimePacket = new RuntimeVisionPacket(packet.Clone(), receivedAt);
            if (packet.Detection is not null)
            {
                latestDetectionPacketsByCamera[packet.Detection.CameraId] = runtimePacket;
            }
            else
            {
                latestNonDetectionPacket = runtimePacket;
            }
        }
    }

    /// <summary>
    /// 未処理の camera ごとの latest packet があれば取り出し、buffer を空にする。
    /// </summary>
    public bool TryTakeLatestBatch(out IReadOnlyList<RuntimeVisionPacket> packets)
    {
        lock (gate)
        {
            if (latestDetectionPacketsByCamera.Count == 0 &&
                latestNonDetectionPacket is null)
            {
                packets = [];
                return false;
            }

            packets = latestDetectionPacketsByCamera.Values
                .Concat(latestNonDetectionPacket is null ? [] : [latestNonDetectionPacket])
                .OrderBy(packet => packet.ReceivedAt)
                .ToArray();
            latestDetectionPacketsByCamera.Clear();
            latestNonDetectionPacket = null;
            return true;
        }
    }
}

/// <summary>
/// RuntimeHost operation loop へ渡す SSL-Vision packet と受信時刻。
/// </summary>
public sealed class RuntimeVisionPacket
{
    /// <summary>
    /// packet と受信時刻を保持する DTO を作成する。
    /// </summary>
    public RuntimeVisionPacket(SSL_WrapperPacket packet, DateTimeOffset receivedAt)
    {
        Packet = packet;
        ReceivedAt = receivedAt;
    }

    /// <summary>
    /// 受信済み SSL-Vision packet。
    /// </summary>
    public SSL_WrapperPacket Packet { get; }

    /// <summary>
    /// RuntimeHost receiver が packet を受信した時刻。
    /// </summary>
    public DateTimeOffset ReceivedAt { get; }
}
