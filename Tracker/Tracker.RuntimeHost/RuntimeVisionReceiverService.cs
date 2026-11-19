using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Google.Protobuf;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tracker.RuntimeHost;

/// <summary>
/// RuntimeHost の headless SSL-Vision UDP receiver。
/// </summary>
internal sealed class RuntimeVisionReceiverService : BackgroundService
{
    private static readonly TimeSpan DiagnosticsLogInterval = TimeSpan.FromSeconds(4);

    private readonly IOptions<RuntimeVisionReceiverOptions> options;
    private readonly RuntimeVisionPacketBuffer packetBuffer;
    private readonly RuntimeVisionReceiverDiagnostics diagnostics;
    private readonly ILogger<RuntimeVisionReceiverService> logger;

    /// <summary>
    /// RuntimeHost receiver options、latest packet buffer、logger を受け取って receiver を作成する。
    /// </summary>
    /// <param name="options">Vision receiver の設定。</param>
    /// <param name="packetBuffer">最新の受信 packet を保持する buffer。</param>
    /// <param name="diagnostics">受信結果を記録する診断オブジェクト。</param>
    /// <param name="logger">receiver のログ出力先。</param>
    public RuntimeVisionReceiverService(
        IOptions<RuntimeVisionReceiverOptions> options,
        RuntimeVisionPacketBuffer packetBuffer,
        RuntimeVisionReceiverDiagnostics diagnostics,
        ILogger<RuntimeVisionReceiverService> logger)
    {
        this.options = options;
        this.packetBuffer = packetBuffer;
        this.diagnostics = diagnostics;
        this.logger = logger;
    }

    /// <summary>
    /// SSL-Vision UDP packet を受信し、latest packet buffer へ保存する。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = options.Value;
        if (!receiverOptions.Enabled)
        {
            logger.LogInformation("RuntimeHost SSL-Vision receiver is disabled.");
            return;
        }

        var endpointDescription = $"{receiverOptions.MulticastAddress}:{receiverOptions.Port}";
        UdpClient udpClient;
        MulticastJoinResult joinResult;

