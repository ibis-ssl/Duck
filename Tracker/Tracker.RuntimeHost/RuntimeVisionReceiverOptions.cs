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

    /// <summary>Whether to open the configured UDP listener.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// SSL-Vision multicast group address。
    /// </summary>
    public string MulticastAddress { get; init; } = "224.5.23.2";

    /// <summary>
    /// SSL-Vision UDP port。
    /// </summary>
    public int Port { get; init; } = 10006;

    /// <summary>
    /// UDP socket bind address. Defaults to all IPv4 interfaces; host-boundary profiles can restrict it to loopback.
    /// </summary>
    public string ListenAddress { get; init; } = "0.0.0.0";

    /// <summary>
    /// multicast join に使う local IPv4 address。未指定時は候補 interface から選ぶ。
    /// </summary>
    public string? InterfaceAddress { get; init; }

    /// <summary>Optional IPv4 destination for a best-effort copy of successfully decoded datagrams.</summary>
    public string? ForwardAddress { get; init; }

    /// <summary>Optional UDP destination port for a best-effort copy of successfully decoded datagrams.</summary>
    public int ForwardPort { get; init; }
}
