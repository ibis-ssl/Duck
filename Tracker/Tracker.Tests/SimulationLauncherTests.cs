namespace Tracker.Tests;

public sealed class SimulationLauncherTests
{
    [Fact]
    public void NoArgumentsSelectTheWindowsNativeSession()
    {
        var options = Launcher.Options.Parse([]);

        Assert.True(options.HostRuntime);
        Assert.False(options.Persistent);
        Assert.False(options.StopPersistent);
        Assert.Equal(options, Launcher.Options.Parse(["--host-runtime"]));
    }

    [Fact]
    public void HelperFreeComposeSelectsTheDockerSession()
    {
        var options = Launcher.Options.Parse(["--helper-free-compose"]);

        Assert.False(options.HostRuntime);
        Assert.False(options.Persistent);
        Assert.False(options.StopPersistent);
    }

    [Fact]
    public void PersistentStackUsesDockerAndCanBeStoppedUsingItsDefaultId()
    {
        var setup = Launcher.Options.Parse(["--persistent"]);
        var teardown = Launcher.Options.Parse(["--stop-persistent", "--stack-id", setup.StackId]);

        Assert.True(setup.Persistent);
        Assert.False(setup.HostRuntime);
        Assert.True(teardown.StopPersistent);
        Assert.False(teardown.HostRuntime);
        Assert.Equal(setup.StackId, teardown.StackId);
    }

    [Theory]
    [InlineData("--persistent")]
    [InlineData("--helper-free-compose")]
    [InlineData("--stop-persistent")]
    public void ExplicitWindowsNativeModeRejectsConflictingOptions(string option)
    {
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--host-runtime", option]));
    }

    [Fact]
    public void StoppingAPersistentStackRequiresAnExplicitId()
    {
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--stop-persistent"]));
    }
}
