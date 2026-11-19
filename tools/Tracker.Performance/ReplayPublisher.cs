using System.Security.Cryptography;
using Google.Protobuf;
using Tracker.Core;

namespace Tracker.Performance;

/// <summary>
/// 製品の配信パケットをバイト列に変換し、品質再生でだけそのバイト列を識別する。
/// </summary>
internal sealed class ReplayPublisher(bool hashOutput) : ITrackerPacketPublisher, IDisposable
{
    private readonly IncrementalHash? wireHash = hashOutput
        ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
        : null;

    /// <summary>
    /// 直列化に成功した配信パケット数。
    /// </summary>
    internal int PacketCount { get; private set; }

    /// <summary>
    /// 直列化に成功した配信パケットの総バイト数。
    /// </summary>
    internal long ByteCount { get; private set; }

    /// <summary>
    /// メモリー配信のため、宛先設定から外部接続を開かない。
    /// </summary>
    public void ApplyConfiguration(TrackerPublisherOptions options)
    {
    }

    /// <summary>
    /// 配信対象を protobuf バイト列に変換し、品質再生では配信順にハッシュへ加える。
    /// </summary>
    public void Publish(TrackerWrapperPacket packet)
    {
        var bytes = packet.ToByteArray();
        wireHash?.AppendData(bytes);
        PacketCount++;
        ByteCount += bytes.Length;
    }

    /// <summary>
    /// 品質再生の出力ハッシュを取得する。通常の計測パスでは null を返す。
    /// </summary>
    internal string? FinishHash() => wireHash is null ? null : Convert.ToHexString(wireHash.GetHashAndReset());

    /// <summary>
    /// 品質再生が使用したハッシュ計算用の資源を解放する。
    /// </summary>
    public void Dispose() => wireHash?.Dispose();
}
