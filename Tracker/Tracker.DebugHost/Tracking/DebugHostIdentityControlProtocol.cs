using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tracker.DebugHost.Vision;

namespace Tracker.DebugHost.Tracking;

internal sealed class DebugHostIdentityControlMessage
{
    public string? Type { get; init; }
    public string? BootId { get; init; }
    public string? ExpectedBootId { get; init; }
    public string? RunId { get; init; }
    public string? ResourceId { get; init; }
    public string? RoleName { get; init; }
    public string? ContainerId { get; init; }
    public long Generation { get; init; }
    public bool Accepted { get; init; }
}

/// <summary>Uses the AppHost prepare/challenge/commit identity-control channel for DebugHost receiver diagnostics.</summary>
internal static class DebugHostIdentityControlProtocol
{
    internal const string SocketPath = "/tmp/duck-debughost-identity.sock";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan StageDeadline = TimeSpan.FromSeconds(5);
    private const int MaximumFrameCharacters = 64 * 1024;

    internal static bool IsRelayCommand(string[] args) =>
        args.Length == 1 && string.Equals(args[0], "--identity-control", StringComparison.Ordinal);

    internal static async Task<int> RelayAsync(TextReader input, TextWriter output, string socketPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var prepare = await ReadFrameAsync(input, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(prepare)) return 2;
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(StageDeadline);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), deadline.Token).ConfigureAwait(false);
            }
            using var stream = new NetworkStream(socket, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(prepare.AsMemory(), cancellationToken).ConfigureAwait(false);
            var challenge = await ReadFrameAsync(reader, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(challenge)) return 3;
            await output.WriteLineAsync(challenge.AsMemory(), cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            var commit = await ReadFrameAsync(input, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(commit)) return 2;
            await writer.WriteLineAsync(commit.AsMemory(), cancellationToken).ConfigureAwait(false);
            var result = await ReadFrameAsync(reader, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result)) return 3;
            await output.WriteLineAsync(result.AsMemory(), cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<DebugHostIdentityControlMessage>(result, JsonOptions)?.Accepted == true ? 0 : 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }
        catch (Exception exception) when (exception is IOException or SocketException or JsonException or
            TimeoutException or InvalidDataException or OperationCanceledException)
        {
            return 1;
        }
    }

    internal static async Task<string?> ReadFrameAsync(TextReader reader, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(StageDeadline);
        var buffer = new char[1];
        var value = new StringBuilder();
        try
        {
            while (true)
            {
                var count = await reader.ReadAsync(buffer.AsMemory(), deadline.Token).ConfigureAwait(false);
                if (count == 0) return value.Length == 0 ? null : value.ToString();
                if (buffer[0] == '\n')
                {
                    if (value.Length > 0 && value[^1] == '\r') value.Length--;
                    return value.ToString();
                }
                if (value.Length >= MaximumFrameCharacters)
                    throw new InvalidDataException("Identity-control frame exceeded its maximum length.");
                value.Append(buffer[0]);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Identity-control frame exceeded its read deadline.");
        }
    }

    internal static Task WriteFrameAsync(TextWriter writer, DebugHostIdentityControlMessage message,
        CancellationToken cancellationToken) =>
        WriteFrameCoreAsync(writer, message, cancellationToken);

    private static async Task WriteFrameCoreAsync(TextWriter writer, DebugHostIdentityControlMessage message,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(StageDeadline);
        await writer.WriteLineAsync(JsonSerializer.Serialize(message, JsonOptions)).WaitAsync(deadline.Token)
            .ConfigureAwait(false);
        await writer.FlushAsync(deadline.Token).ConfigureAwait(false);
    }
}

