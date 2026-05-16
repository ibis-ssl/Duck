using Microsoft.Extensions.Configuration;

namespace Tracker.RuntimeHost;

/// <summary>
/// Tracker.RuntimeHost 固有の短い CLI 引数を .NET command-line configuration provider で解決する。
/// </summary>
public static class RuntimeHostCommandLine
{
    private const string ProfileSwitch = "--profile";
    private const string ProfileKey = "Tracker:ActiveProfileName";

    private static readonly Dictionary<string, string> SwitchMappings = new(StringComparer.Ordinal)
    {
        [ProfileSwitch] = ProfileKey,
    };

    /// <summary>
    /// `--profile <name>` または `--profile=<name>` を `Tracker:ActiveProfileName` override として追加する。
    /// </summary>
    public static void ApplyOverrides(
        IConfigurationManager configuration,
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return;
        }

        ValidateProfileArguments(args);
        configuration.AddCommandLine(args, SwitchMappings);
        if (args.Any(IsProfileOption) && string.IsNullOrWhiteSpace(configuration[ProfileKey]))
        {
            throw new ArgumentException("--profile requires a profile name.", nameof(args));
        }
    }

    private static void ValidateProfileArguments(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, ProfileSwitch, StringComparison.Ordinal))
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException("--profile requires a profile name.", nameof(args));
                }

                continue;
            }

            if (arg.StartsWith($"{ProfileSwitch}=", StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(arg[(ProfileSwitch.Length + 1)..]))
            {
                throw new ArgumentException("--profile requires a profile name.", nameof(args));
            }
        }
    }

    private static bool IsProfileOption(string arg)
    {
        return string.Equals(arg, ProfileSwitch, StringComparison.Ordinal)
               || arg.StartsWith($"{ProfileSwitch}=", StringComparison.Ordinal);
    }
}
