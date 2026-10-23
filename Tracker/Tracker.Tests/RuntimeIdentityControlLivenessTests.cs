using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Tracker.RuntimeHost;

namespace Tracker.Tests;

public sealed class RuntimeIdentityControlLivenessTests
{
    [Fact]
    public async Task FatalAcceptLoopIsObservedAndOwnedSocketIsCleaned()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var socketPath = Path.Combine(Path.GetTempPath(), $"duck-identity-fatal-{Guid.NewGuid():N}.sock");
        var server = new RuntimeIdentityControlServer(socketPath, diagnostics, runId, resourceId);
        await server.StartAsync(CancellationToken.None);
        var fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var listener = (Socket)typeof(RuntimeIdentityControlServer).GetField("listener", fields)!.GetValue(server)!;
        var acceptTask = (Task)typeof(RuntimeIdentityControlServer).GetField("acceptTask", fields)!.GetValue(server)!;
        listener.Dispose();

        var acceptCompleted = await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(2))) == acceptTask;
        Exception? disposeException = null;
        try { await server.DisposeAsync(); }
        catch (Exception exception) { disposeException = exception; }
        var socketPathRemains = File.Exists(socketPath);
        if (socketPathRemains) File.Delete(socketPath);

        Assert.True(acceptCompleted, "The fatal accept task must finish and be observable.");
        Assert.True(acceptTask.IsFaulted);
        Assert.IsType<SocketException>(disposeException);
        Assert.False(socketPathRemains, "Cleanup must run even after the accept task faults.");
    }

    [Fact]
    public async Task HostedServiceObservesFatalAcceptAndCleansSocket()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var socketPath = Path.Combine(Path.GetTempPath(), $"duck-identity-hosted-{Guid.NewGuid():N}.sock");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Testing:IdentityControl:Enabled"] = "true",
            ["Testing:Diagnostics:RunId"] = runId,
            ["Testing:Diagnostics:ResourceId"] = resourceId,
        }).Build();
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        RuntimeIdentityControlServer? server = null;
        var service = new RuntimeIdentityControlHostedService(
            configuration,
            diagnostics,
            NullLogger<RuntimeIdentityControlHostedService>.Instance,
            socketPath,
            () => server = new RuntimeIdentityControlServer(socketPath, diagnostics, runId, resourceId));

        await service.StartAsync(CancellationToken.None);
        var activeServer = Assert.IsType<RuntimeIdentityControlServer>(server);
        var fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var listener = (Socket)typeof(RuntimeIdentityControlServer).GetField("listener", fields)!.GetValue(activeServer)!;
        listener.Dispose();
        var executeTask = service.ExecuteTask ?? throw new InvalidOperationException("The hosted service task was not created.");
        var observedWithoutStop = await Task.WhenAny(executeTask, Task.Delay(TimeSpan.FromSeconds(1))) == executeTask;

        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await service.StopAsync(stopTimeout.Token); }
        catch (Exception) { }
        var socketPathRemains = File.Exists(socketPath);
        if (socketPathRemains) File.Delete(socketPath);

        Assert.True(observedWithoutStop, "The hosted service must observe a fatal accept-loop completion without requiring host shutdown.");
        Assert.True(executeTask.IsFaulted, "The fatal accept-loop error must remain visible to the host.");
        Assert.False(socketPathRemains, "The hosted service must clean its owned socket after a fatal accept-loop completion.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RelayBoundsBlockedOutputWithoutExternalCancellation(bool blockFlush)
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var identity = new ResourceDiagnosticIdentity(runId, resourceId, new string('f', 64), 1);
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var socketPath = Path.Combine(Path.GetTempPath(), $"duck-identity-output-{Guid.NewGuid():N}.sock");
        await using var server = new RuntimeIdentityControlServer(socketPath, diagnostics, runId, resourceId);
        await server.StartAsync(CancellationToken.None);

        var prepare = JsonSerializer.Serialize(new
        {
            type = "prepare", runId, resourceId, containerId = identity.ContainerId, generation = identity.Generation,
        });
        using var input = new StringReader(prepare + "\n");
        using var cancellation = new CancellationTokenSource();
        var output = new BlockingTextWriter(blockFlush);
        var relayTask = RuntimeIdentityControlRelay.RunAsync(input, output, socketPath, cancellation.Token, TimeSpan.FromMilliseconds(150));
        var completedWithoutCancellation = await Task.WhenAny(relayTask, Task.Delay(TimeSpan.FromSeconds(3))) == relayTask;
        if (!completedWithoutCancellation) cancellation.Cancel();
        var firstExit = await relayTask.WaitAsync(TimeSpan.FromSeconds(2));

        var commit = JsonSerializer.Serialize(new
        {
            type = "commit", expectedBootId = server.BootId, runId, resourceId,
            containerId = identity.ContainerId, generation = identity.Generation,
        });
        using var nextInput = new StringReader(prepare + "\n" + commit + "\n");
        using var nextOutput = new StringWriter();
        var nextExit = await RuntimeIdentityControlRelay.RunAsync(nextInput, nextOutput, socketPath, CancellationToken.None);

        Assert.True(completedWithoutCancellation, "A blocked output write or flush must have an internal deadline.");
        Assert.NotEqual(0, firstExit);
        Assert.NotEqual(130, firstExit);
        Assert.Equal(0, nextExit);
    }

    private sealed class BlockingTextWriter(bool blockFlush) : TextWriter
    {
        private readonly TaskCompletionSource never = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Encoding Encoding => Encoding.UTF8;
        public override Task WriteLineAsync(string? value) => blockFlush ? Task.CompletedTask : never.Task;
        public override Task FlushAsync() => blockFlush ? never.Task : Task.CompletedTask;
        public override Task FlushAsync(CancellationToken cancellationToken) => blockFlush ? never.Task.WaitAsync(cancellationToken) : FlushAsync();
    }
}
