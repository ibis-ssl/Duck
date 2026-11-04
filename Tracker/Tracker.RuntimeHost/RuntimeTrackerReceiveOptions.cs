namespace Tracker.RuntimeHost;

/// <summary>Optional in-container receiver for external native tracker output.</summary>
public sealed class RuntimeTrackerReceiveOptions
{
    public const string SectionName = "Tracker:Receive";

    public bool Enabled { get; init; }
    public string MulticastAddress { get; init; } = "224.5.23.2";
    public int Port { get; init; } = 11010;
    public string? InterfaceAddress { get; init; }
}
