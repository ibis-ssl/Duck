namespace Tracker.Core;

/// <summary>
/// Core tracker engine の責務別 partial 実装。
/// </summary>
public sealed partial class TrackerEngine
{
    /// <summary>
    /// primary ball と geometry から field 外退出状態を作る。
    /// </summary>
    private static BallLeftFieldState? CreateBallLeftFieldState(
        TrackedBallState? primaryBall,
        TrackedBallState? previousPrimaryBall,
        TrackerGeometrySnapshot? currentGeometrySnapshot,
        BallLeftFieldState? previousLeftFieldState)
    {
        if (primaryBall is null || currentGeometrySnapshot is null || !IsBallOutOfField(primaryBall, currentGeometrySnapshot))
        {
            return null;
        }

        if (previousLeftFieldState?.IsOutOfField == true)
        {
            return previousLeftFieldState;
        }

        var crossing = ProjectBallCrossing(primaryBall, previousPrimaryBall, currentGeometrySnapshot);
        return new BallLeftFieldState
        {
            IsOutOfField = true,
            BoundaryName = crossing.BoundaryName,
            CrossingXMm = crossing.CrossingXMm,
            CrossingYMm = crossing.CrossingYMm,
            CrossingTimestampNs = crossing.CrossingTimestampNs,
        };
    }

    /// <summary>
    /// primary ball track id ごとの最新 left-field state を更新する。
    /// </summary>
    private void UpdateLatestBallLeftFieldState(
        TrackedBallState? primaryBall,
        BallLeftFieldState? ballLeftFieldState)
    {
        if (primaryBall is null)
        {
            return;
        }

        if (ballLeftFieldState is null)
        {
            latestBallLeftFieldStates.Remove(primaryBall.InternalTrackId);
            return;
        }

        latestBallLeftFieldStates[primaryBall.InternalTrackId] = ballLeftFieldState;
    }

    /// <summary>
    /// 出力対象から消えた ball の left-field state を破棄する。
    /// </summary>
    private void PruneLatestBallLeftFieldStates(IReadOnlyList<TrackedBallState> balls)
    {
        var activeTrackIds = balls
            .Select(ball => ball.InternalTrackId)
            .ToHashSet();
        foreach (var staleTrackId in latestBallLeftFieldStates.Keys.Except(activeTrackIds).ToList())
        {
            latestBallLeftFieldStates.Remove(staleTrackId);
        }
    }

    /// <summary>
    /// ball left field event を出すべき状態遷移か判定する。
    /// </summary>
    private static bool DidBallLeaveField(
        BallLeftFieldState? previousLeftFieldState,
        BallLeftFieldState? currentLeftFieldState)
    {
        return currentLeftFieldState?.IsOutOfField == true
            && previousLeftFieldState?.IsOutOfField != true;
    }

    /// <summary>
    /// ball の現在位置が field perimeter の外か判定する。
    /// </summary>
    private static bool IsBallOutOfField(TrackedBallState ball, TrackerGeometrySnapshot geometrySnapshot)
    {
        var halfFieldLengthMm = geometrySnapshot.FieldLengthMm / 2d;
        var halfFieldWidthMm = geometrySnapshot.FieldWidthMm / 2d;
        return Math.Abs(ball.YMm) > halfFieldWidthMm || Math.Abs(ball.XMm) > halfFieldLengthMm;
    }

    /// <summary>
    /// 現在位置と直前 primary ball から最も妥当な boundary crossing を推定する。
    /// </summary>
    private static (string BoundaryName, double CrossingXMm, double CrossingYMm, long CrossingTimestampNs) ProjectBallCrossing(
        TrackedBallState ball,
        TrackedBallState? previousPrimaryBall,
        TrackerGeometrySnapshot geometrySnapshot)
    {
        var hasPreviousPoint = previousPrimaryBall is not null && previousPrimaryBall.InternalTrackId == ball.InternalTrackId;
        if (hasPreviousPoint && previousPrimaryBall is not null)
        {
            var firstCrossing = TryProjectFirstPerimeterCrossing(ball, previousPrimaryBall, geometrySnapshot);
            if (firstCrossing is not null)
            {
                return firstCrossing.Value;
            }
        }

        var fallbackBoundaryName = ClassifyBoundaryNameFromCurrentPosition(ball, geometrySnapshot);
        return fallbackBoundaryName switch
        {
            "touch-line" => ProjectTouchLineCrossing(ball, previousPrimaryBall, geometrySnapshot, hasPreviousPoint),
            _ => ProjectGoalLineCrossing(ball, previousPrimaryBall, geometrySnapshot, hasPreviousPoint, fallbackBoundaryName),
        };
    }

    private static string ClassifyBoundaryNameFromCurrentPosition(TrackedBallState ball, TrackerGeometrySnapshot geometrySnapshot)
    {
        var halfGoalWidthMm = geometrySnapshot.GoalWidthMm / 2d;
        if (Math.Abs(ball.YMm) > geometrySnapshot.FieldWidthMm / 2d)
        {
            return "touch-line";
        }

        return Math.Abs(ball.YMm) <= halfGoalWidthMm
            ? "goal-interior"
            : "goal-line";
    }

