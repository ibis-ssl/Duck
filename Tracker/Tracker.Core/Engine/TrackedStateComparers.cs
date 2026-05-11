namespace Tracker.Core;

/// <summary>
/// Core tracker engine の責務別 partial 実装。
/// </summary>
public sealed partial class TrackerEngine
{
    /// <summary>
    /// ball 出力を visibility、last visible timestamp、internal track id の安定順で比較する。
    /// </summary>
    private sealed class TrackedBallComparer : IComparer<TrackedBallState>
    {
        /// <summary>
        /// comparer の singleton instance。
        /// </summary>
        public static TrackedBallComparer Instance { get; } = new();

        /// <summary>
        /// tracked ball の出力順を比較する。
        /// </summary>
        public int Compare(TrackedBallState? x, TrackedBallState? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var visibilityComparison = y.Visibility.CompareTo(x.Visibility);
            if (visibilityComparison != 0)
            {
                return visibilityComparison;
            }

            var timestampComparison = y.LastVisibleTimestampNs.CompareTo(x.LastVisibleTimestampNs);
            if (timestampComparison != 0)
            {
                return timestampComparison;
            }

            return x.InternalTrackId.CompareTo(y.InternalTrackId);
        }
    }

    /// <summary>
    /// robot 出力を team、robot id の安定順で比較する。
    /// </summary>
    private sealed class TrackedRobotComparer : IComparer<TrackedRobotState>
    {
        /// <summary>
        /// comparer の singleton instance。
        /// </summary>
        public static TrackedRobotComparer Instance { get; } = new();

        /// <summary>
        /// tracked robot の出力順を比較する。
        /// </summary>
        public int Compare(TrackedRobotState? x, TrackedRobotState? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var teamComparison = x.Team.CompareTo(y.Team);
            if (teamComparison != 0)
            {
                return teamComparison;
            }

            return x.RobotId.CompareTo(y.RobotId);
        }
    }
}
