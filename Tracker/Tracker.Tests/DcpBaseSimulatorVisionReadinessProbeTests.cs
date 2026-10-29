using System.Net;
using System.Text;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tracker.RuntimeHost;

namespace Tracker.Tests;

public sealed class DcpBaseSimulatorVisionReadinessProbeTests
{
    private const string RunId = "base-run";
    private const string StackId = "base-stack";
    private const string NetworkName = "aspire-default-network";
    private const string SimulatorResourceKey = "simulator-runtime-key";
    private const string DuckResourceKey = "duck-runtime-key";
    private const string SimulatorId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string NextSimulatorId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string DuckId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task RequiresFreshDecodedProgressForEachCurrentSimulatorGeneration()
    {
        var fixture = new ProbeFixture();
        fixture.AssertOwnedCurrentPair();

        var baseline = await fixture.CheckHealthAsync();
        Assert.Equal("http://127.0.0.1:39108/health/vision", fixture.LastRequestUri?.AbsoluteUri);
        Assert.Equal(HealthStatus.Unhealthy, baseline.Status);
        Assert.False(fixture.Registry.Get(fixture.Simulator)!.IsReady);

        fixture.DecodedCount = 2;
        var progressed = await fixture.CheckHealthAsync();
        Assert.True(progressed.Status == HealthStatus.Healthy, progressed.Description);
        Assert.True(fixture.Registry.Get(fixture.Simulator)!.IsReady);

        fixture.ReplaceSimulatorGeneration(NextSimulatorId);
        fixture.DecodedCount = 2;
        var newGenerationBaseline = await fixture.CheckHealthAsync();
        Assert.Equal(HealthStatus.Unhealthy, newGenerationBaseline.Status);
        Assert.False(fixture.Registry.Get(fixture.Simulator)!.IsReady);

        fixture.DecodedCount = 3;
        var newGenerationProgress = await fixture.CheckHealthAsync();
        Assert.Equal(HealthStatus.Healthy, newGenerationProgress.Status);
        Assert.True(fixture.Registry.Get(fixture.Simulator)!.IsReady);
    }

    [Fact]
    public async Task RejectsLateVisionDiagnosticsForAFormerDuckIdentity()
    {
        var fixture = new ProbeFixture();
        fixture.AssertOwnedCurrentPair();
        fixture.DiagnosticIdentity = new ResourceDiagnosticIdentity(
            RunId, "duck-runtime-old", DuckId, generation: 1);

        var result = await fixture.CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.False(fixture.Registry.Get(fixture.Simulator)!.IsReady);
        Assert.Equal(1, fixture.HttpRequests);
    }

    [Fact]
    public async Task KeepsReadinessWhenTheCurrentSimulatorKeyIsReobserved()
    {
        var fixture = new ProbeFixture();
        await fixture.CheckHealthAsync();
        fixture.DecodedCount = 2;
        Assert.Equal(HealthStatus.Healthy, (await fixture.CheckHealthAsync()).Status);
        var before = fixture.Registry.Get(fixture.Simulator)!;

        fixture.Registry.ObserveCurrent(new(SimulatorResourceKey, "simulator", SimulatorId), "Running");

        var after = fixture.Registry.Get(fixture.Simulator)!;
        Assert.Equal(before.Generation, after.Generation);
        Assert.True(after.IsReady);
        Assert.Equal(HealthStatus.Healthy, (await fixture.CheckHealthAsync()).Status);
    }

