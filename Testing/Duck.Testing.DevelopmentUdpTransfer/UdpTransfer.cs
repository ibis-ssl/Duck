using System.Net;
using System.Net.Sockets;

namespace Duck.Testing.DevelopmentUdpTransfer;

/// <summary>開発用 UDP Vision および Tracker データグラムの送受信と中継を行います。</summary>
public static class UdpTransfer
{
    /// <summary>Windows ホストへ Vision パケットを転送するために使用する UDP ポートです。</summary>
    public const int WindowsVisionTransportPort = 39109;
    /// <summary>Linux コンテナーから Tracker パケットを受け取る UDP ポートです。</summary>
    public const int LinuxTrackerTransportPort = 39111;
    /// <summary>標準の Vision マルチキャスト送信先です。</summary>
    public static readonly IPEndPoint Vision = new(IPAddress.Parse("224.5.23.2"), 10020);
    /// <summary>標準の Tracker マルチキャスト送信先です。</summary>
    public static readonly IPEndPoint Tracker = new(Vision.Address, 11010);

    /// <summary>指定した IPv4 マルチキャストグループを指定インターフェイスで受信するソケットを作成します。</summary>
    /// <param name="group">参加するマルチキャストグループとポートです。</param>
    /// <param name="interfaceAddress">グループに参加するローカル IPv4 アドレスです。</param>
    /// <returns>受信待ち状態の UDP ソケットです。</returns>
    /// <exception cref="SocketException">ソケットのバインドまたはグループ参加に失敗しました。</exception>
    public static UdpClient ListenMulticast(IPEndPoint group, IPAddress interfaceAddress)
    {
        var socket = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            socket.ExclusiveAddressUse = false;
            socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Client.Bind(new IPEndPoint(IPAddress.Any, group.Port));
            socket.JoinMulticastGroup(group.Address, interfaceAddress);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>指定したローカル IPv4 インターフェイスを送信元とするマルチキャスト送信ソケットを作成します。</summary>
    /// <param name="interfaceAddress">送信に使用するローカル IPv4 アドレスです。</param>
    /// <returns>マルチキャスト送信可能な UDP ソケットです。</returns>
    /// <exception cref="SocketException">送信インターフェイスまたはソケットオプションを設定できません。</exception>
    public static UdpClient SendMulticast(IPAddress interfaceAddress)
    {
        var socket = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            socket.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, interfaceAddress.GetAddressBytes());
            socket.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            socket.MulticastLoopback = true;
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Vision と Tracker の UDP データグラムをそれぞれの送信先へ並行して中継します。</summary>
    /// <param name="visionInput">Vision データグラムの受信ソケットです。</param>
    /// <param name="visionOutput">Vision データグラムの送信ソケットです。</param>
    /// <param name="visionDestination">各 Vision データグラムの送信先を取得する関数です。</param>
    /// <param name="trackerInput">Tracker データグラムの受信ソケットです。</param>
    /// <param name="trackerOutput">Tracker データグラムの送信ソケットです。</param>
    /// <param name="trackerDestination">各 Tracker データグラムの送信先を取得する関数です。</param>
    /// <param name="firstPacketSent">各ストリームで最初のデータグラムを送信したときに呼び出す通知です。</param>
    /// <param name="stoppingToken">中継を停止するキャンセルトークンです。</param>
    /// <returns>いずれかの中継処理が終了した後、残る中継も停止する非同期操作です。</returns>
    /// <exception cref="SocketException">受信または送信ソケットでネットワーク操作に失敗しました。</exception>
    public static async Task RunAsync(
        UdpClient visionInput,
        UdpClient visionOutput,
        Func<IPEndPoint> visionDestination,
        UdpClient trackerInput,
        UdpClient trackerOutput,
        Func<IPEndPoint> trackerDestination,
        Action<string> firstPacketSent,
        CancellationToken stoppingToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var vision = ForwardAsync(visionInput, visionOutput, visionDestination, () => firstPacketSent("Vision"), cancellation.Token);
        var tracker = ForwardAsync(trackerInput, trackerOutput, trackerDestination, () => firstPacketSent("Tracker"), cancellation.Token);
        await Task.WhenAny(vision, tracker);
        await cancellation.CancelAsync();
        try
        {
            await Task.WhenAll(vision, tracker);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private static async Task ForwardAsync(
        UdpClient input,
        UdpClient output,
        Func<IPEndPoint> destination,
        Action firstPacketSent,
        CancellationToken cancellationToken)
    {
        var sentFirstPacket = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            var datagram = await input.ReceiveAsync(cancellationToken);
            await output.SendAsync(datagram.Buffer, destination(), cancellationToken);
            if (!sentFirstPacket)
            {
                firstPacketSent();
                sentFirstPacket = true;
            }
        }
    }
}
