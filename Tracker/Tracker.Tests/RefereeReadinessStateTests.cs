using Google.Protobuf;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class RefereeReadinessStateTests
{
    [Fact]
    public void GameControllerRequiresInitialHaltThenAcceptsKnownTransitionsWhileFresh()
    {
        var state = new RefereeReadinessState(expectedInitialCommand: 0, TimeSpan.FromSeconds(3));
        var now = DateTimeOffset.UtcNow;

        state.Observe(command: 1, now);
        Assert.False(state.InitialCommandObserved);
        Assert.False(state.IsHealthy(now));

        state.Observe(command: 0, now);
        Assert.True(state.InitialCommandObserved);
        Assert.True(state.IsHealthy(now));

        state.Observe(command: 2, now.AddSeconds(1));
        Assert.True(state.InitialCommandObserved);
        Assert.True(state.IsHealthy(now.AddSeconds(1)));
    }

    [Fact]
    public void GameControllerWaitsForExactYellowTeamNameBeforeReportingHealthy()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new RefereeReadinessState(
            expectedInitialCommand: 0,
            freshnessWindow: TimeSpan.FromSeconds(3),
            expectedYellowTeamName: "crane");

        state.Observe(command: 0, now, yellowTeamName: "Unknown");
        Assert.True(state.IsCommandHealthy(now));
        Assert.False(state.HasExpectedYellowTeamName);
        Assert.False(state.IsHealthy(now));

        state.Observe(command: 0, now.AddSeconds(1), yellowTeamName: "Crane");
        Assert.False(state.HasExpectedYellowTeamName);
        Assert.False(state.IsHealthy(now.AddSeconds(1)));

        state.Observe(command: 1, now.AddSeconds(2), yellowTeamName: "crane");
        Assert.True(state.HasExpectedYellowTeamName);
        Assert.True(state.IsHealthy(now.AddSeconds(2)));
    }

    [Fact]
    public void MissingYellowTeamPacketIsUnhealthyAndAValidPacketRecoversReadiness()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new RefereeReadinessState(
            expectedInitialCommand: (int)Referee.Types.Command.Halt,
            expectedYellowTeamName: "crane");
        var missingYellowPacket = global::Referee.Parser.ParseFrom(
            new global::Referee { Command = Referee.Types.Command.Halt }.ToByteArray());

        GameControllerReadinessMonitor.ObserveRefereePacket(state, missingYellowPacket, now);

        Assert.True(state.IsCommandHealthy(now));
        Assert.False(state.HasExpectedYellowTeamName);
        Assert.False(state.IsHealthy(now));

        var validPacket = new global::Referee
        {
            Command = Referee.Types.Command.NormalStart,
            Yellow = new global::Referee.Types.TeamInfo { Name = "crane" },
        };
        GameControllerReadinessMonitor.ObserveRefereePacket(state, validPacket, now.AddSeconds(1));

        Assert.True(state.HasExpectedYellowTeamName);
        Assert.True(state.IsHealthy(now.AddSeconds(1)));
    }

    [Fact]
    public void TeamNameConfigurationRetriesAtIntervalsThenStopsAndConfirmationSuppressesFurtherSends()
    {
        var policy = new GameControllerTeamNameRetryPolicy();
        var now = DateTimeOffset.UtcNow;

        Assert.True(policy.TryBeginSend(now, expectedNameObserved: false));
        Assert.False(policy.TryBeginSend(now.AddMilliseconds(999), expectedNameObserved: false));
        Assert.True(policy.TryBeginSend(now.AddSeconds(1), expectedNameObserved: false));
        Assert.True(policy.TryBeginSend(now.AddSeconds(2), expectedNameObserved: false));
        Assert.False(policy.TryBeginSend(now.AddSeconds(3), expectedNameObserved: false));
        Assert.False(policy.TryBeginSend(now.AddSeconds(4), expectedNameObserved: true));
    }

    [Fact]
    public void TeamNameConfigurationSuppressesRetryImmediatelyAfterExpectedNameIsObserved()
    {
        var policy = new GameControllerTeamNameRetryPolicy();
        var now = DateTimeOffset.UtcNow;

        Assert.True(policy.TryBeginSend(now, expectedNameObserved: false));
        Assert.False(policy.TryBeginSend(now.AddSeconds(1), expectedNameObserved: true));
    }

    [Fact]
    public void UnknownOrStaleCommandIsUnhealthyAndNewGenerationMustSeeHaltAgain()
    {
        var now = DateTimeOffset.UtcNow;
        var generation = new RefereeReadinessState(expectedInitialCommand: 0, TimeSpan.FromSeconds(3));
        generation.Observe(command: 0, now);
        Assert.True(generation.IsHealthy(now));

        generation.Observe(command: 22, now.AddSeconds(1));
        Assert.False(generation.IsHealthy(now.AddSeconds(1)));

        generation.Observe(command: 1, now.AddSeconds(2));
        Assert.True(generation.IsHealthy(now.AddSeconds(2)));
        Assert.False(generation.IsHealthy(now.AddSeconds(6)));

        var restartedGeneration = new RefereeReadinessState(expectedInitialCommand: 0);
        restartedGeneration.Observe(command: 1, now.AddSeconds(7));
        Assert.False(restartedGeneration.InitialCommandObserved);
        Assert.False(restartedGeneration.IsHealthy(now.AddSeconds(7)));
        restartedGeneration.Observe(command: 0, now.AddSeconds(8));
        Assert.True(restartedGeneration.InitialCommandObserved);
        Assert.True(restartedGeneration.IsHealthy(now.AddSeconds(8)));
    }
}
