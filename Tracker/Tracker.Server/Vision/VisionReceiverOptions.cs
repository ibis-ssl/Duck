namespace Tracker.Server.Vision;

public sealed class VisionReceiverOptions
{
    public string MulticastAddress { get; set; } = "224.5.23.2";

    public int Port { get; set; } = 10006;

    public string? InterfaceAddress { get; set; }
}
