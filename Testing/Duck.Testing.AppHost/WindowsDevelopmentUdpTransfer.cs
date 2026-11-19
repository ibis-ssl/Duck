using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.DevelopmentUdpTransfer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duck.Testing.AppHost;

/// <summary>Windows 開発ホストと Docker Linux コンテナー間で Vision と Tracker の UDP を中継します。</summary>
/// <param name="trackerInterface">Windows 上の Duck が送る Tracker マルチキャストを受信するインターフェイスです。</param>
/// <param name="linuxTrackerEndpoint">Linux 側 Tracker 受信ポートを割り当てる Aspire エンドポイントです。</param>
/// <param name="logger">中継の開始と最初のパケットを記録するロガーです。</param>
/// <param name="windowsVisionTransportPort">Windows 側 Vision 転送の受信ポートです。</param>
internal sealed class WindowsDevelopmentUdpTransfer(
    IPAddress trackerInterface,
    EndpointAnnotation linuxTrackerEndpoint,
    ILogger<WindowsDevelopmentUdpTransfer> logger,
    int windowsVisionTransportPort = UdpTransfer.WindowsVisionTransportPort) : BackgroundService
{
    /// <summary>Docker Desktop の Linux ネットワークへ接続する WSL 仮想インターフェイスを選択します。</summary>
    /// <returns>選択したインターフェイス名と非ループバック IPv4 アドレスです。</returns>
    /// <exception cref="PlatformNotSupportedException">現在の OS が Windows ではありません。</exception>
    /// <exception cref="InvalidOperationException">有効な WSL 仮想 IPv4 インターフェイスが一つに特定できません。</exception>
    internal static (string Name, IPAddress Address) ResolveTrackerInterface()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The development UDP transfer requires Windows and Docker Desktop.");

        // The product's existing Tracker publisher selects the first non-loopback IPv4 address by interface name.
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                nic.Name.StartsWith("vEthernet (WSL", StringComparison.OrdinalIgnoreCase))
            .Select(nic => (nic.Name, Address: nic.GetIPProperties().UnicastAddresses
                .Select(address => address.Address)
                .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))))
            .Where(candidate => candidate.Address is not null)
            .ToArray();
        if (candidates.Length != 1)
            throw new InvalidOperationException("Expected one active WSL virtual adapter with an IPv4 address. Start Docker Desktop in Linux container mode and retry.");

        return (candidates[0].Name, candidates[0].Address!);
    }

    /// <summary>Windows 側で受けた Vision と Tracker のデータを Docker 側との境界へ転送します。</summary>
    /// <param name="stoppingToken">ホスト停止を通知するトークンです。</param>
    /// <returns>転送ループの完了を表す非同期処理です。</returns>
    /// <exception cref="SocketException">必要な UDP ソケットを開始できません。</exception>
    /// <exception cref="InvalidOperationException">転送先の Compose ポートが公開されていません。</exception>
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => ExecuteTransferAsync(stoppingToken);

    /// <summary>UDP ソケットを準備して Windows と Linux 間の転送を開始します。</summary>
    /// <param name="stoppingToken">ホスト停止を通知するトークンです。</param>
    /// <returns>転送ループの完了を表す非同期処理です。</returns>
    /// <exception cref="SocketException">必要な UDP ソケットを開始できません。</exception>
    /// <exception cref="InvalidOperationException">転送先の Compose ポートが公開されていません。</exception>
    internal async Task ExecuteTransferAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var visionInput = new UdpClient(new IPEndPoint(IPAddress.Loopback, windowsVisionTransportPort));
            using var visionOutput = UdpTransfer.SendMulticast(IPAddress.Loopback);
            using var trackerInput = UdpTransfer.ListenMulticast(UdpTransfer.Tracker, trackerInterface);
            using var trackerOutput = new UdpClient(AddressFamily.InterNetwork);
            logger.LogInformation("Listening for forwarded Vision on loopback UDP {Port} and Tracker on {TrackerInterface}.",
                windowsVisionTransportPort, trackerInterface);
            await UdpTransfer.RunAsync(
                visionInput, visionOutput, () => UdpTransfer.Vision,
                trackerInput, trackerOutput, () => new IPEndPoint(IPAddress.Loopback,
                    linuxTrackerEndpoint.AllocatedEndpoint?.Port ??
                        throw new InvalidOperationException("Compose did not publish its development Tracker transport port.")),
                stream => logger.LogInformation("Sent first {Stream} datagram.", stream), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Ctrl+C and normal AppHost shutdown preserve the successful exit code.
        }
        catch (Exception exception)
        {
            AppHostTerminationState.MarkFailed();
            logger.LogCritical(exception, "The Windows development UDP transfer stopped unexpectedly.");
            throw;
        }
    }
}
