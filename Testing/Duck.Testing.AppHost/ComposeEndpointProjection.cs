using System.Globalization;
using System.Net;
using Aspire.Hosting.ApplicationModel;

namespace Duck.Testing.AppHost;

internal static class ComposeEndpointProjection
{
    public static void SetAllocatedPort(EndpointAnnotation endpoint, string address, int port)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        if (!IPAddress.TryParse(address, out _) || port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Compose must return a valid IP address and host port.");
        }

        var targetPort = endpoint.TargetPort ?? throw new InvalidOperationException(
            $"Endpoint '{endpoint.Name}' must declare its container target port before Compose allocation.");
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(
            endpoint,
            address,
            port,
            targetPort.ToString(CultureInfo.InvariantCulture));
    }
}
