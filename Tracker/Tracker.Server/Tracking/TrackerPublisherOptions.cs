namespace Tracker.Server.Tracking;

/// <summary>
/// tracker packet publisher に適用済みの送信設定。
/// </summary>
public sealed class TrackerPublisherOptions
{
    /// <summary>
    /// UDP publish を有効にするかどうか。
    /// </summary>
    public bool PublishUdp { get; init; } = true;

    /// <summary>
    /// tracker packet を送信する multicast address。
    /// </summary>
    public string MulticastAddress { get; init; } = "224.5.23.2";

    /// <summary>
    /// tracker packet を送信する UDP port。
    /// </summary>
    public int Port { get; init; } = 10010;

    /// <summary>
    /// tracker packet に埋め込む source name。
    /// </summary>
    public string SourceName { get; init; } = "ibisduck-tracker";

    /// <summary>
    /// tracker packet に埋め込む uuid。
    /// </summary>
    public string Uuid { get; init; } = "ibisduck-tracker";
}
