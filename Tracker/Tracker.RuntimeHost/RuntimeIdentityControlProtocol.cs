using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tracker.RuntimeHost;

/// <summary>container内の診断identity制御に使う固定Unix domain socket。</summary>
internal static class RuntimeIdentityControlSocket
{
    public const string DefaultPath = "/tmp/duck-runtime-identity.sock";
}

internal sealed class RuntimeIdentityControlMessage
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
    public string? Reason { get; init; }
}

/// <summary>identity制御フレームの長さと読み取り時間を制限する。</summary>
internal static class RuntimeIdentityControlFraming
{
    public const int MaximumLineCharacters = 64 * 1024;

    public static async Task<string?> ReadLineAsync(TextReader reader, CancellationToken cancellationToken, TimeSpan timeout)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var builder = new StringBuilder();
        var character = new char[1];
        while (true)
        {
            int count;
            try { count = await reader.ReadAsync(character.AsMemory(), deadline.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("The identity control frame exceeded its read deadline.");
            }

            if (count == 0) return builder.Length == 0 ? null : builder.ToString();
            if (character[0] == '\n')
            {
                if (builder.Length > 0 && builder[^1] == '\r') builder.Length--;
                return builder.ToString();
            }
            if (builder.Length >= MaximumLineCharacters)
                throw new InvalidDataException("The identity control frame exceeded the maximum length.");
            builder.Append(character[0]);
        }
    }
}

