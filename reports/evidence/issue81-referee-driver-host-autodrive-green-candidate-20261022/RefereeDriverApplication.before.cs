using System.Net;
using System.Net.WebSockets;
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
        builder.Services.AddHostedService<RefereeObservationWorker>();
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

/// <summary>単一受信処理へ渡す受信口と、その処理が明示的に破棄すべき所有資源。</summary>
internal sealed record RefereeDriverReceiverLease(
    IRefereeDatagramReceiver Receiver,
    IAsyncDisposable? Owner);

/// <summary>試験では通信境界だけを置換し、標準構成が既存の受信・API処理を組むための宣言。</summary>
internal sealed record RefereeDriverTransportFactories(
    Func<RefereeUdpReceiverOptions, RefereeDriverReceiverLease> CreateReceiver,
    Func<Uri, CancellationToken, Task<WebSocket>> ConnectWebSocket);
