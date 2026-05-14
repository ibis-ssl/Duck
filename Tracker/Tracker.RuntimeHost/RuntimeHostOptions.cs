namespace Tracker.RuntimeHost;

/// <summary>
/// Tracker.RuntimeHost の headless 実行設定。
/// </summary>
public sealed class RuntimeHostOptions
{
    /// <summary>
    /// RuntimeHost 設定 section 名。
    /// </summary>
    public const string SectionName = "RuntimeHost";

    /// <summary>
    /// tracker operation loop の既定周期。
    /// </summary>
    public const int DefaultOperationLoopIntervalMilliseconds = 16;

    /// <summary>
    /// tracker operation loop の実行周期。
    /// </summary>
    public int OperationLoopIntervalMilliseconds { get; init; } = DefaultOperationLoopIntervalMilliseconds;
}
