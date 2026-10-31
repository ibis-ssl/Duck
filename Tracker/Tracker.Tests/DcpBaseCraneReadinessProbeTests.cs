using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Tracker.Tests;

public sealed class DcpBaseCraneReadinessProbeTests
{
    private const string RunId = "crane-run";
    private const string StackId = "crane-stack";
    private const string NetworkName = "aspire-default-network";
    private const string ContainerId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string NextContainerId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task RegisteredConsumerRequiresOwnershipAndUsesTheExistingCraneDiagnostic()
    {
        var fixture = new Fixture();

        var result = await fixture.CheckAsync();

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True(fixture.Registry.Get(fixture.Crane)!.IsReady);
        Assert.Equal(
            ["--context", "desktop-linux", .. DockerServiceReadiness.CreateCraneReadinessProbeArguments(ContainerId, "crane")],
            fixture.Runner.Calls.Single());
    }

    [Fact]
    public async Task ProbeFailureAndCancellationClearPreviouslyReadyState()
    {
        var fixture = new Fixture();
        Assert.Equal(HealthStatus.Healthy, (await fixture.CheckAsync()).Status);

        fixture.Runner.ExitCode = 1;
        Assert.Equal(HealthStatus.Unhealthy, (await fixture.CheckAsync()).Status);
        Assert.False(fixture.Registry.Get(fixture.Crane)!.IsReady);

        fixture.Runner.ExitCode = 0;
        Assert.Equal(HealthStatus.Healthy, (await fixture.CheckAsync()).Status);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.CheckAsync(cancellation.Token));
        Assert.False(fixture.Registry.Get(fixture.Crane)!.IsReady);
    }

    [Fact]
    public async Task LateSuccessfulProbeCannotReadyAReplacementGeneration()
    {
        var fixture = new Fixture();
        fixture.Runner.BeforeResponse = () =>
            fixture.Registry.ObserveCurrent(new("crane-next-key", "crane", NextContainerId), "Running");

        var result = await fixture.CheckAsync();

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        var snapshot = Assert.IsType<DcpBaseResourceReadinessSnapshot>(fixture.Registry.Get(fixture.Crane));
        Assert.False(snapshot.IsReady);
        Assert.Equal("Unknown", snapshot.State);
        Assert.Null(snapshot.ContainerId);
    }

    [Fact]
    public async Task CancellationAfterCommandCompletionCannotPublishReady()
    {
        var fixture = new Fixture();
        Assert.Equal(HealthStatus.Healthy, (await fixture.CheckAsync()).Status);

        var response = new TaskCompletionSource<DockerStdioCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Runner.ResponseTask = response.Task;
        using var cancellation = new CancellationTokenSource();
        var check = fixture.CheckAsync(cancellation.Token);
        await fixture.Runner.CaptureStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        response.SetResult(new(
            0,
            "DUCK_CRANE_ROS_SETUP_OK\n/session_controller\n",
            string.Empty));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check);
        Assert.False(fixture.Registry.Get(fixture.Crane)!.IsReady);
    }

    [Fact]
    public async Task SuccessfulCommandCompletedAfterProbeBudgetCannotPublishReady()
    {
        var fixture = new Fixture();
        var response = new TaskCompletionSource<DockerStdioCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Runner.ResponseTask = response.Task;

        var check = fixture.CheckAsync();
        await fixture.Runner.CaptureStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Task.Delay(TimeSpan.FromSeconds(12.2));
        response.SetResult(new(
            0,
            "DUCK_CRANE_ROS_SETUP_OK\n/session_controller\n",
            string.Empty));

        var result = await check.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.False(fixture.Registry.Get(fixture.Crane)!.IsReady);
    }

    [Fact]
    public async Task MissingOwnershipDoesNotInvokeCraneExec()
    {
        var fixture = new Fixture(markOwned: false);

        var result = await fixture.CheckAsync();

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Empty(fixture.Runner.Calls);
        Assert.False(fixture.Registry.Get(fixture.Crane)!.IsReady);
    }

    private sealed class Fixture
    {
        internal Fixture(bool markOwned = true)
        {
            Crane = new ContainerResource("crane", "example/crane:fixed");
            Registry = new DcpBaseResourceReadinessRegistry([(Crane, "crane", RunId)], StackId);
            Registry.ObserveCurrent(new("crane-resource-key", "crane", ContainerId), "Running");
            if (markOwned)
            {
                var candidate = Registry.GetCandidates().Single();
                Assert.True(Registry.TryMarkIdentityVerified(
                    [new DcpBaseResourceOwnershipProof(
                        Crane, candidate.ResourceId, candidate.ContainerId!, candidate.Generation,
                        "expected-engine", NetworkName)],
                    sharedStandardDcpNetworkVerified: true));
            }

            Runner = new FakeDockerRunner();
            var probe = new DcpBaseCraneReadinessProbe(Registry, Crane, Runner, "desktop-linux", "crane");
            HealthCheck = new DcpBaseResourceReadinessHealthCheck(Registry, Crane, craneReadinessProbe: probe);
        }

        internal ContainerResource Crane { get; }
        internal DcpBaseResourceReadinessRegistry Registry { get; }
        internal FakeDockerRunner Runner { get; }
        internal DcpBaseResourceReadinessHealthCheck HealthCheck { get; }

        internal Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken = default) =>
            HealthCheck.CheckHealthAsync(null!, cancellationToken);
    }

    private sealed class FakeDockerRunner : IDockerStdioCommandRunner
    {
        internal List<IReadOnlyList<string>> Calls { get; } = [];
        internal int ExitCode { get; set; }
        internal Action? BeforeResponse { get; set; }
        internal Task<DockerStdioCommandResult>? ResponseTask { get; set; }
        internal TaskCompletionSource CaptureStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<DockerStdioCommandResult> CaptureBoundedAsync(
            IReadOnlyList<string> arguments,
            int maximumOutputCharacters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(arguments.ToArray());
            CaptureStarted.TrySetResult();
            BeforeResponse?.Invoke();
            if (ResponseTask is not null) return ResponseTask;
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return Task.FromResult(new DockerStdioCommandResult(
                ExitCode,
                "DUCK_CRANE_ROS_SETUP_OK\n/session_controller\n",
                string.Empty));
        }

        public Task<string> CaptureAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ContainerIdentityControlExecResult> RelayAsync(
            IReadOnlyList<string> arguments,
            Duck.Testing.Diagnostics.ResourceDiagnosticIdentity identity,
            string roleName,
            string? expectedBootId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DockerStdioCommandResult> ExecuteWithStdioAsync(
            IReadOnlyList<string> arguments,
            string standardInput,
            int maximumOutputCharacters,
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