internal sealed class DebugHostIdentityControlServer(
    ServiceReceiveDiagnostics diagnostics,
    DiagnosticTrackerDeserializer trackerDeserializer,
    VisionReceiverService visionReceiver,
    string expectedRunId,
    string? expectedResourceId,
    string expectedRole,
    string? socketPath = null,
    bool applyTrackerIdentity = true) : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string path = socketPath ?? DebugHostIdentityControlProtocol.SocketPath;
    private readonly string? boundResourceId = string.IsNullOrWhiteSpace(expectedResourceId) ? null : expectedResourceId;
    private readonly CancellationTokenSource stop = new();
    private readonly string bootId = Guid.NewGuid().ToString("N");
    private Socket? listener;
    private Task? acceptTask;
    private bool ownsSocketPath;

    public string BootId => bootId;
    public Task Completion => acceptTask ?? Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) throw new IOException("DebugHost identity-control socket path is already occupied.");
        listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        ownsSocketPath = true;
        listener.Listen(8);
        acceptTask = AcceptLoopAsync(stop.Token);
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        var socket = listener ?? throw new InvalidOperationException("DebugHost identity-control socket is not started.");
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket client;
            try { client = await socket.AcceptAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (SocketException exception) when (cancellationToken.IsCancellationRequested &&
                exception.SocketErrorCode == SocketError.OperationAborted) { break; }
            try { await HandleClientAsync(client, cancellationToken).ConfigureAwait(false); }
            catch (Exception exception) when (exception is JsonException or IOException or SocketException or
                ObjectDisposedException or TimeoutException or InvalidDataException)
            {
                // A malformed or abandoned exec session must not stop the long-lived DebugHost listener.
            }
        }
    }

    private async Task HandleClientAsync(Socket client, CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = new NetworkStream(client, ownsSocket: false))
        using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true })
        {
            var prepare = Deserialize(await DebugHostIdentityControlProtocol.ReadFrameAsync(reader, cancellationToken)
                .ConfigureAwait(false));
            var preparedIdentity = ParseIdentity(prepare);
            if (prepare?.Type != "prepare" || preparedIdentity is null || !IsExpected(preparedIdentity, prepare.RoleName))
            {
                await DebugHostIdentityControlProtocol.WriteFrameAsync(writer,
                    new() { Type = "result", Accepted = false }, cancellationToken).ConfigureAwait(false);
                return;
            }

            await DebugHostIdentityControlProtocol.WriteFrameAsync(writer,
                new() { Type = "challenge", BootId = bootId }, cancellationToken).ConfigureAwait(false);
            var commit = Deserialize(await DebugHostIdentityControlProtocol.ReadFrameAsync(reader, cancellationToken)
                .ConfigureAwait(false));
            var committedIdentity = ParseIdentity(commit);
            var accepted = commit?.Type == "commit" && committedIdentity == preparedIdentity &&
                string.Equals(commit.ExpectedBootId, bootId, StringComparison.Ordinal) &&
                string.Equals(commit.RoleName, prepare.RoleName, StringComparison.Ordinal) &&
                IsExpected(preparedIdentity, commit.RoleName) && TryAcceptIdentity(preparedIdentity);
            await DebugHostIdentityControlProtocol.WriteFrameAsync(writer,
                new() { Type = "result", Accepted = accepted }, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool TryAcceptIdentity(ResourceDiagnosticIdentity identity)
    {
        var current = diagnostics.Identity;
        if (current == identity) return true;
        if (current is not null && (current.RunId != identity.RunId ||
            current.ResourceId != identity.ResourceId || identity.Generation <= current.Generation))
            return false;

        visionReceiver.ApplyIdentity(identity);
        if (applyTrackerIdentity)
            trackerDeserializer.ApplyIdentityEpoch(diagnostics.BeginEpoch("tracker"));
        return true;
    }

    private bool IsExpected(ResourceDiagnosticIdentity identity, string? roleName) =>
        string.Equals(identity.RunId, expectedRunId, StringComparison.Ordinal) &&
        (boundResourceId is null || string.Equals(identity.ResourceId, boundResourceId, StringComparison.Ordinal)) &&
        string.Equals(roleName, expectedRole, StringComparison.Ordinal);

    private static DebugHostIdentityControlMessage? Deserialize(string? line) =>
        string.IsNullOrWhiteSpace(line) ? null : JsonSerializer.Deserialize<DebugHostIdentityControlMessage>(line, JsonOptions);

    private static ResourceDiagnosticIdentity? ParseIdentity(DebugHostIdentityControlMessage? message)
    {
        if (message is null) return null;
        try { return new(message.RunId!, message.ResourceId!, message.ContainerId!, message.Generation); }
        catch (ArgumentException) { return null; }
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        listener?.Dispose();
        try
        {
            if (acceptTask is not null) await acceptTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally
        {
            try
            {
                if (ownsSocketPath && File.Exists(path)) File.Delete(path);
                ownsSocketPath = false;
            }
            finally { stop.Dispose(); }
        }
    }
}

internal sealed class DebugHostIdentityControlHostedService(
    IConfiguration configuration,
    ServiceReceiveDiagnostics diagnostics,
    DiagnosticTrackerDeserializer trackerDeserializer,
    VisionReceiverService visionReceiver,
    ILogger<DebugHostIdentityControlHostedService> logger,
    string? socketPath = null) : BackgroundService
{
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DebugHostIdentityControlServer? server;
    internal string? BootId { get; private set; }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
        await started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!configuration.GetValue<bool>("Testing:IdentityControl:Enabled"))
            {
                started.TrySetResult();
                return;
            }
            var runId = configuration["Testing:Diagnostics:RunId"];
            var resourceId = configuration["Testing:Diagnostics:ResourceId"];
            var role = configuration["Testing:IdentityControl:ExpectedRole"];
            if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(role))
                throw new InvalidOperationException("DebugHost identity control requires a run ID and expected role.");

            server = new DebugHostIdentityControlServer(
                diagnostics, trackerDeserializer, visionReceiver, runId, resourceId, role, socketPath,
                applyTrackerIdentity: configuration.GetValue<bool>("Tracker:Receive:Enabled"));
            await server.StartAsync(stoppingToken).ConfigureAwait(false);
            BootId = server.BootId;
            started.TrySetResult();
            logger.LogInformation("DebugHost identity-control socket is ready.");
            await server.Completion.WaitAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            started.TrySetCanceled(stoppingToken);
        }
        catch (Exception exception)
        {
            started.TrySetException(exception);
            throw;
        }
        finally
        {
            if (server is not null)
            {
                await server.DisposeAsync().ConfigureAwait(false);
                server = null;
            }
        }
    }
}
