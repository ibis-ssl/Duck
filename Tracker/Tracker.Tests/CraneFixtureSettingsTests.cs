using Duck.Testing.AppHost;
using Microsoft.Extensions.Configuration;

namespace Tracker.Tests;

[CollectionDefinition("Process environment", DisableParallelization = true)]
public sealed class ProcessEnvironmentTestCollection
{
}

[Collection("Process environment")]
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
    public void ResolvesNondefaultTeamNameFromJsonConfiguration()
    {
        using var json = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            """{"Testing":{"Crane":{"TeamName":"json_crane"}}}"""));
        var configuration = new ConfigurationBuilder().AddJsonStream(json).Build();

        Assert.Equal("json_crane", CraneFixtureSettings.FromConfiguration(configuration).TeamName);
    }

    [Fact]
    public void ResolvesNondefaultTeamNameFromEnvironmentConfiguration()
    {
        const string variable = "Testing__Crane__TeamName";
        var previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, "environment_crane");
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            Assert.Equal("environment_crane", CraneFixtureSettings.FromConfiguration(configuration).TeamName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
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
