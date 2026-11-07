using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tracker.Tests;

public sealed class ContainerIdentityHandoffLifecycleTests
{
    private const string RunId = "run-lifecycle-order";
    private const string Role = "duck";
    private const string ResourceId = "duck-lifecycle-event-key";
    private const string ContainerId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string EngineId = "engine-lifecycle-order";

    [Fact]
    public async Task HandoffRunsDuringStartingAsyncWhileDcpStartWaitsForResources()
    {
        using var lifetime = new TestApplicationLifetime();
        using var notifications = new ResourceNotificationService(
            NullLogger<ResourceNotificationService>.Instance, lifetime);
        var resource = new ContainerResource(Role, "duck-runtime");
        var dcp = new BlockingDcpHostedService();
        var runner = new CapturingDockerRunner(dcp.StartEntered.Task);
        using var handoff = new ContainerIdentityHandoffService(
            notifications, resource, Role, RunId, "desktop-linux", EngineId,
            runner, NullLogger<ContainerIdentityHandoffService>.Instance);
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IHostedService>(dcp);
                services.AddSingleton<IHostedService>(handoff);
            })
            .Build();

        Task? startup = null;
        using var startupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await notifications.PublishUpdateAsync(resource, ResourceId, _ => new CustomResourceSnapshot
            {
                ResourceType = "container",
                State = new ResourceStateSnapshot("Running", "fixture"),
                Properties = [new ResourcePropertySnapshot("container.id", ContainerId)],
            });

            startup = host.StartAsync(startupTimeout.Token);
            await dcp.StartEntered.Task.WaitAsync(startupTimeout.Token);
            await runner.HandoffCalled.Task.WaitAsync(startupTimeout.Token);
            Assert.False(dcp.IsReleased);
            Assert.False(startup.IsCompleted);
            Assert.Equal(ResourceId, runner.LastIdentity?.ResourceId);
        }
        finally
        {
            dcp.Release();
            if (startup is not null)
            {
                await startup.WaitAsync(TimeSpan.FromSeconds(5));
            }
            await host.StopAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class BlockingDcpHostedService : IHostedService
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StartEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsReleased => release.Task.IsCompleted;
        public void Release() => release.TrySetResult();

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            StartEntered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Release();
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingDockerRunner : IDockerCommandRunner
    {
        private readonly Task dcpStartEntered;

        public CapturingDockerRunner(Task dcpStartEntered) => this.dcpStartEntered = dcpStartEntered;

        public TaskCompletionSource HandoffCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ResourceDiagnosticIdentity? LastIdentity { get; private set; }

        public Task<string> CaptureAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
            Task.FromResult(arguments.Contains("info") ? EngineId : JsonSerializer.Serialize(new
            {
                Id = ContainerId,
                State = new { Status = "running" },
                Config = new { Labels = new Dictionary<string, string>
                {
                    [ContainerIdentityExecHandoff.RunIdLabel] = RunId,
                    [ContainerIdentityExecHandoff.RoleLabel] = Role,
                } },
            }));

        public async Task<ContainerIdentityControlExecResult> RelayAsync(
            IReadOnlyList<string> arguments,
            ResourceDiagnosticIdentity identity,
            string roleName,
            string? expectedBootId,
            CancellationToken cancellationToken)
        {
            await dcpStartEntered.WaitAsync(cancellationToken);
            LastIdentity = identity;
            HandoffCalled.TrySetResult();
            return new ContainerIdentityControlExecResult(true, "boot-lifecycle", false);
        }
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource started = new();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();
        public CancellationToken ApplicationStarted => started.Token;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => stopped.Token;
        public void StopApplication() => stopping.Cancel();
        public void Dispose() { started.Dispose(); stopping.Dispose(); stopped.Dispose(); }
    }
}
