namespace Tracker.Server.Tracking;

/// <summary>
/// live tracker receiver が起動時に監視する endpoint を解決する。
/// </summary>
internal static class TrackerReceiveEndpointResolver
{
    /// <summary>
    /// `Tracker:Receive` の明示 endpoint を優先し、未指定項目は解決済み publish endpoint へ fallback する。
    /// </summary>
    public static TrackerReceiveEndpointOptions Resolve(
        TrackerReceiveOptions receiveOptions,
        TrackerPublisherOptions publisherOptions)
    {
        ArgumentNullException.ThrowIfNull(receiveOptions);
        ArgumentNullException.ThrowIfNull(publisherOptions);

        return new TrackerReceiveEndpointOptions(
            receiveOptions.MulticastAddress ?? publisherOptions.MulticastAddress,
            receiveOptions.Port ?? publisherOptions.Port,
            receiveOptions.InterfaceAddress);
    }
}

/// <summary>
/// live tracker receiver の起動時解決済み endpoint。
/// </summary>
internal sealed record TrackerReceiveEndpointOptions(
    string MulticastAddress,
    int Port,
    string? InterfaceAddress);
