using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duck.Testing.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tracker.Core;

namespace Tracker.RuntimeHost;

/// <summary>自身の受信証跡を返す。ホストのポート公開と所有者の検証は別途行う。</summary>
/// <param name="diagnostics">Vision と Tracker の受信診断。</param>
/// <param name="trackerSnapshotStore">Tracker 出力 snapshot の取得元。</param>
/// <param name="configuration">サービスの公開範囲と readiness を決める構成。</param>
/// <param name="logger">サービスのログ出力先。</param>
public sealed class RuntimeVisionDiagnosticsHttpService(
    RuntimeVisionReceiverDiagnostics diagnostics,
    TrackedSnapshotStore trackerSnapshotStore,
    IConfiguration configuration,
    ILogger<RuntimeVisionDiagnosticsHttpService> logger) : BackgroundService
{
    /// <summary>診断 HTTP サービスを有効にする構成かどうかを返す。</summary>
    /// <param name="configuration">テスト実行モードを含む構成。</param>
    /// <returns>診断エンドポイントを公開する場合は <see langword="true"/>。</returns>
    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool>("Testing:ContainerMode") ||
        configuration.GetValue<bool>("Testing:HostRuntimeProfile") ||
        string.Equals(configuration["Testing:Mode"], "comparison", StringComparison.Ordinal);

    /// <summary>構成に応じた診断 HTTP リスナーのプレフィックスを返す。</summary>
    /// <param name="configuration">リスナーの公開範囲とポートを決める構成。</param>
    /// <returns>HTTP リスナーに登録する URL プレフィックス。</returns>
    public static string ListenerPrefix(IConfiguration configuration) =>
        configuration.GetValue<bool>("Testing:ContainerMode")
            ? "http://+:39108/"
            : $"http://127.0.0.1:{configuration.GetValue<int?>("Testing:HostRuntimeProfile:HttpPort") ?? 39108}/";

    /// <summary>受信診断と Tracker 出力の現在状態からヘルス応答を作成する。</summary>
    /// <param name="diagnostics">Vision および Tracker の受信診断。</param>
    /// <param name="configuration">readiness の判定に使う構成。</param>
    /// <param name="trackerSnapshotStore">Tracker 出力状態の取得元。未指定なら出力診断を応答に含めない。</param>
    /// <returns>HTTP ステータスコードと応答本文。</returns>
    public static (int StatusCode, RuntimeVisionHealthResponse Response) GetHealth(
        RuntimeVisionReceiverDiagnostics diagnostics,
        IConfiguration configuration,
        TrackedSnapshotStore? trackerSnapshotStore = null)
    {
        var snapshot = diagnostics.Snapshot;
        // 既存の比較用 HTTP は件数を 200 で返す。この応答を厳密な準備完了の証拠にはしない。
        var containerMode = configuration.GetValue<bool>("Testing:ContainerMode");
        var statusReady = !containerMode || snapshot.IsReady;
        var responseReady = snapshot.IsReady;
        var count = containerMode
            ? snapshot.SuccessfullyDecodedCount : diagnostics.DecodedWrapperPacketsReceivedTotal;
        return (statusReady ? 200 : 503,
            new(count, responseReady, snapshot, diagnostics.TrackerSnapshots)
            {
                TrackerReceiverEnabled = configuration.GetValue<bool>("Tracker:Receive:Enabled"),
                TrackerOutput = trackerSnapshotStore is null ? null : ToTrackerOutput(trackerSnapshotStore.GetSnapshot()),
            });
    }

    private static RuntimeTrackerOutputHealth ToTrackerOutput(TrackedSnapshot snapshot) => new(
        snapshot.ActiveProfileName,
        snapshot.PublishSuccessCount,
        snapshot.PublishFailureCount,
        snapshot.LatestFrame?.FrameNumber,
        snapshot.LatestFrame?.Robots.Count ?? 0,
        snapshot.LatestFrame?.Balls.Count ?? 0,
        snapshot.ReceivedAt);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(ListenerPrefix(configuration));
        listener.Start();
        logger.LogInformation("RuntimeHost receive diagnostics listening at {Prefix}", ListenerPrefix(configuration));
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(stoppingToken);
                try
                {
                    if (context.Request.HttpMethod != "GET" || context.Request.Url?.AbsolutePath != "/health/vision")
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.Close();
                        continue;
                    }

                    var health = GetHealth(diagnostics, configuration, trackerSnapshotStore);
                    context.Response.StatusCode = health.StatusCode;
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.OutputStream, health.Response,
                        cancellationToken: stoppingToken);
                    context.Response.Close();
                }
                catch (HttpListenerException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (IOException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { listener.Close(); }
    }
}

/// <summary>Vision と Tracker の受信・出力状態を表すヘルス応答。</summary>
/// <param name="VisionPacketsReceived">正常に復号した Vision パケット数。</param>
/// <param name="IsReady">readiness 条件を満たすかどうか。</param>
/// <param name="Diagnostics">Vision 受信元の診断スナップショット。</param>
/// <param name="TrackerReceiveSources">Tracker 受信元ごとの診断スナップショット。</param>
public sealed record RuntimeVisionHealthResponse(
    long VisionPacketsReceived,
    bool IsReady,
    ServiceReceiveSnapshot Diagnostics,
    [property: JsonPropertyName("trackerDiagnostics")]
    IReadOnlyList<ServiceReceiveSnapshot> TrackerReceiveSources)
{
    /// <summary>このプロセスで Tracker パケットの受信が設定されているかどうか。送信設定とは別に示す。</summary>
    [JsonPropertyName("trackerReceiverEnabled")]
    public bool TrackerReceiverEnabled { get; init; }

    /// <summary>Tracker 出力の累積状態。出力診断が利用できない場合は <see langword="null"/>。</summary>
    [JsonPropertyName("trackerOutput")]
    public RuntimeTrackerOutputHealth? TrackerOutput { get; init; }
}

/// <summary>Tracker の出力件数と最新フレーム情報。</summary>
/// <param name="ActiveProfileName">現在有効なプロファイル名。</param>
/// <param name="PublishSuccessCount">送信に成功した累積件数。</param>
/// <param name="PublishFailureCount">送信に失敗した累積件数。</param>
/// <param name="LatestFrameNumber">最新フレーム番号。</param>
/// <param name="LatestRobotCount">最新フレームに含まれるロボット数。</param>
/// <param name="LatestBallCount">最新フレームに含まれるボール数。</param>
/// <param name="LastFrameReceivedAt">最新フレームの受信時刻。</param>
public sealed record RuntimeTrackerOutputHealth(
    string ActiveProfileName,
    long PublishSuccessCount,
    long PublishFailureCount,
    uint? LatestFrameNumber,
    int LatestRobotCount,
    int LatestBallCount,
    DateTimeOffset? LastFrameReceivedAt);
