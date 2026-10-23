using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Duck.Testing.AppHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tracker.Tests;

public sealed class RegisteredContainerExecHandoffTests
{
    private const string ResourceId = "duck-event-key-opaque";
    private const string Role = "duck";
    private const string RunId = "run-20261023-01";
    private const string ContainerId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string EngineId = "5bc5ab8a-7340-47ba-8b7c-9f645375a54c";

    [Fact]
    public async Task ChecksExplicitEngineAndOwnedFullIdBeforeStructuredExec()
    {
        var target = ProjectTarget();
        var runner = new FakeDockerCommandRunner(EngineId, Inspect(ContainerId, "running", RunId, Role));

        var accepted = await ContainerIdentityExecHandoff.TryHandoffAsync(
            target, RunId, 1, null, "desktop-linux", EngineId, runner, CancellationToken.None);

        Assert.NotNull(accepted);
        Assert.Equal(1, accepted!.Generation);
        Assert.Equal("boot-fake-01", accepted.BootId);
        Assert.Equal(
            new[]
            {
                new[] { "--context", "desktop-linux", "info", "--format", "{{.ID}}" },
                new[] { "--context", "desktop-linux", "inspect", "--format", "{{json .}}", ContainerId },
            },
            runner.CaptureArguments);
        Assert.Equal(
            new[] { "--context", "desktop-linux", "exec", "-i", ContainerId, "dotnet", "Tracker.RuntimeHost.dll", "--identity-control" },
            runner.RelayArguments);
    }

    [Fact]
    public async Task SafeTraceRecordsOnlyHandoffStagesAndOutcomesWhenEnabled()
    {
        var tracePath = Path.Combine(Path.GetTempPath(), $"duck-handoff-trace-{Guid.NewGuid():N}.jsonl");
        try
        {
            var runner = new FakeDockerCommandRunner(EngineId, Inspect(ContainerId, "running", RunId, Role));
            var trace = new SafeIdentityHandoffTrace(tracePath);

            var handoff = await ContainerIdentityExecHandoff.TryHandoffAsync(
                ProjectTarget(), RunId, 1, null, "desktop-linux", EngineId, runner, CancellationToken.None, trace);

            Assert.NotNull(handoff);
            var lines = await File.ReadAllLinesAsync(tracePath);
            var records = lines.Select(line => System.Text.Json.JsonDocument.Parse(line)).ToArray();
            Assert.Equal(
                new[] { "EngineCheck:Started", "EngineCheck:Accepted", "ContainerInspect:Started", "ContainerInspect:Accepted", "IdentityExec:Started", "IdentityExec:Accepted" },
                records.Select(record => $"{record.RootElement.GetProperty("stage").GetString()}:{record.RootElement.GetProperty("outcome").GetString()}"));
            Assert.All(records, record =>
            {
                Assert.Equal(3, record.RootElement.EnumerateObject().Count());
                Assert.True(record.RootElement.TryGetProperty("timestampUtc", out _));
            });
            var output = string.Join("\n", lines);
            Assert.DoesNotContain(ContainerId, output);
            Assert.DoesNotContain(RunId, output);
            Assert.DoesNotContain("boot-fake-01", output);
            Assert.DoesNotContain("Tracker.RuntimeHost.dll", output);
            Assert.DoesNotContain("nonce", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(tracePath)) File.Delete(tracePath);
        }
    }

    [Fact]
    public async Task RuntimeHostBootChangeRetriesWithTheNextGeneration()
    {
        var target = ProjectTarget();
        var runner = new FakeDockerCommandRunner(
            EngineId,
            Inspect(ContainerId, "running", RunId, Role),
            new ContainerIdentityControlExecResult(false, "boot-new", true),
            new ContainerIdentityControlExecResult(true, "boot-new", false));

        var handoff = await ContainerIdentityExecHandoff.TryHandoffAsync(
            target, RunId, 4, "boot-old", "desktop-linux", EngineId, runner, CancellationToken.None);

        Assert.NotNull(handoff);
        Assert.Equal(5, handoff!.Generation);
        Assert.Equal("boot-new", handoff.BootId);
        Assert.Equal(new long[] { 4, 5 }, runner.RelayIdentities.Select(identity => identity.Generation));
        Assert.Equal(new string?[] { "boot-old", "boot-new" }, runner.ExpectedBootIds);
    }

