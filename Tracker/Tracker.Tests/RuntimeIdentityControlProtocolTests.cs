using System.Text.Json;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Tracker.RuntimeHost;

namespace Tracker.Tests;

public sealed class RuntimeIdentityControlProtocolTests
{
    [Fact]
    public async Task SameContainerControlSocketAcceptsIdentityAndResetsOldReadiness()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var oldIdentity = new ResourceDiagnosticIdentity(runId, resourceId, new string('a', 64), 1);
        var newIdentity = new ResourceDiagnosticIdentity(runId, resourceId, new string('b', 64), 2);
        var serviceDiagnostics = new ServiceReceiveDiagnostics(oldIdentity);
        var receiverDiagnostics = new RuntimeVisionReceiverDiagnostics(serviceDiagnostics);
        RecordValidDetection(serviceDiagnostics, receiverDiagnostics);
        RecordValidDetection(serviceDiagnostics, receiverDiagnostics);
        Assert.Equal(200, RuntimeVisionDiagnosticsHttpService.GetHealth(receiverDiagnostics, ContainerConfiguration()).StatusCode);
        var inFlightBeforeHandoff = receiverDiagnostics.BeginReceive();

        var socketPath = Path.Combine(Path.GetTempPath(), $"duck-identity-{Guid.NewGuid():N}.sock");
        await using var controlServer = new RuntimeIdentityControlServer(
            socketPath,
            receiverDiagnostics,
            runId,
            resourceId);
        await controlServer.StartAsync(CancellationToken.None);

        var prepare = JsonSerializer.Serialize(new
        {
            type = "prepare",
            runId = newIdentity.RunId,
            resourceId = newIdentity.ResourceId,
            containerId = newIdentity.ContainerId,
            generation = newIdentity.Generation,
        });
        var commit = JsonSerializer.Serialize(new
        {
            type = "commit",
            expectedBootId = controlServer.BootId,
            runId = newIdentity.RunId,
            resourceId = newIdentity.ResourceId,
            containerId = newIdentity.ContainerId,
            generation = newIdentity.Generation,
        });
        using var input = new StringReader(prepare + "\n" + commit + "\n");
        using var output = new StringWriter();

        var exitCode = await RuntimeIdentityControlRelay.RunAsync(
            input,
            output,
            socketPath,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        using var responseDocument = JsonDocument.Parse(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1]);
        Assert.True(responseDocument.RootElement.GetProperty("accepted").GetBoolean());
        var afterHandoff = Assert.Single(serviceDiagnostics.GetSnapshots("vision"));
        Assert.Equal(newIdentity, afterHandoff.Identity);
        Assert.False(afterHandoff.IsReady);
        Assert.Equal(0, afterHandoff.SuccessfullyDecodedCount);
        Assert.False(serviceDiagnostics.RecordSuccess(inFlightBeforeHandoff));
        Assert.Equal(503, RuntimeVisionDiagnosticsHttpService.GetHealth(receiverDiagnostics, ContainerConfiguration()).StatusCode);

