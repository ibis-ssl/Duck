using System.Text.Json;

namespace Tracker.Tests;

public sealed class MatchSeedBotHeadroomTests
{
    private const int ActiveRobotsPerTeam = 11;
    private const int ConfiguredBotAllowance = 16;

    [Fact]
    public void MatchSeed_ConfiguresSixteenBotsForBothTeams()
    {
        using var fixture = LoadInitialMatchState();
        var state = fixture.RootElement.GetProperty("state");
        var teamState = state.GetProperty("teamState");

        Assert.Equal(ConfiguredBotAllowance, state.GetProperty("maxBotsPerTeam").GetInt32());
        Assert.Equal(ConfiguredBotAllowance, teamState.GetProperty("BLUE").GetProperty("maxAllowedBots").GetInt32());
        Assert.Equal(ConfiguredBotAllowance, teamState.GetProperty("YELLOW").GetProperty("maxAllowedBots").GetInt32());
    }

    [Fact]
    public void MatchSeed_LeavesRoomForElevenRobotsAfterOneYellowCard()
    {
        using var fixture = LoadInitialMatchState();
        var state = fixture.RootElement.GetProperty("state");
        var maxBotsPerTeam = state.GetProperty("maxBotsPerTeam").GetInt32();
        const int activeYellowCards = 1;

        // The Game Controller recalculates the allowance as maxBotsPerTeam minus active cards.
        var allowanceAfterOneCard = maxBotsPerTeam - activeYellowCards;

        Assert.Equal(15, allowanceAfterOneCard);
        Assert.True(allowanceAfterOneCard >= ActiveRobotsPerTeam);
    }

    private static JsonDocument LoadInitialMatchState()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "match",
            "state-store-initial.json.stream");

        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
