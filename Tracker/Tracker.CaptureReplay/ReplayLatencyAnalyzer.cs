using System.Globalization;
using Tracker.Core;

namespace Tracker.CaptureReplay;

/// <summary>
/// raw vision packet cadence と replay 後の ibis tracker commit lag を同じ時刻軸で分析する。
/// </summary>
internal sealed class ReplayLatencyAnalyzer
{
    private readonly TrackerEngineSettings settings;
    private readonly int maxLatencyFrames;
    private readonly List<RawObservation> rawObservations = [];
    private readonly Dictionary<RawObservationKey, RawObservation> rawObservationByKey = [];
    private readonly List<CommitLatency> committedFrames = [];

    /// <summary>
    /// 分析時に使った engine settings を summary 行へ含めるため保持する。
    /// </summary>
    public ReplayLatencyAnalyzer(TrackerEngineSettings settings, int maxLatencyFrames)
    {
        this.settings = settings;
        this.maxLatencyFrames = Math.Max(0, maxLatencyFrames);
    }

    /// <summary>
    /// replay 入力 packet の raw detection cadence を記録する。
    /// </summary>
    public void RecordInput(int packetIndex, VisionPacketCaptureRecord record, SSL_WrapperPacket packet)
    {
        if (packet.Detection is null)
        {
            return;
        }

        var detection = packet.Detection;
        var observation = new RawObservation(
            packetIndex,
            detection.FrameNumber,
            detection.CameraId,
            ToEventTimestampNs(detection),
            record.ReceivedAt);
        rawObservations.Add(observation);
        rawObservationByKey[observation.Key] = observation;
    }

    /// <summary>
    /// tracker が commit した frame と、その frame を emit した入力 packet の時刻差を記録する。
    /// </summary>
    public void RecordCommittedFrame(int packetIndex, DateTimeOffset commitReceivedAt, TrackerFrame frame)
    {
        var sourceObservations = frame.SourceDetections
            .Select(source => rawObservationByKey.GetValueOrDefault(new RawObservationKey(
                source.SourceFrameNumber,
                source.CameraId,
                source.EventTimestampNs)))
            .Where(observation => observation is not null)
            .Select(observation => observation!)
            .ToArray();
        if (sourceObservations.Length == 0)
        {
            return;
        }

        var latestSource = sourceObservations
            .OrderByDescending(observation => observation.ReceivedAt)
            .ThenByDescending(observation => observation.PacketIndex)
            .First();
        var sourceFrameNumbers = string.Join(
            "/",
            sourceObservations
                .Select(observation => observation.FrameNumber)
                .Distinct()
                .Order()
                .Select(frameNumber => frameNumber.ToString(CultureInfo.InvariantCulture)));
        var sourceCameraIds = string.Join(
            "/",
            sourceObservations
                .Select(observation => observation.CameraId)
                .Distinct()
                .Order()
                .Select(cameraId => cameraId.ToString(CultureInfo.InvariantCulture)));

        committedFrames.Add(new CommitLatency(
            packetIndex,
            frame.FrameNumber,
            frame.DataTimestampNs,
            sourceFrameNumbers,
            sourceCameraIds,
            latestSource.ReceivedAt,
            commitReceivedAt,
            packetIndex - latestSource.PacketIndex));
    }

    /// <summary>
    /// CLI に出す summary/detail 行を安定した key=value 形式へ変換する。
    /// </summary>
    public IReadOnlyList<string> FormatLines()
    {
        var lines = new List<string>
        {
            FormatSummaryLine(),
        };
        lines.AddRange(committedFrames.Take(maxLatencyFrames).Select(FormatFrameLine));
        var omittedFrameCount = committedFrames.Count - Math.Min(committedFrames.Count, maxLatencyFrames);
        if (omittedFrameCount > 0)
        {
            lines.Add($"latencyOmittedFrames count={omittedFrameCount}");
        }

        return lines;
    }

    private string FormatSummaryLine()
    {
        var rawAverageDeltaMs = CalculateAverageDeltaMilliseconds(rawObservations.Select(observation => observation.ReceivedAt));
        var committedAverageDeltaMs = CalculateAverageDeltaMilliseconds(committedFrames.Select(frame => frame.CommitReceivedAt));
        var averageCommitLagMs = committedFrames.Count == 0
            ? 0.0d
            : committedFrames.Average(frame => frame.CommitLag.TotalMilliseconds);
        var maxCommitLagMs = committedFrames.Count == 0
            ? 0.0d
            : committedFrames.Max(frame => frame.CommitLag.TotalMilliseconds);
        var maxCommitLagInputs = committedFrames.Count == 0
            ? 0
            : committedFrames.Max(frame => frame.CommitLagInputs);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"latencySummary rawDetections={rawObservations.Count} committedFrames={committedFrames.Count} rawAvgDeltaMs={rawAverageDeltaMs:F3} committedAvgDeltaMs={committedAverageDeltaMs:F3} avgCommitLagMs={averageCommitLagMs:F3} maxCommitLagMs={maxCommitLagMs:F3} maxCommitLagInputs={maxCommitLagInputs} reorderWindowMs={settings.ReorderWindowNs / 1_000_000.0d:F3} mergeWindowMs={settings.MergeWindowNs / 1_000_000.0d:F3}");
    }

    private static string FormatFrameLine(CommitLatency frame)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"latencyFrame input={frame.PacketIndex} committedFrame={frame.FrameNumber} rawFrame={frame.SourceFrameNumbers} rawCamera={frame.SourceCameraIds} sourceReceivedAt={frame.SourceReceivedAt:O} commitReceivedAt={frame.CommitReceivedAt:O} commitLagMs={frame.CommitLag.TotalMilliseconds:F3} commitLagInputs={frame.CommitLagInputs} dataTs={frame.DataTimestampNs}");
    }

    private static double CalculateAverageDeltaMilliseconds(IEnumerable<DateTimeOffset> timestamps)
    {
        var ordered = timestamps.Order().ToArray();
        if (ordered.Length < 2)
        {
            return 0.0d;
        }

        var totalMilliseconds = 0.0d;
        for (var i = 1; i < ordered.Length; i++)
        {
            totalMilliseconds += (ordered[i] - ordered[i - 1]).TotalMilliseconds;
        }

        return totalMilliseconds / (ordered.Length - 1);
    }

    private static long ToEventTimestampNs(SSL_DetectionFrame detection)
    {
        var seconds = detection.TCapture > 0 ? detection.TCapture : detection.TSent;
        return (long)Math.Round(seconds * 1_000_000_000d, MidpointRounding.AwayFromZero);
    }

    private sealed record RawObservation(
        int PacketIndex,
        uint FrameNumber,
        uint CameraId,
        long EventTimestampNs,
        DateTimeOffset ReceivedAt)
    {
        public RawObservationKey Key { get; } = new(FrameNumber, CameraId, EventTimestampNs);
    }

    private readonly record struct RawObservationKey(
        uint FrameNumber,
        uint CameraId,
        long EventTimestampNs);

    private sealed record CommitLatency(
        int PacketIndex,
        uint FrameNumber,
        long DataTimestampNs,
        string SourceFrameNumbers,
        string SourceCameraIds,
        DateTimeOffset SourceReceivedAt,
        DateTimeOffset CommitReceivedAt,
        int CommitLagInputs)
    {
        public TimeSpan CommitLag => CommitReceivedAt - SourceReceivedAt;
    }
}
