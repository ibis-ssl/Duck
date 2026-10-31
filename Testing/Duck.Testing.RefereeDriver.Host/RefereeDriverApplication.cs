using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Duck.Testing.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Duck.Testing.RefereeDriver.Hosting;

/// <summary>明示設定を既存の受信・観測処理へ結び付ける、標準ホストの薄い入口。</summary>
internal static class RefereeDriverApplication
{
    /// <summary>明示有効化時だけ構成を検査し、単一受信・API観測・内部診断を登録する。</summary>
    internal static WebApplication Build(WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        if (!bool.TryParse(configuration["RefereeDriver:Enabled"], out var enabled) || !enabled)
            return builder.Build();

        var container = bool.TryParse(configuration["Testing:ContainerMode"], out var selected) && selected;
        var endpoint = ReadEndpoint(configuration["RefereeDriver:GameControllerUri"], container,
            configuration["Testing:Diagnostics:ResourceId"]);
        var interfaceAddress = configuration["RefereeDriver:InterfaceAddress"];
        var receiverOptions = new RefereeUdpReceiverOptions(InterfaceAddress:
            string.IsNullOrWhiteSpace(interfaceAddress) ? null : IPAddress.Parse(interfaceAddress));
        var identity = ServiceReceiveDiagnostics.ParseIdentity(
            configuration["Testing:Diagnostics:RunId"],
            configuration["Testing:Diagnostics:ResourceId"],
            configuration["Testing:Diagnostics:ContainerId"],
            configuration["Testing:Diagnostics:Generation"]);

        builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.TryAddSingleton(new RefereeDriverTransportFactories(
            options =>
            {
                var receiver = RefereeUdpReceiverFactory.Create(options);
                return new(receiver, receiver);
            },
            ConnectWebSocketAsync));
        builder.Services.AddSingleton(provider =>
            new ServiceReceiveDiagnostics(identity, provider.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(provider =>
        {
            var lease = provider.GetRequiredService<RefereeDriverTransportFactories>().CreateReceiver(receiverOptions);
            return new RefereeReceiveService(lease.Receiver,
                provider.GetRequiredService<ServiceReceiveDiagnostics>(), ownedReceiver: lease.Owner);
        });
        builder.Services.AddSingleton<IRefereeCommandSource>(provider =>
            provider.GetRequiredService<RefereeReceiveService>());
        builder.Services.AddSingleton(provider => new GameControllerStateObserver(endpoint,
            (uri, token) => new GameControllerStateClient(uri,
                provider.GetRequiredService<RefereeDriverTransportFactories>().ConnectWebSocket,
                timeProvider: provider.GetRequiredService<TimeProvider>()).ReadMatchStateAsync(token),
            TimeSpan.FromSeconds(3), provider.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(provider => new RefereeApiMatcher(
            provider.GetRequiredService<RefereeReceiveService>(),
            provider.GetRequiredService<GameControllerStateObserver>()));
        if (bool.TryParse(configuration["Testing:IdentityControl:Enabled"], out var identityEnabled) && identityEnabled)
            builder.Services.AddHostedService<RefereeIdentityControlHostedService>();
        builder.Services.AddHostedService<RefereeObservationWorker>();
        if (bool.TryParse(configuration["RefereeDriver:AutoDriveToActive"], out var autoDrive) && autoDrive)
        {
            builder.Services.TryAddSingleton<IGameControllerWebSocketTransport>(provider =>
                new RefereeDriverWebSocketTransport(
                    provider.GetRequiredService<RefereeDriverTransportFactories>().ConnectWebSocket));
            builder.Services.TryAddSingleton(provider => new GameControllerWebSocketControlClient(
                provider.GetRequiredService<IGameControllerWebSocketTransport>(), endpoint));
            builder.Services.TryAddSingleton<IGameControllerControlClient>(provider =>
                provider.GetRequiredService<GameControllerWebSocketControlClient>());
            builder.Services.AddSingleton(provider => new RefereeDriverFixture(
                provider.GetRequiredService<IRefereeCommandSource>(),
                provider.GetRequiredService<IGameControllerControlClient>()));
            builder.Services.AddHostedService<RefereeAutoDriveWorker>();
        }
        RefereeDriverHost.ConfigureHttp(builder);
        var app = builder.Build();
        RefereeDriverHost.MapHealth(app);
        return app;
    }

    /// <summary>標準構成を起動し、呼出取消またはホスト終了まで待って所有資源を回収する。</summary>
    internal static async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        await using var app = Build(WebApplication.CreateBuilder(args));
        await app.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>内部APIの明示指定を必須とし、コンテナから自身やloopbackへの誤接続を拒否する。</summary>
    private static Uri ReadEndpoint(string? value, bool container, string? resourceId)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != "ws" && endpoint.Scheme != "wss") ||
            endpoint.AbsolutePath != "/api/control" ||
            !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Fragment))
            throw new ArgumentException("Game ControllerのWebSocket API接続先を明示する。", nameof(value));

        if (container &&
            (endpoint.IsLoopback || endpoint.Port == 39110 ||
             string.Equals(endpoint.Host, "referee-driver", StringComparison.OrdinalIgnoreCase) ||
             (!string.IsNullOrWhiteSpace(resourceId) &&
              string.Equals(endpoint.Host, resourceId, StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("内部API接続先に自身またはloopbackは指定できない。", nameof(value));
        return endpoint;
    }

    /// <summary>要求ごとにhostnameのURIから接続し、接続失敗時も生成したsocketを所有したままにしない。</summary>
    private static async Task<WebSocket> ConnectWebSocketAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

/// <summary>受信とAPI観測を常駐させ、どちらかの終端時に両処理と明示所有資源を回収する。</summary>
internal sealed class RefereeObservationWorker(
    RefereeReceiveService receiver,
    RefereeApiMatcher matcher,
    TimeProvider clock) : BackgroundService
{
    /// <summary>受信は単一処理に限定し、API観測側は同じ受信証跡を読むだけにする。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        Task? receive = null;
        Task? observe = null;
        try
        {
            receive = receiver.RunAsync(lifetime.Token);
            observe = ObserveAsync(lifetime.Token);
            await Task.WhenAny(receive, observe).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await lifetime.CancelAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await Task.WhenAll(receive ?? Task.CompletedTask, observe ?? Task.CompletedTask)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
                finally
                {
                    await receiver.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>有限窓のAPI照合を順次更新する。HTTP取得や観測から制御APIへの書込みは行わない。</summary>
    private async Task ObserveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await matcher.ReadAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(100), clock, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>明示選択された既存Driverだけを実行し、受信や観測の処理を重複生成しない。</summary>
internal sealed class RefereeAutoDriveWorker(RefereeDriverFixture driver) : BackgroundService
{
    /// <summary>既存の制御順序を一度実行する。正常完了後も観測は継続し、停止時は保留制御を取消す。</summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        driver.DriveToActiveAsync(stoppingToken);
}

/// <summary>単一受信処理へ渡す受信口と、その処理が明示的に破棄すべき所有資源。</summary>
internal sealed record RefereeDriverReceiverLease(
    IRefereeDatagramReceiver Receiver,
    IAsyncDisposable? Owner);

/// <summary>試験では通信境界だけを置換し、標準構成が既存の受信・API処理を組むための宣言。</summary>
internal sealed record RefereeDriverTransportFactories(
    Func<RefereeUdpReceiverOptions, RefereeDriverReceiverLease> CreateReceiver,
    Func<Uri, CancellationToken, Task<WebSocket>> ConnectWebSocket);

/// <summary>RefereeDriver内だけで使う所有情報制御のメッセージ。</summary>
internal sealed class RefereeIdentityControlMessage
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

/// <summary>既存のprepare/challenge/commit形式をRefereeDriver専用Unixソケットへ中継する。</summary>
internal static class RefereeIdentityControlProtocol
{
    internal const string SocketPath = "/tmp/duck-referee-identity.sock";
    internal static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
    private const int MaxFrameCharacters = 64 * 1024;
    private static readonly TimeSpan StageDeadline = TimeSpan.FromSeconds(5);

    internal static bool IsRelayCommand(string[] args) =>
        args.Length == 1 && string.Equals(args[0], "--identity-control", StringComparison.Ordinal);

    /// <summary>全フレームの長さと待機時間を制限し、途中の切断を未完了として返す。</summary>
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
                if (value.Length >= MaxFrameCharacters)
                    throw new InvalidDataException("制御フレームの長さが上限を超えました。");
                value.Append(buffer[0]);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("所有情報制御の応答期限を超えました。");
        }
    }

    internal static async Task WriteFrameAsync(TextWriter writer, RefereeIdentityControlMessage message,
        CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync(JsonSerializer.Serialize(message, JsonOptions).AsMemory(), cancellationToken)
            .ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>短命のdocker execプロセスは所有情報を決定せず、同じソケットの入出力だけを中継する。</summary>
    internal static async Task<int> RelayAsync(TextReader input, TextWriter output,
        string socketPath, CancellationToken cancellationToken)
    {
        try
        {
            var prepare = await ReadFrameAsync(input, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(prepare)) return 2;
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using (var connectionDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                connectionDeadline.CancelAfter(StageDeadline);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), connectionDeadline.Token)
                    .ConfigureAwait(false);
            }
            using var stream = new NetworkStream(socket, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true)
            {
                AutoFlush = true,
            };
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
            return JsonSerializer.Deserialize<RefereeIdentityControlMessage>(result, JsonOptions)?.Accepted == true ? 0 : 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return 130; }
        catch (Exception exception) when (exception is
            IOException or SocketException or JsonException or TimeoutException or InvalidDataException or OperationCanceledException)
        {
            return 1;
        }
    }
}


/// <summary>明示したRunIdと役割に対する同一接続のcommitだけでRefereeDriverの帰属を確定する。</summary>
internal sealed class RefereeIdentityControlHostedService(
    IConfiguration configuration,
    RefereeReceiveService receiver,
    ServiceReceiveDiagnostics diagnostics,
    string? socketPath = null) : BackgroundService
{
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string path = socketPath ?? RefereeIdentityControlProtocol.SocketPath;
    private readonly string bootId = Guid.NewGuid().ToString("N");
    private ResourceDiagnosticIdentity? acceptedIdentity = diagnostics.Identity;
    private string? boundResourceId = configuration["Testing:Diagnostics:ResourceId"];

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
        await started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>自身のソケットだけを所有し、異常な1接続で受付を止めない。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Socket? listener = null;
        var ownsPath = false;
        try
        {
            var runId = configuration["Testing:Diagnostics:RunId"];
            var role = configuration["Testing:IdentityControl:ExpectedRole"];
            if (string.IsNullOrWhiteSpace(runId) ||
                (string.IsNullOrWhiteSpace(role) && string.IsNullOrWhiteSpace(boundResourceId)))
                throw new InvalidOperationException("識別情報制御には実行IDと資源役割または資源IDが必要です。");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path))
                throw new IOException("所有情報制御ソケットが既に存在します。");
            listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(path));
            ownsPath = true;
            listener.Listen(8);
            started.TrySetResult();
            while (!stoppingToken.IsCancellationRequested)
            {
                Socket client;
                try { client = await listener.AcceptAsync(stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested) { break; }
                try { await HandleClientAsync(client, runId!, role, stoppingToken).ConfigureAwait(false); }
                catch (Exception error) when (error is IOException or SocketException or
                    JsonException or TimeoutException or InvalidDataException)
                {
                    // 個別接続の切断や不正フレームから所有情報を復元しない。
                }
            }
        }
        catch (Exception exception)
        {
            started.TrySetException(exception);
            if (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                throw;
        }
        finally
        {
            listener?.Dispose();
            if (ownsPath && File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>prepareでは帰属を変更せず、challengeで固定したBootIdを同じcommitに要求する。</summary>
    private async Task HandleClientAsync(Socket client, string expectedRunId,
        string? expectedRole, CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = new NetworkStream(client, ownsSocket: false))
        using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true)
        {
            AutoFlush = true,
        })
        {
            var prepare = Deserialize(await RefereeIdentityControlProtocol.ReadFrameAsync(reader, cancellationToken)
                .ConfigureAwait(false));
            var proposed = ParseIdentity(prepare);
            if (prepare?.Type != "prepare" || proposed is null ||
                !IsExpected(proposed, prepare.RoleName, expectedRunId, expectedRole))
            {
                await RefereeIdentityControlProtocol.WriteFrameAsync(writer,
                    new() { Type = "result", Accepted = false }, cancellationToken).ConfigureAwait(false);
                return;
            }

            await RefereeIdentityControlProtocol.WriteFrameAsync(writer,
                new() { Type = "challenge", BootId = bootId }, cancellationToken).ConfigureAwait(false);
            var commit = Deserialize(await RefereeIdentityControlProtocol.ReadFrameAsync(reader, cancellationToken)
                .ConfigureAwait(false));
            var committed = ParseIdentity(commit);
            var accepted = commit?.Type == "commit" && committed == proposed &&
                string.Equals(commit.ExpectedBootId, bootId, StringComparison.Ordinal) &&
                string.Equals(commit.RoleName, prepare.RoleName, StringComparison.Ordinal) &&
                IsExpected(proposed, commit.RoleName, expectedRunId, expectedRole) &&
                TryAccept(proposed);
            await RefereeIdentityControlProtocol.WriteFrameAsync(writer,
                new() { Type = "result", Accepted = accepted }, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>同一識別子の再送は無操作とし、資源切替・世代後退・別実行を拒否する。</summary>
    private bool TryAccept(ResourceDiagnosticIdentity identity)
    {
        if (acceptedIdentity == identity) return true;
        if (acceptedIdentity is { } previous &&
            (identity.RunId != previous.RunId ||
             identity.ResourceId != previous.ResourceId ||
             identity.Generation <= previous.Generation))
            return false;
        receiver.ReplaceIdentity(identity);
        acceptedIdentity = identity;
        boundResourceId ??= identity.ResourceId;
        return true;
    }

    private bool IsExpected(ResourceDiagnosticIdentity identity, string? roleName,
        string runId, string? role) =>
        string.Equals(identity.RunId, runId, StringComparison.Ordinal) &&
        (boundResourceId is null ||
         string.Equals(identity.ResourceId, boundResourceId, StringComparison.Ordinal)) &&
        (string.IsNullOrWhiteSpace(role) ||
         string.Equals(roleName, role, StringComparison.Ordinal));

    private static RefereeIdentityControlMessage? Deserialize(string? line) =>
        string.IsNullOrWhiteSpace(line)
            ? null
            : JsonSerializer.Deserialize<RefereeIdentityControlMessage>(
                line, RefereeIdentityControlProtocol.JsonOptions);

    private static ResourceDiagnosticIdentity? ParseIdentity(RefereeIdentityControlMessage? message)
    {
        if (message is null) return null;
        try
        {
            return new ResourceDiagnosticIdentity(
                message.RunId!, message.ResourceId!, message.ContainerId!, message.Generation);
        }
        catch (ArgumentException) { return null; }
    }
}