    [Fact]
    public async Task RejectsOwnerMismatchWithoutStartingExec()
    {
        var target = ProjectTarget();
        var runner = new FakeDockerCommandRunner(EngineId, Inspect(ContainerId, "running", "another-run", Role));

        var accepted = await ContainerIdentityExecHandoff.TryHandoffAsync(
            target, RunId, 1, null, "desktop-linux", EngineId, runner, CancellationToken.None);

        Assert.Null(accepted);
        Assert.Empty(runner.RelayArguments);
    }

    [Fact]
    public async Task RuntimeHostOnlyDoesNotImplyDesktopBridgeOptIn()
    {
        using var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
            [
                "--Testing:RuntimeHostOnly=true",
                $"--Testing:StackOwnership:LockPath={Path.Combine(Path.GetTempPath(), $"duck-handoff-{Guid.NewGuid():N}.lock")}",
            ]);

        var duck = Assert.IsType<ContainerResource>(Assert.Single(appHost.Resources));
        var arguments = await GetRuntimeArgumentsAsync(duck);
        Assert.DoesNotContain("--label", arguments);
        await using var serviceProvider = appHost.Services.BuildServiceProvider();
        Assert.Empty(serviceProvider.GetServices<IHostedService>().OfType<ContainerIdentityHandoffService>());
    }

    [Fact]
    public async Task ExplicitDesktopBridgeOptInEmitsOwnerLabelsAndRegistersProductionHandoffService()
    {
        using var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
            [
                "--Testing:RuntimeHostOnly=true",
                "--Testing:IdentityControl:DesktopBridgeEnabled=true",
                "--Testing:IdentityControl:DockerContext=desktop-linux",
                $"--Testing:IdentityControl:ExpectedEngineId={EngineId}",
                $"--Testing:StackOwnership:LockPath={Path.Combine(Path.GetTempPath(), $"duck-handoff-{Guid.NewGuid():N}.lock")}",
            ]);

        var duck = Assert.IsType<ContainerResource>(Assert.Single(appHost.Resources));
        var environment = await GetResourceEnvironmentAsync(duck);
        var runId = environment["Testing__Diagnostics__RunId"].ToString();
        var arguments = await GetRuntimeArgumentsAsync(duck);
        Assert.Contains("--label", arguments);
        Assert.Contains($"{ContainerIdentityExecHandoff.RunIdLabel}={runId}", arguments);
        Assert.Contains($"{ContainerIdentityExecHandoff.RoleLabel}=duck", arguments);
        await using var serviceProvider = appHost.Services.BuildServiceProvider();
        Assert.Single(serviceProvider.GetServices<IHostedService>().OfType<ContainerIdentityHandoffService>());
    }

    [Fact]
    public async Task SafeTracePathFromConfigurationRecordsServiceStartBeforeNotificationsAndStop()
    {
        var tracePath = Path.Combine(Path.GetTempPath(), $"duck-handoff-lifecycle-{Guid.NewGuid():N}.jsonl");
        using var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
            [
                "--Testing:RuntimeHostOnly=true",
                "--Testing:IdentityControl:DesktopBridgeEnabled=true",
                "--Testing:IdentityControl:DockerContext=desktop-linux",
                $"--Testing:IdentityControl:ExpectedEngineId={EngineId}",
                $"--Testing:IdentityControl:SafeTracePath={tracePath}",
                $"--Testing:StackOwnership:LockPath={Path.Combine(Path.GetTempPath(), $"duck-handoff-{Guid.NewGuid():N}.lock")}",
            ]);

        await using var serviceProvider = appHost.Services.BuildServiceProvider();
        var service = Assert.Single(serviceProvider.GetServices<IHostedService>().OfType<ContainerIdentityHandoffService>());
        Assert.True(service.IsSafeTraceEnabled);

        await service.StartAsync(CancellationToken.None);
        var beforeNotification = await WaitForTraceEntriesAsync(tracePath, 1);
        Assert.Single(beforeNotification);
        Assert.Contains("ServiceStarted", beforeNotification[0], StringComparison.Ordinal);

        await service.StopAsync(CancellationToken.None);
        var afterStop = await WaitForTraceEntriesAsync(tracePath, 2);
        Assert.Equal(2, afterStop.Length);
        Assert.Contains("ServiceStopped", afterStop[1], StringComparison.Ordinal);
        File.Delete(tracePath);
    }

    private static async Task<string[]> WaitForTraceEntriesAsync(string path, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                var lines = await File.ReadAllLinesAsync(path);
                if (lines.Length >= count)
                {
                    return lines;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        return File.Exists(path) ? await File.ReadAllLinesAsync(path) : [];
    }

    private static async Task<List<object>> GetRuntimeArgumentsAsync(ContainerResource resource)
    {
        var arguments = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
        {
            await annotation.Callback(new ContainerRuntimeArgsCallbackContext(arguments, CancellationToken.None));
        }
        return arguments;
    }

    private static async Task<Dictionary<string, object>> GetResourceEnvironmentAsync(IResourceWithEnvironment resource)
    {
        var variables = new Dictionary<string, object>(StringComparer.Ordinal);
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            resource,
            variables,
            CancellationToken.None);
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }
        return variables;
    }

    private static RegisteredContainerRuntimeTarget ProjectTarget()
    {
        var resource = new ContainerResource(Role, "duck-runtime");
        var current = new ResourceEvent(resource, ResourceId, new CustomResourceSnapshot
        {
            ResourceType = "container",
            State = new ResourceStateSnapshot("Running", "fixture"),
            Properties = [new ResourcePropertySnapshot("container.id", ContainerId)],
        });
        var projected = RegisteredContainerEventProjection.TryProject(
            resource, ResourceId, Role, current, current, out var target);
        Assert.True(projected);
        Assert.NotNull(target);
        return target!;
    }

    private static string Inspect(string id, string status, string runId, string role) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            Id = id,
            State = new { Status = status },
            Config = new
            {
                Labels = new Dictionary<string, string>
                {
                    [ContainerIdentityExecHandoff.RunIdLabel] = runId,
                    [ContainerIdentityExecHandoff.RoleLabel] = role,
                },
            },
        });

    private sealed class FakeDockerCommandRunner(
        string engineId,
        string inspection,
        params ContainerIdentityControlExecResult[] relayResults) : IDockerCommandRunner
    {
        private int relayCount;
        public List<string[]> CaptureArguments { get; } = [];
        public List<string> RelayArguments { get; } = [];
        public List<Duck.Testing.Diagnostics.ResourceDiagnosticIdentity> RelayIdentities { get; } = [];
        public List<string?> ExpectedBootIds { get; } = [];

        public Task<string> CaptureAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            CaptureArguments.Add(arguments.ToArray());
            return Task.FromResult(CaptureArguments.Count == 1 ? engineId : inspection);
        }

        public Task<ContainerIdentityControlExecResult> RelayAsync(
            IReadOnlyList<string> arguments,
            Duck.Testing.Diagnostics.ResourceDiagnosticIdentity identity,
            string roleName,
            string? expectedBootId,
            CancellationToken cancellationToken)
        {
            RelayArguments.AddRange(arguments);
            RelayIdentities.Add(identity);
            ExpectedBootIds.Add(expectedBootId);
            var result = relayCount < relayResults.Length
                ? relayResults[relayCount]
                : new ContainerIdentityControlExecResult(true, expectedBootId ?? "boot-fake-01", false);
            relayCount++;
            return Task.FromResult(result);
        }
    }
}
