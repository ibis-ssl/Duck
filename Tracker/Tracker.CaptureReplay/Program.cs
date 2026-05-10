using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tracker.Core;

var options = ReplayOptions.Parse(args);
if (options.ShowHelp)
{
    ReplayOptions.WriteUsage();
    return 0;
}

if (options.Error is not null)
{
    Console.Error.WriteLine(options.Error);
    ReplayOptions.WriteUsage();
    return 2;
}

var settings = TrackerSettingsFactory.Create(options.ProfileName, options.SettingsPath, options.SettingsOverrides);
var summary = CaptureReplayRunner.Run(options.CapturePath!, settings, options.DetailFilters, options.MaxDetails);

Console.WriteLine($"capture={options.CapturePath}");
Console.WriteLine($"settingsFile={options.SettingsPath ?? "(built-in defaults)"}");
Console.WriteLine($"profile={options.ProfileName}");
Console.WriteLine(
    $"settings=reorderWindowNs={settings.ReorderWindowNs} mergeWindowNs={settings.MergeWindowNs} ballGate={settings.BallTracker.Gate} ballOutlierLimitMm={settings.BallTracker.OutlierLimitMm} ballOutputVisibility={settings.BallTracker.OutputVisibilityThreshold} ballTrackLifetimeNs={settings.BallTracker.TrackLifetimeNs}");
Console.WriteLine(
    $"packets={summary.PacketCount} detections={summary.DetectionCount} geometries={summary.GeometryCount} committedFrames={summary.CommittedFrameCount}");
Console.WriteLine(
    $"maxBalls={summary.MaxBallCount} maxRobots={summary.MaxRobotCount} maxRawBalls={summary.MaxRawBallCount} maxRawYellow={summary.MaxRawYellowCount} maxRawBlue={summary.MaxRawBlueCount}");

foreach (var frame in summary.DetailFrames)
{
    Console.WriteLine(frame);
}

if (summary.OmittedDetailFrameCount > 0)
{
    Console.WriteLine($"... omitted {summary.OmittedDetailFrameCount} detail frames");
}

var failedExpectations = new List<Condition>();
foreach (var expectation in options.Expectations)
{
    var actual = summary.GetMetric(expectation.Metric);
    var passed = expectation.Evaluate(actual);
    Console.WriteLine($"expect {expectation}: actual={actual} result={(passed ? "ok" : "failed")}");
    if (!passed)
    {
        failedExpectations.Add(expectation);
    }
}

return failedExpectations.Count == 0 ? 0 : 1;

internal static class CaptureReplayRunner
{
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
            var rawBallCount = packet.Detection?.Balls.Count ?? 0;
            var rawYellowCount = packet.Detection?.RobotsYellow.Count ?? 0;
            var rawBlueCount = packet.Detection?.RobotsBlue.Count ?? 0;

            maxRawBallCount = Math.Max(maxRawBallCount, rawBallCount);
            maxRawYellowCount = Math.Max(maxRawYellowCount, rawYellowCount);
            maxRawBlueCount = Math.Max(maxRawBlueCount, rawBlueCount);

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
                committedFrameCount++;
                maxBallCount = Math.Max(maxBallCount, frame.Balls.Count);
                maxRobotCount = Math.Max(maxRobotCount, frame.Robots.Count);

                if (!MatchesDetailFilters(detailFilters, rawBallCount, rawYellowCount, rawBlueCount, frame))
                {
                    continue;
                }

                matchingDetailFrameCount++;
                if (detailFrames.Count >= maxDetails)
                {
                    continue;
                }

                detailFrames.Add(FormatFrame(packetCount, record.ReceivedAt, packet, frame));
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

