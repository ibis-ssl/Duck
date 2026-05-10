using Tracker.Core;
using Tracker.Server.Vision;

namespace Tracker.Server.Tracking;

public sealed partial class TrackerCoordinator
{
    // diagnostics log schema と render snapshot 参照を揃えるため、最新 committed frame とその source detection だけを出力する。
    private void LogTrackerDiagnostics(
        SSL_WrapperPacket? packet,
        TrackerUpdateResult result,
        DateTimeOffset receivedAt)
    {
        if (result.CommittedFrames.Count == 0)
        {
            return;
        }

        var newestFrame = result.CommittedFrames[^1];
        var sourceDetections = newestFrame.SourceDetections;
        if (!ShouldLogTrackerDiagnostics(receivedAt, newestFrame))
        {
            return;
        }

        lastTrackerDiagnosticsLogAt = receivedAt;
        var rawBalls = sourceDetections.SelectMany(detection => detection.Balls).ToArray();
        var rawBlue = sourceDetections.SelectMany(detection => detection.RobotsBlue).ToArray();
        var rawYellow = sourceDetections.SelectMany(detection => detection.RobotsYellow).ToArray();
        var rawFrameLabel = TrackerDiagnosticsFormatter.FormatSourceFrameNumbers(sourceDetections);
        var rawCameraLabel = TrackerDiagnosticsFormatter.FormatSourceCameraIds(sourceDetections);
        var rawBallDetails = TrackerDiagnosticsFormatter.FormatRawBalls(rawBalls);
        var rawBlueDetails = TrackerDiagnosticsFormatter.FormatRawRobots(rawBlue, TrackerTeam.Blue);
        var rawYellowDetails = TrackerDiagnosticsFormatter.FormatRawRobots(rawYellow, TrackerTeam.Yellow);
        var trackedBallDetails = TrackerDiagnosticsFormatter.FormatTrackedBalls(newestFrame.Balls);
        var trackedRobotDetails = TrackerDiagnosticsFormatter.FormatTrackedRobots(newestFrame.Robots);
        var diagnosticsLine = FormattableString.Invariant(
            $"{receivedAt:O} Tracker diagnostics profile={currentSettings.ProfileName} rawFrame={rawFrameLabel} rawCamera={rawCameraLabel} rawBalls={rawBalls.Length} rawBallDetails=[{rawBallDetails}] rawBlue=[{rawBlueDetails}] rawYellow=[{rawYellowDetails}] trackedFrame={newestFrame.FrameNumber} trackedBalls={newestFrame.Balls.Count} trackedBallDetails=[{trackedBallDetails}] trackedRobots={newestFrame.Robots.Count} trackedRobotDetails=[{trackedRobotDetails}] robotOutVisibility={currentSettings.RobotTracker.OutputVisibilityThreshold} robotHalfLifeSec={currentSettings.RobotTracker.VisibilityHalfLifeSeconds} ballOutVisibility={currentSettings.BallTracker.OutputVisibilityThreshold} ballHalfLifeSec={currentSettings.BallTracker.VisibilityHalfLifeSeconds} ballLifetimeNs={currentSettings.BallTracker.TrackLifetimeNs}");
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Tracker diagnostics profile={ProfileName} rawFrame={RawFrameNumber} rawCamera={RawCameraId} rawBalls={RawBallCount} rawBallDetails=[{RawBallDetails}] rawBlue=[{RawBlueDetails}] rawYellow=[{RawYellowDetails}] trackedFrame={TrackedFrameNumber} trackedBalls={TrackedBallCount} trackedBallDetails=[{TrackedBallDetails}] trackedRobots={TrackedRobotCount} trackedRobotDetails=[{TrackedRobotDetails}] robotOutVisibility={RobotOutputVisibilityThreshold} robotHalfLifeSec={RobotVisibilityHalfLifeSeconds} ballOutVisibility={BallOutputVisibilityThreshold} ballHalfLifeSec={BallVisibilityHalfLifeSeconds} ballLifetimeNs={BallTrackLifetimeNs}",
                currentSettings.ProfileName,
                rawFrameLabel,
                rawCameraLabel,
                rawBalls.Length,
                rawBallDetails,
                rawBlueDetails,
                rawYellowDetails,
                newestFrame.FrameNumber,
                newestFrame.Balls.Count,
                trackedBallDetails,
                newestFrame.Robots.Count,
                trackedRobotDetails,
                currentSettings.RobotTracker.OutputVisibilityThreshold,
                currentSettings.RobotTracker.VisibilityHalfLifeSeconds,
                currentSettings.BallTracker.OutputVisibilityThreshold,
                currentSettings.BallTracker.VisibilityHalfLifeSeconds,
                currentSettings.BallTracker.TrackLifetimeNs);
        }

        AppendTrackerDiagnosticsFile(diagnosticsLine, receivedAt);
    }

    private void AppendTrackerDiagnosticsFile(string diagnosticsLine, DateTimeOffset receivedAt)
    {
        foreach (var logPath in ResolveTrackerDiagnosticsLogPaths(receivedAt))
        {
            if (failedTrackerDiagnosticsLogPaths.Contains(logPath))
            {
                continue;
            }

            try
            {
                var directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(logPath, diagnosticsLine + Environment.NewLine);
            }
            catch (Exception ex)
            {
                failedTrackerDiagnosticsLogPaths.Add(logPath);
                logger.LogWarning(ex, "Failed to write tracker diagnostics log file {LogPath}", logPath);
            }
        }
    }

    private IReadOnlyList<string> ResolveTrackerDiagnosticsLogPaths(DateTimeOffset receivedAt)
    {
        var logPaths = new List<string>();
        var captureSidecarPath = ResolveTrackerDiagnosticsSidecarPath(receivedAt);
        if (captureSidecarPath is not null)
        {
            logPaths.Add(captureSidecarPath);
        }

        logPaths.Add(diagnosticsOptions.FilePath ?? (captureSidecarPath is null
            ? defaultTrackerDiagnosticsLogPath ??= CreateTrackerDiagnosticsLogPath()
            : captureSidecarPath));

        return logPaths
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private string? ResolveTrackerDiagnosticsSidecarPath(DateTimeOffset receivedAt)
    {
        if (packetCaptureSession?.Enabled != true)
        {
            return null;
        }

        return packetCaptureSession.EnsureStarted(receivedAt)?.DiagnosticsLogPath;
    }

    private string CreateTrackerDiagnosticsLogPath()
    {
        var timestamp = FormattableString.Invariant($"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}");
        return Path.Combine(
            packetCaptureSession?.DirectoryPath ?? Path.Combine(AppContext.BaseDirectory, "packet-captures"),
            $"tracker-diagnostics-{timestamp}-{Guid.NewGuid():N}.log");
    }

    private bool ShouldLogTrackerDiagnostics(
        DateTimeOffset receivedAt,
        TrackerFrame newestFrame)
    {
        if (receivedAt - lastTrackerDiagnosticsLogAt >= TrackerDiagnosticsLogInterval)
        {
            return true;
        }

        return newestFrame.SourceDetections.Sum(detection => detection.Balls.Count) > 1
            || newestFrame.Balls.Count > 1;
    }
}
