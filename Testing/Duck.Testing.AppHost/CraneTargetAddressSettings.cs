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
    internal const string ScriptMountTarget = "/duck/crane-launch-overlay";

    internal static CraneTargetAddressSettings? FromConfiguration(
        IConfiguration configuration,
        string planner,
        string appHostBaseDirectory,
        bool useStandardBase = false,
        bool useMode4PacketAdapter = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(planner);
        ArgumentException.ThrowIfNullOrWhiteSpace(appHostBaseDirectory);

        var expectedResource = useMode4PacketAdapter || planner == "visibility_graph"
            ? "cm4-sim"
            : "simulator";
        var target = configuration[ConfigurationKey];
        if (target is null && useStandardBase)
        {
            target = expectedResource;
        }

        if (target is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            throw InvalidTarget("must not be empty when specified");
        }

        if (target is "cm4-sim" or "simulator")
        {
            if (!string.Equals(target, expectedResource, StringComparison.Ordinal))
            {
                throw InvalidTarget($"does not match the selected planner resource '{expectedResource}'");
            }
        }
        else
        {
            throw InvalidTarget($"must be the selected simulator resource name '{expectedResource}'");
        }

        var executableSource = Path.GetFullPath(Path.Combine(appHostBaseDirectory, "crane-launch-overlay"));
        if (!File.Exists(executableSource))
        {
            throw new InvalidOperationException($"{ConfigurationKey} requires the packaged Crane launch executable at the AppHost output path.");
        }

        return new CraneTargetAddressSettings(target, expectedResource, executableSource);
    }

    private static InvalidOperationException InvalidTarget(string reason) =>
        new($"{ConfigurationKey} {reason}.");
}
