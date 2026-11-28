using Tracker.Core;
using Tracker.Diagnostics.Mcap;
using Tracker.DebugHost.Tracking;

namespace Tracker.CaptureReplay;

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
        int maxDetails,
        int maxDetailRobots,
        string? metadataPath = null,
        bool analyzeLatency = false,
        int maxLatencyFrames = 40,
        bool includeTrackerSnapshots = true)
    {
        var engine = new TrackerEngine();
        var latencyAnalyzer = analyzeLatency ? new ReplayLatencyAnalyzer(settings, maxLatencyFrames) : null;
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
            latencyAnalyzer?.RecordInput(packetCount, record, packet);

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
                latencyAnalyzer?.RecordCommittedFrame(packetCount, record.ReceivedAt, frame);
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

                detailFrames.Add(ReplayFrameFormatter.FormatFrame(packetCount, record.ReceivedAt, frame, maxDetailRobots));
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
            Math.Max(0, matchingDetailFrameCount - detailFrames.Count),
            includeTrackerSnapshots ? TrackerSnapshotReplayLineFormatter.ReadLines(metadataPath) : [],
            latencyAnalyzer?.FormatLines() ?? []);
    }

    /// <summary>Replay only inputs admitted to the engine, in their recorded order and settings generation.</summary>
    public static ReplaySummary Run(
        RecordingMcapSnapshot recording,
        IReadOnlyList<Condition> detailFilters,
        int maxDetails,
        int maxDetailRobots)
    {
        ArgumentNullException.ThrowIfNull(recording);
        RecordingMcapReplayContractValidator.Validate(recording);
        var settingsByGeneration = RecordingMcapSettingsValidator.ReadAll(recording);
        var initialConfiguration = recording.EffectiveConfigurations[0];
        var initialSettings = settingsByGeneration[initialConfiguration.GenerationId];
        var engine = new TrackerEngine();
        if (initialConfiguration.InitialGeometryBase64 is { } initialGeometry)
        {
            var geometryBytes = Convert.FromBase64String(initialGeometry);
            SslVisionGeometryValidator.Validate(geometryBytes);
            _ = engine.Update(new SSL_WrapperPacket { Geometry = SSL_GeometryData.Parser.ParseFrom(geometryBytes) }, initialSettings);
        }

        var rawBySequence = recording.RawPackets.ToDictionary(packet => packet.Sequence);
        var contextsByOrder = recording.PacketContexts
            .Where(context => context.EngineUpdateOrder is not null)
            .ToDictionary(context => context.EngineUpdateOrder!.Value);
        var eventsByOrder = recording.ControlEvents
            .GroupBy(controlEvent => controlEvent.EngineUpdateOrder)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var updateOrders = contextsByOrder.Keys.Concat(eventsByOrder.Keys).Distinct().Order();

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

        foreach (var updateOrder in updateOrders)
        {
            var orderEvents = eventsByOrder.GetValueOrDefault(updateOrder, []);
            var context = contextsByOrder.GetValueOrDefault(updateOrder);
            var generationId = context?.ConfigurationGenerationId
                ?? orderEvents.FirstOrDefault()?.ConfigurationGenerationId
                ?? throw new InvalidDataException($"Recording MCAP update order {updateOrder} has no settings generation.");
            if (!settingsByGeneration.TryGetValue(generationId, out var settings))
            {
                throw new InvalidDataException($"Recording MCAP has no settings for generation '{generationId}'.");
            }

            SSL_WrapperPacket? packet = null;
            RecordingMcapRawPacket? rawRecord = null;
            if (context is not null)
            {
                if (!context.DecodeSucceeded || !context.TrackingEnabled)
                {
                    throw new InvalidDataException($"Recording MCAP update order {updateOrder} does not reference a tracked input.");
                }

                rawRecord = rawBySequence[context.PacketSequence];
                _ = SslVisionGeometryValidator.ExtractWrapperPacketGeometry(rawRecord.Payload);
                packet = SSL_WrapperPacket.Parser.ParseFrom(rawRecord.Payload);
                packetCount++;
            }
            else
            {
                var geometryUpdate = orderEvents.SingleOrDefault(item => item.EventKind == "geometry-updated");
                if (geometryUpdate is not null)
                {
                    var geometryBytes = Convert.FromBase64String(geometryUpdate.Payload.GetProperty("protobufBase64").GetString()!);
                    SslVisionGeometryValidator.Validate(geometryBytes);
                    packet = new SSL_WrapperPacket { Geometry = SSL_GeometryData.Parser.ParseFrom(geometryBytes) };
                }
            }

            if (packet?.Detection is not null)
            {
                detectionCount++;
            }

            if (packet?.Geometry is not null)
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
                if (detailFrames.Count < maxDetails)
                {
                    detailFrames.Add(ReplayFrameFormatter.FormatFrame(
                        packetCount,
                        rawRecord?.ReceivedAtUtc ?? recording.Manifest.StartedAtUtc,
                        frame,
                        maxDetailRobots));
                }
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
            Math.Max(0, matchingDetailFrameCount - detailFrames.Count),
            [],
            []);
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
                "frame" => ToMetricValue(frame.FrameNumber),
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

    private static int ToMetricValue(uint value)
    {
        return value > int.MaxValue ? int.MaxValue : (int)value;
    }
}

/// <summary>
/// Capture metadata から tracker snapshot replay 情報を読み、既存 CLI に合わせた key=value 行へ整形する。
/// </summary>
internal static class TrackerSnapshotReplayLineFormatter
{
    /// <summary>
    /// metadata path が tracker snapshot sidecar を解決できる場合だけ snapshot / comparison 表示行を返す。
    /// </summary>
    public static IReadOnlyList<string> ReadLines(string? metadataPath)
    {
        if (string.IsNullOrWhiteSpace(metadataPath) || !File.Exists(metadataPath))
        {
            return [];
        }

        var session = new TrackerSnapshotReplayReader().ReadSession(metadataPath);
        if (session.SnapshotInputs.Count == 0 && session.ComparisonSummaries.Count == 0)
        {
            return [];
        }

        var lines = new List<string>();
        foreach (var input in session.SnapshotInputs)
        {
            var semanticSummary = input.ComparisonSource.SemanticSummary;
            lines.Add(
                $"trackerSnapshot source={input.SourceLabel} role={input.SourceRole} trackedFrame={input.TrackedFrameNumber} trackedTs={input.TrackedFrameTimestampNs} balls={semanticSummary.BallCount} robots={semanticSummary.RobotCount} rawPayloadRestored={input.ComparisonSource.RawPayloadRestored}");
        }

        foreach (var summary in session.ComparisonSummaries)
        {
            lines.Add(
                $"trackerComparison rule={summary.MatchingRule} ibisTs={summary.IbisDiagnosticsTimestampNs} source={summary.NearestSnapshotSourceLabel} role={summary.NearestSnapshotSourceRole} nearestTs={summary.NearestSnapshotTimestampNs} balls={summary.NearestSnapshotBallCount} robots={summary.NearestSnapshotRobotCount} rawPayloadRestored={summary.NearestSnapshotRawPayloadRestored}");
        }

        return lines;
    }
}
