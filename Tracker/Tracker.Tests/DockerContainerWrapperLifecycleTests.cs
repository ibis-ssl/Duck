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
    [InlineData("crane", "ros2", "exec container-id timeout --signal=TERM --kill-after=2s 10s bash -lc source /root/ibis_ws/install/setup.bash && printf 'DUCK_CRANE_ROS_SETUP_OK\\n' && exec ros2 node list")]
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
    public void CraneDiagnosticFileSurvivesLostConsoleAndRoundTrips()
    {
        var directory = Path.Combine(Path.GetTempPath(), "duck-crane-transport-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "artifacts", "aspire-full-stack", "crane-probe.jsonl");
        try
        {
            var state = new CraneProbeDiagnosticState();
            Action<string> lostConsole = _ => throw new IOException("simulated missing console transport");
            CraneProbeDiagnostics.WriteRecord(path, CraneProbeDiagnostics.SerializeAttemptStarted(1), lostConsole);
            var timeout = CraneProbeDiagnostics.Evaluate(124, "DUCK_CRANE_ROS_SETUP_OK\n", "password=FILE_SECRET_SENTINEL", TimeSpan.FromSeconds(10));
            CraneProbeDiagnostics.WriteRecord(path, CraneProbeDiagnostics.SerializeProgress(state.Record(timeout)!), lostConsole);
            CraneProbeDiagnostics.WriteRecord(path, CraneProbeDiagnostics.SerializeAttemptStarted(2), lostConsole);
            var ready = CraneProbeDiagnostics.Evaluate(0, "DUCK_CRANE_ROS_SETUP_OK\n/session_controller\n", "", TimeSpan.FromMilliseconds(25));
            CraneProbeDiagnostics.WriteRecord(path, CraneProbeDiagnostics.SerializeProgress(state.Record(ready)!), lostConsole);
            CraneProbeDiagnostics.WriteRecord(path, state.CreateFinalSummary(shutdownRequested: true), lostConsole);

            var lines = File.ReadAllLines(path);
            Assert.Equal(5, lines.Length);
            Assert.DoesNotContain("FILE_SECRET_SENTINEL", File.ReadAllText(path));
            Assert.Contains("[REDACTED]", File.ReadAllText(path));
            using var first = System.Text.Json.JsonDocument.Parse(lines[0]);
            Assert.Equal("duck_crane_probe_started", first.RootElement.GetProperty("event_name").GetString());
            Assert.False(first.RootElement.TryGetProperty("Outcome", out _));
            using var completed = System.Text.Json.JsonDocument.Parse(lines[1]);
            Assert.Equal("ros_graph_timeout", completed.RootElement.GetProperty("Outcome").GetProperty("Classification").GetString());
            using var final = System.Text.Json.JsonDocument.Parse(lines[4]);
            Assert.Equal(2, final.RootElement.GetProperty("completed_attempts").GetInt32());
            Assert.True(final.RootElement.GetProperty("shutdown_requested").GetBoolean());
            Assert.True(final.RootElement.GetProperty("latest").GetProperty("Ready").GetBoolean());

            // Optional local verification output, never evidence of a live Crane run.
            var retained = Environment.GetEnvironmentVariable("DUCK_CRANE_DIAGNOSTIC_TEST_OUTPUT");
            if (!string.IsNullOrWhiteSpace(retained))
            {
                Directory.CreateDirectory(retained);
                File.Copy(path, Path.Combine(retained, "crane-probe.jsonl"), overwrite: true);
                File.WriteAllText(Path.Combine(retained, "test-origin.txt"), "Synthetic unit-test probe records; not a live full-stack run.\n");
            }
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CraneDiagnosticWriteFailureDoesNotEscapeOrDiscloseThePath()
    {
        var parent = Path.Combine(Path.GetTempPath(), "duck-crane-sink-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        var file = Path.Combine(parent, "blocked");
        File.WriteAllText(file, "not a directory");
        try
        {
            var messages = new List<string>();
            var record = CraneProbeDiagnostics.SerializeAttemptStarted(1);
            CraneProbeDiagnostics.WriteRecord(Path.Combine(file, "private-location.jsonl"), record, messages.Add);
            Assert.Equal(2, messages.Count);
            Assert.Contains("duck_crane_diagnostics_write_failed", messages[0]);
            Assert.Equal(record, messages[1]);
            Assert.DoesNotContain(parent, string.Join("\n", messages));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void PinnedCraneCoordinatorUsesSessionControllerGraphName()
    {
        // Crane 4063cd31: SessionCoordinatorComponent derives from Node("session_controller").
        // crane.launch.xml and crane_session_coordinator_node.cpp do not rename that node.
        var graph = "DUCK_CRANE_ROS_SETUP_OK\n/session_controller\n";
        Assert.True(CraneProbeDiagnostics.Evaluate(0, graph, "", TimeSpan.Zero).Ready);
        Assert.False(CraneProbeDiagnostics.Evaluate(124, graph, "", TimeSpan.Zero).Ready);
        Assert.False(CraneProbeDiagnostics.Evaluate(1, graph, "", TimeSpan.Zero).Ready);
        Assert.False(DockerServiceReadiness.CraneHasCoordinator("/crane_session_coordinator\n"));
        Assert.False(DockerServiceReadiness.CraneHasCoordinator("/other/session_controller\n"));
        Assert.False(DockerServiceReadiness.CraneHasCoordinator("/session_controller_extra\n"));
        Assert.False(DockerServiceReadiness.CraneHasCoordinator("process session_controller is running\n"));
    }

    [Fact]
    public void CraneKilledProbeIsNotReportedAsAnOrdinaryNonzeroExit()
    {
        var setup = CraneProbeDiagnostics.Evaluate(137, "", "", TimeSpan.FromSeconds(12));
        var graph = CraneProbeDiagnostics.Evaluate(137, "DUCK_CRANE_ROS_SETUP_OK\n", "", TimeSpan.FromSeconds(12));
        Assert.Equal("setup_killed", setup.Classification);
        Assert.Equal("ros_graph_killed", graph.Classification);
        Assert.False(setup.Ready);
        Assert.False(graph.Ready);
    }

    [Fact]
    public void ServiceReadinessPredicatesRequireTheirServiceEvidence()
    {
        Assert.False(DockerServiceReadiness.CraneHasCoordinator("/other_node\n"));
        Assert.True(DockerServiceReadiness.CraneHasCoordinator("/session_controller\n"));
        Assert.False(DockerServiceReadiness.Cm4SimOwnsListener("port 12345 open"));
        Assert.True(DockerServiceReadiness.Cm4SimOwnsListener("cm4_sim-listening-12345"));
    }

    [Fact]
    public void CraneReadinessProbeIsBoundedAndPreservesTheCoordinatorGraphCheck()
    {
        Assert.Equal(
            ["exec", "container-id", "timeout", "--signal=TERM", "--kill-after=2s", "10s", "bash", "-lc",
                "source /root/ibis_ws/install/setup.bash && printf 'DUCK_CRANE_ROS_SETUP_OK\\n' && exec ros2 node list"],
            DockerServiceReadiness.CreateCraneReadinessProbeArguments("container-id"));
        Assert.False(CraneProbeDiagnostics.Evaluate(124, "/session_controller\n", "", TimeSpan.FromSeconds(10)).Ready);
        Assert.False(CraneProbeDiagnostics.Evaluate(0, "/session_controller\n", "", TimeSpan.Zero).Ready);
        Assert.False(CraneProbeDiagnostics.Evaluate(0,
            "error DUCK_CRANE_ROS_SETUP_OK appears here\n/session_controller\n", "", TimeSpan.Zero).Ready);
        Assert.False(CraneProbeDiagnostics.Evaluate(0,
            "/session_controller\nDUCK_CRANE_ROS_SETUP_OK\n/other_node\n", "", TimeSpan.Zero).Ready);
        Assert.True(CraneProbeDiagnostics.Evaluate(0,
            "DUCK_CRANE_ROS_SETUP_OK\n/session_controller\n", "", TimeSpan.Zero).Ready);
        var beyondCap = "DUCK_CRANE_ROS_SETUP_OK\n" + new string('x', 600) + "\n/session_controller\n";
        var beyondCapResult = CraneProbeDiagnostics.Evaluate(0, beyondCap, "", TimeSpan.Zero);
        Assert.True(beyondCapResult.Ready);
        Assert.True(beyondCapResult.StandardOutputTruncated);
        Assert.DoesNotContain("session_controller", beyondCapResult.StandardOutputExcerpt);
    }

    [Fact]
    public void CraneProbeSanitizerMatchesSharedPythonFixturesBeforeApplyingExcerptLimit()
    {
        Assert.Equal("password=[REDACTED]\n", CraneProbeDiagnostics.Sanitize("password=CRANE_DIAGNOSTIC_SENTINEL\n"));
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "crane_probe_sanitizer_fixtures.json");
        using var fixtures = System.Text.Json.JsonDocument.Parse(File.ReadAllText(fixturePath));
        foreach (var fixture in fixtures.RootElement.EnumerateArray())
        {
            Assert.Equal(
                fixture.GetProperty("expected").GetString(),
                CraneProbeDiagnostics.Sanitize(fixture.GetProperty("input").GetString()!));
        }

        var privateKey = "-----BEGIN RSA PRIVATE KEY-----\n" + new string('x', 600) +
            "PRIVATE_KEY_SENTINEL\n-----END RSA PRIVATE KEY-----\n" + new string('z', 600);
        var result = CraneProbeDiagnostics.Evaluate(124, "", privateKey, TimeSpan.FromSeconds(10));
        Assert.True(result.StandardErrorTruncated);
        Assert.DoesNotContain("PRIVATE_KEY_SENTINEL", result.StandardErrorExcerpt);
        Assert.DoesNotContain("-----BEGIN", result.StandardErrorExcerpt);
    }

    [Fact]
    public void CraneProbeProgressCountsEveryAttemptAndThrottlesUnchangedResults()
    {
        var state = new CraneProbeDiagnosticState();
        var failed = CraneProbeDiagnostics.Evaluate(124, "", "temporary", TimeSpan.FromSeconds(10));

        Assert.NotNull(state.Record(failed, TimeSpan.Zero));
        Assert.Null(state.Record(failed, TimeSpan.FromSeconds(1)));
        var heartbeat = state.Record(failed, TimeSpan.FromSeconds(2));
        Assert.NotNull(heartbeat);
        Assert.Equal(3, heartbeat!.Attempt);
        Assert.Equal(3, state.AttemptCount);
        Assert.Equal(failed, state.Latest);
        Assert.Contains("\"attempts\":3", state.CreateFinalSummary());

        var ready = CraneProbeDiagnostics.Evaluate(0,
            "DUCK_CRANE_ROS_SETUP_OK\n/session_controller\n", "", TimeSpan.FromMilliseconds(25));
        var transition = state.Record(ready, TimeSpan.FromMilliseconds(2100));
        Assert.NotNull(transition);
        Assert.Equal(4, transition!.Attempt);
        Assert.True(transition.Outcome.Ready);
    }

    [Fact]
    public async Task CraneReadinessRetriesTimeoutsUntilCoordinatorAppearsThenCleansUp()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(
            slowCreate: false,
            foreignImage: false,
            processName: "ros2",
            craneProbeTimeoutResponses: 2);
        using var cancellation = new CancellationTokenSource();
        var diagnostics = new List<string>();
        var options = fake.Options(FindOpenPort()) with
        {
            ReadinessProfile = "crane",
            ExpectedProcessName = "ros2",
            StartupTimeoutSeconds = 10,
        };
        var run = DockerContainerWrapper.RunAsync(options, cancellation.Token, StopTestChildProcessAsync,
            diagnostics.Add);

        await WaitForReadyAsync(options.HealthPort);
        using var response = await new HttpClient().GetAsync($"http://127.0.0.1:{options.HealthPort}/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        cancellation.Cancel();
        Assert.Equal(0, await run.WaitAsync(TimeSpan.FromSeconds(10)));
        var commands = File.ReadAllText(fake.TracePath);
        Assert.Equal(3, commands.Split("exec container-id timeout --signal=TERM", StringSplitOptions.None).Length - 1);
        Assert.Contains("rm --force container-id", commands);
        Assert.True(diagnostics.Count >= 2);
        Assert.DoesNotContain("CRANE_DIAGNOSTIC_SENTINEL", string.Join('\n', diagnostics));
        using var first = System.Text.Json.JsonDocument.Parse(diagnostics.First(line => line.Contains("duck_crane_probe_progress", StringComparison.Ordinal)));
        Assert.Equal("duck_crane_probe_progress", first.RootElement.GetProperty("event_name").GetString());
        Assert.Equal(1, first.RootElement.GetProperty("attempt").GetInt32());
        Assert.Equal("setup_timeout", first.RootElement.GetProperty("Outcome").GetProperty("Classification").GetString());
        Assert.Equal("password=[REDACTED]\n", first.RootElement.GetProperty("Outcome").GetProperty("StandardErrorExcerpt").GetString());
        Assert.Contains(diagnostics, line => line.Contains("\"attempt\":3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CraneTimeoutFinalDiagnosticRetainsLatestFailureAndCleansOwnedContainer()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fake = new FakeDockerClient(
            slowCreate: false,
            foreignImage: false,
            processName: "ros2",
            craneProbeTimeoutResponses: 100);
        var diagnostics = new List<string>();
        var options = fake.Options(FindOpenPort()) with
        {
            ReadinessProfile = "crane",
            ExpectedProcessName = "ros2",
            StartupTimeoutSeconds = 1,
        };

        Assert.NotEqual(0, await DockerContainerWrapper.RunAsync(
            options, CancellationToken.None, StopTestChildProcessAsync, diagnostics.Add).WaitAsync(TimeSpan.FromSeconds(8)));
        Assert.Contains("rm --force container-id", File.ReadAllText(fake.TracePath));
        Assert.Contains(diagnostics, line => line.Contains("duck_crane_probe_final", StringComparison.Ordinal));
        Assert.DoesNotContain("CRANE_DIAGNOSTIC_SENTINEL", string.Join('\n', diagnostics));
    }

    private static async Task<bool> StopTestChildProcessAsync(System.Diagnostics.Process process, TimeSpan wait)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        await process.WaitForExitAsync();
        process.Dispose();
        return true;
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
            bool delayedDiscovery = false,
            int craneProbeTimeoutResponses = 0)
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
            var craneProbeCountPath = Path.Combine(directory, "crane-probe-count");
            var craneProbeCommand = craneProbeTimeoutResponses == 0
                ? "  exec) exit 9 ;;"
                : "  exec) count=$(cat '" + craneProbeCountPath + "' 2>/dev/null || echo 0); " +
                "count=$((count + 1)); echo \"$count\" > '" + craneProbeCountPath + "'; " +
                    "if [ \"$count\" -le " + craneProbeTimeoutResponses + " ]; then echo 'password=CRANE_DIAGNOSTIC_SENTINEL' >&2; exit 124; fi; " +
                    "printf 'DUCK_CRANE_ROS_SETUP_OK\\n/session_controller\\n' ;;";
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
                craneProbeCommand,
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
