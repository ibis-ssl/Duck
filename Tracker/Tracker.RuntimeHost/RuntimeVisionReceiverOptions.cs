namespace Tracker.RuntimeHost;

/// <summary>
/// RuntimeHost が SSL-Vision input を受信するための設定。
/// </summary>
public sealed class RuntimeVisionReceiverOptions
{
    /// <summary>
    /// SSL-Vision receiver 設定 section 名。
    /// </summary>
    public const string SectionName = "VisionReceiver";

    /// <summary>
    /// SSL-Vision multicast group address。
    /// </summary>
    public string MulticastAddress { get; init; } = "224.5.23.2";

    /// <summary>
    /// SSL-Vision UDP port。
    /// </summary>
    public int Port { get; init; } = 10006;

    /// <summary>
    /// multicast join に使う local IPv4 address。未指定時は候補 interface から選ぶ。
    /// </summary>
    public string? InterfaceAddress { get; init; }
}
