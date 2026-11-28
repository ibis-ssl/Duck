using Tracker.Core;
using Tracker.Diagnostics.Mcap;

namespace Tracker.CaptureReplay;

/// <summary>The argument entry used by the CaptureReplay executable.</summary>
public static class CaptureReplayCommand
{
    /// <summary>Run CaptureReplay arguments and write the normal command output.</summary>
    public static int Run(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>Run CaptureReplay arguments with caller-provided output streams.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter errorOutput)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(errorOutput);

        var options = ReplayOptions.Parse(args);
        if (options.ShowHelp)
        {
            ReplayOptions.WriteUsage(output);
            return 0;
        }

        if (options.Error is not null)
        {
            errorOutput.WriteLine(options.Error);
            ReplayOptions.WriteUsage(errorOutput);
            return 2;
        }

        var inputPaths = ReplayInputPathResolver.Resolve(options.CapturePath!, options.SettingsPath);
        if (inputPaths.CapturePath.EndsWith(".mcap", StringComparison.OrdinalIgnoreCase)
            || inputPaths.CapturePath.EndsWith(".mcap.partial", StringComparison.OrdinalIgnoreCase))
        {
            if (options.SettingsPath is not null
                || options.ProfileWasSpecified
                || HasSettingsOverrides(options.SettingsOverrides)
                || options.AnalyzeLatency)
            {
                errorOutput.WriteLine("MCAP 録画は記録内の設定で再計算します。--settings、--profile、tracker 設定 override、--analyze-latency は指定できません。");
                return 2;
            }

            var recording = RecordingMcapReader.Read(inputPaths.CapturePath);
            var settingsByGeneration = RecordingMcapSettingsValidator.ReadAll(recording);
            var summary = CaptureReplayRunner.Run(
                recording,
                options.DetailFilters,
                options.MaxDetails,
                options.MaxDetailRobots);
            WriteRecordingSummary(output, inputPaths.CapturePath, recording, settingsByGeneration, summary);
            return WriteExpectations(output, options.Expectations, summary);
        }

        var settings = TrackerSettingsFactory.Create(options.ProfileName, inputPaths.SettingsPath, options.SettingsOverrides);
        var legacySummary = CaptureReplayRunner.Run(
            inputPaths.CapturePath,
            settings,
            options.DetailFilters,
            options.MaxDetails,
            options.MaxDetailRobots,
            inputPaths.MetadataPath,
            options.AnalyzeLatency,
            options.MaxLatencyFrames,
            !options.SkipTrackerSnapshots);
        WriteExistingSummary(output, inputPaths.CapturePath, inputPaths.SettingsPath, options.ProfileName, settings, legacySummary);
        return WriteExpectations(output, options.Expectations, legacySummary);
    }

    private static bool HasSettingsOverrides(TrackerSettingOverrides overrides) =>
        overrides.BallGate is not null
        || overrides.BallOutlierLimitMm is not null
        || overrides.BallOutputVisibility is not null
        || overrides.BallTrackLifetimeNs is not null
        || overrides.MergeWindowNs is not null
        || overrides.ReorderWindowNs is not null;

    private static void WriteRecordingSummary(
        TextWriter output,
        string capturePath,
        RecordingMcapSnapshot recording,
        IReadOnlyDictionary<string, TrackerEngineSettings> settingsByGeneration,
        ReplaySummary summary)
    {
        output.WriteLine($"capture={capturePath}");
        output.WriteLine("settingsFile=(recorded MCAP)");
        output.WriteLine($"settingsSource=recording generations={recording.EffectiveConfigurations.Count}");
        output.WriteLine($"profile={settingsByGeneration[recording.EffectiveConfigurations[0].GenerationId].ProfileName}");
        foreach (var configuration in recording.EffectiveConfigurations)
        {
            var settings = settingsByGeneration[configuration.GenerationId];
            output.WriteLine(
                $"settingsGeneration={configuration.GenerationId} profile={settings.ProfileName} reorderWindowNs={settings.ReorderWindowNs} mergeWindowNs={settings.MergeWindowNs} geometryResetFieldLengthThresholdMm={settings.GeometryResetFieldLengthThresholdMm} geometryResetFieldWidthThresholdMm={settings.GeometryResetFieldWidthThresholdMm} ballGate={settings.BallTracker.Gate} ballOutlierLimitMm={settings.BallTracker.OutlierLimitMm} ballOutputVisibility={settings.BallTracker.OutputVisibilityThreshold} ballTrackLifetimeNs={settings.BallTracker.TrackLifetimeNs}");
        }

        WriteReplaySummary(output, summary);
    }

    private static void WriteExistingSummary(
        TextWriter output,
        string capturePath,
        string? settingsPath,
        string profileName,
        TrackerEngineSettings settings,
        ReplaySummary summary)
    {
        output.WriteLine($"capture={capturePath}");
        output.WriteLine($"settingsFile={settingsPath ?? "(built-in defaults)"}");
        output.WriteLine($"profile={profileName}");
        output.WriteLine(
            $"settings=reorderWindowNs={settings.ReorderWindowNs} mergeWindowNs={settings.MergeWindowNs} ballGate={settings.BallTracker.Gate} ballOutlierLimitMm={settings.BallTracker.OutlierLimitMm} ballOutputVisibility={settings.BallTracker.OutputVisibilityThreshold} ballTrackLifetimeNs={settings.BallTracker.TrackLifetimeNs}");
        WriteReplaySummary(output, summary);
    }

    private static void WriteReplaySummary(TextWriter output, ReplaySummary summary)
    {
        output.WriteLine(
            $"packets={summary.PacketCount} detections={summary.DetectionCount} geometries={summary.GeometryCount} committedFrames={summary.CommittedFrameCount}");
        output.WriteLine(
            $"maxBalls={summary.MaxBallCount} maxRobots={summary.MaxRobotCount} maxRawBalls={summary.MaxRawBallCount} maxRawYellow={summary.MaxRawYellowCount} maxRawBlue={summary.MaxRawBlueCount}");
        foreach (var frame in summary.DetailFrames)
        {
            output.WriteLine(frame);
        }

        foreach (var trackerSnapshotLine in summary.TrackerSnapshotLines)
        {
            output.WriteLine(trackerSnapshotLine);
        }

        foreach (var latencyLine in summary.LatencyLines)
        {
            output.WriteLine(latencyLine);
        }

        if (summary.OmittedDetailFrameCount > 0)
        {
            output.WriteLine($"... omitted {summary.OmittedDetailFrameCount} detail frames");
        }
    }

    private static int WriteExpectations(TextWriter output, IReadOnlyList<Condition> expectations, ReplaySummary summary)
    {
        var failed = false;
        foreach (var expectation in expectations)
        {
            var actual = summary.GetMetric(expectation.Metric);
            var passed = expectation.Evaluate(actual);
            output.WriteLine($"expect {expectation}: actual={actual} result={(passed ? "ok" : "failed")}");
            failed |= !passed;
        }

        return failed ? 1 : 0;
    }
}