        RecordValidDetection(serviceDiagnostics, receiverDiagnostics);
        Assert.Equal(503, RuntimeVisionDiagnosticsHttpService.GetHealth(receiverDiagnostics, ContainerConfiguration()).StatusCode);
        RecordValidDetection(serviceDiagnostics, receiverDiagnostics);
        Assert.Equal(200, RuntimeVisionDiagnosticsHttpService.GetHealth(receiverDiagnostics, ContainerConfiguration()).StatusCode);
    }

    [Fact]
    public async Task ControlSocketRejectsCommitWithDifferentBootId()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var identity = new ResourceDiagnosticIdentity(runId, resourceId, new string('c', 64), 1);
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var socketPath = Path.Combine(Path.GetTempPath(), $"duck-identity-{Guid.NewGuid():N}.sock");
        await using var controlServer = new RuntimeIdentityControlServer(socketPath, diagnostics, runId, resourceId);
        await controlServer.StartAsync(CancellationToken.None);
        var prepare = JsonSerializer.Serialize(new
        {
            type = "prepare", runId, resourceId, containerId = identity.ContainerId, generation = identity.Generation,
        });
        var commit = JsonSerializer.Serialize(new
        {
            type = "commit", expectedBootId = Guid.NewGuid().ToString("N"), runId, resourceId,
            containerId = identity.ContainerId, generation = identity.Generation,
        });
        using var input = new StringReader(prepare + "\n" + commit + "\n");
        using var output = new StringWriter();

        var exitCode = await RuntimeIdentityControlRelay.RunAsync(input, output, socketPath, CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.Null(diagnostics.Snapshot.Identity);
    }

    [Fact]
    public async Task ControlSocketRejectsWrongOwnerStaleGenerationAndDelayedCommit()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var original = new ResourceDiagnosticIdentity(runId, resourceId, new string('a', 64), 4);
        var foreignRunId = Guid.NewGuid().ToString("N");
        var cases = new[]
        {
            (Prepare: new ResourceDiagnosticIdentity(foreignRunId, resourceId, new string('b', 64), 5),
                Commit: new ResourceDiagnosticIdentity(foreignRunId, resourceId, new string('b', 64), 5)),
            (Prepare: new ResourceDiagnosticIdentity(runId, "other", new string('b', 64), 5),
                Commit: new ResourceDiagnosticIdentity(runId, "other", new string('b', 64), 5)),
            (Prepare: new ResourceDiagnosticIdentity(runId, resourceId, new string('b', 64), 3),
                Commit: new ResourceDiagnosticIdentity(runId, resourceId, new string('b', 64), 3)),
            (Prepare: new ResourceDiagnosticIdentity(runId, resourceId, new string('b', 64), 4),
                Commit: new ResourceDiagnosticIdentity(runId, resourceId, new string('b', 64), 4)),
            (Prepare: new ResourceDiagnosticIdentity(runId, resourceId, new string('b', 64), 5),
                Commit: new ResourceDiagnosticIdentity(runId, resourceId, new string('c', 64), 5)),
        };

        foreach (var testCase in cases)
        {
            var serviceDiagnostics = new ServiceReceiveDiagnostics(original);
            var receiverDiagnostics = new RuntimeVisionReceiverDiagnostics(serviceDiagnostics);
            var socketPath = Path.Combine(Path.GetTempPath(), $"duck-identity-{Guid.NewGuid():N}.sock");
            await using var controlServer = new RuntimeIdentityControlServer(socketPath, receiverDiagnostics, runId, resourceId);
            await controlServer.StartAsync(CancellationToken.None);
            var prepare = JsonSerializer.Serialize(new
            {
                type = "prepare", runId = testCase.Prepare.RunId, resourceId = testCase.Prepare.ResourceId,
                containerId = testCase.Prepare.ContainerId, generation = testCase.Prepare.Generation,
            });
            var commit = JsonSerializer.Serialize(new
            {
                type = "commit", expectedBootId = controlServer.BootId, runId = testCase.Commit.RunId,
                resourceId = testCase.Commit.ResourceId, containerId = testCase.Commit.ContainerId,
                generation = testCase.Commit.Generation,
            });
            using var input = new StringReader(prepare + "\n" + commit + "\n");
            using var output = new StringWriter();

            var exitCode = await RuntimeIdentityControlRelay.RunAsync(input, output, socketPath, CancellationToken.None);

            Assert.NotEqual(0, exitCode);
            Assert.Equal(original, serviceDiagnostics.Identity);
        }
    }

    [Fact]
    public async Task RepeatingAcceptedIdentityDoesNotResetReadiness()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var identity = new ResourceDiagnosticIdentity(runId, resourceId, new string('d', 64), 1);
        var serviceDiagnostics = new ServiceReceiveDiagnostics();
        var receiverDiagnostics = new RuntimeVisionReceiverDiagnostics(serviceDiagnostics);
        var socketPath = Path.Combine(Path.GetTempPath(), $"duck-identity-{Guid.NewGuid():N}.sock");
        await using var controlServer = new RuntimeIdentityControlServer(socketPath, receiverDiagnostics, runId, resourceId);
        await controlServer.StartAsync(CancellationToken.None);

        async Task<int> ApplyIdentityAsync()
        {
            var prepare = JsonSerializer.Serialize(new
            {
                type = "prepare", runId, resourceId, containerId = identity.ContainerId, generation = identity.Generation,
            });
            var commit = JsonSerializer.Serialize(new
            {
                type = "commit", expectedBootId = controlServer.BootId, runId, resourceId,
                containerId = identity.ContainerId, generation = identity.Generation,
            });
            using var input = new StringReader(prepare + "\n" + commit + "\n");
            using var output = new StringWriter();
            return await RuntimeIdentityControlRelay.RunAsync(input, output, socketPath, CancellationToken.None);
        }

        Assert.Equal(0, await ApplyIdentityAsync());
        RecordValidDetection(serviceDiagnostics, receiverDiagnostics);
        RecordValidDetection(serviceDiagnostics, receiverDiagnostics);
        var readySnapshot = Assert.Single(serviceDiagnostics.GetSnapshots("vision"));
        Assert.True(readySnapshot.IsReady);

        Assert.Equal(0, await ApplyIdentityAsync());

        var duplicateSnapshot = Assert.Single(serviceDiagnostics.GetSnapshots("vision"));
        Assert.Equal(identity, duplicateSnapshot.Identity);
        Assert.Equal(readySnapshot.SuccessfullyDecodedCount, duplicateSnapshot.SuccessfullyDecodedCount);
        Assert.True(duplicateSnapshot.IsReady);
    }

    [Fact]
    public async Task HostedControlServiceStartsSocketAndAcceptsIdentityHandoff()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var identity = new ResourceDiagnosticIdentity(runId, resourceId, new string('e', 64), 1);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Testing:IdentityControl:Enabled"] = "true",
            ["Testing:Diagnostics:RunId"] = runId,
            ["Testing:Diagnostics:ResourceId"] = resourceId,
        }).Build();
        var serviceDiagnostics = new ServiceReceiveDiagnostics();
        var receiverDiagnostics = new RuntimeVisionReceiverDiagnostics(serviceDiagnostics);
        var socketPath = Path.Combine(Path.GetTempPath(), $"duck-identity-{Guid.NewGuid():N}.sock");
        using var service = new RuntimeIdentityControlHostedService(
            configuration, receiverDiagnostics, NullLogger<RuntimeIdentityControlHostedService>.Instance, socketPath);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var prepare = JsonSerializer.Serialize(new
            {
                type = "prepare", runId, resourceId, containerId = identity.ContainerId, generation = identity.Generation,
            });
            var commit = JsonSerializer.Serialize(new
            {
                type = "commit", expectedBootId = service.BootId,
                runId, resourceId, containerId = identity.ContainerId, generation = identity.Generation,
            });
            using var input = new StringReader(prepare + "\n" + commit + "\n");
            using var output = new StringWriter();
            var relayExit = await RuntimeIdentityControlRelay.RunAsync(input, output, socketPath, CancellationToken.None);

            Assert.Equal(0, relayExit);
            Assert.Equal(identity, serviceDiagnostics.Identity);
            Assert.Equal(503, RuntimeVisionDiagnosticsHttpService.GetHealth(receiverDiagnostics, ContainerConfiguration()).StatusCode);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static IConfiguration ContainerConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Testing:ContainerMode"] = "true" })
        .Build();

    private static void RecordValidDetection(
        ServiceReceiveDiagnostics serviceDiagnostics,
        RuntimeVisionReceiverDiagnostics receiverDiagnostics)
    {
        serviceDiagnostics.RecordSuccess(receiverDiagnostics.BeginReceive());
    }
}
