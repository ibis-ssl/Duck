using System.Net;
using Duck.Testing.Diagnostics;

namespace Duck.Testing.AppHost;

internal static class TigersTrackerOutputReadiness
{
    internal static bool TrySelectFreshCurrentSource(
        IReadOnlyList<ServiceReceiveSnapshot> snapshots,
        string runId,
        string duckResourceId,
        string duckContainerId,
        IPAddress tigersAddress,
        TimeSpan requestDuration,
        DateTimeOffset observedAtUtc,
        out ServiceReceiveSnapshot? selected,
        out string reason)
    {
        selected = null;
        if (requestDuration < TimeSpan.Zero || string.IsNullOrWhiteSpace(runId) ||
            string.IsNullOrWhiteSpace(duckResourceId) || !IsFullId(duckContainerId))
        {
            reason = "Current run/container identity or HTTP timing is invalid.";
            return false;
        }

        var matches = snapshots.Where(snapshot =>
            string.Equals(snapshot.InputId, "tracker", StringComparison.Ordinal) &&
            string.Equals(snapshot.SourceName, "TIGERs", StringComparison.OrdinalIgnoreCase) &&
            snapshot.RemoteEndpoint is { } remoteEndpoint &&
            IPEndPoint.TryParse(remoteEndpoint, out var endpoint) &&
            endpoint.Address.Equals(tigersAddress) &&
            snapshot.Identity is { } identity &&
            string.Equals(identity.RunId, runId, StringComparison.Ordinal) &&
            string.Equals(identity.ResourceId, duckResourceId, StringComparison.Ordinal) &&
            string.Equals(identity.ContainerId, duckContainerId, StringComparison.Ordinal) &&
            identity.Generation > 0 && snapshot.ReceiverEpoch > 0 &&
            snapshot.SuccessfullyDecodedCount > 0 &&
            snapshot.DecodeStatus == DiagnosticDecodeStatus.Succeeded &&
            snapshot.LastReceivedAt is not null &&
            snapshot.LastReceivedAt <= observedAtUtc + TimeSpan.FromSeconds(1) &&
            snapshot.DecodeAgeUpperBound is { } age &&
            age >= TimeSpan.Zero &&
            age + requestDuration <= ServiceReceiveDiagnostics.FreshnessWindow)
            .ToArray();

        if (matches.Length != 1)
        {
            reason = "No unique fresh decoded TIGERs tracker source matched the current network sender and Duck run/container identity.";
            return false;
        }

        selected = matches[0];
        reason = "Fresh decoded TIGERs tracker source matches the current Duck and shared-network identities.";
        return true;
    }

    private static bool IsFullId(string value) =>
        value.Length == 64 && value.All(char.IsAsciiHexDigit);
}
