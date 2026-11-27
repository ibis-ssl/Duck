using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;

namespace Duck.Testing.RefereeDriver.Hosting;

/// <summary>標準構成の薄い入口。現在は先行試験のための未実装境界である。</summary>
internal static class RefereeDriverApplication
{
    /// <summary>設定と注入境界を受け取る。標準DI・常駐処理・経路登録はまだ行わない。</summary>
    internal static WebApplication Build(WebApplicationBuilder builder) => builder.Build();

    /// <summary>実行入口の呼出可能な宣言。先行試験のRed取得前はホストを起動しない。</summary>
    internal static Task RunAsync(string[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>単一受信処理へ渡す受信口と、その処理が明示的に破棄すべき所有資源。</summary>
internal sealed record RefereeDriverReceiverLease(
    IRefereeDatagramReceiver Receiver,
    IAsyncDisposable? Owner);

/// <summary>試験では通信境界だけを置換し、標準構成が既存の受信・API処理を組むための宣言。</summary>
internal sealed record RefereeDriverTransportFactories(
    Func<RefereeUdpReceiverOptions, RefereeDriverReceiverLease> CreateReceiver,
    Func<Uri, CancellationToken, Task<WebSocket>> ConnectWebSocket);
