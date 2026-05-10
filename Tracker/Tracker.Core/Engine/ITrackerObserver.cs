namespace Tracker.Core;

/// <summary>
/// Core engine の domain event を受け取る observer 契約。
/// </summary>
public interface ITrackerObserver
{
    /// <summary>
    /// profile switch が適用されたことを通知する。
    /// </summary>
    void OnProfileSwitched(string profileName);

    /// <summary>
    /// geometry reset により tracking state が clear されたことを通知する。
    /// </summary>
    void OnGeometryReset();

    /// <summary>
    /// world frame が確定したことを通知する。
    /// </summary>
    void OnWorldFrameCommitted(TrackerFrame frame);

    /// <summary>
    /// kick を検出したことを通知する。
    /// </summary>
    void OnKickDetected(KickEventState kick, TrackerFrame frame);

    /// <summary>
    /// contact 状態が変化したことを通知する。
    /// </summary>
    void OnContactChanged(TrackerFrame frame);

    /// <summary>
    /// ball が field 外へ出たことを通知する。
    /// </summary>
    void OnBallLeftField(BallLeftFieldState state, TrackerFrame frame);
}
