namespace Tracker.Tests;

/// <summary>SimulationLauncherTests に関する試験をまとめます。</summary>
public sealed class SimulationLauncherTests
{
    /// <summary>試験で 引数なしでは Windows native session が選ばれ、明示指定と同じ設定になることを確認します。</summary>
    [Fact]
    public void NoArgumentsSelectTheWindowsNativeSession()
    {
        var options = Launcher.Options.Parse([]);

        Assert.True(options.HostRuntime);
        Assert.False(options.Persistent);
        Assert.False(options.StopPersistent);
        Assert.Equal(options, Launcher.Options.Parse(["--host-runtime"]));
    }

    /// <summary>試験で helper-free Compose 指定が Docker session を選び、native／persistent mode を有効にしないことを確認します。</summary>
    [Fact]
    public void HelperFreeComposeSelectsTheDockerSession()
    {
        var options = Launcher.Options.Parse(["--helper-free-compose"]);

        Assert.False(options.HostRuntime);
        Assert.False(options.Persistent);
        Assert.False(options.StopPersistent);
    }

    /// <summary>試験で persistent stack が Docker mode を選び、生成時の既定 ID で停止できることを確認します。</summary>
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

    /// <summary>試験で Windows native mode と両立しない Compose／persistent／stop 指定を拒否することを確認します。</summary>
    /// <param name="option">option に指定する試験条件です。</param>
    [Theory]
    [InlineData("--persistent")]
    [InlineData("--helper-free-compose")]
    [InlineData("--stop-persistent")]
    public void ExplicitWindowsNativeModeRejectsConflictingOptions(string option)
    {
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--host-runtime", option]));
    }

    /// <summary>試験で persistent stack の停止には stack ID が必須であることを確認します。</summary>
    [Fact]
    public void StoppingAPersistentStackRequiresAnExplicitId()
    {
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--stop-persistent"]));
    }
}
