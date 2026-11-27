using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Duck.Testing.Diagnostics;
using Tracker.RuntimeHost;

namespace Tracker.Tests;

public sealed class RuntimeIdentityControlBoundaryTests
{
    private static readonly JsonSerializerOptions MessageJsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Theory]
    [InlineData("prepare")]
    [InlineData("commit")]
    [InlineData("disconnect-after-challenge")]
    public async Task InvalidClientDoesNotStopAcceptLoop(string failurePoint)
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var socketPath = NewSocketPath();
        var server = new RuntimeIdentityControlServer(socketPath, diagnostics, runId, resourceId);
        await server.StartAsync(CancellationToken.None);
        var brokenClient = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await brokenClient.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
        var containerId = new string('a', 64);
        var prepare = JsonSerializer.Serialize(new { type = "prepare", runId, resourceId, containerId, generation = 1 });
        var commit = JsonSerializer.Serialize(new { type = "commit", expectedBootId = server.BootId, runId, resourceId, containerId, generation = 1 });
        if (failurePoint == "prepare")
        {
            await SendLineAsync(brokenClient, "{not-json");
        }
        else
        {
            await SendLineAsync(brokenClient, prepare);
            using var stream = new NetworkStream(brokenClient, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var challenge = await reader.ReadLineAsync(timeout.Token);
            Assert.Equal("challenge", JsonSerializer.Deserialize<RuntimeIdentityControlMessage>(challenge!, MessageJsonOptions)?.Type);
            if (failurePoint == "commit") await SendLineAsync(brokenClient, "{not-json");
            else brokenClient.LingerState = new LingerOption(true, 0);
        }
        if (failurePoint != "disconnect-after-challenge") brokenClient.Shutdown(SocketShutdown.Send);
        brokenClient.Dispose();

        using var relayTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var handoffExit = await ApplyIdentityAsync(socketPath, server.BootId, runId, resourceId, new string('b', 64), 1, relayTimeout.Token);
        Exception? cleanupError = null;
        try { await server.DisposeAsync(); }
        catch (Exception exception) { cleanupError = exception; }

        Assert.Equal(0, handoffExit);
        Assert.Null(cleanupError);
    }

    [Fact]
    public async Task FailedServerDoesNotDeleteAnotherOwnersSocketPath()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var socketPath = NewSocketPath();
        await using var owner = new RuntimeIdentityControlServer(socketPath, diagnostics, runId, resourceId);
        await owner.StartAsync(CancellationToken.None);

        var rejected = new RuntimeIdentityControlServer(socketPath, diagnostics, runId, resourceId);
        await Assert.ThrowsAsync<IOException>(() => rejected.StartAsync(CancellationToken.None));
        await rejected.DisposeAsync();
        var ownerPathStillExists = File.Exists(socketPath);
        var handoffExit = ownerPathStillExists
            ? await ApplyIdentityAsync(socketPath, owner.BootId, runId, resourceId, new string('b', 64), 1, CancellationToken.None)
            : -1;

        Assert.True(ownerPathStillExists, "A server that failed to bind must not delete the existing owner's socket path.");
        Assert.Equal(0, handoffExit);
    }

    [Fact]
    public async Task IdleClientHasDeadlineAndDoesNotStarveLaterHandoff()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var socketPath = NewSocketPath();
        await using var server = new RuntimeIdentityControlServer(socketPath, diagnostics, runId, resourceId);
        await server.StartAsync(CancellationToken.None);

        using var idleClient = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await idleClient.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
        using var relayTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var laterHandoff = ApplyIdentityAsync(socketPath, server.BootId, runId, resourceId, new string('c', 64), 1, relayTimeout.Token);
        var completedBeforeDeadline = await Task.WhenAny(laterHandoff, Task.Delay(TimeSpan.FromSeconds(3))) == laterHandoff;
        if (!completedBeforeDeadline)
        {
            var invalidPrepare = JsonSerializer.Serialize(new { type = "prepare", runId, resourceId = "other", containerId = new string('f', 64), generation = 1 });
            try { await SendLineAsync(idleClient, invalidPrepare); }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException or IOException) { }
        }
        var handoffExit = await laterHandoff.WaitAsync(TimeSpan.FromSeconds(8));

        Assert.True(completedBeforeDeadline, "An idle client must be bounded so later control handoffs can proceed.");
        Assert.Equal(0, handoffExit);
    }

    [Fact]
    public async Task OversizedFrameIsRejectedAndLaterHandoffCompletes()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var socketPath = NewSocketPath();
        await using var server = new RuntimeIdentityControlServer(socketPath, diagnostics, runId, resourceId);
        await server.StartAsync(CancellationToken.None);

        var containerId = new string('d', 64);
        var prepare = JsonSerializer.Serialize(new
        {
            type = "prepare", runId, resourceId, containerId, generation = 1,
            padding = new string('x', 64 * 1024),
        });
        var commit = JsonSerializer.Serialize(new
        {
            type = "commit", expectedBootId = server.BootId, runId, resourceId, containerId, generation = 1,
        });
        using var oversizedClient = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await oversizedClient.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
        await SendLineAsync(oversizedClient, prepare);
        await SendLineAsync(oversizedClient, commit);
        using var stream = new NetworkStream(oversizedClient, ownsSocket: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
        using var responseTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var firstLine = await reader.ReadLineAsync(responseTimeout.Token);
        var firstResponse = firstLine is null ? null : JsonSerializer.Deserialize<RuntimeIdentityControlMessage>(firstLine, MessageJsonOptions);
        var rejectedBeforeChallenge = firstResponse?.Type == "result" && !firstResponse.Accepted;
        if (firstResponse?.Type == "challenge")
        {
            var acceptedLine = await reader.ReadLineAsync(responseTimeout.Token);
            var accepted = acceptedLine is null ? null : JsonSerializer.Deserialize<RuntimeIdentityControlMessage>(acceptedLine, MessageJsonOptions);
            _ = accepted;
        }
        oversizedClient.Dispose();
        var handoffExit = await ApplyIdentityAsync(socketPath, server.BootId, runId, resourceId, new string('e', 64), 2, CancellationToken.None);

        Assert.True(rejectedBeforeChallenge, "A control frame above the fixed size limit must be rejected before identity acceptance.");
        Assert.Equal(0, handoffExit);
    }


    [Fact]
    public async Task FailedServerDoesNotDeleteAnotherOwnersRegularFile()
    {
        var path = NewSocketPath();
        var expected = Encoding.UTF8.GetBytes("preserve-this-owner-file");
        await File.WriteAllBytesAsync(path, expected);
        var server = new RuntimeIdentityControlServer(path, new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics()), Guid.NewGuid().ToString("N"), "duck");
        await Assert.ThrowsAsync<IOException>(() => server.StartAsync(CancellationToken.None));
        await server.DisposeAsync();
        var stillExists = File.Exists(path);
        var actual = stillExists ? await File.ReadAllBytesAsync(path) : Array.Empty<byte>();

        Assert.True(stillExists, "A failed server must not remove a pre-existing regular file.");
        Assert.Equal(expected, actual);
        File.Delete(path);
    }

    [Fact]
    public async Task ServerRemovesTheSocketPathItSuccessfullyBound()
    {
        var path = NewSocketPath();
        var server = new RuntimeIdentityControlServer(path, new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics()), Guid.NewGuid().ToString("N"), "duck");
        await server.StartAsync(CancellationToken.None);
        Assert.True(File.Exists(path));
        await server.DisposeAsync();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task IdleCommitHasDeadlineAndDoesNotStarveLaterHandoff()
    {
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var diagnostics = new RuntimeVisionReceiverDiagnostics(new ServiceReceiveDiagnostics());
        var path = NewSocketPath();
        await using var server = new RuntimeIdentityControlServer(path, diagnostics, runId, resourceId);
        await server.StartAsync(CancellationToken.None);
        var containerId = new string('c', 64);
        var prepare = JsonSerializer.Serialize(new { type = "prepare", runId, resourceId, containerId, generation = 1 });
        var commit = JsonSerializer.Serialize(new { type = "commit", expectedBootId = server.BootId, runId, resourceId, containerId, generation = 1 });
        using var idleClient = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await idleClient.ConnectAsync(new UnixDomainSocketEndPoint(path));
        await SendLineAsync(idleClient, prepare);
        using var idleStream = new NetworkStream(idleClient, ownsSocket: false);
        using var idleReader = new StreamReader(idleStream, Encoding.UTF8, false, 1024, leaveOpen: true);
        using var idleDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Assert.Equal("challenge", JsonSerializer.Deserialize<RuntimeIdentityControlMessage>((await idleReader.ReadLineAsync(idleDeadline.Token)!)!, MessageJsonOptions)?.Type);

        using var relayTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var laterHandoff = ApplyIdentityAsync(path, server.BootId, runId, resourceId, containerId, 1, relayTimeout.Token);
        var completedBeforeDeadline = await Task.WhenAny(laterHandoff, Task.Delay(TimeSpan.FromSeconds(3))) == laterHandoff;
        if (!completedBeforeDeadline)
        {
            try
            {
                await SendLineAsync(idleClient, commit);
                using var resultTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await idleReader.ReadLineAsync(resultTimeout.Token);
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException or IOException or OperationCanceledException) { }
        }
        var handoffExit = await laterHandoff.WaitAsync(TimeSpan.FromSeconds(8));

        Assert.True(completedBeforeDeadline, "An incomplete commit must expire so the next control handoff can proceed.");
        Assert.Equal(0, handoffExit);
    }

    [Fact]
    public async Task RelayRejectsOversizedInputFrame()
    {
        var path = NewSocketPath();
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);
        using var acceptTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var acceptTask = listener.AcceptAsync(acceptTimeout.Token).AsTask();
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var containerId = new string('d', 64);
        var oversizedPrepare = JsonSerializer.Serialize(new { type = "prepare", runId, resourceId, containerId, generation = 1, padding = new string('x', 64 * 1024) });
        var commit = JsonSerializer.Serialize(new { type = "commit", expectedBootId = Guid.NewGuid().ToString("N"), runId, resourceId, containerId, generation = 1 });
        using var input = new StringReader(oversizedPrepare + "\n" + commit + "\n");
        using var output = new StringWriter();
        using var relayTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var relay = RuntimeIdentityControlRelay.RunAsync(input, output, path, relayTimeout.Token);
        if (await Task.WhenAny(acceptTask, relay) == acceptTask)
        {
            using var peer = await acceptTask;
            using var stream = new NetworkStream(peer, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
            await reader.ReadLineAsync(acceptTimeout.Token);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new RuntimeIdentityControlMessage { Type = "challenge", BootId = Guid.NewGuid().ToString("N") }));
            await reader.ReadLineAsync(acceptTimeout.Token);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new RuntimeIdentityControlMessage { Type = "result", Accepted = true }));
        }
        var relayExit = await relay.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(0, relayExit);
    }

    [Fact]
    public async Task RelayCancelsWhenOutputWriterDoesNotComplete()
    {
        var path = NewSocketPath();
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);
        var runId = Guid.NewGuid().ToString("N");
        const string resourceId = "duck";
        var containerId = new string('e', 64);
        var prepare = JsonSerializer.Serialize(new { type = "prepare", runId, resourceId, containerId, generation = 1 });
        var commit = JsonSerializer.Serialize(new { type = "commit", expectedBootId = Guid.NewGuid().ToString("N"), runId, resourceId, containerId, generation = 1 });
        using var input = new StringReader(prepare + "\n" + commit + "\n");
        var output = new GatedTextWriter();
        using var relayCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        using var serverTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = Task.Run(async () =>
        {
            using var peer = await listener.AcceptAsync(serverTimeout.Token);
            using var stream = new NetworkStream(peer, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
            await reader.ReadLineAsync(serverTimeout.Token);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new RuntimeIdentityControlMessage { Type = "challenge", BootId = Guid.NewGuid().ToString("N") }));
            var commitLine = await reader.ReadLineAsync(serverTimeout.Token);
            if (commitLine is not null) await writer.WriteLineAsync(JsonSerializer.Serialize(new RuntimeIdentityControlMessage { Type = "result", Accepted = true }));
            return commitLine;
        });
        var relay = RuntimeIdentityControlRelay.RunAsync(input, output, path, relayCancellation.Token);
        var completedOnCancellation = await Task.WhenAny(relay, Task.Delay(TimeSpan.FromSeconds(1))) == relay;
        output.Release();
        var relayExit = await relay.WaitAsync(TimeSpan.FromSeconds(5));
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(completedOnCancellation, "A blocked response writer must observe cancellation.");
        Assert.Equal(130, relayExit);
    }

    private sealed class GatedTextWriter : StringWriter
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Task WriteLineAsync(string? value) => release.Task;
        public void Release() => release.TrySetResult();
    }

    private static string NewSocketPath() => Path.Combine(Path.GetTempPath(), $"duck-identity-boundary-{Guid.NewGuid():N}.sock");

    private static async Task<int> ApplyIdentityAsync(string socketPath, string bootId, string runId, string resourceId, string containerId, long generation, CancellationToken cancellationToken)
    {
        var prepare = JsonSerializer.Serialize(new { type = "prepare", runId, resourceId, containerId, generation });
        var commit = JsonSerializer.Serialize(new { type = "commit", expectedBootId = bootId, runId, resourceId, containerId, generation });
        using var input = new StringReader(prepare + "\n" + commit + "\n");
        using var output = new StringWriter();
        return await RuntimeIdentityControlRelay.RunAsync(input, output, socketPath, cancellationToken);
    }

    private static async Task SendLineAsync(Socket socket, string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        var offset = 0;
        while (offset < bytes.Length)
        {
            var sent = await socket.SendAsync(bytes.AsMemory(offset), SocketFlags.None);
            if (sent == 0) throw new IOException("The control socket closed before the complete frame was sent.");
            offset += sent;
        }
    }
}
