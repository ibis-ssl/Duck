using Duck.Testing.Diagnostics;

namespace Tracker.Tests;

public sealed class ServiceReceiveDiagnosticsTests
{
    private static ResourceDiagnosticIdentity Identity(long generation = 1) =>
        new("run", "duck", new string('a', 64), generation);

    [Fact]
    public void ActualReceiveStampIncludesParseTimeAndPollingDoesNotRefreshIt()
    {
        var clock = new ManualClock();
        var diagnostics = new ServiceReceiveDiagnostics(Identity(), clock);
        var epoch = diagnostics.BeginEpoch("vision");
        diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch));
        var attempt = diagnostics.BeginReceive(epoch);
        clock.Advance(TimeSpan.FromSeconds(2));
        diagnostics.RecordSuccess(attempt);
        var first = Assert.Single(diagnostics.GetSnapshots("vision"));
        Assert.True(first.IsReady);
        Assert.Equal(2, first.SuccessfullyDecodedCount);
        Assert.Equal(TimeSpan.FromSeconds(2), first.DecodeAgeUpperBound);
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(Assert.Single(diagnostics.GetSnapshots("vision")).IsReady);
    }

    [Fact]
    public void MissingOwnerIdentityNeverMakesSelfReportedCountsReady()
    {
        var diagnostics = new ServiceReceiveDiagnostics();
        var epoch = diagnostics.BeginEpoch("vision");
        diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch));
        diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch));
        var snapshot = Assert.Single(diagnostics.GetSnapshots("vision"));
        Assert.Equal(2, snapshot.SuccessfullyDecodedCount);
        Assert.Null(snapshot.Identity);
        Assert.False(snapshot.IsReady);
        Assert.Equal("Unattributed", snapshot.Reason);
    }

    [Fact]
    public void EpochRejectsLateCallbacksAndRequiresNewProgressWithoutResettingCounts()
    {
        var diagnostics = new ServiceReceiveDiagnostics(Identity());
        var oldEpoch = diagnostics.BeginEpoch("vision");
        diagnostics.RecordSuccess(diagnostics.BeginReceive(oldEpoch));
        diagnostics.RecordSuccess(diagnostics.BeginReceive(oldEpoch));
        var late = diagnostics.BeginReceive(oldEpoch);
        var epoch = diagnostics.BeginEpoch("vision");
        Assert.False(diagnostics.RecordSuccess(late));
        Assert.False(diagnostics.RecordFailure(late));
        Assert.False(Assert.Single(diagnostics.GetSnapshots("vision")).IsReady);
        diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch));
        Assert.False(Assert.Single(diagnostics.GetSnapshots("vision")).IsReady);
        diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch));
        var current = Assert.Single(diagnostics.GetSnapshots("vision"));
        Assert.Equal(4, current.SuccessfullyDecodedCount);
        Assert.Equal(epoch.Number, current.ReceiverEpoch);
        Assert.NotEqual(oldEpoch.Number, current.ReceiverEpoch);
        Assert.True(current.IsReady);
    }

    [Fact]
    public void SourcesHaveIndependentProgressAndFailureDoesNotInventASource()
    {
        var diagnostics = new ServiceReceiveDiagnostics(Identity());
        var epoch = diagnostics.BeginEpoch("tracker");
        for (var i = 0; i < 2; i++)
            diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch), "duck", "ibis", "id-a", "a:1");
        diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch), "tigers", "TIGERs", "id-b", "b:1");
        var snapshots = diagnostics.GetSnapshots("tracker");
        Assert.True(Assert.Single(snapshots, s => s.SourceKey == "duck").IsReady);
        Assert.False(Assert.Single(snapshots, s => s.SourceKey == "tigers").IsReady);
        diagnostics.RecordFailure(diagnostics.BeginReceive(epoch));
        Assert.All(diagnostics.GetSnapshots("tracker"), s => Assert.False(s.IsReady));
        diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch), "duck", "ibis", "id-a", "a:2");
        snapshots = diagnostics.GetSnapshots("tracker");
        Assert.True(Assert.Single(snapshots, s => s.SourceKey == "duck").IsReady);
        Assert.False(Assert.Single(snapshots, s => s.SourceKey == "tigers").IsReady);
        Assert.All(snapshots, s => Assert.Equal(1, s.DecodeFailureCount));
    }

    [Fact]
    public void ClockRegressionCannotBeRepairedByPollingOrAnOldAttempt()
    {
        var clock = new ManualClock();
        var diagnostics = new ServiceReceiveDiagnostics(Identity(), clock);
        var epoch = diagnostics.BeginEpoch("vision");
        diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch));
        diagnostics.RecordSuccess(diagnostics.BeginReceive(epoch));
        clock.Advance(TimeSpan.FromSeconds(4));
        var attempt = diagnostics.BeginReceive(epoch);
        diagnostics.GetSnapshots("vision");
        clock.Advance(TimeSpan.FromSeconds(-1));
        Assert.False(Assert.Single(diagnostics.GetSnapshots("vision")).IsReady);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(Assert.Single(diagnostics.GetSnapshots("vision")).DecodeAgeUpperBound);
        diagnostics.RecordSuccess(attempt);
        Assert.Null(Assert.Single(diagnostics.GetSnapshots("vision")).DecodeAgeUpperBound);
        var invalid = diagnostics.BeginReceive(epoch);
        clock.Advance(TimeSpan.FromSeconds(-1));
        diagnostics.RecordSuccess(invalid);
        Assert.Null(Assert.Single(diagnostics.GetSnapshots("vision")).DecodeAgeUpperBound);
    }

    [Theory]
    [InlineData(null, "duck", null, null)]
    [InlineData("run", "duck", "short", "1")]
    [InlineData("run", "duck", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "0")]
    public void InvalidAttributionDoesNotCreateAnIdentity(string? run, string? resource, string? container, string? generation)
        => Assert.Null(ServiceReceiveDiagnostics.ParseIdentity(run, resource, container, generation));

    [Fact]
    public void ExplicitIdentityAndGenerationArePreserved()
    {
        var identity = Identity(2);
        Assert.Equal(identity, ServiceReceiveDiagnostics.ParseIdentity(identity.RunId, identity.ResourceId,
            identity.ContainerId, "2"));
        var diagnostics = new ServiceReceiveDiagnostics(identity);
        Assert.Equal(identity, Assert.Single(diagnostics.GetSnapshots("vision")).Identity);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long ticks = TimeSpan.TicksPerSecond;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public void Advance(TimeSpan duration) => ticks += duration.Ticks;
    }
}
