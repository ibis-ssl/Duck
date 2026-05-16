using System.Globalization;

namespace Tracker.CaptureReplay;

/// <summary>
/// Capture replay CLI の引数と検証条件を保持する。
/// </summary>
internal sealed record ReplayOptions
{
    /// <summary>
    /// replay する jsonl.gz capture file path。
    /// </summary>
    public string? CapturePath { get; private init; }

    /// <summary>
    /// settings file から選択する tracker profile 名。
    /// </summary>
    public string ProfileName { get; private init; } = "sim";

    /// <summary>
    /// appsettings.json または capture metadata の path。
    /// </summary>
    public string? SettingsPath { get; private init; }

    /// <summary>
    /// detail frame の最大出力件数。
    /// </summary>
    public int MaxDetails { get; private init; } = 40;

    /// <summary>
    /// 1 detail frame に表示する tracked robot の最大件数。
    /// </summary>
    public int MaxDetailRobots { get; private init; } = 16;

    /// <summary>
    /// raw vision と replay 後 ibis tracker commit の cadence / lag 分析行を出すかどうか。
    /// </summary>
    public bool AnalyzeLatency { get; private init; }

    /// <summary>
    /// latency detail frame の最大出力件数。
    /// </summary>
    public int MaxLatencyFrames { get; private init; } = 40;

    /// <summary>
    /// metadata 由来 tracker snapshot / comparison 行を抑制するかどうか。
    /// </summary>
    public bool SkipTrackerSnapshots { get; private init; }

    /// <summary>
    /// replay summary に対する自動検証条件。
    /// </summary>
    public IReadOnlyList<Condition> Expectations { get; private init; } = [];

    /// <summary>
    /// detail 行を出力する committed frame の条件。
    /// </summary>
    public IReadOnlyList<Condition> DetailFilters { get; private init; } = [];

    /// <summary>
    /// replay 中だけ engine settings へ適用する CLI override。
    /// </summary>
    public TrackerSettingOverrides SettingsOverrides { get; private init; } = new(
        BallGate: null,
        BallOutlierLimitMm: null,
        BallOutputVisibility: null,
        BallTrackLifetimeNs: null,
        MergeWindowNs: null,
        ReorderWindowNs: null);

    /// <summary>
    /// usage 表示だけで終了する要求かどうか。
    /// </summary>
    public bool ShowHelp { get; private init; }

    /// <summary>
    /// 引数 parse に失敗したときの既存 CLI error message。
    /// </summary>
    public string? Error { get; private init; }

    /// <summary>
    /// CLI 引数を既存の error message と default 値を保ったまま parse する。
    /// </summary>
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
                case "--max-detail-robots":
                    if (!TryReadValue(args, ref i, out var maxDetailRobotsText)
                        || !int.TryParse(maxDetailRobotsText, NumberStyles.None, CultureInfo.InvariantCulture, out var maxDetailRobots)
                        || maxDetailRobots < 0)
                    {
                        return options with { Error = "--max-detail-robots requires a non-negative integer." };
                    }

                    options = options with { MaxDetailRobots = maxDetailRobots };
                    break;
                case "--analyze-latency":
                    options = options with { AnalyzeLatency = true };
                    break;
                case "--max-latency-frames":
                    if (!TryReadValue(args, ref i, out var maxLatencyFramesText)
                        || !int.TryParse(maxLatencyFramesText, NumberStyles.None, CultureInfo.InvariantCulture, out var maxLatencyFrames)
                        || maxLatencyFrames < 0)
                    {
                        return options with { Error = "--max-latency-frames requires a non-negative integer." };
                    }

                    options = options with { MaxLatencyFrames = maxLatencyFrames };
                    break;
                case "--skip-tracker-snapshots":
                    options = options with { SkipTrackerSnapshots = true };
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

    /// <summary>
    /// CaptureReplay CLI の usage を既存文言のまま標準出力へ出す。
    /// </summary>
    public static void WriteUsage()
    {
        Console.WriteLine(
            """
            Usage:
              dotnet run --project Tracker/Tracker.CaptureReplay -- --capture <file-or-session-folder> [options]

            Options:
              --profile <name>             Tracker profile settings to use. default: sim
              --settings <file>            Tracker settings JSON. Accepts Tracker.DebugHost appsettings.json or capture metadata shape.
              --expect <condition>         Assert a summary metric, for automation. Can be repeated.
              --detail-filter <condition>  Print committed frames matching all detail filters. Can be repeated.
              --max-details <count>        Maximum matching frame details to print. default: 40
              --max-detail-robots <count>  Maximum tracked robots per detail frame. default: 16
              --analyze-latency            Print raw vision cadence and tracker commit lag analysis.
              --max-latency-frames <count> Maximum latency frame details to print. default: 40
              --skip-tracker-snapshots     Suppress trackerSnapshot/trackerComparison metadata lines.
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
              frame, balls, robots, raw-balls, raw-yellow, raw-blue

            Operators:
              >=, <=, ==, !=, >, <

            Examples:
              --expect committed-frames>0
              --expect max-balls<=1
              --settings Tracker/Tracker.DebugHost/appsettings.json --profile sim
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
        "frame",
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
