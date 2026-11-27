using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Google.Protobuf;

namespace Duck.Testing.RefereeDriver.Hosting;

internal sealed record SimulatorResponseError(string? Code, string? Message);

internal sealed record SimulatorSetupDispatchReceipt(
    string RequestId,
    string DestinationIp,
    int DestinationPort,
    int SentDatagramBytes,
    string SentDatagramSha256,
    DateTimeOffset SendStartedAtUtc,
    DateTimeOffset SendCompletedAtUtc);

internal sealed record SimulatorResponseObservation(
    string Status,
    string? RemoteEndpoint,
    int? DatagramBytes,
    IReadOnlyList<SimulatorResponseError> Errors,
    string Reason,
    string? DestinationIp = null,
    int? DestinationPort = null,
    DateTimeOffset? SendStartedAtUtc = null,
    DateTimeOffset? SendCompletedAtUtc = null,
    DateTimeOffset? ResponseReceivedAtUtc = null,
    int? SentDatagramBytes = null,
    string? SentDatagramSha256 = null,
    string? ResponseDatagramSha256 = null,
    string? RequestId = null);

/// <summary>Observes one response without sending or gating match progression.</summary>
internal sealed class InitialSetupResponseCapture : IAsyncDisposable
{
    private readonly UdpClient client;
    private readonly CancellationTokenSource deadline;
    private readonly CancellationTokenSource lifetime;
    private readonly Task<SimulatorResponseObservation> observation;
    private readonly Task sendCompleted;
    private readonly TimeSpan timeout;
    private bool completed;

    internal InitialSetupResponseCapture(
        UdpClient client, IPEndPoint simulator, TimeSpan timeout, CancellationToken cancellationToken,
        Task? sendCompleted = null)
    {
        this.client = client;
        this.timeout = timeout;
        this.sendCompleted = sendCompleted ?? Task.CompletedTask;
        deadline = new CancellationTokenSource(timeout);
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        // Receive is pending before the existing single SendAsync, using its local endpoint.
        observation = ObserveAsync(simulator);
    }

    internal Task<SimulatorResponseObservation> Observation => observation;

    private async Task<SimulatorResponseObservation> ObserveAsync(IPEndPoint simulator)
    {
        try
        {
            while (true)
            {
                var datagram = await client.ReceiveAsync(lifetime.Token).ConfigureAwait(false);
                var responseReceivedAtUtc = DateTimeOffset.UtcNow;
                if (!datagram.RemoteEndPoint.Equals(simulator))
                    continue;
                try
                {
                    var response = SimulatorResponse.Parser.ParseFrom(datagram.Buffer);
                    return new("response_received", datagram.RemoteEndPoint.ToString(), datagram.Buffer.Length,
                        response.Errors.Select(error => new SimulatorResponseError(
                            error.HasCode ? error.Code : null, error.HasMessage ? error.Message : null)).ToArray(),
                        "SimulatorResponse received; this does not independently confirm that the requested poses were applied.",
                        ResponseReceivedAtUtc: responseReceivedAtUtc,
                        ResponseDatagramSha256: Convert.ToHexString(SHA256.HashData(datagram.Buffer)));
                }
                catch (InvalidProtocolBufferException exception)
                {
                    return new("invalid_response", datagram.RemoteEndPoint.ToString(), datagram.Buffer.Length, [],
                        exception.Message,
                        ResponseReceivedAtUtc: responseReceivedAtUtc,
                        ResponseDatagramSha256: Convert.ToHexString(SHA256.HashData(datagram.Buffer)));
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            return new("no_response_observed", null, null, [],
                (deadline.IsCancellationRequested
                    ? $"No response observed within {timeout.TotalSeconds:g}s."
                    : "No response observed before the setup/start observation was canceled or the match result was finalized.") +
                " This does not establish non-receipt or non-application of the setup datagram.");
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            return new("receive_error", null, null, [], exception.ToString());
        }
        finally
        {
            // The observer may finish first, but must not close a still-pending send.
            await sendCompleted.ConfigureAwait(false);
            client.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (completed)
            return;
        lifetime.Cancel();
        await observation.ConfigureAwait(false);
        lifetime.Dispose();
        deadline.Dispose();
        completed = true;
    }
}