    private static string FormatFrame(
        int packetIndex,
        DateTimeOffset receivedAt,
        SSL_WrapperPacket packet,
        TrackerFrame frame)
    {
        var detection = packet.Detection;
        var rawSummary = detection is null
            ? "rawFrame=- rawCamera=- rawBalls=0 rawYellow=0 rawBlue=0"
            : $"rawFrame={detection.FrameNumber} rawCamera={detection.CameraId} rawBalls={detection.Balls.Count} rawYellow={detection.RobotsYellow.Count} rawBlue={detection.RobotsBlue.Count}";
        var balls = string.Join("; ", frame.Balls.Select(ball =>
            $"#{ball.InternalTrackId}:x={ball.XMm.ToString("F1", CultureInfo.InvariantCulture)},y={ball.YMm.ToString("F1", CultureInfo.InvariantCulture)},vis={ball.Visibility.ToString("F3", CultureInfo.InvariantCulture)},cams={string.Join("/", ball.SourceCameraIds.OrderBy(id => id))}"));
        var robots = string.Join("; ", frame.Robots.Take(8).Select(robot =>
            $"{robot.Team}{robot.RobotId}:x={robot.XMm.ToString("F1", CultureInfo.InvariantCulture)},y={robot.YMm.ToString("F1", CultureInfo.InvariantCulture)},vis={robot.Visibility.ToString("F3", CultureInfo.InvariantCulture)}"));
        var robotSuffix = frame.Robots.Count > 8 ? $"; ... +{frame.Robots.Count - 8}" : "";

        return $"input={packetIndex} receivedAt={receivedAt:O} {rawSummary} committedFrame={frame.FrameNumber} dataTs={frame.DataTimestampNs} balls={frame.Balls.Count} [{balls}] robots={frame.Robots.Count} [{robots}{robotSuffix}]";
    }
}

internal sealed record ReplaySummary(
    int PacketCount,
    int DetectionCount,
    int GeometryCount,
    int CommittedFrameCount,
    int MaxBallCount,
    int MaxRobotCount,
    int MaxRawBallCount,
    int MaxRawYellowCount,
    int MaxRawBlueCount,
    IReadOnlyList<string> DetailFrames,
    int OmittedDetailFrameCount)
{
    public int GetMetric(string metric)
    {
        return metric switch
        {
            "packets" => PacketCount,
            "detections" => DetectionCount,
            "geometries" => GeometryCount,
            "committed-frames" => CommittedFrameCount,
            "max-balls" => MaxBallCount,
            "max-robots" => MaxRobotCount,
            "max-raw-balls" => MaxRawBallCount,
            "max-raw-yellow" => MaxRawYellowCount,
            "max-raw-blue" => MaxRawBlueCount,
            _ => throw new InvalidOperationException($"Unsupported summary metric '{metric}'."),
        };
    }
}

internal static class VisionPacketCaptureReader
{
    private const int SchemaVersion = 1;

    public static IEnumerable<VisionPacketCaptureRecord> ReadRecords(string path)
    {
        using var fileStream = File.OpenRead(path);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var record = JsonSerializer.Deserialize<CaptureRecordDto>(line)
                ?? throw new InvalidDataException("Vision packet capture record is empty.");
            if (record.SchemaVersion != SchemaVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported vision packet capture schema version '{record.SchemaVersion}'.");
            }

            yield return new VisionPacketCaptureRecord(
                DateTimeOffset.Parse(record.ReceivedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                record.RemoteEndpoint,
                Convert.FromBase64String(record.PayloadBase64));
        }
    }

    private sealed class CaptureRecordDto
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("receivedAt")]
        public string ReceivedAt { get; set; } = "";

        [JsonPropertyName("remoteEndpoint")]
        public string? RemoteEndpoint { get; set; }

        [JsonPropertyName("payloadBase64")]
        public string PayloadBase64 { get; set; } = "";
    }
}

internal sealed record VisionPacketCaptureRecord(
    DateTimeOffset ReceivedAt,
    string? RemoteEndpoint,
    byte[] Payload)
{
    public SSL_WrapperPacket ParsePacket()
    {
        return SSL_WrapperPacket.Parser.ParseFrom(Payload);
    }
}

internal static class TrackerSettingsFactory
{
    public static TrackerEngineSettings Create(
        string profileName,
        string? settingsPath,
        TrackerSettingOverrides overrides)
    {
        var settings = settingsPath is null
            ? CreateDefault(profileName)
            : CreateFromFile(profileName, settingsPath);

        return ApplyOverrides(settings, overrides);
    }