    [Fact]
    public async Task RejectsAResponseWhenDuckChangesDuringTheHealthRequest()
    {
        var fixture = new ProbeFixture();
        fixture.Handler.BeforeResponse = () => fixture.ReplaceDuckGeneration(
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd");

        var result = await fixture.CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(1, fixture.HttpRequests);
        Assert.False(fixture.Registry.Get(fixture.Simulator)!.IsReady);
        Assert.False(fixture.Registry.Get(fixture.Duck)!.OwnershipVerified);
    }

    [Fact]
    public async Task RejectsVisionEvidenceThatExpiresWhileTheResponseIsRead()
    {
        var fixture = new ProbeFixture();
        await fixture.CheckHealthAsync();
        fixture.DecodedCount = 2;
        fixture.Handler.DecodeAge = TimeSpan.FromMilliseconds(4900);
        fixture.Handler.ResponseDelay = TimeSpan.FromMilliseconds(300);

        var result = await fixture.CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.False(fixture.Registry.Get(fixture.Simulator)!.IsReady);
    }

    [Fact]
    public async Task StaleVisionAndHttpFailureClearPreviouslyReadyState()
    {
        var fixture = new ProbeFixture();
        await fixture.CheckHealthAsync();
        fixture.DecodedCount = 2;
        Assert.Equal(HealthStatus.Healthy, (await fixture.CheckHealthAsync()).Status);

        fixture.Handler.DecodeAge = TimeSpan.FromSeconds(6);
        Assert.Equal(HealthStatus.Unhealthy, (await fixture.CheckHealthAsync()).Status);
        Assert.False(fixture.Registry.Get(fixture.Simulator)!.IsReady);

        fixture.Handler.DecodeAge = TimeSpan.Zero;
        fixture.DecodedCount = 3;
        Assert.Equal(HealthStatus.Healthy, (await fixture.CheckHealthAsync()).Status);
        fixture.Handler.StatusCode = HttpStatusCode.ServiceUnavailable;
        Assert.Equal(HealthStatus.Unhealthy, (await fixture.CheckHealthAsync()).Status);
        Assert.False(fixture.Registry.Get(fixture.Simulator)!.IsReady);
    }

    [Fact]
    public async Task CancellationClearsSimulatorReadinessBeforeItPropagates()
    {
        var fixture = new ProbeFixture();
        await fixture.CheckHealthAsync();
        fixture.DecodedCount = 2;
        Assert.Equal(HealthStatus.Healthy, (await fixture.CheckHealthAsync()).Status);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.CheckHealthAsync(cancellation.Token));

        Assert.False(fixture.Registry.Get(fixture.Simulator)!.IsReady);
    }

    private sealed class ProbeFixture
    {
        private readonly HttpClient httpClient;
        private DcpBaseResourceIdentityCandidate simulatorCandidate = null!;
        private DcpBaseResourceIdentityCandidate duckCandidate = null!;

        internal ProbeFixture()
        {
            Simulator = new ContainerResource("simulator", "example/simulator:fixed");
            Duck = new ContainerResource("duck", "example/duck:fixed");
            Registry = new DcpBaseResourceReadinessRegistry(
                [(Simulator, "simulator", RunId), (Duck, "duck", RunId)], StackId);
            HandoffStates = new ContainerIdentityHandoffStateStore();
            Handler = new VisionHandler(this);
            httpClient = new HttpClient(Handler) { Timeout = Timeout.InfiniteTimeSpan };
            SetCandidates(SimulatorId);
            Probe = new DcpBaseSimulatorVisionReadinessProbe(
                Registry,
                Simulator,
                Duck,
                _ => Task.FromResult(new Uri("http://127.0.0.1:39108/", UriKind.Absolute)),
                HandoffStates,
                httpClient);
        }

        internal ContainerResource Simulator { get; }
        internal ContainerResource Duck { get; }
        internal DcpBaseResourceReadinessRegistry Registry { get; }
        internal ContainerIdentityHandoffStateStore HandoffStates { get; }
        internal VisionHandler Handler { get; }
        internal DcpBaseSimulatorVisionReadinessProbe Probe { get; }
        internal long DecodedCount { get; set; } = 1;
        internal int HttpRequests { get; set; }
        internal Uri? LastRequestUri { get; set; }
        internal ResourceDiagnosticIdentity DiagnosticIdentity { get; set; } = new(RunId, DuckResourceKey, DuckId, 1);
        internal void ReplaceDuckGeneration(string containerId)
        {
            Registry.ObserveCurrent(new(DuckResourceKey, "duck", containerId), "Running");
            var current = Registry.GetCandidates().Single(candidate => ReferenceEquals(candidate.Resource, Duck));
            HandoffStates.SetCurrent(Duck, new ResourceDiagnosticIdentity(
                RunId, current.ResourceId, containerId, current.Generation));
        }

