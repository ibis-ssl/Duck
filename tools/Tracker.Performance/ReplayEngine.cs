using Tracker.Core;

namespace Tracker.Performance;

/// <summary>
/// 製品エンジンの結果を同じ周期内に保持し、診断集計を計測区間の外で行えるようにする。
/// </summary>
internal sealed class ReplayEngine(int batchCapacity) : ITrackerEngine
{
    private readonly TrackerEngine engine = new();

    /// <summary>
    /// 周期内に実行されたエンジン更新の入力検出と公開結果を処理順に保持する。
    /// </summary>
    internal List<(SSL_DetectionFrame? Detection, TrackerUpdateResult Result)> Updates { get; } = new(batchCapacity);

    /// <summary>
    /// 入力と設定をそのまま製品エンジンに渡し、結果を呼び出し順に記録する。
    /// </summary>
    public TrackerUpdateResult Update(
        SSL_WrapperPacket? packet,
        TrackerEngineSettings settings,
        TrackerProfileSwitchRequest? profileSwitchRequest = null)
    {
        var result = engine.Update(packet, settings, profileSwitchRequest);
        Updates.Add((packet?.Detection, result));
        return result;
    }
}
