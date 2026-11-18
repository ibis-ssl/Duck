using Tracker.Core;
using Tracker.DebugHost.Tracking;

namespace Tracker.Tests;

/// <summary>
/// TrackerCoordinator test が publish された tracker packet を記録するための shared support double を提供する。
/// </summary>
internal sealed class TrackerCoordinatorRecordingTrackerPacketPublisher : ITrackerPacketPublisher
{
    /// <summary>coordinator から適用された現在の publish 設定を保持します。</summary>
    public TrackerPublisherOptions CurrentOptions { get; private set; } = new();

    /// <summary>coordinator が publish した packet の複製を記録します。</summary>
    public List<TrackerWrapperPacket> Packets { get; } = [];

    /// <summary>各 packet を publish した時点の port を記録します。</summary>
    public List<int> PublishedPorts { get; } = [];

    /// <summary>publish 設定を複製して保持し、後続の記録で参照できるようにします。</summary>
    /// <param name="options">送信先や送信元識別に使う Tracker の配信設定です。</param>
    public void ApplyConfiguration(TrackerPublisherOptions options)
    {
        CurrentOptions = new TrackerPublisherOptions
        {
            PublishUdp = options.PublishUdp,
            MulticastAddress = options.MulticastAddress,
            Port = options.Port,
            InterfaceName = options.InterfaceName,
            SourceName = options.SourceName,
            Uuid = options.Uuid,
        };
    }

    /// <summary>packet と publish 時点の port を記録します。</summary>
    /// <param name="packet">送信する Tracker パケットです。</param>
    public void Publish(TrackerWrapperPacket packet)
    {
        Packets.Add(packet.Clone());
        PublishedPorts.Add(CurrentOptions.Port);
    }
}
