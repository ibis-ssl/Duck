namespace Tracker.Server.Vision;

public sealed class VisionReceiverOptions
{
    public string MulticastAddress { get; set; } = "224.5.23.2";

    public int Port { get; set; } = 10006;

    public string? InterfaceAddress { get; set; }

    public VisionPacketCaptureOptions PacketCapture { get; set; } = new();

    public Dictionary<string, VisionReceiverProfileOptions> Profiles { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class VisionPacketCaptureOptions
{
    public bool Enabled { get; set; }

    public string DirectoryPath { get; set; } = "packet-captures";

    public string FilePrefix { get; set; } = "ssl-vision-packets";

    public bool FlushEachPacket { get; set; }
}

public sealed class VisionReceiverProfileOptions
{
    public string? MulticastAddress { get; set; }

    public int? Port { get; set; }

    public string? InterfaceAddress { get; set; }
}
