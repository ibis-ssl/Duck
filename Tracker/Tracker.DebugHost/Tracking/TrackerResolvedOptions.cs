using Tracker.Core;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// appsettings、選択 profile、runtime override を合成した coordinator 実行用設定。
/// </summary>
public sealed class TrackerResolvedOptions : TrackerRuntimeResolvedOptions
{
    /// <summary>
    /// diagnostics log 出力に使う解決済み設定。
    /// </summary>
    public TrackerDiagnosticsOptions Diagnostics { get; init; } = new();
}
