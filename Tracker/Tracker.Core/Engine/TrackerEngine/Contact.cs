namespace Tracker.Core;

/// <summary>
/// Core tracker engine の責務別 partial 実装。
/// </summary>
public sealed partial class TrackerEngine
{
    /// <summary>
    /// primary ball と fresh robot 観測から contact 状態を作る。
    /// </summary>
    private static BallContactState? CreateBallContactState(
        TrackerEngineSettings settings,
        TrackedBallState? primaryBall,
        IReadOnlyList<TrackedRobotState> robots,
        ISet<RobotKey> freshRobotKeys,
        long frameTimestampNs,
        BallContactState? previousContactState)
    {
        var contactMarginMm = GetContactMarginMm(settings);
        var currentContact = primaryBall is null
            ? null
            : robots
                .Where(robot => freshRobotKeys.Contains(new RobotKey(robot.Team, robot.RobotId)))
                .Select(
                    robot => new
                    {
                        Robot = robot,
                        DistanceMm = GetDistanceMm(robot.XMm, robot.YMm, primaryBall.XMm, primaryBall.YMm),
                    })
                .Where(candidate => candidate.DistanceMm <= RobotRadiusMm + BallRadiusMm + contactMarginMm)
                .OrderBy(candidate => candidate.DistanceMm)
                .ThenBy(candidate => candidate.Robot.Team)
                .ThenBy(candidate => candidate.Robot.RobotId)
                .FirstOrDefault();
        if (currentContact is not null)
        {
            return new BallContactState
            {
                IsInContact = true,
                ContactingRobotId = currentContact.Robot.RobotId,
                ContactingTeam = currentContact.Robot.Team,
                LastRobotId = currentContact.Robot.RobotId,
                LastTeam = currentContact.Robot.Team,
                LastContactTimestampNs = frameTimestampNs,
            };
        }

        if (previousContactState is null)
        {
            return null;
        }

        return new BallContactState
        {
            IsInContact = false,
            ContactingRobotId = null,
            ContactingTeam = TrackerTeam.Unknown,
            LastRobotId = previousContactState.LastRobotId,
            LastTeam = previousContactState.LastTeam,
            LastContactTimestampNs = previousContactState.LastContactTimestampNs,
        };
    }

    /// <summary>
    /// contact state を robot DTO の HasBallContact flag へ反映する。
    /// </summary>
    private static List<TrackedRobotState> ApplyBallContactFlags(
        IReadOnlyList<TrackedRobotState> robots,
        BallContactState? contactState)
    {
        return robots
            .Select(
                robot => new TrackedRobotState
                {
                    Team = robot.Team,
                    RobotId = robot.RobotId,
                    XMm = robot.XMm,
                    YMm = robot.YMm,
                    OrientationRad = robot.OrientationRad,
                    VXMmPerS = robot.VXMmPerS,
                    VYMmPerS = robot.VYMmPerS,
                    AngularVelocityRadPerS = robot.AngularVelocityRadPerS,
                    Visibility = robot.Visibility,
                    Quality = robot.Quality,
                    HasBallContact = contactState is not null
                        && contactState.IsInContact
                        && contactState.ContactingTeam == robot.Team
                        && contactState.ContactingRobotId == robot.RobotId,
                })
            .ToList();
    }

    /// <summary>
    /// primary ball track id ごとの最新 contact state を更新する。
    /// </summary>
    private void UpdateLatestBallContactState(
        TrackedBallState? primaryBall,
        BallContactState? contactState)
    {
        if (primaryBall is null)
        {
            return;
        }

        if (contactState is null)
        {
            latestBallContactStates.Remove(primaryBall.InternalTrackId);
            return;
        }

        latestBallContactStates[primaryBall.InternalTrackId] = contactState;
    }

    /// <summary>
    /// 出力対象から消えた ball の contact state を破棄する。
    /// </summary>
    private void PruneLatestBallContactStates(IReadOnlyList<TrackedBallState> balls)
    {
        var activeTrackIds = balls
            .Select(ball => ball.InternalTrackId)
            .ToHashSet();
        foreach (var staleTrackId in latestBallContactStates.Keys.Except(activeTrackIds).ToList())
        {
            latestBallContactStates.Remove(staleTrackId);
        }
    }


    /// <summary>
    /// contact changed event を出すべき状態差分か判定する。
    /// </summary>
    private static bool DidBallContactChange(
        BallContactState? previousContactState,
        BallContactState? currentContactState)
    {
        if (previousContactState is null || currentContactState is null)
        {
            return previousContactState is not null || currentContactState is not null;
        }

        return previousContactState.IsInContact != currentContactState.IsInContact
            || previousContactState.ContactingTeam != currentContactState.ContactingTeam
            || previousContactState.ContactingRobotId != currentContactState.ContactingRobotId;
    }
}
