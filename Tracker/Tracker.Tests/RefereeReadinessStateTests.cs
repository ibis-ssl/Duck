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
