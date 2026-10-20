using Duck.Testing.AppHost;
using Microsoft.Extensions.Configuration;

namespace Tracker.Tests;

public sealed class CraneFixtureSettingsTests
{
    [Fact]
    public void DefaultsToCraneAndYellow()
    {
        var settings = CraneFixtureSettings.FromConfiguration(new ConfigurationBuilder().Build());

        Assert.Equal("crane", settings.TeamName);
        Assert.Equal("YELLOW", settings.TeamColor);
        Assert.True(settings.MatchesExpectedYellowTeamName("crane"));
        Assert.False(settings.MatchesExpectedYellowTeamName("custom-team"));
    }

    [Fact]
    public void UsesTheConfiguredCustomTeamNameForAcceptanceChecks()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CraneFixtureSettings.TeamNameConfigurationKey] = "custom-team_7",
            })
            .Build();
        var settings = CraneFixtureSettings.FromConfiguration(configuration);

        Assert.Equal("custom-team_7", settings.TeamName);
        Assert.True(settings.MatchesExpectedYellowTeamName("custom-team_7"));
        Assert.False(settings.MatchesExpectedYellowTeamName("crane"));
    }

    [Fact]
    public void RejectsBlueBeforeTheAppHostCreatesResources()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CraneFixtureSettings.TeamColorConfigurationKey] = "BLUE",
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => CraneFixtureSettings.FromConfiguration(configuration));

        Assert.Contains("supports Testing:Crane:TeamColor=YELLOW only", exception.Message);
    }
}
