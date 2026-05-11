namespace Tracker.Server.Tracking;

/// <summary>
/// tracker packet の配信先を抽象化し、実行中の publisher 設定変更と packet 送信を扱う。
/// </summary>
public interface ITrackerPacketPublisher
{
    /// <summary>
    /// appsettings と runtime override から解決済みの publisher 設定を適用する。
    /// </summary>
    void ApplyConfiguration(TrackerPublisherOptions options);

    /// <summary>
    /// 指定された tracker wrapper packet を現在の配信設定で送信する。
    /// </summary>
    void Publish(TrackerWrapperPacket packet);
}
