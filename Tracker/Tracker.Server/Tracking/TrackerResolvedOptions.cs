using Tracker.Core;

namespace Tracker.Server.Tracking;

/// <summary>
/// appsettings、選択 profile、runtime override を合成した coordinator 実行用設定。
/// </summary>
public sealed class TrackerResolvedOptions
{
    /// <summary>
    /// tracker pipeline が有効かどうか。
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// tracker engine に渡す解決済み設定。
    /// </summary>
    public TrackerEngineSettings EngineSettings { get; init; } = new();

    /// <summary>
    /// tracker packet publisher に渡す解決済み設定。
    /// </summary>
    public TrackerPublisherOptions PublisherOptions { get; init; } = new();

    /// <summary>
    /// diagnostics log 出力に使う解決済み設定。
    /// </summary>
    public TrackerDiagnosticsOptions Diagnostics { get; init; } = new();
}
