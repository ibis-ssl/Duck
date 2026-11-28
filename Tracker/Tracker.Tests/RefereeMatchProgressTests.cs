using Duck.Testing.RefereeDriver;

namespace Tracker.Tests;

public sealed class RefereeMatchProgressTests
{
    [Theory]
    [InlineData(Referee.Types.Command.NormalStart, 2u)]
    [InlineData(Referee.Types.Command.ForceStart, 12u)]
    public void FirstHalfClockAfterConfirmedKickoffCanConfirmProgress(Referee.Types.Command command, uint counter)
    {
        var progress = new RefereeMatchProgress();
        progress.RecordInitialKickoff(Kickoff());
        var first = Sample(command, counter, 200, 37);
        var later = Sample(command, counter, 201, 36);

        Assert.Null(progress.ObserveAfterKickoff(first));
        Assert.Null(progress.Evidence);
        Assert.Null(progress.ObserveAfterKickoff(later));

        Assert.Equal(2u, progress.InitialKickoff!.CommandCounter);
        var evidence = Assert.IsType<RefereeMatchProgressEvidence>(progress.Evidence);
        Assert.Equal(command.ToString(), evidence.First.RefereeCommand);
        Assert.Equal(command.ToString(), evidence.Later.ApiCommand);
        Assert.Equal(TimeSpan.FromSeconds(37), evidence.First.StageTimeLeft);
        Assert.Equal(TimeSpan.FromSeconds(36), evidence.Later.StageTimeLeft);
    }

    [Fact]
    public void ForceStartCannotStandInForTheInitialKickoff()
    {
        var progress = new RefereeMatchProgress();
        progress.RecordInitialKickoff(Kickoff() with { Command = Referee.Types.Command.ForceStart });
        Assert.Equal("initial_kickoff_missing", progress.ObserveAfterKickoff(ForceStart(12, 200, 37)));
        Assert.Equal("initial_kickoff_missing", progress.ObserveAfterKickoff(ForceStart(12, 201, 36)));
        Assert.Null(progress.InitialKickoff);
        Assert.Null(progress.Evidence);
    }

    [Theory]
    [InlineData("stopped_clock", "clock_did_not_decrease")]
    [InlineData("zero_clock", "clock_missing_or_nonpositive")]
    [InlineData("counter_changed", "command_changed_between_samples")]
    [InlineData("command_changed", "command_changed_between_samples")]
    [InlineData("api_command_mismatch", "command_not_eligible_or_mismatched")]
    [InlineData("duplicate_packet", "packet_missing_or_stale")]
    [InlineData("stale_packet", "packet_missing_or_stale")]
    public void InvalidPairDoesNotConfirmProgress(string invalid, string rejection)
    {
        var progress = new RefereeMatchProgress();
        progress.RecordInitialKickoff(Kickoff());
        var baseline = ForceStart(12, 200, 37);
        var later = invalid switch
        {
            "stopped_clock" => ForceStart(12, 201, 37),
            "zero_clock" => ForceStart(12, 201, 0),
            "counter_changed" => ForceStart(13, 201, 36),
            "command_changed" => Sample(Referee.Types.Command.NormalStart, 12, 201, 36),
            "api_command_mismatch" => ForceStart(12, 201, 36) with { ApiCommand = "NormalStart" },
            "duplicate_packet" => ForceStart(12, 200, 36),
            "stale_packet" => ForceStart(12, 199, 36),
            _ => throw new ArgumentOutOfRangeException(nameof(invalid)),
        };

        Assert.Null(progress.ObserveAfterKickoff(baseline));
        Assert.Equal(rejection, progress.ObserveAfterKickoff(later));
        Assert.Null(progress.Evidence);
    }

    [Fact]
    public void SamplesMustFollowTheKickoffAndTheLatestEligiblePacket()
    {
        var progress = new RefereeMatchProgress();
        progress.RecordInitialKickoff(Kickoff());

        Assert.Equal("command_not_after_kickoff", progress.ObserveAfterKickoff(ForceStart(2, 101, 38)));
        Assert.Equal("packet_missing_or_stale", progress.ObserveAfterKickoff(ForceStart(12, 100, 38)));
        Assert.Null(progress.ObserveAfterKickoff(ForceStart(12, 200, 37)));
        Assert.Equal("clock_did_not_decrease", progress.ObserveAfterKickoff(ForceStart(12, 202, 37)));
        Assert.Equal("packet_missing_or_stale", progress.ObserveAfterKickoff(ForceStart(12, 201, 36)));
        Assert.Null(progress.Evidence);
        Assert.Null(progress.ObserveAfterKickoff(ForceStart(12, 203, 36)));
        Assert.Equal(203ul, progress.Evidence!.Later.PacketTimestamp);
    }

    private static RefereeCommandSnapshot Kickoff() =>
        new(Referee.Types.Command.NormalStart, 2, Stage: Referee.Types.Stage.NormalFirstHalfPre, PacketTimestamp: 100);

    private static RefereeLifecycleObservation ForceStart(uint counter, ulong packet, int seconds) =>
        Sample(Referee.Types.Command.ForceStart, counter, packet, seconds);

    private static RefereeLifecycleObservation Sample(Referee.Types.Command command, uint counter, ulong packet, int seconds) =>
        new((long)packet, new(command, counter, Stage: Referee.Types.Stage.NormalFirstHalf, PacketTimestamp: packet),
            command.ToString(), "Running", "NORMAL_FIRST_HALF", TimeSpan.FromSeconds(seconds));
}
