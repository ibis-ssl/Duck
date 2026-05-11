namespace Tracker.Core;

/// <summary>
/// Core tracker engine の責務別 partial 実装。
/// </summary>
public sealed partial class TrackerEngine
{
    /// <summary>
    /// 1 軸の Kalman state を観測位置で初期化する。
    /// </summary>
    private static KalmanAxisState CreateInitialKalmanAxis(
        TrackerEngineSettings settings,
        double position,
        double measurementVariance)
    {
        return new KalmanAxisState(
            position,
            0d,
            measurementVariance,
            GetInitialVelocityVariance(settings));
    }

    /// <summary>
    /// 等速直線運動前提で 1 軸の Kalman state を予測する。
    /// </summary>
    private static KalmanAxisState PredictKalmanAxis(
        TrackerEngineSettings settings,
        KalmanAxisState state,
        double deltaSeconds,
        double processNoise)
    {
        var processVariance = processNoise * GetKalmanProcessNoiseScale(settings);
        return state with
        {
            Position = state.Position + state.Velocity * deltaSeconds,
            PositionVariance = state.PositionVariance
                + (deltaSeconds * deltaSeconds * state.VelocityVariance)
                + (processVariance * deltaSeconds * deltaSeconds),
            VelocityVariance = state.VelocityVariance + processVariance,
        };
    }

    /// <summary>
    /// predicted state と measurement から 1 軸の Kalman state を更新する。
    /// </summary>
    private static KalmanAxisState UpdateKalmanAxis(
        KalmanAxisState predictedState,
        double previousPosition,
        double measurement,
        double deltaSeconds,
        double measurementVariance)
    {
        var innovationVariance = predictedState.PositionVariance + measurementVariance;
        if (innovationVariance <= 0d)
        {
            return predictedState;
        }

        var gain = predictedState.PositionVariance / innovationVariance;
        // 速度観測は previous position と measurement から計算し、位置補正は predicted state を基準にする。
        var observedVelocity = deltaSeconds > 0d
            ? (measurement - previousPosition) / deltaSeconds
            : predictedState.Velocity;
        if (gain >= 0.9999d)
        {
            return predictedState with
            {
                Position = measurement,
                Velocity = observedVelocity,
                PositionVariance = Math.Max(0.001d, (1d - gain) * predictedState.PositionVariance),
                VelocityVariance = Math.Max(0.001d, (1d - gain) * predictedState.VelocityVariance),
            };
        }

        return predictedState with
        {
            Position = predictedState.Position + gain * (measurement - predictedState.Position),
            Velocity = predictedState.Velocity + gain * (observedVelocity - predictedState.Velocity),
            PositionVariance = Math.Max(0.001d, (1d - gain) * predictedState.PositionVariance),
            VelocityVariance = Math.Max(0.001d, (1d - gain) * predictedState.VelocityVariance),
        };
    }

    /// <summary>
    /// 1 軸の位置、速度、それぞれの分散を保持する Kalman state。
    /// </summary>
    private readonly record struct KalmanAxisState(
        double Position,
        double Velocity,
        double PositionVariance,
        double VelocityVariance);
}