/// <summary>常駐RuntimeHostへ同一container内からidentityを渡すcontrol server。</summary>
internal sealed class RuntimeIdentityControlServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly TimeSpan ClientStageTimeout = TimeSpan.FromSeconds(1);
    private readonly string socketPath;
    private readonly RuntimeVisionReceiverDiagnostics diagnostics;
    private readonly string expectedRunId;
    private string? expectedResourceId;
    private readonly string? expectedRole;
    private readonly CancellationTokenSource stop = new();
    private Socket? listener;
    private Task? acceptTask;
    private ResourceDiagnosticIdentity? acceptedIdentity;
    private bool ownsSocketPath;

    public RuntimeIdentityControlServer(string socketPath, RuntimeVisionReceiverDiagnostics diagnostics, string expectedRunId, string? expectedResourceId, string? expectedRole = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRunId);
        if (string.IsNullOrWhiteSpace(expectedResourceId) && string.IsNullOrWhiteSpace(expectedRole))
            throw new ArgumentException("Unbound identity control requires an expected role.", nameof(expectedRole));
        this.socketPath = socketPath;
        this.diagnostics = diagnostics;
        this.expectedRunId = expectedRunId;
        this.expectedResourceId = string.IsNullOrWhiteSpace(expectedResourceId) ? null : expectedResourceId;
        this.expectedRole = string.IsNullOrWhiteSpace(expectedRole) ? null : expectedRole;
        acceptedIdentity = diagnostics.Identity;
        BootId = Guid.NewGuid().ToString("N");
    }

    public string BootId { get; }
    public Task Completion => acceptTask ?? Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(socketPath)!);
        if (File.Exists(socketPath)) throw new IOException("The identity control socket path is already occupied.");
        listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        ownsSocketPath = true;
        listener.Listen(8);
        acceptTask = AcceptLoopAsync(stop.Token);
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        var socket = listener ?? throw new InvalidOperationException("The identity control socket has not started.");
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket client;
            try { client = await socket.AcceptAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (SocketException exception) when (
                cancellationToken.IsCancellationRequested &&
                exception.SocketErrorCode == SocketError.OperationAborted) { break; }
            try
            {
                await HandleClientAsync(client, cancellationToken);
            }
            catch (Exception exception) when (exception is JsonException or IOException or SocketException or ObjectDisposedException or TimeoutException)
            {
                // 1接続の不正な入力や切断で、常駐サーバーの受付を止めない。
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
            string? prepareLine;
            try { prepareLine = await RuntimeIdentityControlRelay.RunWithDeadlineAsync(
                token => RuntimeIdentityControlFraming.ReadLineAsync(reader, token, ClientStageTimeout),
                cancellationToken,
                ClientStageTimeout); }
            catch (InvalidDataException)
            {
                await WriteAsync(writer, new() { Type = "result", Accepted = false, Reason = "frame-too-large" }, cancellationToken);
                return;
            }
            catch (TimeoutException)
            {
                await WriteAsync(writer, new() { Type = "result", Accepted = false, Reason = "prepare-timeout" }, cancellationToken);
                return;
            }
            var prepare = Deserialize(prepareLine);
            var preparedIdentity = ParseIdentity(prepare);
            if (prepare?.Type != "prepare" || preparedIdentity is null || !IsExpectedResource(preparedIdentity, prepare.RoleName))
            {
                await WriteAsync(writer, new() { Type = "result", Accepted = false, Reason = "invalid-prepare" }, cancellationToken);
                return;
            }

            await WriteAsync(writer, new() { Type = "challenge", BootId = BootId }, cancellationToken);
            string? commitLine;
            try { commitLine = await RuntimeIdentityControlRelay.RunWithDeadlineAsync(
                token => RuntimeIdentityControlFraming.ReadLineAsync(reader, token, ClientStageTimeout),
                cancellationToken,
                ClientStageTimeout); }
            catch (InvalidDataException)
            {
                await WriteAsync(writer, new() { Type = "result", Accepted = false, Reason = "frame-too-large" }, cancellationToken);
                return;
            }
            catch (TimeoutException)
            {
                await WriteAsync(writer, new() { Type = "result", Accepted = false, Reason = "commit-timeout" }, cancellationToken);
                return;
            }
            var commit = Deserialize(commitLine);
            var committedIdentity = ParseIdentity(commit);
            var accepted = commit?.Type == "commit" &&
                committedIdentity == preparedIdentity &&
                string.Equals(commit.ExpectedBootId, BootId, StringComparison.Ordinal) &&
                string.Equals(commit.RoleName, prepare.RoleName, StringComparison.Ordinal) &&
                IsExpectedResource(preparedIdentity, commit.RoleName) &&
                TryAcceptIdentity(preparedIdentity);
            await WriteAsync(writer, new() { Type = "result", Accepted = accepted, Reason = accepted ? null : "identity-not-accepted" }, cancellationToken);
        }
    }

    private bool TryAcceptIdentity(ResourceDiagnosticIdentity identity)
    {
        var current = acceptedIdentity;
        if (current == identity) return true;
        if (current is not null &&
            (current.RunId != identity.RunId ||
             current.ResourceId != identity.ResourceId ||
             identity.Generation <= current.Generation))
            return false;

        diagnostics.ReplaceIdentity(identity);
        acceptedIdentity = identity;
        expectedResourceId ??= identity.ResourceId;
        return true;
    }

    private bool IsExpectedResource(ResourceDiagnosticIdentity identity, string? roleName) =>
        string.Equals(identity.RunId, expectedRunId, StringComparison.Ordinal) &&
        (expectedResourceId is null || string.Equals(identity.ResourceId, expectedResourceId, StringComparison.Ordinal)) &&
        (expectedRole is null || string.Equals(roleName, expectedRole, StringComparison.Ordinal));

    private static RuntimeIdentityControlMessage? Deserialize(string? line) =>
        string.IsNullOrWhiteSpace(line) ? null : JsonSerializer.Deserialize<RuntimeIdentityControlMessage>(line, JsonOptions);

    private static ResourceDiagnosticIdentity? ParseIdentity(RuntimeIdentityControlMessage? message)
    {
        if (message is null) return null;
        try { return new(message.RunId!, message.ResourceId!, message.ContainerId!, message.Generation); }
        catch (ArgumentException) { return null; }
    }

    private static Task WriteAsync(StreamWriter writer, RuntimeIdentityControlMessage message, CancellationToken cancellationToken) =>
        RuntimeIdentityControlRelay.RunWithDeadlineAsync(
            token => writer.WriteLineAsync(JsonSerializer.Serialize(message, JsonOptions)).WaitAsync(token),
            cancellationToken,
            ClientStageTimeout);

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        listener?.Dispose();
        Exception? acceptFailure = null;
        try
        {
            if (acceptTask is not null) await acceptTask;
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception exception)
        {
            acceptFailure = exception;
        }
        finally
        {
            try
            {
                if (ownsSocketPath && File.Exists(socketPath))
                {
                    File.Delete(socketPath);
                    ownsSocketPath = false;
                }
            }
            finally { stop.Dispose(); }
        }
        if (acceptFailure is not null) ExceptionDispatchInfo.Capture(acceptFailure).Throw();
    }
}

/// <summary>ContainerMode時に診断identity用のUDSを起動する。</summary>
internal sealed class RuntimeIdentityControlHostedService : BackgroundService
{
    private readonly IConfiguration configuration;
    private readonly RuntimeVisionReceiverDiagnostics diagnostics;
    private readonly ILogger<RuntimeIdentityControlHostedService> logger;
    private readonly string socketPath;
    private readonly Func<RuntimeIdentityControlServer>? serverFactory;
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal string? BootId { get; private set; }

