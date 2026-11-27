using System.Text.Json;

namespace Tracker.Tests;

public sealed class NamespaceProbeBridgeNetworkObservationTests
{
    [Theory]
    [InlineData("id", "id")]
    [InlineData("id", "name")]
    public void SharedVerifierConsumerAcceptsAnAttachmentIdAsNetworkMode(string ownerMode, string followerMode)
    {
        const string networkName = "bridge-probe";
        var networkId = new string('b', 64);
        var ownerInspect = CreateInspect(ownerMode == "id" ? networkId : networkName,
            [Attachment(networkName, "192.0.2.10", networkId)]);
        var followerInspect = CreateInspect(followerMode == "id" ? networkId : networkName,
            [Attachment(networkName, "192.0.2.11", networkId)]);
        string? ownerSummary = null;
        string? followerSummary = null;

        var proof = NamespaceProbeBridgeNetworkVerifier.VerifyWithObservation(
            ownerInspect,
            followerInspect,
            (owner, follower) =>
            {
                ownerSummary = owner;
                followerSummary = follower;
            });

        Assert.Equal(networkName, proof.NetworkName);
        Assert.Equal(networkId, proof.NetworkId);
        Assert.NotNull(ownerSummary);
        Assert.NotNull(followerSummary);
    }

    [Fact]
    public void SharedVerifierConsumerRejectsAnUnrelatedNetworkIdAsMode()
    {
        var ownerInspect = CreateInspect(new string('c', 64), [Attachment("bridge-probe", "192.0.2.10", new string('b', 64))]);
        var followerInspect = CreateInspect(new string('c', 64), [Attachment("bridge-probe", "192.0.2.11", new string('b', 64))]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeBridgeNetworkVerifier.VerifyWithObservation(ownerInspect, followerInspect, (_, _) => { }));

        Assert.Contains("does not identify", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedVerifierConsumerRecordsOnlyBoundedNetworkFieldsBeforeStrictFailure()
    {
        var ownerInspect = CreateInspect("default", [Attachment("owner-net", "192.0.2.10")]);
        var followerInspect = CreateInspect(
            "default",
            [
                Attachment("follower-net-1", "192.0.2.11"),
                Attachment("follower-net-2", "192.0.2.12"),
                Attachment("secret-third-network", "192.0.2.13"),
            ]);
        string? ownerSummary = null;
        string? followerSummary = null;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeBridgeNetworkVerifier.VerifyWithObservation(
                ownerInspect,
                followerInspect,
                (owner, follower) =>
                {
                    ownerSummary = owner;
                    followerSummary = follower;
                }));

        Assert.Equal("The follower must have exactly one network attachment.", exception.Message);
        Assert.NotNull(ownerSummary);
        Assert.NotNull(followerSummary);
        Assert.Contains("\"Mode\":\"default\"", ownerSummary, StringComparison.Ordinal);
        Assert.Contains("\"AttachmentCount\":1", ownerSummary, StringComparison.Ordinal);
        Assert.Contains("\"Name\":\"owner-net\"", ownerSummary, StringComparison.Ordinal);
        Assert.Contains(new string('a', 64), ownerSummary, StringComparison.Ordinal);
        Assert.Contains("\"IPv4\":\"192.0.2.10\"", ownerSummary, StringComparison.Ordinal);
        Assert.Contains("\"AttachmentCount\":3", followerSummary, StringComparison.Ordinal);
        Assert.Contains("\"Name\":\"follower-net-1\"", followerSummary, StringComparison.Ordinal);
        Assert.Contains("\"Name\":\"follower-net-2\"", followerSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-third-network", followerSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL_SECRET", ownerSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL_SECRET", followerSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("Config", ownerSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("Env", followerSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("Args", followerSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("Labels", followerSummary, StringComparison.Ordinal);
        Assert.InRange(ownerSummary.Length, 1, NamespaceProbeBridgeNetworkObservation.MaximumSummaryCharacters);
        Assert.InRange(followerSummary.Length, 1, NamespaceProbeBridgeNetworkObservation.MaximumSummaryCharacters);
    }

    [Fact]
    public void MalformedInspectSummaryDoesNotReplaceTheVerifierExceptionOrEchoInput()
    {
        const string malformedInspect = "{not-json-SENTINEL_SECRET";
        var followerInspect = CreateInspect("default", [Attachment("follower-net", "192.0.2.11")]);
        string? ownerSummary = null;

        var exception = Assert.ThrowsAny<JsonException>(() =>
            NamespaceProbeBridgeNetworkVerifier.VerifyWithObservation(
                malformedInspect,
                followerInspect,
                (owner, _) => ownerSummary = owner));

        Assert.NotNull(ownerSummary);
        Assert.DoesNotContain("SENTINEL_SECRET", ownerSummary, StringComparison.Ordinal);
        Assert.InRange(ownerSummary!.Length, 1, NamespaceProbeBridgeNetworkObservation.MaximumSummaryCharacters);
    }

    [Fact]
    public void NonObjectInspectSummaryDoesNotReplaceTheVerifierException()
    {
        const string nonObjectInspect = "[]";
        var followerInspect = CreateInspect("default", [Attachment("follower-net", "192.0.2.11")]);
        string? ownerSummary = null;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeBridgeNetworkVerifier.VerifyWithObservation(
                nonObjectInspect,
                followerInspect,
                (owner, _) => ownerSummary = owner));

        Assert.Contains("object", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(ownerSummary);
        Assert.Contains("invalid-inspect", ownerSummary, StringComparison.Ordinal);
        Assert.InRange(ownerSummary!.Length, 1, NamespaceProbeBridgeNetworkObservation.MaximumSummaryCharacters);
    }

    [Fact]
    public void InvalidFieldTypesProduceFixedSummaryWithoutMaskingVerifierFailure()
    {
        const string invalidInspect = "{\"HostConfig\":{\"NetworkMode\":17},\"NetworkSettings\":{\"Networks\":[]},\"Config\":{\"Env\":[\"SENTINEL_SECRET\"]}}";
        var followerInspect = CreateInspect("default", [Attachment("follower-net", "192.0.2.11")]);
        string? ownerSummary = null;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeBridgeNetworkVerifier.VerifyWithObservation(
                invalidInspect,
                followerInspect,
                (owner, _) => ownerSummary = owner));

        Assert.Contains("NetworkMode", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(ownerSummary);
        Assert.Equal("invalid-inspect", JsonDocument.Parse(ownerSummary!).RootElement.GetProperty("Status").GetString());
        Assert.DoesNotContain("SENTINEL_SECRET", ownerSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void ObservationSummaryCapsLongNetworkStringsAndAttachmentCountDetails()
    {
        var longName = new string('n', 600);
        var inspect = CreateInspect(longName, [Attachment(longName, "192.0.2.10")]);

        var summary = NamespaceProbeBridgeNetworkObservation.Summarize("owner", inspect);

        Assert.InRange(summary.Length, 1, NamespaceProbeBridgeNetworkObservation.MaximumSummaryCharacters);
        Assert.DoesNotContain(longName, summary, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(summary);
        Assert.Equal(128, document.RootElement.GetProperty("Mode").GetString()!.Length);
        Assert.Equal(128, document.RootElement.GetProperty("Attachments")[0].GetProperty("Name").GetString()!.Length);
    }

    private static AttachmentData Attachment(string name, string address, string? networkId = null) =>
        new(name, networkId ?? new string('a', 64), address);

    private static string CreateInspect(string mode, AttachmentData[] attachments)
    {
        var networks = attachments.ToDictionary(attachment => attachment.Name, attachment => new
        {
            attachment.NetworkID,
            IPAddress = attachment.Address,
            Name = attachment.Name,
            Driver = "bridge",
            Options = new { secret = "SENTINEL_SECRET" },
        });
        return JsonSerializer.Serialize(new
        {
            HostConfig = new { NetworkMode = mode },
            NetworkSettings = new { Networks = networks },
            Config = new
            {
                Env = new[] { "SENTINEL_SECRET=value" },
                Cmd = new[] { "SENTINEL_SECRET" },
                Labels = new { secret = "SENTINEL_SECRET" },
            },
        });
    }

    private sealed record AttachmentData(string Name, string NetworkID, string Address);
}
