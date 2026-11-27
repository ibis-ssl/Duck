namespace Tracker.Performance;

/// <summary>
/// 1 パスの投入、確定、配信を照合するための件数。
/// </summary>
/// <param name="Cycles">空の周期も含む仮想周期の総数。</param>
/// <param name="EngineUpdates">エンジンを呼び出した回数。</param>
/// <param name="LatestDetectionReplacements">同じ周期の同じカメラで上書きされた検出パケット数。</param>
/// <param name="LatestNonDetectionReplacements">同じ周期で上書きされた検出を含まないパケット数。</param>
/// <param name="LateDrops">エンジンが時刻を理由に破棄した検出パケット数。</param>
/// <param name="GeometryResetDiscards">フィールド形状の変更に伴うリセットで失われた保留検出件数。</param>
/// <param name="PendingDetections">再生末尾で保留されている検出件数。</param>
/// <param name="CommittedDetections">確定フレームの入力に含まれる検出件数。</param>
/// <param name="CommittedFrames">確定した追跡フレーム数。</param>
/// <param name="OutputPackets">正常に直列化した配信パケット数。</param>
/// <param name="OutputBytes">正常に直列化した配信パケットの総バイト数。</param>
/// <param name="PublishFailures">コーディネーターが記録した配信失敗件数。</param>
internal sealed record ReplayCounts(
    int Cycles,
    int EngineUpdates,
    int LatestDetectionReplacements,
    int LatestNonDetectionReplacements,
    int LateDrops,
    int GeometryResetDiscards,
    int PendingDetections,
    int CommittedDetections,
    int CommittedFrames,
    int OutputPackets,
    long OutputBytes,
    long PublishFailures);