    private static TrackerEngineSettings CreateDefault(string profileName)
    {
        var gate = string.Equals(profileName, "fast", StringComparison.OrdinalIgnoreCase) ? 0.85d : 1.0d;
        return new TrackerEngineSettings
        {
            ProfileName = profileName,
            ReorderWindowNs = 100_000_000,
            MergeWindowNs = 20_000_000,
            GeometryResetFieldLengthThresholdMm = 500,
            GeometryResetFieldWidthThresholdMm = 500,
            RobotTracker = new TrackerRobotTrackerOverrides
            {
                ProcessNoise = 0.1,
                MeasurementNoise = 20.0,
                VisibilityHalfLifeSeconds = 0.462756,
                OutputVisibilityThreshold = 0.05,
                Gate = gate,
                OutlierLimitMm = 120.0,
            },
            BallTracker = new TrackerBallTrackerOverrides
            {
                ProcessNoise = 0.1,
                MeasurementNoise = 100.0,
                VisibilityHalfLifeSeconds = 1.0,
                OutputVisibilityThreshold = 0.0,
                Gate = gate,
                OutlierLimitMm = 120.0,
                TrackLifetimeNs = 1_000_000_000,
            },
            KickDetector = new TrackerKickDetectorOverrides
            {
                KickSpeedThresholdMmPerS = 800.0,
                ChipHeightThresholdMm = 120.0,
                ContactMarginMm = 25.0,
            },
        };
    }

    private static TrackerEngineSettings CreateFromFile(string profileName, string settingsPath)
    {
        using var stream = File.OpenRead(settingsPath);
        var document = JsonSerializer.Deserialize<ReplaySettingsFile>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Settings file '{settingsPath}' is empty.");
        if (document.ResolvedTrackerOptions?.EngineSettings is not null)
        {
            return document.ResolvedTrackerOptions.EngineSettings;
        }

        var tracker = document.Tracker ?? document.TrackerOptions ?? document;

        if (tracker.Profiles is null || !tracker.Profiles.TryGetValue(profileName, out var profile))
        {
            throw new InvalidOperationException(
                $"Tracker profile '{profileName}' was not found in settings file '{settingsPath}'.");
        }

        var defaults = CreateDefault(profileName);
        return new TrackerEngineSettings
        {
            ProfileName = profileName,
            ReorderWindowNs = profile.Engine?.ReorderWindowNs ?? defaults.ReorderWindowNs,
            MergeWindowNs = profile.Engine?.MergeWindowNs ?? defaults.MergeWindowNs,
            GeometryResetFieldLengthThresholdMm = profile.Engine?.GeometryResetFieldLengthThresholdMm
                ?? defaults.GeometryResetFieldLengthThresholdMm,
            GeometryResetFieldWidthThresholdMm = profile.Engine?.GeometryResetFieldWidthThresholdMm
                ?? defaults.GeometryResetFieldWidthThresholdMm,
            RobotTracker = MergeRobotTracker(defaults.RobotTracker, profile.RobotTracker),
            BallTracker = MergeBallTracker(defaults.BallTracker, profile.BallTracker),
            KickDetector = MergeKickDetector(defaults.KickDetector, profile.KickDetector),
        };
    }

    private static TrackerEngineSettings ApplyOverrides(
        TrackerEngineSettings settings,
        TrackerSettingOverrides overrides)
    {
        return new TrackerEngineSettings
        {
            ProfileName = settings.ProfileName,
            ReorderWindowNs = overrides.ReorderWindowNs ?? settings.ReorderWindowNs,
            MergeWindowNs = overrides.MergeWindowNs ?? settings.MergeWindowNs,
            GeometryResetFieldLengthThresholdMm = settings.GeometryResetFieldLengthThresholdMm,
            GeometryResetFieldWidthThresholdMm = settings.GeometryResetFieldWidthThresholdMm,
            RobotTracker = settings.RobotTracker,
            BallTracker = new TrackerBallTrackerOverrides
            {
                ProcessNoise = settings.BallTracker.ProcessNoise,
                MeasurementNoise = settings.BallTracker.MeasurementNoise,
                VisibilityHalfLifeSeconds = settings.BallTracker.VisibilityHalfLifeSeconds,
                OutputVisibilityThreshold = overrides.BallOutputVisibility
                    ?? settings.BallTracker.OutputVisibilityThreshold,
                Gate = overrides.BallGate ?? settings.BallTracker.Gate,
                OutlierLimitMm = overrides.BallOutlierLimitMm ?? settings.BallTracker.OutlierLimitMm,
                TrackLifetimeNs = overrides.BallTrackLifetimeNs ?? settings.BallTracker.TrackLifetimeNs,
            },
            KickDetector = settings.KickDetector,
        };
    }