        internal Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default) =>
            new DcpBaseResourceReadinessHealthCheck(Registry, Simulator, Probe)
                .CheckHealthAsync(null!, cancellationToken);

        internal void ReplaceSimulatorGeneration(string containerId)
        {
            SetCandidates(containerId);
        }

        internal void AssertOwnedCurrentPair()
        {
            Assert.True(IsOwned(Registry.GetCandidates().Single(candidate => ReferenceEquals(candidate.Resource, Simulator))));
            Assert.True(IsOwned(Registry.GetCandidates().Single(candidate => ReferenceEquals(candidate.Resource, Duck))));
            var duck = Registry.GetCandidates().Single(candidate => ReferenceEquals(candidate.Resource, Duck));
            Assert.Equal(new ResourceDiagnosticIdentity(RunId, DuckResourceKey, DuckId, duck.Generation), HandoffStates.GetCurrent(Duck));
        }

        private void SetCandidates(string simulatorId)
        {
            Registry.ObserveCurrent(new(SimulatorResourceKey, "simulator", simulatorId), "Running");
            Registry.ObserveCurrent(new(DuckResourceKey, "duck", DuckId), "Running");
            simulatorCandidate = Registry.GetCandidates().Single(candidate => ReferenceEquals(candidate.Resource, Simulator));
            duckCandidate = Registry.GetCandidates().Single(candidate => ReferenceEquals(candidate.Resource, Duck));
            var proofs = new[]
            {
                new DcpBaseResourceOwnershipProof(Simulator, simulatorCandidate.ResourceId, simulatorCandidate.ContainerId!,
                    simulatorCandidate.Generation, "engine", NetworkName),
                new DcpBaseResourceOwnershipProof(Duck, duckCandidate.ResourceId, duckCandidate.ContainerId!,
                    duckCandidate.Generation, "engine", NetworkName),
            };
            Assert.True(Registry.TryMarkIdentityVerified(proofs, sharedStandardDcpNetworkVerified: true));
            HandoffStates.SetCurrent(Duck, new ResourceDiagnosticIdentity(
                RunId, duckCandidate.ResourceId, DuckId, duckCandidate.Generation));
        }

        private static bool IsOwned(DcpBaseResourceIdentityCandidate candidate) =>
            candidate.State == "Running" && candidate.ContainerId is not null &&
            candidate.IdentityVerified && candidate.NetworkVerified && candidate.OwnershipVerified;

        internal sealed class VisionHandler(ProbeFixture fixture) : HttpMessageHandler
        {
            internal HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
            internal TimeSpan? DecodeAge { get; set; } = TimeSpan.Zero;
            internal TimeSpan ResponseDelay { get; set; }
            internal Action? BeforeResponse { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                fixture.HttpRequests++;
                fixture.LastRequestUri = request.RequestUri;
                BeforeResponse?.Invoke();
                if (ResponseDelay > TimeSpan.Zero)
                {
                    await Task.Delay(ResponseDelay, cancellationToken);
                }
                var diagnostics = new ServiceReceiveSnapshot(
                    fixture.DiagnosticIdentity,
                    "vision",
                    1,
                    "source",
                    null,
                    null,
                    null,
                    fixture.DecodedCount,
                    0,
                    DateTimeOffset.UtcNow,
                    DecodeAge,
                    DiagnosticDecodeStatus.Succeeded,
                    true,
                    "ready");
                var response = new RuntimeVisionHealthResponse(fixture.DecodedCount, true, diagnostics);
                var content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json");
                return new HttpResponseMessage(StatusCode) { Content = content };
            }
        }
    }
}
