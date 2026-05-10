namespace Tracker.Core;

/// <summary>
/// 1 回の engine 更新で publish 可能になった frame、event、diagnostics。
/// </summary>
public sealed class TrackerUpdateResult
{
    /// <summary>
    /// この更新で確定した world frame。0 件以上で、publish 順に並ぶ。
    /// </summary>
    public IReadOnlyList<TrackerFrame> CommittedFrames { get; init; } = [];

    /// <summary>
    /// この更新で発火した tracker event。ProfileSwitched などの control event と WorldFrameCommitted を publish 順に並べる。
    /// </summary>
    public IReadOnlyList<TrackerEvent> EmittedEvents { get; init; } = [];

    /// <summary>
    /// この更新に関する診断情報。
    /// </summary>
    public TrackerEngineDiagnostics Diagnostics { get; init; } = new();
}

/// <summary>
/// Core engine の更新診断値。
/// </summary>
public sealed class TrackerEngineDiagnostics
{
    /// <summary>
    /// late cutoff 以下の event timestamp として破棄した packet 数。
    /// </summary>
    public int LatePacketDropCount { get; init; }
}

/// <summary>
/// Core engine が observer や publisher へ通知する domain event。
/// </summary>
public sealed class TrackerEvent
{
    /// <summary>
    /// event の種類。
    /// </summary>
    public TrackerEventKind Kind { get; init; }

    /// <summary>
    /// event が関連する committed frame number。control event など frame に紐づかない場合は null。
    /// </summary>
    public uint? FrameNumber { get; init; }

    /// <summary>
    /// event 発火時に有効だった profile 名。未設定の場合は null。
    /// </summary>
    public string? ProfileName { get; init; }
}

/// <summary>
/// Core engine が発火する event の種類。
/// </summary>
public enum TrackerEventKind
{
    /// <summary>
    /// profile switch が適用された。
    /// </summary>
    ProfileSwitched = 1,
    /// <summary>
    /// geometry の大きな変更により tracking state を reset した。
    /// </summary>
    GeometryReset = 2,
    /// <summary>
    /// world frame が確定した。
    /// </summary>
    WorldFrameCommitted = 3,
    /// <summary>
    /// kick を検出した。
    /// </summary>
    KickDetected = 4,
    /// <summary>
    /// ball contact 状態が変化した。
    /// </summary>
    ContactChanged = 5,
    /// <summary>
    /// ball が field 外へ出た。
    /// </summary>
    BallLeftField = 6,
}
