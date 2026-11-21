namespace Tracker.Performance;

/// <summary>
/// 指定した再生区間と、その区間の入力を照合するための統計。
/// </summary>
/// <param name="Inputs">区間内の SSL-Vision 入力を記録順に並べたもの。</param>
/// <param name="ArchiveSha256">圧縮記録ファイル全体の SHA-256。</param>
/// <param name="PayloadSha256">区間内の SSL-Vision バイト列を記録順に連結した SHA-256。</param>
/// <param name="CameraDetectionCounts">カメラ単位の検出パケット数。</param>
/// <param name="GeometryCount">フィールド形状情報を含むパケット数。</param>
/// <param name="MaximumRobotsPerInput">1 パケットに含まれたロボット観測数の最大値。</param>
/// <param name="MaximumBallsPerInput">1 パケットに含まれたボール観測数の最大値。</param>
/// <param name="ReachedWindowEnd">区間終了時刻以降の type 4 のヘッダーを読み取ったかどうか。</param>
internal sealed record VisionWindow(
    IReadOnlyList<VisionInput> Inputs,
    string ArchiveSha256,
    string PayloadSha256,
    IReadOnlyDictionary<uint, int> CameraDetectionCounts,
    int GeometryCount,
    int MaximumRobotsPerInput,
    int MaximumBallsPerInput,
    bool ReachedWindowEnd);