        try
        {
            (udpClient, joinResult) = CreateUdpClient(receiverOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize RuntimeHost SSL-Vision receiver for {Endpoint}", endpointDescription);
            throw;
        }

        using (udpClient)
        {
            LogReceiverStarted(endpointDescription, joinResult);
            var joinedInterfaces = joinResult.JoinedInterfaces
                .Select(address => address.ToString())
                .ToArray();
            LogDiagnostics(
                logger,
                endpointDescription,
                joinedInterfaces,
                diagnostics.VisionPacketsReceivedTotal);

            using var diagnosticsTimer = new PeriodicTimer(DiagnosticsLogInterval);
            var diagnosticsTask = LogDiagnosticsPeriodicallyAsync(
                diagnosticsTimer,
                endpointDescription,
                joinedInterfaces,
                stoppingToken);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await udpClient.ReceiveAsync(stoppingToken);
                        var attempt = diagnostics.BeginReceive();
                        ProcessDatagram(
                            result.Buffer,
                            DateTimeOffset.UtcNow,
                            packetBuffer,
                            diagnostics,
                            logger,
                            attempt,
                            result.RemoteEndPoint.ToString());
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to receive RuntimeHost SSL-Vision packet.");
                    }
                }
            }
            finally
            {
                diagnosticsTimer.Dispose();
                await diagnosticsTask;
            }
        }
    }

    private void LogReceiverStarted(string endpointDescription, MulticastJoinResult joinResult)
    {
        if (joinResult.FailedInterfaces.Count > 0)
        {
            logger.LogWarning(
                "RuntimeHost joined SSL-Vision multicast group on {JoinedInterfaces}; failed on {FailedInterfaces}",
                string.Join(", ", joinResult.JoinedInterfaces),
                string.Join(", ", joinResult.FailedInterfaces));
        }

        if (joinResult.JoinedInterfaces.Count > 0)
        {
            logger.LogInformation(
                "RuntimeHost receiving SSL-Vision packets from {Endpoint} via {JoinedInterfaces}",
                endpointDescription,
                string.Join(", ", joinResult.JoinedInterfaces));
        }
        else
        {
            logger.LogInformation("RuntimeHost receiving SSL-Vision packets from {Endpoint}", endpointDescription);
        }
    }

    internal static bool ProcessDatagram(
        byte[] payload,
        DateTimeOffset receivedAt,
        RuntimeVisionPacketBuffer packetBuffer,
        RuntimeVisionReceiverDiagnostics diagnostics,
        ILogger logger,
        ServiceReceiveDiagnostics.ReceiveAttempt? attempt = null,
        string? remoteEndpoint = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(packetBuffer);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(logger);

        attempt ??= diagnostics.BeginReceive();
        try
        {
            var packet = SSL_WrapperPacket.Parser.ParseFrom(payload);
            packetBuffer.StorePacket(packet, receivedAt);
            diagnostics.RecordPacketReceived(attempt, packet.Detection is not null, remoteEndpoint);
            return true;
        }
        catch (InvalidProtocolBufferException ex)
        {
            diagnostics.RecordDecodeFailure(attempt);
            logger.LogWarning(ex, "Failed to decode RuntimeHost SSL-Vision packet.");
            return false;
        }
    }

    internal static void LogDiagnostics(
        ILogger logger,
        string endpointDescription,
        IReadOnlyList<string> joinedInterfaces,
        long visionPacketsReceivedTotal)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointDescription);
        ArgumentNullException.ThrowIfNull(joinedInterfaces);

        logger.LogInformation(
            "RuntimeHost SSL-Vision receiver diagnostics: Endpoint={Endpoint}; Interfaces={Interfaces}; VisionPacketsReceivedTotal={VisionPacketsReceivedTotal}",
            endpointDescription,
            joinedInterfaces.Count == 0 ? "<none>" : string.Join(", ", joinedInterfaces),
            visionPacketsReceivedTotal);
    }

    private async Task LogDiagnosticsPeriodicallyAsync(
        PeriodicTimer timer,
        string endpointDescription,
        IReadOnlyList<string> joinedInterfaces,
        CancellationToken stoppingToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                LogDiagnostics(
                    logger,
                    endpointDescription,
                    joinedInterfaces,
                    diagnostics.VisionPacketsReceivedTotal);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// IPv4の待受アドレスへ bind し、指定アドレスがマルチキャストの場合だけグループへ参加する。
    /// </summary>
    internal static (UdpClient Client, MulticastJoinResult JoinResult) CreateUdpClient(RuntimeVisionReceiverOptions options)
    {
        if (!IPAddress.TryParse(options.MulticastAddress, out var groupAddress))
        {
            throw new InvalidOperationException($"Invalid VisionReceiver multicast address '{options.MulticastAddress}'.");
        }

        var udpClient = new UdpClient(AddressFamily.InterNetwork);
        udpClient.ExclusiveAddressUse = false;
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        if (!IPAddress.TryParse(options.ListenAddress, out var listenAddress) ||
            listenAddress.AddressFamily != AddressFamily.InterNetwork)
        {
            udpClient.Dispose();
            throw new InvalidOperationException($"Invalid VisionReceiver listen address '{options.ListenAddress}'.");
        }

        udpClient.Client.Bind(new IPEndPoint(listenAddress, options.Port));
        var joinResult = MulticastJoinResult.None;

        if (IsMulticast(groupAddress))
        {
            joinResult = JoinMulticastGroup(udpClient, groupAddress, options.InterfaceAddress);
        }

        return (udpClient, joinResult);
    }

    private static bool IsMulticast(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] >= 224 && bytes[0] <= 239;
    }

    private static MulticastJoinResult JoinMulticastGroup(
        UdpClient udpClient,
        IPAddress groupAddress,
        string? configuredInterfaceAddress)
    {
        var candidateAddresses = ResolveMulticastJoinAddresses(
            configuredInterfaceAddress,
            DiscoverMulticastJoinAddresses());

        if (candidateAddresses.Count == 0)
        {
            throw new InvalidOperationException(
                $"No IPv4 interface is available to join SSL-Vision multicast group '{groupAddress}'. " +
                "Set VisionReceiver:InterfaceAddress to a specific local IPv4 address.");
        }

        var joinedInterfaces = new List<IPAddress>(candidateAddresses.Count);
        var failedInterfaces = new List<string>();
        SocketException? firstSocketException = null;

        foreach (var candidateAddress in candidateAddresses)
        {
            try
            {
                udpClient.JoinMulticastGroup(groupAddress, candidateAddress);
                joinedInterfaces.Add(candidateAddress);
            }
            catch (SocketException ex)
            {
                firstSocketException ??= ex;
                failedInterfaces.Add($"{candidateAddress} ({ex.SocketErrorCode})");
            }
        }

        if (joinedInterfaces.Count == 0)
        {
            throw new InvalidOperationException(
                $"Failed to join SSL-Vision multicast group '{groupAddress}' on any local IPv4 interface. " +
                $"Tried: {string.Join(", ", failedInterfaces)}",
                firstSocketException);
        }

        return new MulticastJoinResult(joinedInterfaces, failedInterfaces);
    }

    /// <summary>
    /// 明示 interface address があればそれだけを、なければ multicast join 候補 IPv4 address を返す。
    /// </summary>
    internal static IReadOnlyList<IPAddress> ResolveMulticastJoinAddresses(
        string? configuredInterfaceAddress,
        IEnumerable<IPAddress> discoveredAddresses)
    {
        if (!string.IsNullOrWhiteSpace(configuredInterfaceAddress))
        {
            if (!IPAddress.TryParse(configuredInterfaceAddress, out var interfaceAddress) ||
                interfaceAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new InvalidOperationException(
                    $"Invalid VisionReceiver interface address '{configuredInterfaceAddress}'.");
            }

            return [interfaceAddress];
        }

        return discoveredAddresses
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(address))
            .Distinct()
            .OrderBy(address => IPAddress.IsLoopback(address) ? 1 : 0)
            .ToArray();
    }

    private static IReadOnlyList<IPAddress> DiscoverMulticastJoinAddresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                (networkInterface.SupportsMulticast ||
                 networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback))
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(unicastAddress => unicastAddress.Address)
            .ToArray();
    }

    /// <summary>マルチキャスト参加に成功した IPv4 アドレスと、参加に失敗した interface 名をまとめる結果。</summary>
    internal sealed record MulticastJoinResult(
        IReadOnlyList<IPAddress> JoinedInterfaces,
        IReadOnlyList<string> FailedInterfaces)
    {
        /// <summary>参加できた interface がなく、失敗記録もない結果。</summary>
        public static MulticastJoinResult None { get; } =
            new(Array.Empty<IPAddress>(), Array.Empty<string>());
    }
}

