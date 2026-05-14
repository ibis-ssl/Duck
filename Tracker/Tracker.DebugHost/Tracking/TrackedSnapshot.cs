using Tracker.Core;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// UI と diagnostics が参照する tracker の最新 frame、active profile、publish 統計の snapshot。
/// </summary>
/// <param name="LatestFrame">最後に committed された tracker frame。未受信または reset 後は null。</param>
/// <param name="ReceivedAt">最新 frame を受信した時刻。frame がない場合は null。</param>
/// <param name="ActiveProfileName">現在 UI と publisher に反映済みの tracker profile 名。</param>
/// <param name="PublishSuccessCount">tracker packet publish 成功回数。</param>
/// <param name="PublishFailureCount">tracker packet publish 失敗回数。</param>
public sealed record TrackedSnapshot(
    TrackerFrame? LatestFrame,
    DateTimeOffset? ReceivedAt,
    string ActiveProfileName,
    long PublishSuccessCount,
    long PublishFailureCount);
