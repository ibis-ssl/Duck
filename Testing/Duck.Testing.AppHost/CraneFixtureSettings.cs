using Microsoft.Extensions.Configuration;

namespace Duck.Testing.AppHost;

public sealed record CraneFixtureSettings(string TeamName, string TeamColor)
{
    public const string TeamNameConfigurationKey = "Testing:Crane:TeamName";
    public const string TeamColorConfigurationKey = "Testing:Crane:TeamColor";
    public const string DefaultTeamName = "crane";
    public const string YellowTeamColor = "YELLOW";

    public static CraneFixtureSettings FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return FromValues(
            configuration[TeamNameConfigurationKey] ?? DefaultTeamName,
            configuration[TeamColorConfigurationKey] ?? YellowTeamColor);
    }

    public static CraneFixtureSettings FromValues(string teamName, string teamColor)
    {
        if (string.IsNullOrWhiteSpace(teamName) || teamName.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new InvalidOperationException(
                $"{TeamNameConfigurationKey} must contain only ASCII letters, digits, underscores, or hyphens.");
        }

        if (teamColor != YellowTeamColor)
        {
            throw new InvalidOperationException(
                $"This acceptance fixture supports {TeamColorConfigurationKey}={YellowTeamColor} only; received '{teamColor}'.");
        }

        return new CraneFixtureSettings(teamName, teamColor);
    }

    public bool MatchesExpectedYellowTeamName(string? actualTeamName) =>
        string.Equals(TeamName, actualTeamName, StringComparison.Ordinal);
}
