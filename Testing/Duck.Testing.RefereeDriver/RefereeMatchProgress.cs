namespace Duck.Testing.RefereeDriver;

internal sealed record RefereeRunningSample(uint CommandCounter, ulong PacketTimestamp, string Stage,
    string RefereeCommand, string GameControllerPhase, TimeSpan StageTimeLeft);

internal sealed record RefereeMatchProgressEvidence(RefereeRunningSample First, RefereeRunningSample Later);

/// <summary>Records a decreasing stage clock under the same kickoff command and two fresh referee packets.</summary>
internal sealed class RefereeMatchProgress
{
    private RefereeRunningSample? first;

    internal RefereeMatchProgressEvidence? Evidence { get; private set; }

    internal void Observe(RefereeLifecycleObservation observation)
    {
        if (Evidence is not null ||
            observation.Udp.Command != Referee.Types.Command.NormalStart ||
            observation.Udp.Stage != Referee.Types.Stage.NormalFirstHalf ||
            observation.ApiCommand != nameof(GameControllerCommandKind.NormalStart) ||
            observation.ApiPhase != nameof(GameControllerMatchPhase.Running) ||
            observation.ApiStage != "NORMAL_FIRST_HALF" ||
            observation.ApiStageTimeLeft is not { } timeLeft || timeLeft <= TimeSpan.Zero ||
            observation.Udp.PacketTimestamp is not { } packetTimestamp)
            return;

        var sample = new RefereeRunningSample(observation.Udp.CommandCounter, packetTimestamp,
            observation.ApiStage, observation.Udp.Command.ToString(), observation.ApiPhase, timeLeft);
        if (first is null || sample.CommandCounter > first.CommandCounter)
            first = sample;
        else if (sample.CommandCounter == first.CommandCounter &&
            sample.PacketTimestamp > first.PacketTimestamp && sample.StageTimeLeft < first.StageTimeLeft)
            Evidence = new(first, sample);
    }
}
