namespace Duck.Testing.AppHost;

internal static class MatchPreparedInputs
{
    public static void RequireAvailable(string repositoryRoot, string appHostOutput)
    {
        var missing = new List<string>();

        void Require(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length == 0)
                missing.Add(file.FullName);
        }

        void RequirePublish(string projectPath, string entryAssembly, params string[] dependencies)
        {
            var project = Path.Combine(repositoryRoot, projectPath);
            Require(Path.Combine(project, "Dockerfile"));
            var publish = Path.Combine(project, "bin", "Release", "net10.0", "publish");
            foreach (var name in new[] { entryAssembly + ".dll", entryAssembly + ".deps.json", entryAssembly + ".runtimeconfig.json" }.Concat(dependencies))
                Require(Path.Combine(publish, name));
        }

        Require(Path.Combine(appHostOutput, BaseSimulationResourceRegistration.Cm4TransportHelperAssetName));
        Require(Path.Combine(appHostOutput, "autoref-game-log-agent.jar"));
        Require(Path.Combine(appHostOutput, "crane-launch-overlay"));
        RequirePublish("Tracker/Tracker.RuntimeHost", "Tracker.RuntimeHost",
            "appsettings.json", "Tracker.Core.dll", "TrackerConnectionLib.dll",
            "Duck.Testing.Diagnostics.dll", "SslProto.dll", "Google.Protobuf.dll", "Microsoft.Extensions.Hosting.dll");
        RequirePublish("Testing/Duck.Testing.RefereeDriver.Host", "Duck.Testing.RefereeDriver.Host",
            "Duck.Testing.RefereeDriver.dll", "Duck.Testing.Diagnostics.dll", "SslProto.dll", "Google.Protobuf.dll");

        if (missing.Count != 0)
        {
            throw new InvalidOperationException(
                "Match prepared inputs are missing or empty:" + Environment.NewLine +
                string.Join(Environment.NewLine, missing) + Environment.NewLine +
                "Use .github/workflows/dotnet-test.yml: Build AppHost and full-stack acceptance test " +
                "(static Linux CM4 helper, scripts/build_autoref_game_log_agent.sh, and AppHost build) " +
                "and Publish Linux match Docker contexts (Release publish of Tracker.RuntimeHost and " +
                "Duck.Testing.RefereeDriver.Host) before running AppHost without a build.");
        }
    }
}
