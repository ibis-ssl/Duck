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

        if (!string.Equals(owner.NetworkName, follower.NetworkName, StringComparison.Ordinal) ||
            !string.Equals(owner.NetworkId, follower.NetworkId, StringComparison.OrdinalIgnoreCase) ||
            IPAddress.Parse(owner.Address).Equals(IPAddress.Parse(follower.Address)))
        {
            throw new InvalidOperationException("Owner and follower are not attached to the same inspected network.");
        }

        return new SharedNetworkProof(owner.NetworkName, owner.NetworkId, owner.Address, follower.Address);
    }

    internal static SharedNetworkProof VerifyWithObservation(
        string ownerInspectJson,
        string followerInspectJson,
        Action<string, string> recordObservation)
    {
        ArgumentNullException.ThrowIfNull(recordObservation);
        var ownerSummary = NamespaceProbeBridgeNetworkObservation.Summarize("owner", ownerInspectJson);
        var followerSummary = NamespaceProbeBridgeNetworkObservation.Summarize("follower", followerInspectJson);
        recordObservation(ownerSummary, followerSummary);
        return Verify(ownerInspectJson, followerInspectJson);
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
        if (attachments.Length != 1 || string.IsNullOrWhiteSpace(attachments[0].Name))
        {
            throw new InvalidOperationException($"The {role} must have exactly one network attachment.");
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

        if (!string.Equals(mode, "default", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(attachments[0].Name, mode, StringComparison.Ordinal) &&
            !string.Equals(networkId, mode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The {role} network mode does not identify its sole attachment.");
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

internal static class NamespaceProbeBridgeNetworkObservation
{
    internal const int MaximumSummaryCharacters = 2048;
    private const int MaximumInputCharacters = 1_048_576;
    private const int MaximumModeCharacters = 128;
    private const int MaximumNameCharacters = 128;
    private const int MaximumAddressCharacters = 64;
    private const int MaximumAttachments = 2;

    internal static string Summarize(string role, string inspectJson)
    {
        var safeRole = role is "owner" or "follower" ? role : "unknown";
        if (inspectJson is null || inspectJson.Length > MaximumInputCharacters)
        {
            return Serialize(safeRole, "unavailable", null, []);
        }

        try
        {
            using var document = JsonDocument.Parse(inspectJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Serialize(safeRole, "invalid-inspect", null, []);
            }

            if (!TryReadString(root, "HostConfig", "NetworkMode", out var rawMode) || string.IsNullOrWhiteSpace(rawMode) ||
                !root.TryGetProperty("NetworkSettings", out var settings) ||
                settings.ValueKind != JsonValueKind.Object ||
                !settings.TryGetProperty("Networks", out var networks) ||
                networks.ValueKind != JsonValueKind.Object)
            {
                return Serialize(safeRole, "invalid-inspect", null, []);
            }

            var mode = Limit(rawMode, MaximumModeCharacters);
            var attachments = new List<AttachmentSummary>(MaximumAttachments);
            var count = 0;
            foreach (var attachment in networks.EnumerateObject())
            {
                count = count == int.MaxValue ? count : count + 1;
                if (attachments.Count < MaximumAttachments)
                {
                    if (attachment.Value.ValueKind != JsonValueKind.Object ||
                        !TryReadString(attachment.Value, "NetworkID", out var networkId) ||
                        string.IsNullOrWhiteSpace(networkId) ||
                        !TryReadString(attachment.Value, "IPAddress", out var address) ||
                        string.IsNullOrWhiteSpace(address))
                    {
                        return Serialize(safeRole, "invalid-inspect", null, []);
                    }

                    attachments.Add(new AttachmentSummary(
                        Limit(attachment.Name, MaximumNameCharacters),
                        Limit(networkId, 64),
                        Limit(address, MaximumAddressCharacters)));
                }
            }

            return Serialize(safeRole, "available", mode, attachments, count);
        }
        catch (JsonException)
        {
            return Serialize(safeRole, "invalid-inspect", null, []);
        }
    }

    private static bool TryReadString(JsonElement root, string parent, string property, out string? result)
    {
        if (!root.TryGetProperty(parent, out var parentValue) ||
            parentValue.ValueKind != JsonValueKind.Object ||
            !TryReadString(parentValue, property, out result))
        {
            result = null;
            return false;
        }

        return true;
    }

    private static bool TryReadString(JsonElement element, string property, out string? result)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            result = null;
            return false;
        }

        result = value.GetString();
        return true;
    }

    private static string? Limit(string? value, int maximumCharacters) =>
        value is null || value.Length <= maximumCharacters ? value : value[..maximumCharacters];

    private static string Serialize(
        string role,
        string status,
        string? mode,
        IReadOnlyList<AttachmentSummary> attachments,
        int? attachmentCount = null)
    {
        var summary = JsonSerializer.Serialize(new
        {
            Role = role,
            Status = status,
            Mode = mode,
            AttachmentCount = attachmentCount,
            Attachments = attachments,
        });
        return summary.Length <= MaximumSummaryCharacters
            ? summary
            : JsonSerializer.Serialize(new { Role = role, Status = "summary-truncated" });
    }

    private sealed record AttachmentSummary(string? Name, string? NetworkID, string? IPv4);
}
