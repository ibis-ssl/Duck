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

    internal static IReadOnlyList<ComposeBindMount> PrepareFixture(string directory, string fixtures, string yellowTeam, int halfSeconds = 45)
    {
        if (halfSeconds is < 1 or > 148)
            throw new ArgumentOutOfRangeException(nameof(halfSeconds));
        var game = File.ReadAllText(Path.Combine(fixtures, "ssl-game-controller-match.yaml"));
        var lines = game.Split('\n');
        var normal = Array.FindIndex(lines, line => line.TrimEnd('\r') == "  normal:");
        var overtime = Array.FindIndex(lines, line => line.TrimEnd('\r') == "  overtime:");
        if (normal < 0 || overtime <= normal)
            throw new InvalidDataException("The shared Game Controller fixture has no normal half section.");
        var halves = Enumerable.Range(normal + 1, overtime - normal - 1)
            .Where(i => lines[i].StartsWith("    half-duration:", StringComparison.Ordinal)).ToArray();
        if (halves.Length != 1) throw new InvalidDataException("The normal half duration is missing or ambiguous.");
        lines[halves[0]] = "    half-duration: " + halfSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "s";
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

    internal sealed record FixedImage(string Service, string Reference, string PinnedReference, string PlatformDigest);

    internal static IReadOnlyList<FixedImage> FixedImages { get; } =
    [
        new("simulator", $"ghcr.io/ibis-ssl/framework-simulatorcli:{SimulatorTag}",
            "ghcr.io/ibis-ssl/framework-simulatorcli@sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79",
            "sha256:9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554"),
        new("cm4-sim", $"ghcr.io/ibis-ssl/orion-cm4-sim:{Cm4Tag}",
            "ghcr.io/ibis-ssl/orion-cm4-sim@sha256:aef193194cce7c1c3729ac344065945b3d92c0a7ccdca0d5036cc472a8bfa61a",
            "sha256:e67b773e8b1391d755ec93d4dce30af074481fe02c1cdca21754aa1273c6223b"),
        new("game-controller", "robocupssl/ssl-game-controller:3.20.3",
            "robocupssl/ssl-game-controller@sha256:d62d772de063c2951f600a0b7898803f97822f4f46a412a731e42f7d82a99dc7",
            "sha256:bd0d621373098bbf6f27243a2c1b9a219d6402a96dd30b9825ded5258c106721"),
        new("crane", $"ghcr.io/ibis-ssl/crane:{CraneTag}",
            "ghcr.io/ibis-ssl/crane@sha256:db6ab99009f98003c2f078e5d0820004a8e726c7a293965e7022ecfdb1bb2a49",
            "sha256:ce12b180bc689c792178b9c0990038f5f3acab9fd461f914356167cd170d47ac"),
        new("transfer-base", "mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3",
            "mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3",
            "sha256:1bac183f0f5e1b7dd58cf1083fa79e6310d47afe3ca5c9130c910ebba7720929"),
    ];

    internal static async Task<string> PrepareImageAsync(FixedImage image,
        Func<string, Task<bool>> exists, Func<string, Task> verify, Func<string, Task> pull)
    {
        // Never overwrite a tag or replace a present image of the wrong version.
        if (await exists(image.Reference))
        {
            await verify(image.Reference);
            // Reuse the cached pinned alias, not the mutable tag, for execution.
            if (image.Reference != image.PinnedReference)
            {
                if (!await exists(image.PinnedReference))
                    throw new InvalidDataException($"Missing fixed local digest alias for {image.Reference}; no pull attempted.");
                await verify(image.PinnedReference);
            }
            return image.PinnedReference;
        }
        if (!await exists(image.PinnedReference))
            await pull(image.PinnedReference);
        await verify(image.PinnedReference);
        return image.PinnedReference;
    }

    internal static void VerifyFixedImage(FixedImage expected, WindowsMatchImage actual)
    {
        static string Normalize(string value) => value.StartsWith("docker.io/", StringComparison.Ordinal)
            ? value["docker.io/".Length..] : value;
        var pinned = Normalize(expected.PinnedReference);
        var selected = pinned.Split('@')[0] + "@" + expected.PlatformDigest;
        if (actual.Platform != "linux/amd64" || actual.PlatformDigest != expected.PlatformDigest ||
            !actual.RepoDigests.Any(value => Normalize(value) == pinned || Normalize(value) == selected))
            throw new InvalidDataException($"Fixed image mismatch for {expected.Reference}: expected {expected.PlatformDigest} on linux/amd64.");
    }

    // Keep the complete published recipe identical to the repository-pinned transfer recipe.
    internal static void VerifyPublishedTransferDockerfile(string buildContext)
    {
        ReadOnlySpan<byte> expected =
            "FROM mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3\nWORKDIR /app\nCOPY Duck.Testing.DevelopmentUdpTransfer.dll Duck.Testing.DevelopmentUdpTransfer.deps.json Duck.Testing.DevelopmentUdpTransfer.runtimeconfig.json ./\nENTRYPOINT [\"dotnet\", \"Duck.Testing.DevelopmentUdpTransfer.dll\"]\n"u8;
        var path = Path.Combine(buildContext, "Dockerfile");
        if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(expected))
            throw new InvalidDataException("Published development UDP transfer Dockerfile differs from the fixed recipe; no match startup.");
    }

    internal static async Task<WindowsMatchPreflight> ReadImagesAsync(ComposeSimulationStack stack,
        string engineId, IComposeCommandRunner runner, CancellationToken cancellationToken, bool prepareMissing = false)
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
        if (prepareMissing) VerifyPublishedTransferDockerfile(transfer.BuildContext!);
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
        async Task<WindowsMatchImage> Inspect(string service, string reference)
        {
            // ContainerInspect.Image reports the default image ID. Keep it separate from the
            // platform-selected inspection ID, which can identify a different image object.
            var defaultJson = await RunDockerAsync(stack, runner,
                ["image", "inspect", "--format", "{{json .}}", reference],
                cancellationToken).ConfigureAwait(false);
            using var defaultInspection = JsonDocument.Parse(defaultJson);
            if (prepareMissing && (defaultInspection.RootElement.GetProperty("Os").GetString() != "linux" ||
                defaultInspection.RootElement.GetProperty("Architecture").GetString() != "amd64"))
                throw new InvalidDataException($"Default local image platform differs from linux/amd64 for {reference}.");
            var json = await RunDockerAsync(stack, runner,
                ["image", "inspect", "--platform", "linux/amd64", "--format", "{{json .}}", reference],
                cancellationToken).ConfigureAwait(false);
            using var inspection = JsonDocument.Parse(json);
            var root = inspection.RootElement;
            var digests = root.GetProperty("RepoDigests").EnumerateArray().Select(element => element.GetString()!).ToArray();
            var platform = $"{root.GetProperty("Os").GetString()}/{root.GetProperty("Architecture").GetString()}";
            if (platform != "linux/amd64" || digests.Length == 0)
                throw new InvalidDataException($"Missing local digest or expected platform for {reference}.");
            var defaultImageId = defaultInspection.RootElement.GetProperty("Id").GetString();
            var platformImageId = root.GetProperty("Id").GetString();
            if (string.IsNullOrWhiteSpace(defaultImageId) || string.IsNullOrWhiteSpace(platformImageId))
                throw new InvalidDataException($"Missing image ID for {reference}.");
            var platformDigest = root.TryGetProperty("Descriptor", out var descriptor) &&
                descriptor.ValueKind == JsonValueKind.Object && descriptor.TryGetProperty("digest", out var digest)
                ? digest.GetString()
                : null;
            return new(service, reference, defaultImageId, platform, digests, platformDigest, platformImageId);
        }
        if (prepareMissing)
        {
            if (references.Any(pair => !FixedImages.Any(image => image.Service == pair.Service && image.Reference == pair.Reference)))
                throw new InvalidDataException("Unexpected fixed Windows image reference; no pull attempted.");
            // First validate every existing reference before acquiring any missing one.
            var prepared = new Dictionary<string, WindowsMatchImage>(StringComparer.Ordinal);
            async Task<bool> Exists(string reference)
            {
                var result = await runner.RunAsync(stack.DockerExecutable,
                    ["--context", DockerContext, "image", "inspect", "--format", "{{json .}}", reference], cancellationToken);
                if (result.ExitCode == 0) return true;
                if (result.StandardError.Trim().StartsWith("Error response from daemon: No such image:", StringComparison.Ordinal))
                    return false;
                throw new InvalidOperationException($"Image inspection failed for {reference} ({result.ExitCode}); no pull attempted.");
            }
            async Task VerifyEngine()
            {
                var actual = await RunDockerAsync(stack, runner,
                    ["info", "--format", "{{.ID}}|{{.OSType}}|{{.Architecture}}|{{.ServerVersion}}|{{.OperatingSystem}}"], cancellationToken);
                if (actual != engine) throw new InvalidOperationException("Docker engine changed during fixed image preparation.");
            }
            foreach (var (service, reference) in references)
            {
                var fixedImage = FixedImages.Single(image => image.Service == service);
                async Task Check(string candidate)
                {
                    var actual = await Inspect(service, candidate);
                    VerifyFixedImage(fixedImage, actual);
                    prepared[service] = actual;
                }
                if (await Exists(reference))
                {
                    await Check(reference);
                    if (reference != fixedImage.PinnedReference)
                    {
                        if (!await Exists(fixedImage.PinnedReference))
                            throw new InvalidDataException($"Missing fixed local digest alias for {reference}; no pull attempted.");
                        await Check(fixedImage.PinnedReference);
                    }
                }
                else if (reference != fixedImage.PinnedReference && await Exists(fixedImage.PinnedReference))
                    await Check(fixedImage.PinnedReference);
            }
            foreach (var (service, reference) in references)
            {
                var fixedImage = FixedImages.Single(image => image.Service == service);
                if (!prepared.ContainsKey(service))
                {
                    await PrepareImageAsync(fixedImage, Exists, async candidate =>
                    {
                        var actual = await Inspect(service, candidate);
                        VerifyFixedImage(fixedImage, actual);
                        prepared[service] = actual;
                    }, async candidate =>
                    {
                        await VerifyEngine();
                        var registry = await RunDockerAsync(stack, runner,
                            ["info", "--format", "{{json .RegistryConfig}}"], cancellationToken);
                        VerifyOfficialRegistryConfiguration(registry);
                        await RunDockerAsync(stack, runner, ["pull", "--platform", "linux/amd64", candidate], cancellationToken);
                        await VerifyEngine();
                    });
                }
                var actual = prepared[service];
                images.Add(actual);
                if (actual.Reference != fixedImage.PinnedReference)
                    throw new InvalidDataException($"Prepared execution reference differs from the fixed digest for {service}.");
                if (service != "transfer-base")
                    stack.Services.Single(item => item.ServiceName == service).UsePreparedImage(actual.Reference);
            }
            stack.Services.Single(item => item.ServiceName == "development-udp-transfer").UsePreparedPlatform();
            await VerifyEngine();
        }
        else
            foreach (var (service, reference) in references)
                images.Add(await Inspect(service, reference));
        return new(DockerContext, engineId, info[3], info[4], images);
    }

    internal static void VerifyOfficialRegistryConfiguration(string json)
    {
        using var document = JsonDocument.Parse(json);
        var config = document.RootElement;
        var mirrors = config.GetProperty("Mirrors");
        var indexes = config.GetProperty("IndexConfigs");
        var insecure = config.GetProperty("InsecureRegistryCIDRs");
        if ((mirrors.ValueKind != JsonValueKind.Null && mirrors.EnumerateArray().Any()) ||
            (indexes.ValueKind != JsonValueKind.Null && indexes.EnumerateObject().Any(index => !index.Value.GetProperty("Secure").GetBoolean())) ||
            (insecure.ValueKind != JsonValueKind.Null && insecure.EnumerateArray()
                .Any(cidr => cidr.GetString() is not ("127.0.0.0/8" or "::1/128"))))
            throw new InvalidOperationException("Registry mirrors or insecure registry configuration prevent fixed-source acquisition.");
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

internal sealed record WindowsMatchImage(string Service, string Reference, string DefaultImageId, string Platform,
    string[] RepoDigests, string? PlatformDigest, string PlatformImageId);
