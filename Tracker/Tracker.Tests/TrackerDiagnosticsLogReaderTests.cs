using Microsoft.Extensions.Options;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;

namespace Tracker.Tests;

public class TrackerDiagnosticsLogReaderTests
{
    private const string DiagnosticsLine = "2026-05-10T09:38:31.3513054+00:00 Tracker diagnostics profile=sim rawFrame=723541 rawCamera=0 rawBalls=1 rawBallDetails=[x=-329.3,y=4739.1,z=0,c=1] rawBlue=[] rawYellow=[Y1:x=-2492.9,y=-747.6,o=1.277,c=1; Y3:x=-3880.2,y=-1812.9,o=1.816,c=1] trackedFrame=634 trackedBalls=2 trackedBallDetails=[#27:x=-325.7,y=4737.6,z=0,vis=1,q=1,cams=0; #53:x=-75.1,y=4623.8,z=0,vis=0.725,q=0.725,cams=1] trackedRobots=22 trackedRobotDetails=[Y0:x=2557.8,y=2186.9,o=0.3,w=0.1,vis=1,q=0.05] robotOutVisibility=0.05 robotHalfLifeSec=0.462756 ballOutVisibility=0 ballHalfLifeSec=1 ballLifetimeNs=1000000000";

    /// <summary>
    /// 何を確認しているか: diagnostics log 1 行から raw / tracked timeline の主要 field を抽出できること。
    /// </summary>
    [Fact]
    public void TryParseLine_ExtractsRawAndTrackedTimelineFields()
    {
        var parsed = TrackerDiagnosticsLogReader.TryParseLine(DiagnosticsLine, lineNumber: 7, out var entry);

        Assert.True(parsed);
        Assert.Equal(7, entry.LineNumber);
        Assert.Equal("sim", entry.ProfileName);
        Assert.Equal("723541", entry.RawFrame);
        Assert.Equal("0", entry.RawCamera);
        Assert.Equal(1, entry.RawBallCount);
        Assert.Equal("634", entry.TrackedFrame);
        Assert.Equal(2, entry.TrackedBallCount);
        Assert.Equal(22, entry.TrackedRobotCount);
        Assert.True(entry.HasMultipleTrackedBalls);
        Assert.Contains("#53:x=-75.1", entry.TrackedBallDetails);
        Assert.Contains("Y1:x=-2492.9", entry.RawYellowDetails);
        Assert.Equal("1000000000", entry.BallTrackLifetimeNs);
    }

    /// <summary>
    /// 何を確認しているか: capture sidecar、default、configured diagnostics log を列挙し、対象外 sidecar を除外すること。
    /// </summary>
    [Fact]
    public void ListFiles_IncludesCaptureDirectorySidecarDefaultAndConfiguredLogs()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-diagnostics-capture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(captureDirectory);
        var sidecarPath = Path.Combine(captureDirectory, "ssl-vision-packets-test.tracker-diagnostics.log");
        var baseDirectorySidecarPath = Path.Combine(AppContext.BaseDirectory, $"ssl-vision-packets-reader-test-{Guid.NewGuid():N}.tracker-diagnostics.log");
        var defaultPath = Path.Combine(captureDirectory, $"tracker-diagnostics-reader-test-{Guid.NewGuid():N}.log");
        var configuredPath = Path.Combine(Path.GetTempPath(), $"tracker-diagnostics-configured-{Guid.NewGuid():N}.log");

        try
        {
            File.WriteAllText(sidecarPath, DiagnosticsLine);
            File.WriteAllText(baseDirectorySidecarPath, DiagnosticsLine);
            File.WriteAllText(defaultPath, DiagnosticsLine);
            File.WriteAllText(configuredPath, DiagnosticsLine);

            var reader = CreateReader(captureDirectory, configuredPath);

            var files = reader.ListFiles();

            Assert.Contains(files, file => file.FullPath == sidecarPath);
            Assert.Contains(files, file => file.FullPath == defaultPath);
            Assert.Contains(files, file => file.FullPath == configuredPath);
            Assert.DoesNotContain(files, file => file.FullPath == baseDirectorySidecarPath);

            var snapshot = reader.ReadFile(configuredPath);
            Assert.Null(snapshot.Error);
            Assert.Single(snapshot.Entries);
            Assert.Equal("634", snapshot.Entries[0].TrackedFrame);
        }
        finally
        {
            File.Delete(baseDirectorySidecarPath);
            File.Delete(defaultPath);
            File.Delete(configuredPath);

            if (Directory.Exists(captureDirectory))
            {
                Directory.Delete(captureDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// 何を確認しているか: 列挙対象外の diagnostics log path 読み取りを拒否すること。
    /// </summary>
    [Fact]
    public void ReadFile_RejectsPathOutsideListedDiagnosticsLogs()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"tracker-diagnostics-capture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(captureDirectory);
        var unlistedPath = Path.Combine(Path.GetTempPath(), $"tracker-diagnostics-unlisted-{Guid.NewGuid():N}.log");

        try
        {
            File.WriteAllText(unlistedPath, DiagnosticsLine);
            var reader = CreateReader(captureDirectory, configuredPath: null);

            var snapshot = reader.ReadFile(unlistedPath);

            Assert.NotNull(snapshot.Error);
            Assert.Empty(snapshot.Entries);
        }
        finally
        {
            if (Directory.Exists(captureDirectory))
            {
                Directory.Delete(captureDirectory, recursive: true);
            }

            File.Delete(unlistedPath);
        }
    }

    private static TrackerDiagnosticsLogReader CreateReader(string captureDirectory, string? configuredPath)
    {
        return new TrackerDiagnosticsLogReader(
            Options.Create(new VisionReceiverOptions
            {
                PacketCapture = new VisionPacketCaptureOptions
                {
                    DirectoryPath = captureDirectory,
                },
            }),
            new TrackerDiagnosticsOptions
            {
                FilePath = configuredPath,
            });
    }
}