    private static TrackerRobotTrackerOverrides MergeRobotTracker(
        TrackerRobotTrackerOverrides defaults,
        TrackerRobotTrackerOptions? options)
    {
        return new TrackerRobotTrackerOverrides
        {
            ProcessNoise = options?.ProcessNoise ?? defaults.ProcessNoise,
            MeasurementNoise = options?.MeasurementNoise ?? defaults.MeasurementNoise,
            VisibilityHalfLifeSeconds = options?.VisibilityHalfLifeSeconds ?? defaults.VisibilityHalfLifeSeconds,
            OutputVisibilityThreshold = options?.OutputVisibilityThreshold ?? defaults.OutputVisibilityThreshold,
            Gate = options?.Gate ?? defaults.Gate,
            OutlierLimitMm = options?.OutlierLimitMm ?? defaults.OutlierLimitMm,
        };
    }

    private static TrackerBallTrackerOverrides MergeBallTracker(
        TrackerBallTrackerOverrides defaults,
        TrackerBallTrackerOptions? options)
    {
        return new TrackerBallTrackerOverrides
        {
            ProcessNoise = options?.ProcessNoise ?? defaults.ProcessNoise,
            MeasurementNoise = options?.MeasurementNoise ?? defaults.MeasurementNoise,
            VisibilityHalfLifeSeconds = options?.VisibilityHalfLifeSeconds ?? defaults.VisibilityHalfLifeSeconds,
            OutputVisibilityThreshold = options?.OutputVisibilityThreshold ?? defaults.OutputVisibilityThreshold,
            Gate = options?.Gate ?? defaults.Gate,
            OutlierLimitMm = options?.OutlierLimitMm ?? defaults.OutlierLimitMm,
            TrackLifetimeNs = options?.TrackLifetimeNs ?? defaults.TrackLifetimeNs,
        };
    }

    private static TrackerKickDetectorOverrides MergeKickDetector(
        TrackerKickDetectorOverrides defaults,
        TrackerKickDetectorOptions? options)
    {
        return new TrackerKickDetectorOverrides
        {
            KickSpeedThresholdMmPerS = options?.KickSpeedThresholdMmPerS ?? defaults.KickSpeedThresholdMmPerS,
            ChipHeightThresholdMm = options?.ChipHeightThresholdMm ?? defaults.ChipHeightThresholdMm,
            ContactMarginMm = options?.ContactMarginMm ?? defaults.ContactMarginMm,
        };
    }
}

internal sealed class ReplaySettingsFile
{
    public ReplaySettingsFile? Tracker { get; set; }

    public ReplaySettingsFile? TrackerOptions { get; set; }

    public ReplayResolvedOptions? ResolvedTrackerOptions { get; set; }

    public Dictionary<string, TrackerProfileOptions>? Profiles { get; set; }
}

internal sealed class ReplayResolvedOptions
{
    public TrackerEngineSettings? EngineSettings { get; set; }
}

internal sealed class TrackerProfileOptions
{
    public TrackerEngineOptions? Engine { get; set; }

    public TrackerRobotTrackerOptions? RobotTracker { get; set; }

    public TrackerBallTrackerOptions? BallTracker { get; set; }

    public TrackerKickDetectorOptions? KickDetector { get; set; }
}

internal sealed class TrackerEngineOptions
{
    public long? ReorderWindowNs { get; set; }

    public long? MergeWindowNs { get; set; }

    public int? GeometryResetFieldLengthThresholdMm { get; set; }

    public int? GeometryResetFieldWidthThresholdMm { get; set; }
}

internal class TrackerRobotTrackerOptions
{
    public double? ProcessNoise { get; set; }

    public double? MeasurementNoise { get; set; }

    public double? VisibilityHalfLifeSeconds { get; set; }

    public double? OutputVisibilityThreshold { get; set; }

