using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Tracker.Tests;

/// <summary>Docker inspectの既知フィールドだけで、二資源の標準ネットワーク接続を照合します。</summary>
internal static class NamespaceProbeBridgeNetworkVerifier
{
    internal static SharedNetworkProof Verify(string ownerInspectJson, string followerInspectJson)
    {
        var owner = Read(ownerInspectJson, "owner");
        var follower = Read(followerInspectJson, "follower");

        if (!string.Equals(owner.Mode, follower.Mode, StringComparison.Ordinal) ||
            !string.Equals(owner.NetworkName, follower.NetworkName, StringComparison.Ordinal) ||
            !string.Equals(owner.NetworkId, follower.NetworkId, StringComparison.OrdinalIgnoreCase) ||
            IPAddress.Parse(owner.Address).Equals(IPAddress.Parse(follower.Address)))
        {
            throw new InvalidOperationException("Owner and follower are not attached to the same inspected network.");
        }

        return new SharedNetworkProof(owner.NetworkName, owner.NetworkId, owner.Address, follower.Address);
    }

    private static NetworkObservation Read(string json, string role)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var mode = RequiredString(root, "HostConfig", "NetworkMode");
        if (string.IsNullOrWhiteSpace(mode) ||
            string.Equals(mode, "host", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "none", StringComparison.OrdinalIgnoreCase) ||
            mode.StartsWith("container:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The {role} does not report a named standard network mode.");
        }

        if (!root.TryGetProperty("NetworkSettings", out var settings) ||
            settings.ValueKind != JsonValueKind.Object ||
            !settings.TryGetProperty("Networks", out var networks) ||
            networks.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"The {role} network attachments are missing or malformed.");
        }

        var attachments = networks.EnumerateObject().ToArray();
        if (attachments.Length != 1 ||
            (!string.Equals(mode, "default", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(attachments[0].Name, mode, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"The {role} must have exactly one attachment matching its named network mode.");
        }

        var network = attachments[0].Value;
        if (network.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"The {role} network attachment is malformed.");
        }

        var networkId = RequiredString(network, "NetworkID");
        if (networkId.Length != 64 || networkId.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException($"The {role} network identity is not a full network ID.");
        }

        var addressText = RequiredString(network, "IPAddress");
        if (!IPAddress.TryParse(addressText, out var address) ||
            address.AddressFamily != AddressFamily.InterNetwork ||
            IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any))
        {
            throw new InvalidOperationException($"The {role} network address is not a routable IPv4 address.");
        }

        return new NetworkObservation(mode, attachments[0].Name, networkId, addressText);
    }

    private static string RequiredString(JsonElement root, string parent, string property)
    {
        if (!root.TryGetProperty(parent, out var parentElement) ||
            parentElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Docker inspect omitted {parent}.");
        }

        return RequiredString(parentElement, property);
    }

    private static string RequiredString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException($"Docker inspect omitted a valid {property} value.");
        }

        return value.GetString()!;
    }

    internal sealed record SharedNetworkProof(string NetworkName, string NetworkId, string OwnerAddress, string FollowerAddress);

    private sealed record NetworkObservation(string Mode, string NetworkName, string NetworkId, string Address);
}
