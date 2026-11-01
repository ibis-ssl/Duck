using Microsoft.Extensions.Configuration;

namespace Duck.Testing.AppHost;

internal sealed record CraneTargetAddressSettings(
    string TargetAddress,
    string ExpectedResourceName,
    string ScriptSource)
{
    internal const string ConfigurationKey = "Testing:Crane:TargetAddress";
    internal const string TargetAddressEnvironmentKey = "DUCK_CRANE_TARGET_ADDRESS";
    internal const string ExpectedResourceEnvironmentKey = "DUCK_CRANE_EXPECTED_RESOURCE";
    internal const string ScriptMountTarget = "/duck/crane-launch-overlay.py";

    internal static CraneTargetAddressSettings? FromConfiguration(
        IConfiguration configuration,
        string planner,
        string appHostBaseDirectory,
        bool useStandardBase = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(planner);
        ArgumentException.ThrowIfNullOrWhiteSpace(appHostBaseDirectory);

        var target = configuration[ConfigurationKey];
        if (target is null && useStandardBase && planner == "visibility_graph")
        {
            target = "cm4-sim";
        }

        if (target is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            throw InvalidTarget("must not be empty when specified");
        }

        var expectedResource = planner == "visibility_graph" ? "cm4-sim" : "simulator";
        if (target is "cm4-sim" or "simulator")
        {
            if (!string.Equals(target, expectedResource, StringComparison.Ordinal))
            {
                throw InvalidTarget($"does not match the selected planner resource '{expectedResource}'");
            }
        }
        else if (!string.Equals(target, "127.0.0.1", StringComparison.Ordinal))
        {
            throw InvalidTarget("must be the selected simulator resource name or the host-network loopback address 127.0.0.1");
        }

        var scriptSource = Path.GetFullPath(Path.Combine(appHostBaseDirectory, "CraneLaunchOverlay.py"));
        if (!File.Exists(scriptSource))
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} requires the packaged Crane launch helper at the AppHost output path.");
        }

        return new CraneTargetAddressSettings(target, expectedResource, scriptSource);
    }

    private static InvalidOperationException InvalidTarget(string reason) =>
        new($"{ConfigurationKey} {reason}.");
}
