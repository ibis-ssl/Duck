using System.Net;
using System.Text;
using System.Text.Json;
using Duck.Testing.RefereeDriver.Hosting;

namespace Tracker.Tests;

public sealed class SimulatorControlRelayClientTests
{
    private const string ValidSha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task SetupAndBallPlacementUseTheDcpRelayHttpRoutes()
    {
        var requests = new List<(string Path, string Body)>();
        using var http = new HttpClient(new RecordingHandler(requests)) { BaseAddress = new Uri("http://127.0.0.1:39111/") };
        using var client = new SimulatorControlRelayClient(http.BaseAddress, http);

        var dispatch = await client.SendInitialOwnHalfSetupAsync(
            blueTeamOnPositiveHalf: false, CancellationToken.None, CancellationToken.None);
        Assert.Equal("relay-test-request", dispatch.RequestId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CompleteInitialSetupObservationAsync(CancellationToken.None));
        var observation = await client.CompleteInitialSetupObservationAsync(CancellationToken.None);
        await client.SendBallToPositionAsync(1.5f, -0.75f, CancellationToken.None);

        Assert.Equal("response_received", observation?.Status);
        Assert.Equal(4, requests.Count);
        Assert.Equal("/setup/initial-own-half", requests[0].Path);
        using (var setup = JsonDocument.Parse(requests[0].Body))
            Assert.False(setup.RootElement.GetProperty("blueTeamOnPositiveHalf").GetBoolean());
        Assert.Equal("/setup/initial-own-half/observation/relay-test-request", requests[1].Path);
        Assert.Equal("/setup/initial-own-half/observation/relay-test-request", requests[2].Path);
        Assert.Equal("/setup/place-ball", requests[3].Path);
        using var placement = JsonDocument.Parse(requests[3].Body);
        Assert.Equal(1.5f, placement.RootElement.GetProperty("x").GetSingle());
        Assert.Equal(-0.75f, placement.RootElement.GetProperty("y").GetSingle());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task InvalidDispatchRequestIdIsRejectedBeforeItCanBeCachedOrUsedForGet(string? requestId)
    {
        var requests = new List<(string Path, string Body)>();
        var rawBody = DispatchReceiptJson(requestId, 915, ValidSha256);
        using var http = new HttpClient(new RecordingHandler(requests, dispatchBody: rawBody))
        {
            BaseAddress = new Uri("http://127.0.0.1:39111/"),
        };
        using var client = new SimulatorControlRelayClient(http.BaseAddress, http);

        var failure = await Assert.ThrowsAsync<SimulatorControlRelayDispatchException>(() =>
            client.SendInitialOwnHalfSetupAsync(false, CancellationToken.None, CancellationToken.None));
        Assert.Equal(HttpStatusCode.Accepted, failure.HttpStatusCode);
        Assert.Equal("Accepted", failure.ReasonPhrase);
        Assert.Equal(rawBody, failure.RawResponseBody);
        Assert.Contains(rawBody, failure.Message, StringComparison.Ordinal);
        Assert.Null(await client.CompleteInitialSetupObservationAsync(CancellationToken.None));
        Assert.Collection(requests, request => Assert.Equal("/setup/initial-own-half", request.Path));
    }

    [Fact]
    public async Task NonPositiveDispatchByteCountIsRejectedBeforeCaching()
    {
        await AssertInvalidDispatchReceiptAsync(DispatchReceiptJson("relay-test-request", 0, ValidSha256));
    }

    [Fact]
    public async Task MissingDispatchHashIsRejectedBeforeCaching()
    {
        const string body = """{"requestId":"relay-test-request","destinationIp":"172.18.0.4","destinationPort":10300,"sentDatagramBytes":915,"sendStartedAtUtc":"2026-11-13T00:00:00Z","sendCompletedAtUtc":"2026-11-13T00:00:00Z"}""";
        await AssertInvalidDispatchReceiptAsync(body);
    }

    [Fact]
    public async Task InvalidDispatchHashIsRejectedBeforeCaching()
    {
        await AssertInvalidDispatchReceiptAsync(DispatchReceiptJson("relay-test-request", 915, "not-a-sha256"));
    }

    [Fact]
    public async Task UnparsableDispatchReceiptPreservesHttpDiagnosticsAndRawBody()
    {
        const string rawBody = "{ malformed receipt";
        var requests = new List<(string Path, string Body)>();
        using var http = new HttpClient(new RecordingHandler(requests, dispatchBody: rawBody))
        {
            BaseAddress = new Uri("http://127.0.0.1:39111/"),
        };
        using var client = new SimulatorControlRelayClient(http.BaseAddress, http);

        var failure = await Assert.ThrowsAsync<SimulatorControlRelayDispatchException>(() =>
            client.SendInitialOwnHalfSetupAsync(false, CancellationToken.None, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Accepted, failure.HttpStatusCode);
        Assert.Equal("Accepted", failure.ReasonPhrase);
        Assert.Equal(rawBody, failure.RawResponseBody);
        Assert.Contains(rawBody, failure.Message, StringComparison.Ordinal);
        Assert.IsType<JsonException>(failure.InnerException);
        Assert.Null(await client.CompleteInitialSetupObservationAsync(CancellationToken.None));
        Assert.Collection(requests, request => Assert.Equal("/setup/initial-own-half", request.Path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("wrong-request-id")]
    public async Task BlankOrMismatchedObservationRequestIdIsRejected(string? requestId)
    {
        var requests = new List<(string Path, string Body)>();
        using var http = new HttpClient(new RecordingHandler(
            requests,
            observationBody: ObservationJson(requestId)))
        {
            BaseAddress = new Uri("http://127.0.0.1:39111/"),
        };
        using var client = new SimulatorControlRelayClient(http.BaseAddress, http);
        await client.SendInitialOwnHalfSetupAsync(false, CancellationToken.None, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CompleteInitialSetupObservationAsync(CancellationToken.None));
        Assert.Equal("/setup/initial-own-half/observation/relay-test-request", requests[^1].Path);
    }

    private static async Task AssertInvalidDispatchReceiptAsync(string body)
    {
        var requests = new List<(string Path, string Body)>();
        using var http = new HttpClient(new RecordingHandler(requests, dispatchBody: body))
        {
            BaseAddress = new Uri("http://127.0.0.1:39111/"),
        };
        using var client = new SimulatorControlRelayClient(http.BaseAddress, http);

        var failure = await Assert.ThrowsAsync<SimulatorControlRelayDispatchException>(() =>
            client.SendInitialOwnHalfSetupAsync(false, CancellationToken.None, CancellationToken.None));
        Assert.Equal(HttpStatusCode.Accepted, failure.HttpStatusCode);
        Assert.Equal("Accepted", failure.ReasonPhrase);
        Assert.Equal(body, failure.RawResponseBody);
        Assert.Contains(body, failure.Message, StringComparison.Ordinal);
        Assert.Null(await client.CompleteInitialSetupObservationAsync(CancellationToken.None));
        Assert.Collection(requests, request => Assert.Equal("/setup/initial-own-half", request.Path));
    }

    private static string DispatchReceiptJson(string? requestId, int sentBytes, string? sha256) =>
        JsonSerializer.Serialize(new SimulatorSetupDispatchReceipt(
            requestId!, "172.18.0.4", 10300, sentBytes, sha256!, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));

    private static string ObservationJson(string? requestId) =>
        JsonSerializer.Serialize(new SimulatorResponseObservation(
            "response_received", "172.18.0.4:10300", 2, [], "observed", RequestId: requestId));

    private sealed class RecordingHandler(
        List<(string Path, string Body)> requests,
        string? dispatchBody = null,
        string? observationBody = null) : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);
        private int observationReadCount;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            requests.Add((request.RequestUri!.AbsolutePath,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            var json = request.RequestUri.AbsolutePath switch
            {
                "/setup/initial-own-half" => dispatchBody ?? JsonSerializer.Serialize(new SimulatorSetupDispatchReceipt(
                    "relay-test-request", "172.18.0.4", 10300, 915, ValidSha256,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), WebOptions),
                "/setup/initial-own-half/observation/relay-test-request" => observationBody ?? JsonSerializer.Serialize(
                    new SimulatorResponseObservation("response_received", "172.18.0.4:10300", 2, [], "observed",
                        RequestId: Interlocked.Increment(ref observationReadCount) == 1 ? "wrong-request-id" : "relay-test-request"), WebOptions),
                _ => "{\"sent\":true}",
            };
            return new HttpResponseMessage(request.RequestUri.AbsolutePath == "/setup/initial-own-half"
                ? HttpStatusCode.Accepted
                : HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }
}
