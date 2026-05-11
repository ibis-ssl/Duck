using Tracker.Core;

/// <summary>
/// 保存済み vision capture を tracker engine に順序通り再投入し、summary と detail 行を作る。
/// </summary>
internal static class CaptureReplayRunner
{
    /// <summary>
    /// capture file の packet を全件 replay し、既存 CLI 出力で使う集計値を返す。
    /// </summary>
    public static ReplaySummary Run(
        string capturePath,
        TrackerEngineSettings settings,
        IReadOnlyList<Condition> detailFilters,
        int maxDetails)
    {
        var engine = new TrackerEngine();
        var packetCount = 0;
        var detectionCount = 0;
        var geometryCount = 0;
        var committedFrameCount = 0;
        var maxBallCount = 0;
        var maxRobotCount = 0;
        var maxRawBallCount = 0;
        var maxRawYellowCount = 0;
        var maxRawBlueCount = 0;
        var matchingDetailFrameCount = 0;
        var detailFrames = new List<string>();

        foreach (var record in VisionPacketCaptureReader.ReadRecords(capturePath))
        {
            packetCount++;
            var packet = record.ParsePacket();

            if (packet.Detection is not null)
            {
                detectionCount++;
            }

            if (packet.Geometry is not null)
            {
                geometryCount++;
            }

            var result = engine.Update(packet, settings);
            foreach (var frame in result.CommittedFrames)
            {
                var rawBallCount = ReplayFrameFormatter.CountRawBalls(frame);
                var rawYellowCount = ReplayFrameFormatter.CountRawYellowRobots(frame);
                var rawBlueCount = ReplayFrameFormatter.CountRawBlueRobots(frame);
                committedFrameCount++;
                maxBallCount = Math.Max(maxBallCount, frame.Balls.Count);
                maxRobotCount = Math.Max(maxRobotCount, frame.Robots.Count);
                maxRawBallCount = Math.Max(maxRawBallCount, rawBallCount);
                maxRawYellowCount = Math.Max(maxRawYellowCount, rawYellowCount);
                maxRawBlueCount = Math.Max(maxRawBlueCount, rawBlueCount);

                if (!MatchesDetailFilters(detailFilters, rawBallCount, rawYellowCount, rawBlueCount, frame))
                {
                    continue;
                }

                matchingDetailFrameCount++;
                if (detailFrames.Count >= maxDetails)
                {
                    continue;
                }

                detailFrames.Add(ReplayFrameFormatter.FormatFrame(packetCount, record.ReceivedAt, frame));
            }
        }

        return new ReplaySummary(
            packetCount,
            detectionCount,
            geometryCount,
            committedFrameCount,
            maxBallCount,
            maxRobotCount,
            maxRawBallCount,
            maxRawYellowCount,
            maxRawBlueCount,
            detailFrames,
            Math.Max(0, matchingDetailFrameCount - detailFrames.Count));
    }

    private static bool MatchesDetailFilters(
        IReadOnlyList<Condition> detailFilters,
        int rawBallCount,
        int rawYellowCount,
        int rawBlueCount,
        TrackerFrame frame)
    {
        if (detailFilters.Count == 0)
        {
            return false;
        }

        foreach (var condition in detailFilters)
        {
            var metricValue = condition.Metric switch
            {
                "balls" => frame.Balls.Count,
                "robots" => frame.Robots.Count,
                "raw-balls" => rawBallCount,
                "raw-yellow" => rawYellowCount,
                "raw-blue" => rawBlueCount,
                _ => throw new InvalidOperationException($"Unsupported detail metric '{condition.Metric}'."),
            };

            if (!condition.Evaluate(metricValue))
            {
                return false;
            }
        }

        return true;
    }
}