    public double? Gate { get; set; }

    public double? OutlierLimitMm { get; set; }
}

internal sealed class TrackerBallTrackerOptions : TrackerRobotTrackerOptions
{
    public long? TrackLifetimeNs { get; set; }
}

internal sealed class TrackerKickDetectorOptions
{
    public double? KickSpeedThresholdMmPerS { get; set; }

    public double? ChipHeightThresholdMm { get; set; }

    public double? ContactMarginMm { get; set; }
}

internal sealed record TrackerSettingOverrides(
    double? BallGate,
    double? BallOutlierLimitMm,
    double? BallOutputVisibility,
    long? BallTrackLifetimeNs,
    long? MergeWindowNs,
    long? ReorderWindowNs);

internal sealed record ReplayOptions
{
    public string? CapturePath { get; private init; }

    public string ProfileName { get; private init; } = "sim";

    public string? SettingsPath { get; private init; }

    public int MaxDetails { get; private init; } = 40;

    public IReadOnlyList<Condition> Expectations { get; private init; } = [];

    public IReadOnlyList<Condition> DetailFilters { get; private init; } = [];

    public TrackerSettingOverrides SettingsOverrides { get; private init; } = new(
        BallGate: null,
        BallOutlierLimitMm: null,
        BallOutputVisibility: null,
        BallTrackLifetimeNs: null,
        MergeWindowNs: null,
        ReorderWindowNs: null);

    public bool ShowHelp { get; private init; }

    public string? Error { get; private init; }

