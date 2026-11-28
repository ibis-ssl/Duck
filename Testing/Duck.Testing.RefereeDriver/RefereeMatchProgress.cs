namespace Duck.Testing.RefereeDriver;

internal sealed record RefereeRunningSample(uint CommandCounter, ulong PacketTimestamp, string Stage,
    string RefereeCommand, string GameControllerPhase, TimeSpan StageTimeLeft,
    string ApiCommand, long RequestSequence);

internal sealed record RefereeMatchProgressEvidence(RefereeRunningSample First, RefereeRunningSample Later);

internal sealed record RefereeInitialKickoff(string RefereeCommand, string Stage, uint CommandCounter, ulong PacketTimestamp);

/// <summary>Records a decreasing first-half clock over two fresh packets for one referee command.</summary>
internal sealed class RefereeMatchProgress
{
    private RefereeRunningSample? first;
    private ulong latestPacketTimestamp;

    internal RefereeInitialKickoff? InitialKickoff { get; private set; }
    internal RefereeMatchProgressEvidence? Evidence { get; private set; }

    internal void RecordInitialKickoff(RefereeCommandSnapshot verifiedNormalStart)
    {
        if (InitialKickoff is null && verifiedNormalStart is
            {
                Command: Referee.Types.Command.NormalStart,
                Stage: Referee.Types.Stage.NormalFirstHalf or Referee.Types.Stage.NormalFirstHalfPre,
                PacketTimestamp: > 0 and var packetTimestamp,
            })
        {
            InitialKickoff = new(verifiedNormalStart.Command.ToString(), verifiedNormalStart.Stage.Value.ToString(),
                verifiedNormalStart.CommandCounter, packetTimestamp);
            latestPacketTimestamp = packetTimestamp;
        }
    }

    // Windows calls this during its initial kickoff window before enabling auto-continue.
    internal void Observe(RefereeLifecycleObservation observation) => Observe(observation, kickoff: null);

    internal string? ObserveAfterKickoff(RefereeLifecycleObservation observation) =>
        InitialKickoff is { } kickoff ? Observe(observation, kickoff) : "initial_kickoff_missing";

    private string? Observe(RefereeLifecycleObservation observation, RefereeInitialKickoff? kickoff)
    {
        if (Evidence is not null)
            return null;

        if (observation.Udp.Stage != Referee.Types.Stage.NormalFirstHalf ||
            observation.ApiPhase != nameof(GameControllerMatchPhase.Running) ||
            observation.ApiStage != "NORMAL_FIRST_HALF")
            return "not_first_half_running";

        var command = observation.Udp.Command;
        var apiCommand = observation.ApiCommand;
        var matchingCommand = (command, apiCommand) is (Referee.Types.Command.NormalStart, nameof(GameControllerCommandKind.NormalStart)) ||
            kickoff is not null &&
            (command, apiCommand) is (Referee.Types.Command.ForceStart, nameof(GameControllerCommandKind.ForceStart));
        if (!matchingCommand)
            return "command_not_eligible_or_mismatched";

        if (observation.ApiStageTimeLeft is not { } timeLeft || timeLeft <= TimeSpan.Zero)
            return "clock_missing_or_nonpositive";

        if (observation.Udp.PacketTimestamp is not { } packetTimestamp ||
            kickoff is not null && packetTimestamp <= latestPacketTimestamp)
            return "packet_missing_or_stale";

        if (kickoff is not null)
        {
            if (observation.Udp.CommandCounter < kickoff.CommandCounter ||
                command == Referee.Types.Command.ForceStart && observation.Udp.CommandCounter == kickoff.CommandCounter)
                return "command_not_after_kickoff";
            latestPacketTimestamp = packetTimestamp;
        }

        var sample = new RefereeRunningSample(observation.Udp.CommandCounter, packetTimestamp,
            observation.ApiStage, command.ToString(), observation.ApiPhase, timeLeft, apiCommand, observation.RequestSequence);
        if (first is null)
        {
            first = sample;
            return null;
        }
        if (sample.CommandCounter > first.CommandCounter)
        {
            first = sample;
            return "command_changed_between_samples";
        }
        if (sample.CommandCounter != first.CommandCounter || sample.RefereeCommand != first.RefereeCommand)
            return "command_changed_between_samples";
        if (sample.PacketTimestamp <= first.PacketTimestamp)
            return "packet_missing_or_stale";
        if (sample.StageTimeLeft >= first.StageTimeLeft)
            return "clock_did_not_decrease";

        Evidence = new(first, sample);
        return null;
    }
}
