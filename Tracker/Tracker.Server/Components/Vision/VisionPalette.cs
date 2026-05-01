namespace Tracker.Server.Components.Vision;

internal static class VisionPalette
{
    public const string TeamYellow = "#c4a932";
    public const string TeamBlue = "#3d63bc";
    public const string TeamYellowBright = "#f4d233";
    public const string TeamBlueBright = "#2f6fff";
    public const string MarkerStroke = "#ffffff";
    public const string MarkerTextStroke = "rgba(0, 0, 0, 0.38)";

    public static string TeamFill(string className)
    {
        return className.Contains("yellow", StringComparison.Ordinal)
            ? TeamYellow
            : TeamBlue;
    }

    public static string GoalStroke(string modifierClass)
    {
        return modifierClass.Contains("yellow", StringComparison.Ordinal)
            ? TeamYellowBright
            : TeamBlueBright;
    }
}
