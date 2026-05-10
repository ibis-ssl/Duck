using Tracker.Server.Tracking;

namespace Tracker.Tests;

public class TrackerDiagnosticsLogReaderTests
{
    [Fact]
    public void TryParseLine_ExtractsRawAndTrackedTimelineFields()
    {
        const string line = "2026-05-10T09:38:31.3513054+00:00 Tracker diagnostics profile=sim rawFrame=723541 rawCamera=0 rawBalls=1 rawBallDetails=[x=-329.3,y=4739.1,z=0,c=1] rawBlue=[] rawYellow=[Y1:x=-2492.9,y=-747.6,o=1.277,c=1; Y3:x=-3880.2,y=-1812.9,o=1.816,c=1] trackedFrame=634 trackedBalls=2 trackedBallDetails=[#27:x=-325.7,y=4737.6,z=0,vis=1,q=1,cams=0; #53:x=-75.1,y=4623.8,z=0,vis=0.725,q=0.725,cams=1] trackedRobots=22 trackedRobotDetails=[Y0:x=2557.8,y=2186.9,vis=1,q=0.05] robotOutVisibility=0.05 robotHalfLifeSec=0.462756 ballOutVisibility=0 ballHalfLifeSec=1 ballLifetimeNs=1000000000";

        var parsed = TrackerDiagnosticsLogReader.TryParseLine(line, lineNumber: 7, out var entry);

        Assert.True(parsed);
        Assert.Equal(7, entry.LineNumber);
        Assert.Equal("sim", entry.ProfileName);
        Assert.Equal("723541", entry.RawFrame);
        Assert.Equal(0, entry.RawCamera);
        Assert.Equal(1, entry.RawBallCount);
        Assert.Equal("634", entry.TrackedFrame);
        Assert.Equal(2, entry.TrackedBallCount);
        Assert.Equal(22, entry.TrackedRobotCount);
        Assert.True(entry.HasMultipleTrackedBalls);
        Assert.Contains("#53:x=-75.1", entry.TrackedBallDetails);
        Assert.Contains("Y1:x=-2492.9", entry.RawYellowDetails);
        Assert.Equal("1000000000", entry.BallTrackLifetimeNs);
    }
}