    public static ReplayOptions Parse(string[] args)
    {
        var options = new ReplayOptions();
        var expectations = new List<Condition>();
        var detailFilters = new List<Condition>();
        var settingsOverrides = options.SettingsOverrides;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h":
                case "--help":
                    return options with { ShowHelp = true };
                case "--capture":
                    if (!TryReadValue(args, ref i, out var capturePath))
                    {
                        return options with { Error = "--capture requires a file path." };
                    }

                    options = options with { CapturePath = capturePath };
                    break;
                case "--profile":
                    if (!TryReadValue(args, ref i, out var profileName))
                    {
                        return options with { Error = "--profile requires a profile name." };
                    }

                    options = options with { ProfileName = profileName };
                    break;
                case "--settings":
                    if (!TryReadValue(args, ref i, out var settingsPath))
                    {
                        return options with { Error = "--settings requires a JSON file path." };
                    }

                    options = options with { SettingsPath = settingsPath };
                    break;
                case "--max-details":
                    if (!TryReadValue(args, ref i, out var maxDetailsText)
                        || !int.TryParse(maxDetailsText, NumberStyles.None, CultureInfo.InvariantCulture, out var maxDetails)
                        || maxDetails < 0)
                    {
                        return options with { Error = "--max-details requires a non-negative integer." };
                    }

                    options = options with { MaxDetails = maxDetails };
                    break;
                case "--ball-gate":
                    if (!TryReadNonNegativeDouble(args, ref i, "--ball-gate", out var ballGate, out var ballGateError))
                    {
                        return options with { Error = ballGateError };
                    }

                    settingsOverrides = settingsOverrides with { BallGate = ballGate };
                    break;
                case "--ball-outlier-limit-mm":
                    if (!TryReadNonNegativeDouble(args, ref i, "--ball-outlier-limit-mm", out var ballOutlierLimit, out var ballOutlierError))
                    {
                        return options with { Error = ballOutlierError };
                    }

                    settingsOverrides = settingsOverrides with { BallOutlierLimitMm = ballOutlierLimit };
                    break;
                case "--ball-output-visibility":
                    if (!TryReadNonNegativeDouble(args, ref i, "--ball-output-visibility", out var ballOutputVisibility, out var ballOutputVisibilityError)
                        || ballOutputVisibility > 1.0d)
                    {
                        return options with { Error = ballOutputVisibilityError ?? "--ball-output-visibility requires a value between 0 and 1." };
                    }

                    settingsOverrides = settingsOverrides with { BallOutputVisibility = ballOutputVisibility };
                    break;
                case "--ball-track-lifetime-ns":
                    if (!TryReadNonNegativeLong(args, ref i, "--ball-track-lifetime-ns", out var ballTrackLifetimeNs, out var ballTrackLifetimeError))
                    {
                        return options with { Error = ballTrackLifetimeError };
                    }

                    settingsOverrides = settingsOverrides with { BallTrackLifetimeNs = ballTrackLifetimeNs };
                    break;
                case "--merge-window-ns":
                    if (!TryReadNonNegativeLong(args, ref i, "--merge-window-ns", out var mergeWindowNs, out var mergeWindowError))
                    {
                        return options with { Error = mergeWindowError };
                    }

                    settingsOverrides = settingsOverrides with { MergeWindowNs = mergeWindowNs };
                    break;
                case "--reorder-window-ns":
                    if (!TryReadNonNegativeLong(args, ref i, "--reorder-window-ns", out var reorderWindowNs, out var reorderWindowError))
                    {
                        return options with { Error = reorderWindowError };
                    }

                    settingsOverrides = settingsOverrides with { ReorderWindowNs = reorderWindowNs };
                    break;
                case "--expect":
                    if (!TryReadValue(args, ref i, out var expectationText))
                    {
                        return options with { Error = "--expect requires a condition." };
                    }

                    if (!Condition.TryParse(expectationText, SummaryMetrics, out var expectation, out var expectationError))
                    {
                        return options with { Error = expectationError ?? "--expect requires a condition." };
                    }

                    expectations.Add(expectation);
                    break;
                case "--detail-filter":
                    if (!TryReadValue(args, ref i, out var filterText))
                    {
                        return options with { Error = "--detail-filter requires a condition." };
                    }

                    if (!Condition.TryParse(filterText, DetailMetrics, out var detailFilter, out var filterError))
                    {
                        return options with { Error = filterError ?? "--detail-filter requires a condition." };
                    }

                    detailFilters.Add(detailFilter);
                    break;
                default:
                    return options with { Error = $"Unknown argument: {args[i]}" };
            }
        }

        if (string.IsNullOrWhiteSpace(options.CapturePath))
        {
            return options with { Error = "--capture is required." };
        }

        return options with
        {
            Expectations = expectations,
            DetailFilters = detailFilters,
            SettingsOverrides = settingsOverrides,
        };
    }

    public static void WriteUsage()
    {
        Console.WriteLine(
            """
            Usage:
              dotnet run --project Tracker/Tracker.CaptureReplay -- --capture <file> [options]

            Options:
              --profile <name>             Tracker profile settings to use. default: sim
              --settings <file>            Tracker settings JSON. Accepts Tracker.Server appsettings.json or capture metadata shape.
              --expect <condition>         Assert a summary metric, for automation. Can be repeated.
              --detail-filter <condition>  Print committed frames matching all detail filters. Can be repeated.
              --max-details <count>        Maximum matching frame details to print. default: 40
              --ball-gate <value>          Override BallTracker.Gate for replay.
              --ball-outlier-limit-mm <v>  Override BallTracker.OutlierLimitMm for replay.
              --ball-output-visibility <v> Override BallTracker.OutputVisibilityThreshold for replay.
              --ball-track-lifetime-ns <v> Override BallTracker.TrackLifetimeNs for replay.
              --merge-window-ns <value>    Override Engine.MergeWindowNs for replay.
              --reorder-window-ns <value>  Override Engine.ReorderWindowNs for replay.
              -h, --help                   Show help.

            Summary metrics for --expect:
              packets, detections, geometries, committed-frames,
              max-balls, max-robots, max-raw-balls, max-raw-yellow, max-raw-blue

            Frame metrics for --detail-filter:
              balls, robots, raw-balls, raw-yellow, raw-blue

            Operators:
              >=, <=, ==, !=, >, <

            Examples:
              --expect committed-frames>0
              --expect max-balls<=1
              --settings Tracker/Tracker.Server/appsettings.json --profile sim
              --settings <capture.metadata.json>
              --detail-filter balls>=2 --detail-filter raw-balls==1
            """);
    }

    private static readonly HashSet<string> SummaryMetrics =
    [
        "packets",
        "detections",
        "geometries",
        "committed-frames",
        "max-balls",
        "max-robots",
        "max-raw-balls",
        "max-raw-yellow",
        "max-raw-blue",
    ];

    private static readonly HashSet<string> DetailMetrics =
    [
        "balls",
        "robots",
        "raw-balls",
        "raw-yellow",
        "raw-blue",
    ];

    private static bool TryReadValue(string[] args, ref int index, out string value)
    {
        value = "";
        if (index + 1 >= args.Length)
        {
            return false;
        }

        value = args[++index];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadNonNegativeDouble(
        string[] args,
        ref int index,
        string optionName,
        out double value,
        out string? error)
    {
        value = 0;
        error = null;

        if (!TryReadValue(args, ref index, out var text))
        {
            error = $"{optionName} requires a value.";
            return false;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < 0d)
        {
            error = $"{optionName} requires a non-negative number.";
            return false;
        }

        return true;
    }

    private static bool TryReadNonNegativeLong(
        string[] args,
        ref int index,
        string optionName,
        out long value,
        out string? error)
    {
        value = 0;
        error = null;

        if (!TryReadValue(args, ref index, out var text))
        {
            error = $"{optionName} requires a value.";
            return false;
        }

        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value < 0)
        {
            error = $"{optionName} requires a non-negative integer.";
            return false;
        }

        return true;
    }
}

