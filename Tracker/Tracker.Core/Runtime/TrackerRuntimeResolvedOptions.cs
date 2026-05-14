namespace Tracker.Core;

/// <summary>
/// tracker operation loop が必要とする UI 非依存の解決済み runtime 設定。
/// </summary>
public class TrackerRuntimeResolvedOptions
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
}
