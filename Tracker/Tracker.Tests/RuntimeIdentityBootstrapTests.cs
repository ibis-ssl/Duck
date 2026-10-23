using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Duck.Testing.AppHost;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Configuration;
using Tracker.RuntimeHost;

namespace Tracker.Tests;

public sealed class RuntimeIdentityBootstrapTests
{
    private const string RunId = "run-06a3-fixture";
    private const string ResourceKey = "duck-abcdwxyz";
    private const string RoleName = "duck";
    private const string ContainerId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task FirstHandoffBindsTheVerifiedResourceKeyAndRejectsAnotherKey()
    {
        var resource = new ContainerResource(RoleName, "duck-runtime");
        var observed = Event(resource, ResourceKey, ContainerId);
        var current = Event(resource, ResourceKey, ContainerId);

        // fixtureの役割名・一意キー・実行状態・完全IDが期待どおりか先に検査する。
        Assert.Same(resource, observed.Resource);
        Assert.Equal(ResourceKey, observed.ResourceId);
        Assert.NotEqual(RoleName, observed.ResourceId);
        Assert.Equal(RoleName, observed.Resource.Name);
        Assert.Equal("Running", observed.Snapshot.State.Text);
        Assert.Equal(ContainerId, Assert.Single(observed.Snapshot.Properties).Value);
        Assert.True(RegisteredContainerEventProjection.TryProject(
            resource, ResourceKey, RoleName, observed, current, out var target));
        Assert.Equal(ResourceKey, target!.ResourceId);
        Assert.Equal(RoleName, target.RoleName);
        Assert.Equal(ContainerId, target.ContainerId);

        var receiveDiagnostics = new ServiceReceiveDiagnostics();
        var receiverDiagnostics = new RuntimeVisionReceiverDiagnostics(receiveDiagnostics);
        var socketPath = Path.Combine(Path.GetTempPath(), $"duck-bootstrap-{Guid.NewGuid():N}.sock");

        await using var server = new RuntimeIdentityControlServer(
            socketPath,
            receiverDiagnostics,
            RunId,
            expectedResourceId: null,
            expectedRole: RoleName);
        await server.StartAsync(CancellationToken.None);

        Assert.Equal(503, RuntimeVisionDiagnosticsHttpService.GetHealth(receiverDiagnostics, ContainerConfiguration()).StatusCode);

        var identity = new ResourceDiagnosticIdentity(RunId, ResourceKey, ContainerId, 1);
        var accepted = await ApplyAsync(server, socketPath, identity, RoleName);
        Assert.Equal(0, accepted.ExitCode);
        Assert.True(accepted.Accepted);
        Assert.Equal(identity, receiveDiagnostics.Identity);
        Assert.Equal(503, RuntimeVisionDiagnosticsHttpService.GetHealth(receiverDiagnostics, ContainerConfiguration()).StatusCode);

        RecordValidDetection(receiveDiagnostics, receiverDiagnostics);
        RecordValidDetection(receiveDiagnostics, receiverDiagnostics);
        Assert.Equal(200, RuntimeVisionDiagnosticsHttpService.GetHealth(receiverDiagnostics, ContainerConfiguration()).StatusCode);

        var duplicate = await ApplyAsync(server, socketPath, identity, RoleName);
        Assert.Equal(0, duplicate.ExitCode);
        Assert.True(duplicate.Accepted);
        Assert.Equal(200, RuntimeVisionDiagnosticsHttpService.GetHealth(receiverDiagnostics, ContainerConfiguration()).StatusCode);

        var otherResource = new ResourceDiagnosticIdentity(RunId, "duck-foreign-key", ContainerId, 2);
        var rejected = await ApplyAsync(server, socketPath, otherResource, RoleName);
        Assert.NotEqual(0, rejected.ExitCode);
        Assert.False(rejected.Accepted);
        Assert.Equal(identity, receiveDiagnostics.Identity);
        Assert.Equal(200, RuntimeVisionDiagnosticsHttpService.GetHealth(receiverDiagnostics, ContainerConfiguration()).StatusCode);
    }

    [Fact]
    public async Task RuntimeHostOnlyOptInRegistersDuckAndEnablesUnboundIdentityControl()
    {
        using var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
            ["--Testing:RuntimeHostOnly=true", CreateIsolatedOwnershipLockArgument()]);

        var duck = Assert.IsType<ContainerResource>(Assert.Single(appHost.Resources));
        Assert.Equal("duck", duck.Name);
        Assert.Single(duck.Annotations.OfType<DockerfileBuildAnnotation>());

        var environment = await GetResourceEnvironmentAsync(duck);
        Assert.Equal("true", environment["Testing__IdentityControl__Enabled"].ToString());
        Assert.False(string.IsNullOrWhiteSpace(environment["Testing__Diagnostics__RunId"].ToString()));
        Assert.Equal("duck", environment["Testing__IdentityControl__ExpectedRole"].ToString());
        Assert.False(environment.ContainsKey("Testing__Diagnostics__ResourceId"));
    }

    private static ResourceEvent Event(IResource resource, string resourceId, string containerId) =>
        new(resource, resourceId, new CustomResourceSnapshot
        {
            ResourceType = "container",
            State = new ResourceStateSnapshot("Running", "fixture"),
            Properties = [new ResourcePropertySnapshot("container.id", containerId)],
        });

    private static async Task<(int ExitCode, bool Accepted)> ApplyAsync(
        RuntimeIdentityControlServer server,
        string socketPath,
        ResourceDiagnosticIdentity identity,
        string roleName)
    {
        var prepare = JsonSerializer.Serialize(new
        {
            type = "prepare",
            roleName,
            runId = identity.RunId,
            resourceId = identity.ResourceId,
            containerId = identity.ContainerId,
            generation = identity.Generation,
        });
        var commit = JsonSerializer.Serialize(new
        {
            type = "commit",
            roleName,
            expectedBootId = server.BootId,
            runId = identity.RunId,
            resourceId = identity.ResourceId,
            containerId = identity.ContainerId,
            generation = identity.Generation,
        });
        using var input = new StringReader(prepare + "\n" + commit + "\n");
        using var output = new StringWriter();
        var exitCode = await RuntimeIdentityControlRelay.RunAsync(input, output, socketPath, CancellationToken.None);
        var resultLine = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1];
        using var result = JsonDocument.Parse(resultLine);
        return (exitCode, result.RootElement.GetProperty("accepted").GetBoolean());
    }

    private static IConfiguration ContainerConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Testing:ContainerMode"] = "true" })
        .Build();

    private static void RecordValidDetection(ServiceReceiveDiagnostics service, RuntimeVisionReceiverDiagnostics receiver)
    {
        Assert.True(service.RecordSuccess(receiver.BeginReceive()));
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

    private static string CreateIsolatedOwnershipLockArgument() =>
        $"--Testing:StackOwnership:LockPath={Path.Combine(Path.GetTempPath(), $"duck-aspire-bootstrap-{Guid.NewGuid():N}.lock")}";
}