    private static (string BoundaryName, double CrossingXMm, double CrossingYMm, long CrossingTimestampNs)? TryProjectFirstPerimeterCrossing(
        TrackedBallState ball,
        TrackedBallState previousPrimaryBall,
        TrackerGeometrySnapshot geometrySnapshot)
    {
        var halfFieldLengthMm = geometrySnapshot.FieldLengthMm / 2d;
        var halfFieldWidthMm = geometrySnapshot.FieldWidthMm / 2d;
        var halfGoalWidthMm = geometrySnapshot.GoalWidthMm / 2d;
        var candidates = new List<(double Ratio, string BoundaryName, double CrossingXMm, double CrossingYMm, long CrossingTimestampNs)>();

        if (Math.Abs(ball.XMm) > halfFieldLengthMm && Math.Abs(ball.XMm - previousPrimaryBall.XMm) >= double.Epsilon)
        {
            var boundaryXMm = Math.Sign(ball.XMm) * halfFieldLengthMm;
            var ratio = (boundaryXMm - previousPrimaryBall.XMm) / (ball.XMm - previousPrimaryBall.XMm);
            if (ratio >= 0d && ratio <= 1d)
            {
                var crossingYMm = previousPrimaryBall.YMm + ((ball.YMm - previousPrimaryBall.YMm) * ratio);
                if (Math.Abs(crossingYMm) <= halfFieldWidthMm)
                {
                    candidates.Add((
                        ratio,
                        Math.Abs(crossingYMm) <= halfGoalWidthMm ? "goal-interior" : "goal-line",
                        boundaryXMm,
                        crossingYMm,
                        InterpolateTimestamp(previousPrimaryBall.LastVisibleTimestampNs, ball.LastVisibleTimestampNs, ratio)));
                }
            }
        }

        if (Math.Abs(ball.YMm) > halfFieldWidthMm && Math.Abs(ball.YMm - previousPrimaryBall.YMm) >= double.Epsilon)
        {
            var boundaryYMm = Math.Sign(ball.YMm) * halfFieldWidthMm;
            var ratio = (boundaryYMm - previousPrimaryBall.YMm) / (ball.YMm - previousPrimaryBall.YMm);
            if (ratio >= 0d && ratio <= 1d)
            {
                var crossingXMm = previousPrimaryBall.XMm + ((ball.XMm - previousPrimaryBall.XMm) * ratio);
                if (Math.Abs(crossingXMm) <= halfFieldLengthMm)
                {
                    candidates.Add((
                        ratio,
                        "touch-line",
                        crossingXMm,
                        boundaryYMm,
                        InterpolateTimestamp(previousPrimaryBall.LastVisibleTimestampNs, ball.LastVisibleTimestampNs, ratio)));
                }
            }
        }

        return candidates.Count == 0
            ? null
            : candidates
                .OrderBy(candidate => candidate.Ratio)
                .Select(candidate => (candidate.BoundaryName, candidate.CrossingXMm, candidate.CrossingYMm, candidate.CrossingTimestampNs))
                .First();
    }

    private static (string BoundaryName, double CrossingXMm, double CrossingYMm, long CrossingTimestampNs) ProjectTouchLineCrossing(
        TrackedBallState ball,
        TrackedBallState? previousPrimaryBall,
        TrackerGeometrySnapshot geometrySnapshot,
        bool hasPreviousPoint)
    {
        var boundaryYMm = Math.Sign(ball.YMm) * (geometrySnapshot.FieldWidthMm / 2d);
        if (!hasPreviousPoint || previousPrimaryBall is null || Math.Abs(ball.YMm - previousPrimaryBall.YMm) < double.Epsilon)
        {
            return ("touch-line", ball.XMm, boundaryYMm, ball.LastVisibleTimestampNs);
        }

        var ratio = Math.Clamp(
            (boundaryYMm - previousPrimaryBall.YMm) / (ball.YMm - previousPrimaryBall.YMm),
            0d,
            1d);
        return (
            "touch-line",
            previousPrimaryBall.XMm + ((ball.XMm - previousPrimaryBall.XMm) * ratio),
            boundaryYMm,
            InterpolateTimestamp(previousPrimaryBall.LastVisibleTimestampNs, ball.LastVisibleTimestampNs, ratio));
    }

    private static (string BoundaryName, double CrossingXMm, double CrossingYMm, long CrossingTimestampNs) ProjectGoalLineCrossing(
        TrackedBallState ball,
        TrackedBallState? previousPrimaryBall,
        TrackerGeometrySnapshot geometrySnapshot,
        bool hasPreviousPoint,
        string boundaryName)
    {
        var boundaryXMm = Math.Sign(ball.XMm) * (geometrySnapshot.FieldLengthMm / 2d);
        if (!hasPreviousPoint || previousPrimaryBall is null || Math.Abs(ball.XMm - previousPrimaryBall.XMm) < double.Epsilon)
        {
            return (boundaryName, boundaryXMm, ball.YMm, ball.LastVisibleTimestampNs);
        }

        var ratio = Math.Clamp(
            (boundaryXMm - previousPrimaryBall.XMm) / (ball.XMm - previousPrimaryBall.XMm),
            0d,
            1d);
        return (
            boundaryName,
            boundaryXMm,
            previousPrimaryBall.YMm + ((ball.YMm - previousPrimaryBall.YMm) * ratio),
            InterpolateTimestamp(previousPrimaryBall.LastVisibleTimestampNs, ball.LastVisibleTimestampNs, ratio));
    }

    private static long InterpolateTimestamp(long startTimestampNs, long endTimestampNs, double ratio)
    {
        return startTimestampNs + (long)Math.Round((endTimestampNs - startTimestampNs) * ratio);
    }
}
