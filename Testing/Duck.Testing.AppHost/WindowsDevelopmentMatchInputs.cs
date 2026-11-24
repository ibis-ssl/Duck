using System.Text.Json;
using System.Text.Json.Nodes;

namespace Duck.Testing.AppHost;

/// <summary>Prepares the run-scoped fixture and records the local images used by the Windows development match.</summary>
internal static class WindowsDevelopmentMatchInputs
{
    internal const string SimulatorTag = "a52b6bd";
    internal const string Cm4Tag = "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a";
    internal const string CraneTag = "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d";
    internal const string DockerContext = "desktop-linux";

    internal static IReadOnlyList<ComposeBindMount> PrepareFixture(string directory, string fixtures, string yellowTeam)
    {
        var game = File.ReadAllText(Path.Combine(fixtures, "ssl-game-controller-match.yaml"));
        var lines = game.Split('\n');
        var prepare = Array.FindIndex(lines, line => line.StartsWith("  prepare-timeout:", StringComparison.Ordinal));
        if (prepare < 0)
            throw new InvalidDataException("The shared Game Controller fixture has no prepare-timeout.");
        // NORMAL_START retains the KICKOFF phase until the controller releases it. Give a
        // stationary kickoff time to reach RUNNING inside the initial ten-second window.
        lines[prepare] = "  prepare-timeout: 3s";
        var configPath = Path.Combine(directory, "ssl-game-controller.yaml");
        File.WriteAllText(configPath, string.Join('\n', lines));

        var state = JsonNode.Parse(File.ReadAllText(Path.Combine(fixtures, "state-store-initial.json.stream")))!;
        var teams = state["state"]!["teamState"]!;
        teams["YELLOW"]!["name"] = yellowTeam;
        teams["BLUE"]!["name"] = "Blue";
        state["change"]!["updateTeamStateChange"]!["teamName"] = yellowTeam;
        var statePath = Path.Combine(directory, "state-store.json.stream");
        File.WriteAllText(statePath, state.ToJsonString() + "\n");

        var enginePath = Path.Combine(directory, "match-engine.json");
        File.Copy(Path.Combine(fixtures, "engine-config-initial.json"), enginePath);
        return
        [
            new(configPath, "/config/ssl-game-controller.yaml", ReadOnly: true),
            new(statePath, "/tmp/state-store.json.stream", ReadOnly: false),
            new(enginePath, "/tmp/match-engine.json", ReadOnly: false),
        ];
    }

    internal static async Task<WindowsMatchPreflight> ReadImagesAsync(ComposeSimulationStack stack,
        string engineId, IComposeCommandRunner runner, CancellationToken cancellationToken)
    {
        if (stack.DockerContextName != DockerContext)
            throw new InvalidOperationException($"The development match requires the active '{DockerContext}' context.");

        var engine = await RunDockerAsync(stack, runner,
            ["info", "--format", "{{.ID}}|{{.OSType}}|{{.Architecture}}|{{.ServerVersion}}|{{.OperatingSystem}}"],
            cancellationToken).ConfigureAwait(false);
        var info = engine.Split('|');
        if (info.Length != 5 || info[0] != engineId || info[1] != "linux" ||
            info[2] is not ("x86_64" or "amd64") || !info[4].StartsWith("Docker Desktop", StringComparison.Ordinal))
            throw new InvalidOperationException("The selected Docker engine must be unchanged and support Linux amd64.");

        var transfer = stack.Services.Single(service => service.ServiceName == "development-udp-transfer");
        var dockerfile = File.ReadLines(Path.Combine(transfer.BuildContext!, "Dockerfile"))
            .First(line => line.StartsWith("FROM ", StringComparison.Ordinal));
        var baseReference = dockerfile.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
        if (!baseReference.Contains("@sha256:", StringComparison.Ordinal))
            throw new InvalidDataException("The development UDP transfer Dockerfile must pin its base by digest.");

        var references = stack.Services.Where(service => service.Image is not null)
            .Select(service => (Service: service.ServiceName, Reference: service.Image!))
            .Append((Service: "transfer-base", Reference: baseReference)).ToArray();
        if (references.Length != 5)
            throw new InvalidOperationException("The development match requires the four perimeter images and the transfer base.");

        var images = new List<WindowsMatchImage>();
        foreach (var (service, reference) in references)
        {
            var json = await RunDockerAsync(stack, runner,
                ["image", "inspect", "--platform", "linux/amd64", "--format", "{{json .}}", reference],
                cancellationToken).ConfigureAwait(false);
            using var inspection = JsonDocument.Parse(json);
            var root = inspection.RootElement;
            var digests = root.GetProperty("RepoDigests").EnumerateArray().Select(element => element.GetString()!).ToArray();
            var platform = $"{root.GetProperty("Os").GetString()}/{root.GetProperty("Architecture").GetString()}";
            if (platform != "linux/amd64" || digests.Length == 0)
                throw new InvalidDataException($"Missing local digest or expected platform for {reference}.");
            var platformDigest = root.TryGetProperty("Descriptor", out var descriptor) &&
                descriptor.ValueKind == JsonValueKind.Object && descriptor.TryGetProperty("digest", out var digest)
                ? digest.GetString()
                : null;
            images.Add(new(service, reference, root.GetProperty("Id").GetString()!, platform, digests, platformDigest));
        }
        return new(DockerContext, engineId, info[3], info[4], images);
    }

    internal static async Task<string> RunDockerAsync(ComposeSimulationStack stack, IComposeCommandRunner runner,
        string[] arguments, CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(stack.DockerExecutable,
            ["--context", stack.DockerContextName!, .. arguments], cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Docker {arguments[0]} failed ({result.ExitCode}): {result.StandardError.Trim()}");
        return result.StandardOutput.Trim();
    }
}

internal sealed record WindowsMatchPreflight(string DockerContext, string EngineId, string EngineVersion, string EngineOperatingSystem,
    IReadOnlyList<WindowsMatchImage> Images);

internal sealed record WindowsMatchImage(string Service, string Reference, string Id, string Platform,
    string[] RepoDigests, string? PlatformDigest);
