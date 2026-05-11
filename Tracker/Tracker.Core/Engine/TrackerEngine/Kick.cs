namespace Tracker.Core;

/// <summary>
/// Core tracker engine の責務別 partial 実装。
/// </summary>
public sealed partial class TrackerEngine
{
    /// <summary>
    /// kick 検出と既存 kick state の継続・停止判定を更新する。
    /// </summary>
    private (KickEventState? KickState, bool KickDetected) UpdateKickState(
        TrackerEngineSettings settings,
        TrackedBallState? primaryBall,
        TrackedBallState? previousPrimaryBall,
        BallContactState? previousContactState,
        BallContactState? currentContactState,
        long frameTimestampNs)
    {
        var detectedKick = TryCreateKickEventState(settings, primaryBall, previousPrimaryBall, previousContactState, currentContactState, frameTimestampNs);
        if (detectedKick is not null)
        {
            kickBelowStillMovingThresholdFrameCount = 0;
            return (detectedKick, true);
        }

        if (activeKickState is null || primaryBall is null || activeKickState.BallTrackId != primaryBall.InternalTrackId)
        {
            kickBelowStillMovingThresholdFrameCount = 0;
            return (null, false);
        }

        var planarSpeedMmPerS = GetPlanarSpeedMmPerS(primaryBall);
        var isStillMoving = planarSpeedMmPerS >= KickStillMovingSpeedThresholdMmPerS;
        if (!isStillMoving)
        {
            kickBelowStillMovingThresholdFrameCount++;
            if (kickBelowStillMovingThresholdFrameCount < KickStillMovingGraceFrames)
            {
                isStillMoving = true;
            }
        }
        else
        {
            kickBelowStillMovingThresholdFrameCount = 0;
        }

        return (new KickEventState
        {
            StartXMm = activeKickState.StartXMm,
            StartYMm = activeKickState.StartYMm,
            StartTimestampNs = activeKickState.StartTimestampNs,
            InitialVelocityXMmPerS = activeKickState.InitialVelocityXMmPerS,
            InitialVelocityYMmPerS = activeKickState.InitialVelocityYMmPerS,
            InitialVelocityZMmPerS = activeKickState.InitialVelocityZMmPerS,
            BallTrackId = activeKickState.BallTrackId,
            LatestSpeedMmPerS = planarSpeedMmPerS,
            LatestUpdateTimestampNs = frameTimestampNs,
            StopXMm = isStillMoving ? null : primaryBall.XMm,
            StopYMm = isStillMoving ? null : primaryBall.YMm,
            StopTimestampNs = isStillMoving ? null : frameTimestampNs,
            KickerRobotId = activeKickState.KickerRobotId,
            KickKind = activeKickState.KickKind,
            IsStillMoving = isStillMoving,
        }, false);
    }

    /// <summary>
    /// speed threshold crossing と recent contact から新規 kick event を作る。
    /// </summary>
    private static KickEventState? TryCreateKickEventState(
        TrackerEngineSettings settings,
        TrackedBallState? primaryBall,
        TrackedBallState? previousPrimaryBall,
        BallContactState? previousContactState,
        BallContactState? currentContactState,
        long frameTimestampNs)
    {
        if (primaryBall is null)
        {
            return null;
        }

        var planarSpeedMmPerS = GetPlanarSpeedMmPerS(primaryBall);
        var kickDetectionSpeedThresholdMmPerS = GetKickDetectionSpeedThresholdMmPerS(settings);
        if (planarSpeedMmPerS < kickDetectionSpeedThresholdMmPerS)
        {
            return null;
        }

        var previousPlanarSpeedMmPerS = previousPrimaryBall is not null
            && previousPrimaryBall.InternalTrackId == primaryBall.InternalTrackId
            ? GetPlanarSpeedMmPerS(previousPrimaryBall)
            : 0d;
        if (previousPlanarSpeedMmPerS >= kickDetectionSpeedThresholdMmPerS)
        {
            return null;
        }

        var recentContact = SelectRecentContact(previousContactState, currentContactState, frameTimestampNs);
        if (recentContact?.LastRobotId is null)
        {
            return null;
        }

        var startBall = previousPrimaryBall is not null && previousPrimaryBall.InternalTrackId == primaryBall.InternalTrackId
            ? previousPrimaryBall
            : primaryBall;

        return new KickEventState
        {
            StartXMm = startBall.XMm,
            StartYMm = startBall.YMm,
            StartTimestampNs = startBall.LastVisibleTimestampNs,
            InitialVelocityXMmPerS = primaryBall.VXMmPerS,
            InitialVelocityYMmPerS = primaryBall.VYMmPerS,
            InitialVelocityZMmPerS = primaryBall.VZMmPerS,
            BallTrackId = primaryBall.InternalTrackId,
            LatestSpeedMmPerS = planarSpeedMmPerS,
            LatestUpdateTimestampNs = frameTimestampNs,
            KickerRobotId = recentContact.LastRobotId,
            KickKind = IsChipKick(settings, primaryBall) ? "chip" : "flat",
            IsStillMoving = true,
        };
    }

    /// <summary>
    /// 現在または直前の contact state から kick に紐づける最近の接触者を選ぶ。
    /// </summary>
    private static BallContactState? SelectRecentContact(
        BallContactState? previousContactState,
        BallContactState? currentContactState,
        long frameTimestampNs)
    {
        return new BallContactState?[] { currentContactState, previousContactState }
            .Where(state => state?.LastRobotId is not null)
            .Cast<BallContactState>()
            .Where(state => frameTimestampNs - state.LastContactTimestampNs <= RecentContactWindowNs)
            .OrderByDescending(state => state.LastContactTimestampNs)
            .FirstOrDefault();
    }

    private static double GetPlanarSpeedMmPerS(TrackedBallState ball)
    {
        return Math.Sqrt((ball.VXMmPerS * ball.VXMmPerS) + (ball.VYMmPerS * ball.VYMmPerS));
    }

    private static bool IsChipKick(TrackerEngineSettings settings, TrackedBallState ball)
    {
        var chipHeightThresholdMm = GetChipHeightThresholdMm(settings);
        return ball.ZMm >= chipHeightThresholdMm || ball.VZMmPerS >= chipHeightThresholdMm;
    }
}
