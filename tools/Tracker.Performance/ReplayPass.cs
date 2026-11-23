namespace Tracker.Performance;

/// <summary>
/// 一度の高速再生で測定した各区間の値と処理結果。
/// </summary>
/// <param name="WallMilliseconds">再生パス全体の経過時間（ミリ秒）。</param>
/// <param name="ProcessCpuMilliseconds">パス全体のプロセス CPU 時間（ミリ秒）。</param>
/// <param name="CycleMicroseconds">周期処理時間（マイクロ秒）の分位点。</param>
/// <param name="ReceiveMicroseconds">入力の復号とバッファ保存の時間（マイクロ秒）の分位点。</param>
/// <param name="VirtualCommitLagMilliseconds">最新入力の受信から確定までの仮想時間（ミリ秒）の分位点。</param>
/// <param name="CycleAllocatedBytes">周期処理で再生スレッドに割り当てられたバイト数。</param>
/// <param name="ReceiveAllocatedBytes">入力処理で再生スレッドに割り当てられたバイト数。</param>
/// <param name="GcCollectionsByGeneration">パス全体の世代 0、1、2 の GC 回数。</param>
/// <param name="Counts">投入、保留、確定と配信の件数。</param>
/// <param name="WireSha256">品質再生の配信バイト列の SHA-256。正式計測では null。</param>
internal sealed record ReplayPass(
    double WallMilliseconds,
    double ProcessCpuMilliseconds,
    Quantiles CycleMicroseconds,
    Quantiles ReceiveMicroseconds,
    Quantiles VirtualCommitLagMilliseconds,
    long CycleAllocatedBytes,
    long ReceiveAllocatedBytes,
    int[] GcCollectionsByGeneration,
    ReplayCounts Counts,
    string? WireSha256);
