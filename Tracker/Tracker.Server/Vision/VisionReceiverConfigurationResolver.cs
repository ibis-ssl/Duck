namespace Tracker.Server.Vision;

public static class VisionReceiverConfigurationResolver
{
    public static VisionReceiverResolvedOptions Resolve(
        VisionReceiverOptions options,
        string? profileName)
    {
        ArgumentNullException.ThrowIfNull(options);

        var resolved = new VisionReceiverResolvedOptions
        {
            MulticastAddress = options.MulticastAddress,
            Port = options.Port,
            InterfaceAddress = options.InterfaceAddress,
        };

        if (!string.IsNullOrWhiteSpace(profileName) &&
            options.Profiles.TryGetValue(profileName, out var profile))
        {
            resolved = new VisionReceiverResolvedOptions
            {
                MulticastAddress = profile.MulticastAddress ?? resolved.MulticastAddress,
                Port = profile.Port ?? resolved.Port,
                InterfaceAddress = profile.InterfaceAddress ?? resolved.InterfaceAddress,
            };
        }

        return resolved;
    }
}

public sealed record VisionReceiverResolvedOptions
{
    public string MulticastAddress { get; init; } = "224.5.23.2";

    public int Port { get; init; } = 10006;

    public string? InterfaceAddress { get; init; }
}
