using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Duck.Testing.RefereeDriver;
using Duck.Testing.RefereeDriver.Hosting;

namespace Tracker.Tests;

internal sealed class SimulatorControlRelayClient(Uri endpoint, HttpClient? httpClient = null) : ISimulatorBallControlClient, IDisposable
{
    private static readonly TimeSpan ObservationRequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient client = httpClient ?? new HttpClient();
    private readonly bool ownsClient = httpClient is null;
    private SimulatorSetupDispatchReceipt? initialSetupDispatch;
    private SimulatorResponseObservation? initialSetupObservation;

    public async Task<SimulatorSetupDispatchReceipt> SendInitialOwnHalfSetupAsync(
        bool blueTeamOnPositiveHalf,
        CancellationToken sendCancellationToken,
        CancellationToken observationCancellationToken)
    {
        // The remote relay owns response observation lifetime through ApplicationStopping;
        // this caller token bounds only its HTTP request and JSON receipt read.
        _ = observationCancellationToken;
        if (initialSetupDispatch is not null)
            throw new InvalidOperationException("The initial simulator setup has already been sent through the relay.");

        using var response = await client.PostAsJsonAsync(
            new Uri(endpoint, "setup/initial-own-half"),
            new SimulatorControlRelayApplication.InitialOwnHalfSetupRequest(blueTeamOnPositiveHalf),
            sendCancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(sendCancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateDispatchException(response, responseBody,
                "Simulator setup relay rejected the initial setup dispatch.");
        }

        SimulatorSetupDispatchReceipt? dispatch;
        try
        {
            dispatch = JsonSerializer.Deserialize<SimulatorSetupDispatchReceipt>(responseBody, WebJsonOptions);
        }
        catch (JsonException exception)
        {
            throw CreateDispatchException(response, responseBody,
                "Simulator setup relay returned an unparsable dispatch receipt.", exception);
        }

        var validationError = ValidateDispatchReceipt(dispatch);
        if (validationError is not null)
        {
            throw CreateDispatchException(response, responseBody, validationError);
        }

        // Validation above must complete before the receipt becomes the key for the later GET.
        initialSetupDispatch = dispatch!;
        return dispatch!;
    }

    public async Task<SimulatorResponseObservation?> CompleteInitialSetupObservationAsync(
        CancellationToken cancellationToken = default)
    {
        if (initialSetupDispatch is null || initialSetupObservation is not null)
            return initialSetupObservation;

        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(ObservationRequestTimeout);
        using var response = await client.GetAsync(
            new Uri(endpoint, $"setup/initial-own-half/observation/{initialSetupDispatch.RequestId}"),
            HttpCompletionOption.ResponseHeadersRead,
            requestTimeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync(requestTimeout.Token).ConfigureAwait(false);
        var observation = JsonSerializer.Deserialize<SimulatorResponseObservation>(responseJson, WebJsonOptions)
            ?? throw new InvalidOperationException("Simulator setup relay returned no observation.");
        if (string.IsNullOrWhiteSpace(observation.RequestId) ||
            !string.Equals(observation.RequestId, initialSetupDispatch.RequestId, StringComparison.Ordinal))
            throw new InvalidOperationException("Simulator setup relay returned an observation for a different request.");
        initialSetupObservation = observation;
        return initialSetupObservation;
    }

    public async Task SendBallToPositionAsync(float x, float y, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri(endpoint, "setup/place-ball"),
            new SimulatorControlRelayApplication.BallPlacementRequest(x, y),
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (ownsClient)
            client.Dispose();
    }

    private static string? ValidateDispatchReceipt(SimulatorSetupDispatchReceipt? dispatch)
    {
        if (dispatch is null)
            return "Simulator setup relay returned no dispatch receipt.";
        if (string.IsNullOrWhiteSpace(dispatch.RequestId))
            return "Simulator setup relay dispatch receipt has a blank request ID.";
        if (dispatch.SentDatagramBytes <= 0)
            return "Simulator setup relay dispatch receipt has a non-positive datagram byte count.";
        if (!IsSha256(dispatch.SentDatagramSha256))
            return "Simulator setup relay dispatch receipt has a missing or invalid datagram SHA-256.";
        return null;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static SimulatorControlRelayDispatchException CreateDispatchException(
        HttpResponseMessage response,
        string rawResponseBody,
        string details,
        Exception? innerException = null) =>
        new(response.StatusCode, response.ReasonPhrase, rawResponseBody, details, innerException);
}

internal sealed class SimulatorControlRelayDispatchException : InvalidOperationException
{
    internal SimulatorControlRelayDispatchException(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string rawResponseBody,
        string details,
        Exception? innerException = null)
        : base($"{details} HTTP {(int)statusCode} {reasonPhrase ?? "(no reason phrase)"}; raw POST response body: {rawResponseBody}", innerException)
    {
        HttpStatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        RawResponseBody = rawResponseBody;
    }

    internal HttpStatusCode HttpStatusCode { get; }
    internal string? ReasonPhrase { get; }
    internal string RawResponseBody { get; }
}
