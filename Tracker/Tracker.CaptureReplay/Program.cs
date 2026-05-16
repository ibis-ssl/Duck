using Tracker.CaptureReplay;

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

var inputPaths = ReplayInputPathResolver.Resolve(options.CapturePath!, options.SettingsPath);
var settings = TrackerSettingsFactory.Create(options.ProfileName, inputPaths.SettingsPath, options.SettingsOverrides);
var summary = CaptureReplayRunner.Run(
    inputPaths.CapturePath,
    settings,
    options.DetailFilters,
    options.MaxDetails,
    options.MaxDetailRobots,
    inputPaths.MetadataPath,
    options.AnalyzeLatency,
    options.MaxLatencyFrames,
    !options.SkipTrackerSnapshots);

Console.WriteLine($"capture={inputPaths.CapturePath}");
Console.WriteLine($"settingsFile={inputPaths.SettingsPath ?? "(built-in defaults)"}");
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

foreach (var trackerSnapshotLine in summary.TrackerSnapshotLines)
{
    Console.WriteLine(trackerSnapshotLine);
}

foreach (var latencyLine in summary.LatencyLines)
{
    Console.WriteLine(latencyLine);
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
