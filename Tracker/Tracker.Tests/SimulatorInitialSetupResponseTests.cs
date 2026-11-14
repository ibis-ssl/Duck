using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Duck.Testing.RefereeDriver.Hosting;
using Google.Protobuf;

namespace Tracker.Tests;

public sealed class SimulatorInitialSetupResponseTests
{
    [Fact]
    public async Task InitialSetupReturnsBeforeResponseAndSendsExactlyOneUnchangedDatagram()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var sender = new SimulatorBallControlClient((IPEndPoint)simulator.Client.LocalEndPoint!, TimeSpan.FromMilliseconds(500));
        var receive = simulator.ReceiveAsync();

        var beforeSend = DateTimeOffset.UtcNow;
        await sender.SendInitialOwnHalfSetupAsync(true, CancellationToken.None, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
        var afterSend = DateTimeOffset.UtcNow;
        var sent = await receive.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(SimulatorBallControlClient.CreateInitialOwnHalfSetupCommand(true), sent.Buffer);
        var responseSent = simulator.SendAsync(new SimulatorResponse().ToByteArray(), sent.RemoteEndPoint);
        var observation = await sender.CompleteInitialSetupObservationAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await responseSent;
        var expectedCommand = SimulatorBallControlClient.CreateInitialOwnHalfSetupCommand(true);
        var expectedResponse = new SimulatorResponse().ToByteArray();

        Assert.Equal("response_received", observation!.Status);
        Assert.Equal(expectedCommand.Length, observation.SentDatagramBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expectedCommand)), observation.SentDatagramSha256);
        Assert.Equal("127.0.0.1", observation.DestinationIp);
        Assert.Equal(((IPEndPoint)simulator.Client.LocalEndPoint!).Port, observation.DestinationPort);
        Assert.NotNull(observation.SendStartedAtUtc);
        Assert.NotNull(observation.SendCompletedAtUtc);
        Assert.InRange(observation.SendStartedAtUtc.Value, beforeSend, afterSend);
        Assert.InRange(observation.SendCompletedAtUtc.Value, observation.SendStartedAtUtc.Value, afterSend);
        Assert.NotNull(observation.ResponseReceivedAtUtc);
        Assert.Equal(expectedResponse.Length, observation.DatagramBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expectedResponse)), observation.ResponseDatagramSha256);
        Assert.Empty(observation.Errors);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendInitialOwnHalfSetupAsync(true, CancellationToken.None, CancellationToken.None));
        Assert.Equal(0, simulator.Available);
        Assert.Equal(observation, await sender.CompleteInitialSetupObservationAsync());
    }

    [Fact]
    public async Task CompletingInitialSetupWaitsForBoundedResponseDeadlineInsteadOfCancelingReceive()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var sender = new SimulatorBallControlClient((IPEndPoint)simulator.Client.LocalEndPoint!, TimeSpan.FromMilliseconds(40));
        var receive = simulator.ReceiveAsync();

        await sender.SendInitialOwnHalfSetupAsync(false, CancellationToken.None, CancellationToken.None);
        await receive.WaitAsync(TimeSpan.FromSeconds(2));
        var startedAt = DateTimeOffset.UtcNow;

        var observation = await sender.CompleteInitialSetupObservationAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("no_response_observed", observation!.Status);
        Assert.Contains("within 0.04s", observation.Reason);
        Assert.True(DateTimeOffset.UtcNow - startedAt >= TimeSpan.FromMilliseconds(25));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturesEmptyOrErrorResponseAndClosesTheSocket(bool withError)
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var local = (IPEndPoint)socket.Client.LocalEndPoint!;
        await using var capture = new InitialSetupResponseCapture(socket,
            (IPEndPoint)simulator.Client.LocalEndPoint!, TimeSpan.FromSeconds(2), CancellationToken.None);
        var response = new SimulatorResponse();
        if (withError)
            response.Errors.Add(new SimulatorError { Code = "UNSUPPORTED", Message = "Unsupported teleport option" });
        var payload = response.ToByteArray();

        var beforeResponse = DateTimeOffset.UtcNow;
        await simulator.SendAsync(payload, local);
        var observation = await capture.Observation.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("response_received", observation.Status);
        Assert.NotNull(observation.ResponseReceivedAtUtc);
        Assert.InRange(observation.ResponseReceivedAtUtc.Value, beforeResponse, DateTimeOffset.UtcNow);
        Assert.Equal(payload.Length, observation.DatagramBytes);
        Assert.Equal(simulator.Client.LocalEndPoint!.ToString(), observation.RemoteEndpoint);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(payload)), observation.ResponseDatagramSha256);
        Assert.Equal(withError ? 1 : 0, observation.Errors.Count);
        if (withError)
            Assert.Equal(new("UNSUPPORTED", "Unsupported teleport option"), Assert.Single(observation.Errors));
        Assert.Throws<ObjectDisposedException>(() => socket.Send([1], 1, local));
    }

    [Fact]
    public async Task InvalidResponseIsDistinctFromSimulatorErrors()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var local = (IPEndPoint)socket.Client.LocalEndPoint!;
        await using var capture = new InitialSetupResponseCapture(socket,
            (IPEndPoint)simulator.Client.LocalEndPoint!, TimeSpan.FromSeconds(2), CancellationToken.None);

        var payload = new byte[] { 0x0a, 0xff };
        await simulator.SendAsync(payload, local);
        var observation = await capture.Observation.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("invalid_response", observation.Status);
        Assert.NotNull(observation.ResponseReceivedAtUtc);
        Assert.Equal(2, observation.DatagramBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(payload)), observation.ResponseDatagramSha256);
        Assert.Empty(observation.Errors);
    }

    [Fact]
    public async Task DeadlineRecordsNeutralMissingResponseAndClosesTheSocket()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var local = (IPEndPoint)socket.Client.LocalEndPoint!;
        await using var capture = new InitialSetupResponseCapture(socket,
            (IPEndPoint)simulator.Client.LocalEndPoint!, TimeSpan.FromMilliseconds(30), CancellationToken.None);

        var observation = await capture.Observation.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("no_response_observed", observation.Status);
        Assert.Contains("within 0.03s", observation.Reason);
        Assert.Contains("does not establish non-receipt or non-application", observation.Reason);
        Assert.Throws<ObjectDisposedException>(() => socket.Send([1], 1, local));
    }

    [Fact]
    public async Task StartCancellationStopsObservationAndCanBeDisposedRepeatedly()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var start = new CancellationTokenSource();
        var capture = new InitialSetupResponseCapture(socket,
            (IPEndPoint)simulator.Client.LocalEndPoint!, TimeSpan.FromSeconds(30), start.Token);

        start.Cancel();
        var observation = await capture.Observation.WaitAsync(TimeSpan.FromSeconds(2));
        await capture.DisposeAsync();
        await capture.DisposeAsync();

        Assert.Equal("no_response_observed", observation.Status);
        Assert.Contains("canceled", observation.Reason);
    }

    [Fact]
    public async Task CanceledObserverCannotDisposeSocketWhileSendOwnershipIsPending()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var local = (IPEndPoint)socket.Client.LocalEndPoint!;
        using var observerCanceled = new CancellationTokenSource();
        observerCanceled.Cancel();
        var sendCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var capture = new InitialSetupResponseCapture(socket,
            (IPEndPoint)simulator.Client.LocalEndPoint!, TimeSpan.FromSeconds(5), observerCanceled.Token,
            sendCompleted.Task);

        try
        {
            // A pre-canceled receive deterministically reaches cleanup before the send releases ownership.
            Assert.False(capture.Observation.IsCompleted);
            Assert.Equal(local, socket.Client.LocalEndPoint);
            Assert.Equal(0, simulator.Available);
        }
        finally
        {
            sendCompleted.TrySetResult();
        }

        var observation = await capture.Observation.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("no_response_observed", observation.Status);
        Assert.Throws<ObjectDisposedException>(() => socket.Send([1], 1, local));
    }
    [Fact]
    public async Task ReceiveFailureIsRecordedWithoutThrowingIntoMatchProgression()
    {
        using var simulator = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var capture = new InitialSetupResponseCapture(socket,
            (IPEndPoint)simulator.Client.LocalEndPoint!, TimeSpan.FromSeconds(2), CancellationToken.None);

        socket.Dispose();
        var observation = await capture.Observation.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("receive_error", observation.Status);
        Assert.Empty(observation.Errors);
        Assert.NotEmpty(observation.Reason);
    }
}
