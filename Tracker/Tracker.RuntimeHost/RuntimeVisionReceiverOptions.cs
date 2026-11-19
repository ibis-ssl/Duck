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

    /// <summary>設定した UDP リスナーを開くかどうか。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// SSL-Vision の受信時に参加するマルチキャストグループのアドレスです。
    /// </summary>
    public string MulticastAddress { get; init; } = "224.5.23.2";

    /// <summary>
    /// SSL-Vision を受信する UDP ポートです。
    /// </summary>
    public int Port { get; init; } = 10006;

    /// <summary>
    /// UDP ソケットの待受アドレスです。既定ではすべての IPv4 インターフェイスで待ち受け、ホスト境界用の構成ではループバックに限定できます。
    /// </summary>
    public string ListenAddress { get; init; } = "0.0.0.0";

    /// <summary>
    /// multicast join に使う local IPv4 address。未指定時は候補 interface から選ぶ。
    /// </summary>
    public string? InterfaceAddress { get; init; }

}