internal sealed record Condition(string Metric, ComparisonOperator Operator, int Expected)
{
    public bool Evaluate(int actual)
    {
        return Operator switch
        {
            ComparisonOperator.GreaterThanOrEqual => actual >= Expected,
            ComparisonOperator.LessThanOrEqual => actual <= Expected,
            ComparisonOperator.Equal => actual == Expected,
            ComparisonOperator.NotEqual => actual != Expected,
            ComparisonOperator.GreaterThan => actual > Expected,
            ComparisonOperator.LessThan => actual < Expected,
            _ => throw new InvalidOperationException($"Unsupported operator '{Operator}'."),
        };
    }

    public override string ToString()
    {
        return $"{Metric}{Operator.ToSymbol()}{Expected.ToString(CultureInfo.InvariantCulture)}";
    }

    public static bool TryParse(
        string text,
        IReadOnlySet<string> allowedMetrics,
        out Condition condition,
        out string? error)
    {
        condition = new Condition("", ComparisonOperator.Equal, 0);
        error = null;

        foreach (var candidate in ComparisonOperatorExtensions.ParseOrder)
        {
            var symbol = candidate.ToSymbol();
            var index = text.IndexOf(symbol, StringComparison.Ordinal);
            if (index <= 0)
            {
                continue;
            }

            var metric = text[..index].Trim();
            var expectedText = text[(index + symbol.Length)..].Trim();
            if (!allowedMetrics.Contains(metric))
            {
                error = $"Unsupported metric '{metric}'.";
                return false;
            }

            if (!int.TryParse(expectedText, NumberStyles.None, CultureInfo.InvariantCulture, out var expected))
            {
                error = $"Condition '{text}' has an invalid integer value.";
                return false;
            }

            condition = new Condition(metric, candidate, expected);
            return true;
        }

        error = $"Condition '{text}' must contain one of: >=, <=, ==, !=, >, <.";
        return false;
    }
}

internal enum ComparisonOperator
{
    GreaterThanOrEqual,
    LessThanOrEqual,
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
}

internal static class ComparisonOperatorExtensions
{
    public static readonly IReadOnlyList<ComparisonOperator> ParseOrder =
    [
        ComparisonOperator.GreaterThanOrEqual,
        ComparisonOperator.LessThanOrEqual,
        ComparisonOperator.Equal,
        ComparisonOperator.NotEqual,
        ComparisonOperator.GreaterThan,
        ComparisonOperator.LessThan,
    ];

    public static string ToSymbol(this ComparisonOperator comparisonOperator)
    {
        return comparisonOperator switch
        {
            ComparisonOperator.GreaterThanOrEqual => ">=",
            ComparisonOperator.LessThanOrEqual => "<=",
            ComparisonOperator.Equal => "==",
            ComparisonOperator.NotEqual => "!=",
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.LessThan => "<",
            _ => throw new InvalidOperationException($"Unsupported operator '{comparisonOperator}'."),
        };
    }
}
