namespace Tracker.Core;

/// <summary>
/// runtime profile switch を Core engine へ渡すための immutable snapshot。
/// </summary>
public sealed class TrackerProfileSwitchRequest
{
    /// <summary>
    /// caller 側で単調増加させる request version。
    /// </summary>
    public int RequestVersion { get; init; }

    /// <summary>
    /// 適用する profile 名。
    /// </summary>
    public string ProfileName { get; init; } = "default";

    /// <summary>
    /// profile switch 時点で解決済みの base settings snapshot。
    /// </summary>
    public TrackerEngineSettings ResolvedBaseSettings { get; init; } = new();

    /// <summary>
    /// profile switch 時点で解決済みの runtime overrides snapshot。
    /// </summary>
    public TrackerRuntimeOverrides RuntimeOverrides { get; init; } = new();
}
