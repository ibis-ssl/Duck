using Duck.Testing.Diagnostics;

namespace Tracker.Tests;

public sealed class ResourceReadinessStateTests
{
    private static readonly ResourceDiagnosticIdentity Identity = new("run-a", "duck", new string('a', 64), 1);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

    [Fact]
    public void InitialStateAndFirstObservationRequireProgress()
    {
        var state = Create(out _);
        Assert.Equal(ResourceReadinessReason.NoObservation, state.GetReadiness().Reason);

        Assert.True(state.Observe(Snapshot(1, 10)));
        Assert.False(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.AwaitingProgress, state.GetReadiness().Reason);
    }

    [Fact]
    public void MatchingServiceDecodeCountIncreaseBecomesReady()
    {
        var state = Ready(out _);
        Assert.True(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.Ready, state.GetReadiness().Reason);
    }

    [Theory]
    [InlineData("run-b", "duck", 'a', 1)]
    [InlineData("run-a", "other", 'a', 1)]
    [InlineData("run-a", "duck", 'b', 1)]
    [InlineData("run-a", "duck", 'a', 2)]
    public void IdentityMismatchCannotEstablishOrReplaceProgress(string run, string resource, char id, long generation)
    {
        var state = Create(out _);
        var foreign = new ResourceDiagnosticIdentity(run, resource, new string(id, 64), generation);
        Assert.False(state.Observe(Snapshot(1, 10, identity: foreign)));
        Assert.Equal(ResourceReadinessReason.NoObservation, state.GetReadiness().Reason);

        state = Ready(out _);
        Assert.False(state.Observe(Snapshot(3, 100, identity: foreign)));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Fact]
    public void WrongInputCannotSatisfyTheExpectedInput()
    {
        var state = Create(out _);
        Assert.False(state.Observe(Snapshot(1, 10) with { InputId = "referee" }));
        Assert.Equal(ResourceReadinessReason.NoObservation, state.GetReadiness().Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("aaaaaaaaaaaa")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void IdentityRequiresAFullContainerId(string containerId)
    {
        Assert.Throws<ArgumentException>(() => new ResourceDiagnosticIdentity("run", "resource", containerId, 1));
    }

    [Fact]
    public void IdentityValuesCompareExactly()
    {
        Assert.Equal(Identity, new ResourceDiagnosticIdentity("run-a", "duck", new string('a', 64), 1));
        Assert.NotEqual(Identity, new ResourceDiagnosticIdentity("RUN-A", "duck", new string('a', 64), 1));
        Assert.Throws<ArgumentException>(() => new ResourceDiagnosticIdentity(" ", "duck", new string('a', 64), 1));
        Assert.Throws<ArgumentException>(() => new ResourceDiagnosticIdentity("run", "", new string('a', 64), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceDiagnosticIdentity("run", "duck", new string('a', 64), 0));
    }

    [Theory]
    [InlineData(DiagnosticEvidenceKind.AuxiliaryObservation)]
    [InlineData(DiagnosticEvidenceKind.Unknown)]
    [InlineData((DiagnosticEvidenceKind)99)]
    public void AuxiliaryOrUnknownEvidenceCannotBecomeReady(DiagnosticEvidenceKind kind)
    {
        var state = Ready(out _);
        Assert.True(state.Observe(Snapshot(3, 11) with { EvidenceKind = kind }));
        Assert.False(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.NonServiceEvidence, state.GetReadiness().Reason);
    }

    [Theory]
    [InlineData(DiagnosticDecodeStatus.Failed)]
    [InlineData(DiagnosticDecodeStatus.Unknown)]
    [InlineData((DiagnosticDecodeStatus)99)]
    public void FailedOrUnknownDecodeInvalidatesReadiness(DiagnosticDecodeStatus status)
    {
        var state = Ready(out _);
        Assert.True(state.Observe(Snapshot(3, 11) with { DecodeStatus = status }));
        Assert.Equal(ResourceReadinessReason.DecodeUnsuccessful, state.GetReadiness().Reason);
        Assert.False(state.GetReadiness().IsReady);
        Assert.True(state.Observe(Snapshot(4, 12)));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Fact]
    public void UnknownAgeCannotBeReplacedByThePollTime()
    {
        var state = Ready(out var clock);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(state.Observe(Snapshot(3, 11) with { DecodeAgeUpperBound = null }));
        Assert.False(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.UnknownAge, state.GetReadiness().Reason);
    }

    [Fact]
    public void NegativeAgeIsNotFresh()
    {
        var state = Ready(out _);
        state.Observe(Snapshot(3, 11) with { DecodeAgeUpperBound = TimeSpan.FromTicks(-1) });
        Assert.Equal(ResourceReadinessReason.InvalidAge, state.GetReadiness().Reason);
        Assert.False(state.GetReadiness().IsReady);
    }

    [Fact]
    public void AgeAndLocalElapsedTimeShareOneFiniteWindow()
    {
        var state = Create(out var clock);
        state.Observe(Snapshot(1, 0));
        state.Observe(Snapshot(2, 1) with { DecodeAgeUpperBound = TimeSpan.FromSeconds(2) });
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.True(state.GetReadiness().IsReady);
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.False(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.Expired, state.GetReadiness().Reason);
    }

    [Fact]
    public void SnapshotThatIsAlreadyStaleCannotBecomeReady()
    {
        var state = Ready(out _);
        state.Observe(Snapshot(3, 11) with { DecodeAgeUpperBound = Window + TimeSpan.FromTicks(1) });
        Assert.False(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.Expired, state.GetReadiness().Reason);
    }

    [Fact]
    public void RepeatedCountCannotExtendReadinessOrRecoverExpiredProgress()
    {
        var state = Ready(out var clock);
        clock.Advance(TimeSpan.FromSeconds(4));
        state.Observe(Snapshot(3, 10));
        clock.Advance(TimeSpan.FromSeconds(2));
        state.Observe(Snapshot(4, 10));
        Assert.False(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.Expired, state.GetReadiness().Reason);
        state.Observe(Snapshot(5, 11));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void DuplicateOrOldObservationCannotExtendProgress(long sequence)
    {
        var state = Ready(out var clock);
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(state.Observe(Snapshot(sequence, 100)));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(state.GetReadiness().IsReady);
    }

    [Fact]
    public void LateFailureCannotOverwriteNewerValidEvidence()
    {
        var state = Ready(out _);
        Assert.False(state.Observe(Snapshot(1, 0) with { DecodeStatus = DiagnosticDecodeStatus.Failed }));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Fact]
    public void LatestFailureConsumesSequenceAndNeedsNewValidProgress()
    {
        var state = Ready(out _);
        state.Observe(Snapshot(4, 10) with { DecodeStatus = DiagnosticDecodeStatus.Failed });
        Assert.False(state.Observe(Snapshot(3, 11)));
        Assert.False(state.GetReadiness().IsReady);
        state.Observe(Snapshot(5, 10));
        Assert.False(state.GetReadiness().IsReady);
        state.Observe(Snapshot(6, 11));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Fact]
    public void CountRegressionIsNotAnImplicitGenerationReset()
    {
        var state = Ready(out _);
        state.Observe(Snapshot(3, 1));
        Assert.Equal(ResourceReadinessReason.CountRegressed, state.GetReadiness().Reason);
        state.Observe(Snapshot(4, 2));
        Assert.False(state.GetReadiness().IsReady);
        state.Observe(Snapshot(5, 11));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Fact]
    public void NegativeCountIsInvalid()
    {
        var state = Ready(out _);
        state.Observe(Snapshot(3, -1));
        Assert.False(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.InvalidCount, state.GetReadiness().Reason);
    }

    [Fact]
    public void LargeCountsAreComparedWithoutAnOverflowingDelta()
    {
        var state = Create(out _);
        state.Observe(Snapshot(1, long.MaxValue - 1));
        state.Observe(Snapshot(2, long.MaxValue));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdvancingGenerationClearsBaselineAndRejectsLateOldIdentity(bool sameContainer)
    {
        var state = Ready(out _);
        var next = new ResourceDiagnosticIdentity("run-a", "duck", new string(sameContainer ? 'a' : 'b', 64), 2);
        state.AdvanceGeneration(next);
        Assert.Equal(ResourceReadinessReason.NoObservation, state.GetReadiness().Reason);
        Assert.False(state.Observe(Snapshot(100, 100)));
        Assert.True(state.Observe(Snapshot(1, 1, identity: next)));
        Assert.False(state.GetReadiness().IsReady);
        state.Observe(Snapshot(2, 2, identity: next));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Fact]
    public void GenerationCannotGoBackOrSwitchToAnotherResource()
    {
        var state = Ready(out _);
        Assert.Throws<ArgumentException>(() => state.AdvanceGeneration(Identity));
        Assert.Throws<ArgumentException>(() => state.AdvanceGeneration(new("other-run", "duck", new string('b', 64), 2)));
        Assert.Throws<ArgumentException>(() => state.AdvanceGeneration(new("run-a", "other-resource", new string('b', 64), 2)));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Fact]
    public void BackwardLocalClockDoesNotMakeEvidenceFresh()
    {
        var state = Ready(out var clock);
        clock.Advance(TimeSpan.FromTicks(-1));
        Assert.False(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.InvalidClock, state.GetReadiness().Reason);
    }

    [Fact]
    public void BackwardClockAfterUnchangedPollInvalidatesReadiness()
    {
        var state = Ready(out var clock);
        clock.Advance(TimeSpan.FromSeconds(4));
        state.Observe(Snapshot(3, 10));
        clock.Advance(TimeSpan.FromSeconds(-1));
        Assert.False(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.InvalidClock, state.GetReadiness().Reason);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(state.GetReadiness().IsReady);
        state.Observe(Snapshot(4, 11));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(6, 4)]
    public void ReadToReadClockRegressionInvalidatesProgress(int firstReadSeconds, int secondReadSeconds)
    {
        var state = Ready(out var clock);
        clock.Advance(TimeSpan.FromSeconds(firstReadSeconds));
        Assert.Equal(firstReadSeconds <= 5, state.GetReadiness().IsReady);

        clock.Advance(TimeSpan.FromSeconds(secondReadSeconds - firstReadSeconds));
        Assert.False(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.InvalidClock, state.GetReadiness().Reason);

        clock.Advance(TimeSpan.FromSeconds(firstReadSeconds - secondReadSeconds));
        Assert.False(state.GetReadiness().IsReady);
        state.Observe(Snapshot(3, 10));
        Assert.False(state.GetReadiness().IsReady);
        state.Observe(Snapshot(4, 11));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(6, 4)]
    public void ObserveRejectsClockEarlierThanLastRead(int readSeconds, int observeSeconds)
    {
        var state = Ready(out var clock);
        clock.Advance(TimeSpan.FromSeconds(readSeconds));
        state.GetReadiness();

        clock.Advance(TimeSpan.FromSeconds(observeSeconds - readSeconds));
        Assert.True(state.Observe(Snapshot(3, 11)));
        Assert.False(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.InvalidClock, state.GetReadiness().Reason);

        clock.Advance(TimeSpan.FromSeconds(readSeconds - observeSeconds));
        Assert.False(state.GetReadiness().IsReady);
        Assert.False(state.Observe(Snapshot(3, 11)));
        state.Observe(Snapshot(4, 10));
        Assert.False(state.GetReadiness().IsReady);
        state.Observe(Snapshot(5, 11));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Fact]
    public void NonReadyReadAdvancesClockHighWaterMark()
    {
        var state = Create(out var clock);
        clock.Advance(TimeSpan.FromSeconds(6));
        Assert.Equal(ResourceReadinessReason.NoObservation, state.GetReadiness().Reason);

        clock.Advance(TimeSpan.FromSeconds(-2));
        state.Observe(Snapshot(1, 10));
        Assert.False(state.GetReadiness().IsReady);
        Assert.Equal(ResourceReadinessReason.InvalidClock, state.GetReadiness().Reason);

        clock.Advance(TimeSpan.FromSeconds(2));
        state.Observe(Snapshot(2, 10));
        Assert.False(state.GetReadiness().IsReady);
        state.Observe(Snapshot(3, 11));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Fact]
    public void NewGenerationResetsClockHighWaterMarkAndProgress()
    {
        var state = Ready(out var clock);
        clock.Advance(TimeSpan.FromSeconds(6));
        Assert.Equal(ResourceReadinessReason.Expired, state.GetReadiness().Reason);
        clock.Advance(TimeSpan.FromSeconds(-2));
        Assert.Equal(ResourceReadinessReason.InvalidClock, state.GetReadiness().Reason);

        var next = new ResourceDiagnosticIdentity("run-a", "duck", new string('b', 64), 2);
        state.AdvanceGeneration(next);
        Assert.Equal(ResourceReadinessReason.NoObservation, state.GetReadiness().Reason);
        Assert.False(state.Observe(Snapshot(100, 100)));
        state.Observe(Snapshot(1, 0, identity: next));
        Assert.False(state.GetReadiness().IsReady);
        state.Observe(Snapshot(2, 1, identity: next));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Fact]
    public void ExpiredReadNeedsNewCountBeforeReady()
    {
        var state = Ready(out var clock);
        clock.Advance(TimeSpan.FromSeconds(6));
        Assert.Equal(ResourceReadinessReason.Expired, state.GetReadiness().Reason);
        state.Observe(Snapshot(3, 10));
        Assert.False(state.GetReadiness().IsReady);
        state.Observe(Snapshot(4, 11));
        Assert.True(state.GetReadiness().IsReady);
    }

    [Fact]
    public void FreshnessAdditionDoesNotOverflow()
    {
        var state = Create(out var clock);
        state.Observe(Snapshot(1, 0));
        state.Observe(Snapshot(2, 1) with { DecodeAgeUpperBound = TimeSpan.MaxValue });
        Assert.False(state.GetReadiness().IsReady);
        clock.Advance(TimeSpan.FromDays(365));
        Assert.False(state.GetReadiness().IsReady);
    }

    [Fact]
    public void ConstructorRejectsNonPositiveWindowOrMissingInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceReadinessState(Identity, "vision", TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceReadinessState(Identity, "vision", TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentException>(() => new ResourceReadinessState(Identity, " ", Window));
    }

    private static ResourceReadinessState Create(out ManualTimeProvider clock)
    {
        clock = new ManualTimeProvider();
        return new ResourceReadinessState(Identity, "vision", Window, clock);
    }

    private static ResourceReadinessState Ready(out ManualTimeProvider clock)
    {
        var state = Create(out clock);
        state.Observe(Snapshot(1, 9));
        state.Observe(Snapshot(2, 10));
        return state;
    }

    private static ResourceDiagnosticSnapshot Snapshot(long sequence, long count, ResourceDiagnosticIdentity? identity = null) =>
        new(identity ?? Identity, "vision", sequence, DiagnosticEvidenceKind.ServiceDecode,
            DiagnosticDecodeStatus.Succeeded, count, TimeSpan.Zero);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp = TimeSpan.TicksPerSecond;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public void Advance(TimeSpan elapsed) => timestamp += elapsed.Ticks;
    }
}
