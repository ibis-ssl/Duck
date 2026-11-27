namespace Tracker.Tests;

/// <summary>起動ランチャーの既定値、引数の組み合わせと永続スタックの停止条件を検証します。</summary>
public sealed class SimulationLauncherTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("60")]
    [InlineData("148")]
    public void MatchHalfSecondsAcceptsValidValues(string value)
    {
        var options = Launcher.Options.Parse(["--play-match", "--half-seconds", value]);
        Assert.Equal(int.Parse(value), options.HalfSeconds);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("149")]
    [InlineData("abc")]
    public void MatchHalfSecondsRejectsInvalidValues(string value) =>
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--play-match", "--half-seconds", value]));

    [Fact]
    public void MatchHalfSecondsDefaultsAndRejectsDuplicateOrMissingValue()
    {
        Assert.Equal(45, Launcher.Options.Parse(["--play-match"]).HalfSeconds);
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--play-match", "--half-seconds"]));
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--play-match", "--half-seconds", "45", "--half-seconds", "60"]));
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--play-match", "--helper-free-compose", "--half-seconds", "60"]));
    }

    [Fact]
    public void HalfSecondsRequiresMatch() =>
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--half-seconds", "60"]));

    /// <summary>引数なしでは Windows native session が選ばれ、明示指定と同じ設定になることを確認します。</summary>
    [Fact]
    public void NoArgumentsSelectTheWindowsNativeSession()
    {
        var options = Launcher.Options.Parse([]);

        Assert.True(options.HostRuntime);
        Assert.False(options.Persistent);
        Assert.False(options.StopPersistent);
        Assert.Equal(options, Launcher.Options.Parse(["--host-runtime"]));
    }

    /// <summary>helper-free Compose 指定が Docker session を選び、native／persistent mode を有効にしないことを確認します。</summary>
    [Fact]
    public void HelperFreeComposeSelectsTheDockerSession()
    {
        var options = Launcher.Options.Parse(["--helper-free-compose"]);

        Assert.False(options.HostRuntime);
        Assert.False(options.Persistent);
        Assert.False(options.StopPersistent);
    }

    /// <summary>persistent stack が Docker mode を選び、生成時の既定 ID で停止できることを確認します。</summary>
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

    /// <summary>Windows native mode と両立しない Compose／persistent／stop 指定を拒否することを確認します。</summary>
    /// <param name="option">Windows の RuntimeHost と組み合わせられない起動オプションです。</param>
    [Theory]
    [InlineData("--persistent")]
    [InlineData("--helper-free-compose")]
    [InlineData("--stop-persistent")]
    public void ExplicitWindowsNativeModeRejectsConflictingOptions(string option)
    {
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--host-runtime", option]));
    }

    /// <summary>persistent stack の停止には stack ID が必須であることを確認します。</summary>
    [Fact]
    public void StoppingAPersistentStackRequiresAnExplicitId()
    {
        Assert.Throws<ArgumentException>(() => Launcher.Options.Parse(["--stop-persistent"]));
    }
}