/// <summary>
/// RuntimeHost SSL-Vision receiver の OS 受入確認に使う累積診断値。
/// </summary>
public sealed class RuntimeVisionReceiverDiagnostics
{
    private readonly ServiceReceiveDiagnostics diagnostics;
    private readonly object epochGate = new();
    private ServiceReceiveDiagnostics.ReceiveEpoch epoch;
    private ServiceReceiveDiagnostics.ReceiveEpoch trackerEpoch;
    private long decodedWrapperPacketsReceivedTotal;

    /// <summary>新しい受信診断ストアを使って Vision receiver の診断を作成する。</summary>
    public RuntimeVisionReceiverDiagnostics() : this(new ServiceReceiveDiagnostics()) { }

    /// <summary>指定した診断ストアで Vision と Tracker の受信世代を初期化する。</summary>
    /// <param name="diagnostics">受信試行と snapshot を管理する診断ストア。</param>
    public RuntimeVisionReceiverDiagnostics(ServiceReceiveDiagnostics diagnostics)
    {
        this.diagnostics = diagnostics;
        epoch = diagnostics.BeginEpoch("vision");
        trackerEpoch = diagnostics.BeginEpoch("tracker");
    }

    /// <summary>正常に復号した Detection 付きパケットの件数。geometry だけの入力は除く。</summary>
    public long VisionPacketsReceivedTotal => diagnostics.GetSnapshots("vision").Sum(s => s.SuccessfullyDecodedCount);

    /// <summary>正常に復号した Tracker wrapper packet の累積件数。</summary>
    public long DecodedWrapperPacketsReceivedTotal => Interlocked.Read(ref decodedWrapperPacketsReceivedTotal);

    /// <summary>現在の Vision 受信世代に対する受信試行を開始する。</summary>
    /// <returns>結果を診断へ報告するための受信試行。</returns>
    public ServiceReceiveDiagnostics.ReceiveAttempt BeginReceive()
    {
        lock (epochGate) return diagnostics.BeginReceive(epoch);
    }

    /// <summary>現在の受信診断に関連付けられた owner identity。</summary>
    public ResourceDiagnosticIdentity? Identity => diagnostics.Identity;

    /// <summary>新しい owner identity に受信世代を同期し、以前の試行を無効化する。</summary>
    /// <param name="identity">診断に割り当てる owner identity。</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> が <see langword="null"/> の場合。</exception>
    public void ReplaceIdentity(ResourceDiagnosticIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        lock (epochGate)
        {
            epoch = diagnostics.ReplaceIdentityAndBeginEpoch(identity, "vision");
            trackerEpoch = diagnostics.BeginEpoch("tracker");
        }
    }

    /// <summary>Vision 受信元の最新診断スナップショット。</summary>
    public ServiceReceiveSnapshot Snapshot => diagnostics.GetSnapshots("vision").Single();

    /// <summary>Tracker 受信元ごとの診断スナップショット。</summary>
    public IReadOnlyList<ServiceReceiveSnapshot> TrackerSnapshots => diagnostics.GetSnapshots("tracker");

    internal ServiceReceiveDiagnostics.ReceiveAttempt BeginTrackerReceive()
    {
        lock (epochGate) return diagnostics.BeginReceive(trackerEpoch);
    }

    internal void RecordTrackerPacket(
        ServiceReceiveDiagnostics.ReceiveAttempt attempt,
        TrackerConnectionLib.TrackerPacketAdapter packet,
        IPEndPoint remoteEndpoint)
    {
        if (packet.Packet.TrackedFrame is null) return;
        diagnostics.RecordSuccess(
            attempt,
            string.Join('\u001f', packet.SourceName, packet.Uuid, remoteEndpoint.ToString()),
            packet.SourceName,
            packet.Uuid,
            remoteEndpoint.ToString());
    }

    internal void RecordTrackerDecodeFailure(ServiceReceiveDiagnostics.ReceiveAttempt attempt) =>
        diagnostics.RecordFailure(attempt);

    internal void RecordPacketReceived(ServiceReceiveDiagnostics.ReceiveAttempt attempt, bool hasDetection, string? remoteEndpoint)
    {
        Interlocked.Increment(ref decodedWrapperPacketsReceivedTotal);
        if (hasDetection) diagnostics.RecordSuccess(attempt, remoteEndpoint: remoteEndpoint);
    }

    internal void RecordDecodeFailure(ServiceReceiveDiagnostics.ReceiveAttempt attempt) => diagnostics.RecordFailure(attempt);
}
