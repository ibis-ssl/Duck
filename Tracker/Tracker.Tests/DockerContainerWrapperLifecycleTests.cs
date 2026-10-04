using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class DockerContainerWrapperLifecycleTests
{
    [Fact]
    public async Task GracefulShutdownStopsAndRemovesOnlyTheVerifiedOwnedContainer()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(slowCreate: false, foreignImage: false);
        using var cancellation = new CancellationTokenSource();
        var options = fake.Options(FindOpenPort());
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var run = DockerContainerWrapper.RunAsync(options, cancellation.Token);

        await WaitForReadyAsync(options.HealthPort);
        using var response = await new HttpClient().GetAsync($"http://127.0.0.1:{options.HealthPort}/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        cancellation.Cancel();
        Assert.Equal(0, await run.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(15), $"Graceful cleanup exceeded the DCP stop cap: {elapsed.Elapsed}.");

        var commands = File.ReadAllText(fake.TracePath);
        Assert.Contains("run --detach", commands);
        Assert.Contains("--network host", commands);
        Assert.Contains("--label duck.aspire.owner=duck-apphost", commands);
        Assert.Contains("inspect --format {{json .}} container-id", commands);
        Assert.Contains("top container-id", commands);
        Assert.Contains("stop --time 3 container-id", commands);
        Assert.Contains("rm --force container-id", commands);
        Assert.DoesNotContain("rm --force -", commands);
    }

    [Fact]
    public async Task CancellationDuringCreateDiscoversAndCleansTheExactLabelOwner()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(slowCreate: true, foreignImage: false);
        using var cancellation = new CancellationTokenSource();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var run = DockerContainerWrapper.RunAsync(fake.Options(FindOpenPort()), cancellation.Token);

        await WaitUntilAsync(() => File.Exists(fake.TracePath) && File.ReadAllText(fake.TracePath).Contains("run --detach", StringComparison.Ordinal));
        cancellation.Cancel();

        Assert.Equal(0, await run.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(15), $"Create cancellation cleanup exceeded the DCP stop cap: {elapsed.Elapsed}.");
        var commands = File.ReadAllText(fake.TracePath);
        Assert.Contains("ps --all --quiet", commands);
        Assert.Contains("label=duck.aspire.run=run-17", commands);
        Assert.Contains("inspect --format {{json .}} container-id", commands);
        Assert.Contains("rm --force container-id", commands);
    }

    [Fact]
    public async Task DelayedOwnerDiscoveryRetriesAndCleansOnlyTheExactOwner()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(
            slowCreate: true, foreignImage: false, delayedDiscovery: true);
        using var cancellation = new CancellationTokenSource();
        var run = DockerContainerWrapper.RunAsync(fake.Options(FindOpenPort()), cancellation.Token);
        await WaitUntilAsync(() => File.Exists(fake.TracePath) &&
            File.ReadAllText(fake.TracePath).Contains("run --detach", StringComparison.Ordinal));
        cancellation.Cancel();

        Assert.Equal(0, await run.WaitAsync(TimeSpan.FromSeconds(8)));
        var commands = File.ReadAllLines(fake.TracePath);
        Assert.True(commands.Count(command => command.StartsWith("ps --all --quiet", StringComparison.Ordinal)) >= 2);
        Assert.Contains(commands, command => command.StartsWith("ps --all --quiet", StringComparison.Ordinal) &&
            command.Contains("label=duck.aspire.run=run-17", StringComparison.Ordinal));
        Assert.Equal(1, commands.Count(command => command == "inspect --format {{json .}} container-id"));
        Assert.Equal(1, commands.Count(command => command == "rm --force container-id"));
        Assert.DoesNotContain(commands, command => command.Contains("foreign", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OwnershipMismatchFailsReadinessAndLeavesTheForeignContainerUntouched()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(slowCreate: false, foreignImage: true);

        var exitCode = await DockerContainerWrapper.RunAsync(fake.Options(FindOpenPort())).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotEqual(0, exitCode);
        var commands = File.ReadAllText(fake.TracePath);
        Assert.DoesNotContain("stop --time", commands);
        Assert.DoesNotContain("rm --force", commands);
    }

    [Fact]
    public async Task MissingExpectedServiceProcessPreventsReadinessAndTriggersExactCleanup()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(slowCreate: false, foreignImage: false);
        var options = fake.Options(FindOpenPort()) with
        {
            ExpectedProcessName = "process-that-is-not-running",
            StartupTimeoutSeconds = 1,
        };

        var exitCode = await DockerContainerWrapper.RunAsync(options).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotEqual(0, exitCode);
        var commands = File.ReadAllText(fake.TracePath);
        Assert.Contains("top container-id", commands);
        Assert.Contains("rm --force container-id", commands);
    }

    [Theory]
    [InlineData("crane", "ros2", "exec container-id bash -lc")]
    [InlineData("cm4-sim", "cm4_sim", "exec container-id sh -c")]
    public async Task ExpectedProcessWithoutServicePredicatePreventsReadiness(
        string profile,
        string processName,
        string probeCommand)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(slowCreate: false, foreignImage: false, processName: processName);
        var options = fake.Options(FindOpenPort()) with
        {
            ReadinessProfile = profile,
            StartupTimeoutSeconds = 1,
        };

        var exitCode = await DockerContainerWrapper.RunAsync(options).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotEqual(0, exitCode);
        var commands = File.ReadAllText(fake.TracePath);
        Assert.Contains($"top container-id", commands);
        Assert.Contains(probeCommand, commands);
        Assert.Contains("rm --force container-id", commands);
    }

    [Fact]
    public void ServiceReadinessPredicatesRequireTheirServiceEvidence()
    {
        Assert.False(DockerServiceReadiness.CraneHasCoordinator("/other_node\n"));
        Assert.True(DockerServiceReadiness.CraneHasCoordinator("/crane_session_coordinator\n"));
        Assert.False(DockerServiceReadiness.Cm4SimOwnsListener("port 12345 open"));
        Assert.True(DockerServiceReadiness.Cm4SimOwnsListener("cm4_sim-listening-12345"));
    }

    [Fact]
    public async Task SimulatorProcessWithoutDecodedVisionDoesNotBecomeReady()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(slowCreate: false, foreignImage: false);
        var options = fake.Options(FindOpenPort()) with
        {
            ReadinessProfile = "simulator",
            StartupTimeoutSeconds = 1,
        };

        var exitCode = await DockerContainerWrapper.RunAsync(options).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("top container-id", File.ReadAllText(fake.TracePath));
    }

    [Fact]
    public async Task CleanupTimeoutReturnsFailureBeforeTheDcpStopCapAndReapsDockerChild()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(slowCreate: false, foreignImage: false, slowStop: true);
        using var cancellation = new CancellationTokenSource();
        var options = fake.Options(FindOpenPort()) with { ShutdownTimeoutSeconds = 1 };
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var run = DockerContainerWrapper.RunAsync(options, cancellation.Token);
        await WaitForReadyAsync(options.HealthPort);
        cancellation.Cancel();

        Assert.Equal(1, await run.WaitAsync(TimeSpan.FromSeconds(4)));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(15), $"Timeout path exceeded the DCP stop cap: {elapsed.Elapsed}.");
        Assert.Contains("stop --time 3 container-id", File.ReadAllText(fake.TracePath));
    }

    [Fact]
    public async Task LogFollowerReapTimeoutReturnsFailureAndStillCleansContainer()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(slowCreate: false, foreignImage: false);
        using var cancellation = new CancellationTokenSource();
        var options = fake.Options(FindOpenPort()) with { LogFollowerReapTimeoutMilliseconds = 0 };
        var run = DockerContainerWrapper.RunAsync(options, cancellation.Token);
        await WaitForReadyAsync(options.HealthPort);
        cancellation.Cancel();

        Assert.Equal(1, await run.WaitAsync(TimeSpan.FromSeconds(5)));
        var commands = File.ReadAllText(fake.TracePath);
        Assert.Contains("stop --time 3 container-id", commands);
        Assert.Contains("rm --force container-id", commands);
    }

    [Fact]
    public async Task LogFollowerKillExitRaceStillCleansContainerAndReturnsFailure()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(slowCreate: false, foreignImage: false);
        using var cancellation = new CancellationTokenSource();
        var options = fake.Options(FindOpenPort());
        var run = DockerContainerWrapper.RunAsync(
            options,
            cancellation.Token,
            async (process, _) =>
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw new InvalidOperationException("Simulated process exit between HasExited and Kill.");
            });
        await WaitForReadyAsync(options.HealthPort);
        cancellation.Cancel();

        Assert.Equal(1, await run.WaitAsync(TimeSpan.FromSeconds(5)));
        var commands = File.ReadAllText(fake.TracePath);
        Assert.Contains("stop --time 3 container-id", commands);
        Assert.Contains("rm --force container-id", commands);
    }

    [Fact]
    public async Task DockerRemoveFailureReturnsFailure()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(slowCreate: false, foreignImage: false, rejectRemove: true);
        using var cancellation = new CancellationTokenSource();
        var options = fake.Options(FindOpenPort());
        var run = DockerContainerWrapper.RunAsync(options, cancellation.Token);
        await WaitForReadyAsync(options.HealthPort);
        cancellation.Cancel();

        Assert.Equal(1, await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("rm --force container-id", File.ReadAllText(fake.TracePath));
    }

    [Fact]
    public async Task UnresolvedCreateCancellationFailsWithoutDeletingAnUnverifiedContainer()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(slowCreate: true, foreignImage: false, emptyDiscovery: true);
        using var cancellation = new CancellationTokenSource();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var run = DockerContainerWrapper.RunAsync(fake.Options(FindOpenPort()), cancellation.Token);
        await WaitUntilAsync(() => File.Exists(fake.TracePath) && File.ReadAllText(fake.TracePath).Contains("run --detach", StringComparison.Ordinal));
        cancellation.Cancel();

        Assert.Equal(1, await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(15));
        var commands = File.ReadAllText(fake.TracePath);
        Assert.Contains("ps --all --quiet", commands);
        Assert.DoesNotContain("inspect --format", commands);
        Assert.DoesNotContain("rm --force", commands);
    }

    private static async Task WaitForReadyAsync(int port) =>
        await WaitUntilAsync(async () =>
        {
            try
            {
                using var response = await new HttpClient().GetAsync($"http://127.0.0.1:{port}/health/ready");
                return response.StatusCode == HttpStatusCode.OK;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        });

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        await WaitUntilAsync(() => Task.FromResult(predicate()));
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (await predicate())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("Condition was not met before its deadline.");
    }

    private static int FindOpenPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class FakeDockerClient : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), $"duck-wrapper-test-{Guid.NewGuid():N}");

        public FakeDockerClient(
            bool slowCreate,
            bool foreignImage,
            string processName = "simulator-cli",
            bool slowStop = false,
            bool emptyDiscovery = false,
            bool rejectRemove = false,
            bool delayedDiscovery = false)
        {
            Directory.CreateDirectory(directory);
            TracePath = Path.Combine(directory, "docker-trace.log");
            var spec = CreateSpec();
            var inspection = new
            {
                Id = "container-id",
                Name = $"/{spec.ContainerName}",
                Config = new
                {
                    Image = foreignImage ? "foreign:image" : spec.Image,
                    Labels = new Dictionary<string, string>
                    {
                        [DockerContainerSpec.OwnerLabel] = DockerContainerSpec.OwnerValue,
                        [DockerContainerSpec.StackLabel] = spec.StackId,
                        [DockerContainerSpec.ResourceLabel] = spec.ResourceName,
                        [DockerContainerSpec.RunLabel] = spec.RunId,
                    },
                },
                HostConfig = new { NetworkMode = "host" },
                State = new { Running = true },
            };
            var inspectionPath = Path.Combine(directory, "inspection.json");
            File.WriteAllText(inspectionPath, JsonSerializer.Serialize(inspection));
            var discoveryMarkerPath = Path.Combine(directory, "discovery-seen");
            ExecutablePath = Path.Combine(directory, "docker-fake");
            var createDelay = slowCreate ? "sleep 20" : ":";
            File.WriteAllText(ExecutablePath, string.Join('\n',
            [
                "#!/bin/sh",
                $"printf '%s\\n' \"$*\" >> '{TracePath}'",
                "case \"$1\" in",
                $"  run) {createDelay}; echo container-id ;;",
                $"  ps) {(emptyDiscovery ? ":" : delayedDiscovery ? $"if [ -f '{discoveryMarkerPath}' ]; then echo container-id; else touch '{discoveryMarkerPath}'; fi" : "echo container-id")} ;;",
                $"  inspect) cat '{inspectionPath}' ;;",
                $"  top) echo 'PID USER COMMAND'; echo '1 root {processName}' ;;",
                "  logs) exec sleep 120 ;;",
                $"  stop) {(slowStop ? "sleep 20" : ":")}; exit 0 ;;",
                $"  rm) {(rejectRemove ? "echo remove rejected >&2; exit 7" : "exit 0")} ;;",
                "  *) exit 9 ;;",
                "esac",
            ]));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(ExecutablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        public string ExecutablePath { get; }

        public string TracePath { get; }

        public DockerWrapperLaunchOptions Options(int port) => new(
            "simulator", "test/image:tag", ["tini", "--", "simulator"], new(), "stack-17", "run-17", port,
            ExecutablePath, "simulator-cli");

        public void Dispose() => Directory.Delete(directory, recursive: true);

        private static DockerContainerSpec CreateSpec() =>
            new("simulator", "test/image:tag", ["tini", "--", "simulator"], new Dictionary<string, string>(), "stack-17", "run-17");
    }
}
