using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Duck.Testing.RefereeDriver.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Tracker.Tests;

public sealed class SimulatorControlRelayServerTests
{
    [Fact]
    public async Task KestrelBinds39111AndServesRelayRoutes()
    {
        var expectedSetupDatagram = SimulatorBallControlClient.CreateInitialOwnHalfSetupCommand(false);
        var expectedSetupDatagramSha256 = Convert.ToHexString(SHA256.HashData(expectedSetupDatagram));
        var simulator = new RecordingSimulator(expectedSetupDatagram.Length, expectedSetupDatagramSha256);
        Assert.Equal("http://0.0.0.0:39111", SimulatorControlRelayApplication.ListenUrl);
        await using var app = SimulatorControlRelayApplication.CreateApplication(simulator, "http://127.0.0.1:0");
        await app.StartAsync();
        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var address = Assert.Single(server.Features.Get<IServerAddressesFeature>()!.Addresses);
            var baseAddress = new Uri(address);
            Assert.Equal(IPAddress.Loopback, IPAddress.Parse(baseAddress.Host));
            Assert.InRange(baseAddress.Port, 1, 65535);
            using var http = new HttpClient { BaseAddress = new Uri(baseAddress, "/") };

            using var ready = await http.GetAsync("health/ready");
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

            using var setup = await http.PostAsJsonAsync("setup/initial-own-half", new { blueTeamOnPositiveHalf = false });
            Assert.Equal(HttpStatusCode.Accepted, setup.StatusCode);
            var dispatch = await setup.Content.ReadFromJsonAsync<SimulatorSetupDispatchReceipt>();
            Assert.Equal("test-request", dispatch?.RequestId);
            Assert.Equal(expectedSetupDatagram.Length, dispatch?.SentDatagramBytes);
            Assert.Equal(expectedSetupDatagramSha256, dispatch?.SentDatagramSha256);
            Assert.Equal("172.18.0.4", dispatch?.DestinationIp);
            Assert.Equal(10300, dispatch?.DestinationPort);
            Assert.NotNull(dispatch?.SendStartedAtUtc);
            Assert.NotNull(dispatch?.SendCompletedAtUtc);
            Assert.Equal([false], simulator.HalfSetupCalls);
            Assert.NotEqual(simulator.SendCancellationToken, simulator.ObservationCancellationToken);
            Assert.False(simulator.ObservationCancellationToken.IsCancellationRequested);

            using var wrongObservationId = await http.GetAsync("setup/initial-own-half/observation/wrong-request-id");
            Assert.Equal(HttpStatusCode.Conflict, wrongObservationId.StatusCode);

            using var observationResponse = await http.GetAsync($"setup/initial-own-half/observation/{dispatch!.RequestId}");
            Assert.Equal(HttpStatusCode.OK, observationResponse.StatusCode);
            var observationJson = await observationResponse.Content.ReadAsStringAsync();
            var observation = System.Text.Json.JsonSerializer.Deserialize<SimulatorResponseObservation>(observationJson,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            Assert.Equal("response_received", observation?.Status);
            Assert.Equal(dispatch.RequestId, observation?.RequestId);
            Assert.Equal(expectedSetupDatagram.Length, observation?.SentDatagramBytes);
            Assert.Equal(expectedSetupDatagramSha256, observation?.SentDatagramSha256);
            Assert.Equal("172.18.0.4", observation?.DestinationIp);
            Assert.Equal(10300, observation?.DestinationPort);
            Assert.Equal("simulator:10300", observation?.RemoteEndpoint);
            Assert.Equal(3, observation?.DatagramBytes);
            Assert.Equal(new string('b', 64), observation?.ResponseDatagramSha256);
            Assert.NotNull(observation?.ResponseReceivedAtUtc);

            using var duplicateObservation = await http.GetAsync($"setup/initial-own-half/observation/{dispatch.RequestId}");
            Assert.Equal(HttpStatusCode.OK, duplicateObservation.StatusCode);
            Assert.Equal(observationJson, await duplicateObservation.Content.ReadAsStringAsync());

            using var duplicate = await http.PostAsJsonAsync("setup/initial-own-half", new { blueTeamOnPositiveHalf = true });
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Single(simulator.HalfSetupCalls);

            using var placement = await http.PostAsJsonAsync("setup/place-ball", new { x = 1.5f, y = -0.75f });
            Assert.Equal(HttpStatusCode.OK, placement.StatusCode);
            Assert.Equal([(1.5f, -0.75f)], simulator.Placements);
            using var invalidPlacement = await http.PostAsync("setup/place-ball",
                new StringContent("{\"x\":NaN,\"y\":0}", System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, invalidPlacement.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
        Assert.True(simulator.ObservationCancellationToken.IsCancellationRequested);
    }

    private sealed class RecordingSimulator(int sentDatagramBytes, string sentDatagramSha256) : ISimulatorBallControlClient
    {
        public List<bool> HalfSetupCalls { get; } = [];
        public List<(float X, float Y)> Placements { get; } = [];
        public CancellationToken SendCancellationToken { get; private set; }
        public CancellationToken ObservationCancellationToken { get; private set; }

        public Task SendBallToPositionAsync(float x, float y, CancellationToken cancellationToken)
        {
            Placements.Add((x, y));
            return Task.CompletedTask;
        }

        public Task<SimulatorSetupDispatchReceipt> SendInitialOwnHalfSetupAsync(
            bool blueTeamOnPositiveHalf,
            CancellationToken sendCancellationToken,
            CancellationToken observationCancellationToken)
        {
            SendCancellationToken = sendCancellationToken;
            ObservationCancellationToken = observationCancellationToken;
            HalfSetupCalls.Add(blueTeamOnPositiveHalf);
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new SimulatorSetupDispatchReceipt(
                "test-request", "172.18.0.4", 10300, sentDatagramBytes, sentDatagramSha256, now, now));
        }

        public Task<SimulatorResponseObservation?> CompleteInitialSetupObservationAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<SimulatorResponseObservation?>(new(
                "response_received", "simulator:10300", 3, [], "test observation",
                DestinationIp: "172.18.0.4",
                DestinationPort: 10300,
                SendStartedAtUtc: DateTimeOffset.UtcNow.AddMilliseconds(-10),
                SendCompletedAtUtc: DateTimeOffset.UtcNow.AddMilliseconds(-9),
                ResponseReceivedAtUtc: DateTimeOffset.UtcNow.AddMilliseconds(-8),
                SentDatagramBytes: sentDatagramBytes,
                SentDatagramSha256: sentDatagramSha256,
                ResponseDatagramSha256: new string('b', 64)));
    }
}
