namespace Tracker.RuntimeHost;

/// <summary>
/// RuntimeHost receiver が受信し、operation loop が周期的に取り出す latest SSL-Vision packet buffer。
/// </summary>
public sealed class RuntimeVisionPacketBuffer
{
    private readonly object gate = new();
    private RuntimeVisionPacket? latestPacket;

    /// <summary>
    /// latest SSL-Vision packet と受信時刻を保存する。
    /// </summary>
    public void StorePacket(SSL_WrapperPacket packet, DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(packet);

        lock (gate)
        {
            latestPacket = new RuntimeVisionPacket(packet.Clone(), receivedAt);
        }
    }

    /// <summary>
    /// 未処理の latest packet があれば取り出し、buffer を空にする。
    /// </summary>
    public bool TryTakeLatest(out RuntimeVisionPacket packet)
    {
        lock (gate)
        {
            if (latestPacket is null)
            {
                packet = null!;
                return false;
            }

            packet = latestPacket;
            latestPacket = null;
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
