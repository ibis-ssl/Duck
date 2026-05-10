namespace Tracker.Server.Tracking;

public sealed class TrackerPublisherOptions
{
    public bool PublishUdp { get; init; } = true;

    public string MulticastAddress { get; init; } = "224.5.23.2";

    public int Port { get; init; } = 10010;

    public string SourceName { get; init; } = "ibisduck-tracker";

    public string Uuid { get; init; } = "ibisduck-tracker";
}