    public RuntimeIdentityControlHostedService(
        IConfiguration configuration,
        RuntimeVisionReceiverDiagnostics diagnostics,
        ILogger<RuntimeIdentityControlHostedService> logger,
        string? socketPath = null,
        Func<RuntimeIdentityControlServer>? serverFactory = null)
    {
        this.configuration = configuration;
        this.diagnostics = diagnostics;
        this.logger = logger;
        this.socketPath = socketPath ?? RuntimeIdentityControlSocket.DefaultPath;
        this.serverFactory = serverFactory;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
        await started.Task.WaitAsync(cancellationToken);
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
            var expectedRole = configuration["Testing:IdentityControl:ExpectedRole"];
            if (string.IsNullOrWhiteSpace(runId) ||
                (string.IsNullOrWhiteSpace(resourceId) && string.IsNullOrWhiteSpace(expectedRole)))
                throw new InvalidOperationException("Identity control requires a run identifier and either a resource identifier or expected role.");

            await using var server = serverFactory?.Invoke() ?? new RuntimeIdentityControlServer(
                socketPath, diagnostics, runId, resourceId, expectedRole);
            await server.StartAsync(stoppingToken);
            BootId = server.BootId;
            started.TrySetResult();
            logger.LogInformation("RuntimeHost identity control socket is ready.");
            try { await server.Completion.WaitAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
        catch (Exception exception)
        {
            started.TrySetException(exception);
            throw;
        }
    }

}

/// <summary>AppHostのexec control processと常駐RuntimeHost間のUDS relay。</summary>
internal static class RuntimeIdentityControlRelay
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(30);

    public static bool IsRelayCommand(string[] args) =>
        args.Length == 1 && string.Equals(args[0], "--identity-control", StringComparison.Ordinal);

    public static async Task<int> RunAsync(TextReader input, TextWriter output, string socketPath, CancellationToken cancellationToken, TimeSpan? operationTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        var timeout = operationTimeout ?? DefaultOperationTimeout;
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(operationTimeout));
        try
        {
            var prepare = await RunWithDeadlineAsync(
                token => RuntimeIdentityControlFraming.ReadLineAsync(input, token, timeout),
                cancellationToken,
                timeout);
            if (string.IsNullOrWhiteSpace(prepare)) return 2;
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await RunWithDeadlineAsync(
                token => socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), token).AsTask(),
                cancellationToken,
                timeout);
            using var stream = new NetworkStream(socket, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
            await RunWithDeadlineAsync(token => writer.WriteLineAsync(prepare).WaitAsync(token), cancellationToken, timeout);
            var challenge = await RunWithDeadlineAsync(
                token => RuntimeIdentityControlFraming.ReadLineAsync(reader, token, timeout),
                cancellationToken,
                timeout);
            if (string.IsNullOrWhiteSpace(challenge)) return 3;
            await RunWithDeadlineAsync(token => output.WriteLineAsync(challenge).WaitAsync(token), cancellationToken, timeout);
            await RunWithDeadlineAsync(token => output.FlushAsync(token), cancellationToken, timeout);
            var commit = await RunWithDeadlineAsync(
                token => RuntimeIdentityControlFraming.ReadLineAsync(input, token, timeout),
                cancellationToken,
                timeout);
            if (string.IsNullOrWhiteSpace(commit)) return 2;
            await RunWithDeadlineAsync(token => writer.WriteLineAsync(commit).WaitAsync(token), cancellationToken, timeout);
            var result = await RunWithDeadlineAsync(
                token => RuntimeIdentityControlFraming.ReadLineAsync(reader, token, timeout),
                cancellationToken,
                timeout);
            if (string.IsNullOrWhiteSpace(result)) return 3;
            await RunWithDeadlineAsync(token => output.WriteLineAsync(result).WaitAsync(token), cancellationToken, timeout);
            await RunWithDeadlineAsync(token => output.FlushAsync(token), cancellationToken, timeout);
            var response = JsonSerializer.Deserialize<RuntimeIdentityControlMessage>(result, JsonOptions);
            return response?.Accepted == true ? 0 : 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return 130; }
        catch (SocketException) { return 1; }
        catch (IOException) { return 1; }
        catch (TimeoutException) { return 2; }
        catch (InvalidDataException) { return 2; }
    }

    /// <summary>取消不能保証しない入出力も期限付きで待ち、呼出し側へ時間内に制御を戻す。</summary>
    internal static async Task RunWithDeadlineAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken, TimeSpan timeout)
    {
        await RunWithDeadlineAsync(async token =>
        {
            await operation(token);
            return true;
        }, cancellationToken, timeout);
    }

    /// <summary>処理の同期的な入口と非協力な非同期taskの双方を有限時間で待つ。</summary>
    internal static async Task<T> RunWithDeadlineAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken, TimeSpan timeout)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        Task<T>? operationTask = null;
        try
        {
            operationTask = Task.Run(() => operation(deadline.Token));
            return await operationTask.WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The identity control relay operation exceeded its deadline.");
        }
        finally
        {
            if (operationTask is { IsCompletedSuccessfully: false })
                ObserveFault(operationTask);
        }
    }

    /// <summary>期限切れ後に完了した処理の例外を観測する。</summary>
    private static void ObserveFault(Task operationTask)
    {
        _ = operationTask.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
