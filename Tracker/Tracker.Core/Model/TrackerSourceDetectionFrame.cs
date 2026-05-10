namespace Tracker.Core;

/// <summary>
/// committed frame に統合された raw detection frame の snapshot。
/// diagnostics と replay 境界で、入力観測と tracked 出力を突き合わせるために使う。
/// </summary>
public sealed class TrackerSourceDetectionFrame
{
    /// <summary>
    /// 元 SSL_DetectionFrame の frame number。
    /// </summary>
    public uint SourceFrameNumber { get; init; }

    /// <summary>
    /// 元 SSL_DetectionFrame の camera id。
    /// </summary>
    public uint CameraId { get; init; }

    /// <summary>
    /// engine が採用した event timestamp。単位は ns。
    /// </summary>
    public long EventTimestampNs { get; init; }

    /// <summary>
    /// 元 frame に含まれる ball detection の clone 一覧。
    /// </summary>
    public IReadOnlyList<SSL_DetectionBall> Balls { get; init; } = [];

    /// <summary>
    /// 元 frame に含まれる yellow robot detection の clone 一覧。
    /// </summary>
    public IReadOnlyList<SSL_DetectionRobot> RobotsYellow { get; init; } = [];

    /// <summary>
    /// 元 frame に含まれる blue robot detection の clone 一覧。
    /// </summary>
    public IReadOnlyList<SSL_DetectionRobot> RobotsBlue { get; init; } = [];
}
