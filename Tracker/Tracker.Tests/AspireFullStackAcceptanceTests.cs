using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Duck.Testing.AppHost;
using Duck.Testing.RefereeDriver;
using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using TrackerConnectionLib;
using Xunit.Abstractions;

namespace Tracker.Tests;

[Collection("Process environment")]
public sealed class AspireFullStackAcceptanceTests(ITestOutputHelper output)
{
    private static readonly TimeSpan StackSignalTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MotionTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public void ResolvesConfiguredDockerExecutableWhenPathIsUnset()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var dockerExecutable = CreateDockerExecutable(temporaryDirectory.Path);

        Assert.Equal(dockerExecutable, ResolveDockerExecutable(dockerExecutable, null));
    }

    [Fact]
    public void ResolvesDockerExecutableFromConfiguredPath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var dockerDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "docker-bin")).FullName;
        var dockerExecutable = CreateDockerExecutable(dockerDirectory);

        Assert.Equal(dockerExecutable, ResolveDockerExecutable(null, dockerDirectory));
    }

    [Fact]
    public void PrependsDockerDirectoryAndPreservesExistingPathEntries()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var dockerDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "docker-bin")).FullName;
        var existingDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "existing-bin")).FullName;
        var dockerExecutable = CreateDockerExecutable(dockerDirectory);

        Assert.Equal(dockerDirectory, PrependDockerDirectoryToPath(dockerExecutable, null));
        Assert.Equal(
            dockerDirectory + Path.PathSeparator + existingDirectory,
            PrependDockerDirectoryToPath(dockerExecutable, existingDirectory));
        Assert.Equal(dockerDirectory, PrependDockerDirectoryToPath(dockerExecutable, dockerDirectory));
    }

    [Fact]
    public void MovesSelectedDockerDirectoryAheadOfEarlierPathEntries()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var earlierDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "earlier-bin")).FullName;
        var dockerDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "selected-docker-bin")).FullName;
        var laterDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "later-bin")).FullName;
        var dockerExecutable = CreateDockerExecutable(dockerDirectory);
        var existingPath = string.Join(Path.PathSeparator, earlierDirectory, dockerDirectory, laterDirectory);

        Assert.Equal(
            string.Join(Path.PathSeparator, dockerDirectory, earlierDirectory, laterDirectory),
            PrependDockerDirectoryToPath(dockerExecutable, existingPath));
    }

    [Fact]
    public void InitializesCancellationAcceptanceDockerPathBeforeBareDockerPreflight()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var dockerDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "docker-bin")).FullName;
        var dockerExecutable = CreateDockerExecutable(dockerDirectory);
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", null);
            var (resolvedExecutable, pathScope) = ResolveAndPrepareDockerExecutable(dockerExecutable, null);
            using (pathScope)
            {
                Assert.Equal(dockerExecutable, resolvedExecutable);
                Assert.Equal(dockerDirectory, Environment.GetEnvironmentVariable("PATH"));
                Assert.Equal(dockerExecutable, ResolveDockerExecutable(null, Environment.GetEnvironmentVariable("PATH")));
            }

            Assert.Null(Environment.GetEnvironmentVariable("PATH"));
            if (OperatingSystem.IsWindows())
            {
                var existingDockerDesktopExecutable = NamespaceProbeDcpAcceptanceTests.GetDockerExecutablePath();
                var (fallbackExecutable, fallbackPathScope) = ResolveAndPrepareDockerExecutable(null, null);
                using (fallbackPathScope)
                {
                    Assert.Equal(existingDockerDesktopExecutable, fallbackExecutable);
                    Assert.Equal(
                        fallbackExecutable,
                        ResolveDockerExecutable(null, Environment.GetEnvironmentVariable("PATH")));
                }

                Assert.Null(Environment.GetEnvironmentVariable("PATH"));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void RestoresPathAfterDockerPathScopeExitsWithAnError()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var dockerDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "docker-bin")).FullName;
        var dockerExecutable = CreateDockerExecutable(dockerDirectory);
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", null);
            Assert.Throws<InvalidOperationException>((Action)(() =>
            {
                using var pathScope = AddDockerDirectoryToProcessPath(dockerExecutable);
                Assert.Equal(dockerDirectory, Environment.GetEnvironmentVariable("PATH"));
                throw new InvalidOperationException("simulated preflight failure");
            }));

            Assert.Null(Environment.GetEnvironmentVariable("PATH"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void ResolvesCm4TransportHelperFromExistingRuntimeBundleWithoutEnvironmentOverride()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var helperDirectory = Directory.CreateDirectory(Path.Combine(
            temporaryDirectory.Path, "artifacts", "issue81-runtime-bundle", "Duck.Testing.AppHost")).FullName;
        var helperPath = Path.Combine(helperDirectory, "cm4-transport-control");
        File.WriteAllText(helperPath, "verified helper fixture");
        var originalHelperPath = Environment.GetEnvironmentVariable("DUCK_ASPIRE_CM4_TRANSPORT_HELPER");

        try
        {
            Environment.SetEnvironmentVariable("DUCK_ASPIRE_CM4_TRANSPORT_HELPER", null);
            Assert.Equal(Path.GetFullPath(helperPath), ResolveCm4TransportHelperSource(temporaryDirectory.Path, Path.Combine(temporaryDirectory.Path, "test-output", "net10.0")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DUCK_ASPIRE_CM4_TRANSPORT_HELPER", originalHelperPath);
        }
    }

    [Fact]
    public void RejectsInvalidConfiguredCm4TransportHelperInsteadOfUsingBundleFallback()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var helperDirectory = Directory.CreateDirectory(Path.Combine(
            temporaryDirectory.Path, "artifacts", "issue81-runtime-bundle", "Duck.Testing.AppHost")).FullName;
        File.WriteAllText(Path.Combine(helperDirectory, "cm4-transport-control"), "verified helper fixture");
        var invalidConfiguredPath = Path.Combine(temporaryDirectory.Path, "missing-helper");
        var originalHelperPath = Environment.GetEnvironmentVariable("DUCK_ASPIRE_CM4_TRANSPORT_HELPER");

        try
        {
            Environment.SetEnvironmentVariable("DUCK_ASPIRE_CM4_TRANSPORT_HELPER", invalidConfiguredPath);
            var exception = Assert.ThrowsAny<Exception>(
                () => ResolveCm4TransportHelperSource(temporaryDirectory.Path));
            Assert.Contains("Configured CM4 transport helper does not exist", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DUCK_ASPIRE_CM4_TRANSPORT_HELPER", originalHelperPath);
        }
    }

    [Fact]
    public void ReportsWhenDockerExecutableCannotBeResolved()
    {
        using var temporaryDirectory = new TemporaryDirectory();

        var exception = Assert.Throws<FileNotFoundException>(
            () => ResolveDockerExecutable(null, temporaryDirectory.Path));

        Assert.Contains("Docker CLI could not be resolved from PATH", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullStack_ReachesActiveRefereeAndMovesCraneRobot()
    {
        var stackId = Environment.GetEnvironmentVariable("DUCK_ASPIRE_STACK_ID");
        if (string.IsNullOrWhiteSpace(stackId))
        {
            throw new InvalidOperationException("DUCK_ASPIRE_STACK_ID must identify the AppHost stack under test.");
        }

        var manifestPath = Environment.GetEnvironmentVariable("DUCK_ASPIRE_FIXTURE_MANIFEST")
            ?? CraneFixtureManifest.GetDefaultPath(stackId);
        var craneFixtureSettings = CraneFixtureManifest.ReadForStack(manifestPath, stackId);
        var artifactsDirectory = Environment.GetEnvironmentVariable("DUCK_ASPIRE_ARTIFACTS")
            ?? Path.Combine(Path.GetTempPath(), "duck-aspire-full-stack-artifacts");
        Directory.CreateDirectory(artifactsDirectory);

        var refereeSnapshots = new ConcurrentQueue<RefereeCommandSnapshot>();
        var stageReached = "test_setup";
        var refereeRawDatagramCount = 0;
        int? visionPacketsConsumed = null;
        int? trackerPacketsConsumed = null;
        string? failure = null;
        RobotMotionDelta? observedMovement = null;
        TrackerComparisonMeasurementEvidence? comparisonMeasurement = null;
        MulticastInterface? selectedMulticastInterface = null;
        UdpMulticastReceiver? refereeReceiver = null;
        UdpMulticastReceiver? visionReceiver = null;
        UdpMulticastReceiver? trackerReceiver = null;

        try
        {
            using var timeout = new CancellationTokenSource(StackSignalTimeout);
            var multicastInterface = FindMulticastInterface();
            selectedMulticastInterface = multicastInterface;
            output.WriteLine(
                $"multicast interface={multicastInterface.Name}; ipv4={multicastInterface.Address}");
            refereeReceiver = new UdpMulticastReceiver("224.5.23.1", 11003, multicastInterface.Address);
            using var refereeSource = new RecordingRefereeCommandSource(
                new UdpRefereeCommandSource(refereeReceiver),
                refereeSnapshots,
                output);
            visionReceiver = new UdpMulticastReceiver("224.5.23.2", 10020, multicastInterface.Address);
            trackerReceiver = new UdpMulticastReceiver("224.5.23.2", 11010, multicastInterface.Address);

            RefereeCommandSnapshot initial;
            stageReached = "initial_referee_wait";
            try
            {
                initial = await refereeSource.WaitForChangeAsync(null, timeout.Token);
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    "REFEREE_FIXTURE_FAILURE: Game Controller did not publish a referee packet on UDP 11003 before the deadline.",
                    exception);
            }

            if (initial.Command != Referee.Types.Command.Halt)
            {
                throw new InvalidOperationException(
                    $"REFEREE_FIXTURE_FAILURE: expected initial HALT, observed {initial.Command}.");
            }

            Assert.True(
                craneFixtureSettings.MatchesExpectedYellowTeamName(initial.YellowTeamName),
                $"TEAM_IDENTITY_FAILURE: expected Game Controller Yellow team name '{craneFixtureSettings.TeamName}' before starting motion, observed '{initial.YellowTeamName}'.");
            output.WriteLine($"pre-motion team identity: yellow={initial.YellowTeamName}");

            SSL_WrapperPacket initialVision;
            stageReached = "initial_vision_wait";
            try
            {
                initialVision = await ReadVisionFrameWithYellowRobotsAsync(visionReceiver, timeout.Token);
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    "VISION_INGRESS_FAILURE: no SSL-Vision detection frame with Yellow robots arrived on UDP 10020 before the deadline.",
                    exception);
            }
            visionPacketsConsumed = visionReceiver.PacketCount;
            stageReached = "active_referee_transition";
            var motionVerifier = new RobotMotionAcceptanceVerifier(minimumDistanceMm: 100);
            motionVerifier.CaptureBaseline(initialVision.Detection);
            output.WriteLine(
                $"baseline referee={initial.Command} counter={initial.CommandCounter}; " +
                $"visionFrame={initialVision.Detection.FrameNumber}; " +
                $"yellowRobots={initialVision.Detection.RobotsYellow.Count}");

            var cachedInitialSource = new InitialSnapshotRefereeCommandSource(refereeSource, initial);
            var driver = new RefereeDriverFixture(
                cachedInitialSource,
                new GameControllerWebSocketControlClient());
            try
            {
                await driver.DriveToActiveAsync(timeout.Token);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"REFEREE_FIXTURE_FAILURE: could not drive Game Controller from HALT to active: {exception.Message}",
                    exception);
            }

            var active = refereeSnapshots.LastOrDefault(snapshot =>
                snapshot.Command is Referee.Types.Command.NormalStart or Referee.Types.Command.ForceStart);
            Assert.True(
                active is not null,
                "REFEREE_FIXTURE_FAILURE: Game Controller did not publish an active command on UDP 11003.");
            output.WriteLine($"active referee={active!.Command} counter={active.CommandCounter}");

            stageReached = "active_robot_motion_wait";
            var discardedPreMotionPackets = visionReceiver.DrainPendingPackets();
            output.WriteLine($"discarded queued vision packets before active-motion window={discardedPreMotionPackets}");

            using var motionTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            motionTimeout.CancelAfter(MotionTimeout);
            while (observedMovement is null)
            {
                try
                {
                    var datagram = await visionReceiver.ReceiveAsync(motionTimeout.Token);
                    var packet = SSL_WrapperPacket.Parser.ParseFrom(datagram.ToArray());
                    if (packet.Detection is null)
                    {
                        continue;
                    }

                    observedMovement = motionVerifier.FindMovement(packet.Detection);
                }
                catch (OperationCanceledException exception)
                {
                    throw new InvalidOperationException(
                        "UDP_MOTION_FAILURE: no Yellow robot moved at least 100 mm after the active referee transition within 60 seconds.",
                        exception);
                }
            }

            output.WriteLine(
                $"UDP motion verified: team={observedMovement.Team}; " +
                $"robotId={observedMovement.RobotId}; distanceMm={observedMovement.DistanceMm:F1}");

            TrackerWrapperPacket tracker;
            stageReached = "duck_tracker_output_wait";
            try
            {
                tracker = await ReadDuckTrackerPacketAsync(trackerReceiver, timeout.Token);
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    "DUCK_TRACKER_OUTPUT_FAILURE: no ibis TrackerWrapperPacket was received on UDP 11010 before the deadline.",
                    exception);
            }
            trackerPacketsConsumed = trackerReceiver.PacketCount;
            output.WriteLine(
                $"Duck tracker packet verified: uuid={tracker.Uuid}; source={tracker.SourceName}; " +
                $"frame={tracker.TrackedFrame!.FrameNumber}; robots={tracker.TrackedFrame.Robots.Count}");
            Assert.Equal("ibis", tracker.Uuid);
            Assert.Equal("ibis", tracker.SourceName);
            Assert.NotNull(tracker.TrackedFrame);

            stageReached = "tracker_comparison_measurement";
            comparisonMeasurement = await MeasureLiveTrackerDifferencePathAsync(
                trackerReceiver,
                visionReceiver,
                timeout.Token);
            output.WriteLine(
                $"live tracker comparison measurement: {System.Text.Json.JsonSerializer.Serialize(comparisonMeasurement)}");
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            throw;
        }
        finally
        {
            refereeRawDatagramCount = refereeReceiver?.PacketCount ?? 0;
            if (stageReached is "initial_vision_wait" or "active_referee_transition" or "active_robot_motion_wait" or "duck_tracker_output_wait" or "tracker_comparison_measurement")
            {
                visionPacketsConsumed = visionReceiver?.PacketCount ?? visionPacketsConsumed;
            }

            if (stageReached is "duck_tracker_output_wait" or "tracker_comparison_measurement")
            {
                trackerPacketsConsumed = trackerReceiver?.PacketCount;
            }

            var linuxMulticastMemberships = ReadLinuxMulticastMemberships();
            refereeReceiver?.Dispose();
            visionReceiver?.Dispose();
            trackerReceiver?.Dispose();
            var evidence = new
            {
                status = failure is null ? "passed" : "failed",
                failure,
                stageReached,
                refereeCommands = refereeSnapshots.ToArray().Select(snapshot => new
                {
                    command = snapshot.Command.ToString(),
                    snapshot.CommandCounter,
                }),
                refereeRawDatagramCount,
                visionPacketsConsumed,
                trackerPacketsConsumed,
                comparisonMeasurement,
                packetCountSemantics = "Consumed counts record datagrams read by the harness; null means that reader stage was not reached and does not imply network ingress was zero.",
                receiverSockets = new
                {
                    interfaceName = selectedMulticastInterface?.Name,
                    interfaceAddress = selectedMulticastInterface?.Address.ToString(),
                    bindAddress = IPAddress.Any.ToString(),
                    reuseAddress = true,
                    referee = new { group = "224.5.23.1", port = 11003 },
                    sslVision = new { group = "224.5.23.2", port = 10020 },
                    duckTracker = new { group = "224.5.23.2", port = 11010 },
                    linuxMemberships = linuxMulticastMemberships,
                },
                movement = observedMovement,
                runner = Environment.GetEnvironmentVariable("RUNNER_OS"),
                sha = Environment.GetEnvironmentVariable("GITHUB_SHA"),
            };
            var path = Path.Combine(artifactsDirectory, "acceptance-result.json");
            await File.WriteAllTextAsync(
                path,
                System.Text.Json.JsonSerializer.Serialize(
                    evidence,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            output.WriteLine($"Acceptance evidence: {path}");
        }
    }

    [Fact]
    public async Task SecondAppHostWithSameLockIsRejectedWithoutDisruptingFirstStack()
    {
        var configuredArtifactsDirectory = Environment.GetEnvironmentVariable("DUCK_ASPIRE_ARTIFACTS");
        var artifactsDirectory = configuredArtifactsDirectory
            ?? Path.Combine(Path.GetTempPath(), "duck-aspire-full-stack-artifacts");
        Directory.CreateDirectory(artifactsDirectory);
        var processInfoPath = Path.Combine(artifactsDirectory, "apphost-process.txt");
        var repositoryRoot = FindRepositoryRoot();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var dockerExecutable = ResolveDockerExecutable(
            Environment.GetEnvironmentVariable("Testing__IdentityControl__DockerExecutable"), originalPath);
        IDisposable? dockerPathScope = null;
        var stagedAppHostAssets = new List<StagedAppHostAsset>();
        DistributedApplication? managedFirstApplication = null;
        Process? firstAppHost = null;
        string firstStackId = string.Empty;
        string lockPath = string.Empty;
        string[] firstContainerIds;
        var managedFirstStartAttempted = false;

        try
        {
            if (!string.IsNullOrWhiteSpace(configuredArtifactsDirectory))
            {
                Assert.True(File.Exists(processInfoPath), $"CI AppHost process record was not found: {processInfoPath}");
                var processInfo = File.ReadAllLines(processInfoPath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Split('=', 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
                Assert.True(processInfo.TryGetValue("apphost_pid", out var firstPidText), "apphost_pid is missing from apphost-process.txt.");
                Assert.True(processInfo.TryGetValue("stack_lock", out lockPath!), "stack_lock is missing from apphost-process.txt.");
                Assert.True(processInfo.TryGetValue("stack_id", out firstStackId!), "stack_id is missing from apphost-process.txt.");
                Assert.True(int.TryParse(firstPidText, out var firstPid), $"Invalid apphost_pid in {processInfoPath}.");
                Assert.True(File.Exists(lockPath), $"The first AppHost lock file does not exist: {lockPath}");

                firstAppHost = Process.GetProcessById(firstPid);
                Assert.False(firstAppHost.HasExited, "The first AppHost process exited before the second-owner check.");
            }
            else
            {
                dockerPathScope = AddDockerDirectoryToProcessPath(dockerExecutable);
                firstStackId = $"first-owner-{Guid.NewGuid():N}";
                lockPath = Path.Combine(artifactsDirectory, $"{firstStackId}.lock");
                var firstRunId = $"first-run-{Guid.NewGuid():N}";
                var fixtureManifestPath = Path.Combine(artifactsDirectory, $"{firstStackId}-crane-fixture.json");
                var craneDiagnosticsPath = Path.Combine(artifactsDirectory, $"{firstStackId}-crane-diagnostics.jsonl");
                var cm4HelperSource = ResolveCm4TransportHelperSource(repositoryRoot);
                var craneOverlaySource = Path.Combine(Projects.Duck_Testing_AppHost.ProjectPath, "CraneLaunchOverlay.py");
                stagedAppHostAssets.AddRange(StageLocalAppHostAssets(cm4HelperSource, craneOverlaySource));
                var (dockerContext, expectedEngineId) = await ResolveDockerIdentityAsync();
                var firstAppHostBuilder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>([
                    "--Testing:Mode=base",
                    "--Testing:Crane:Planner=visibility_graph",
                    "--Testing:IdentityControl:DesktopBridgeEnabled=true",
                    "--Testing:IdentityControl:DockerContext=" + dockerContext,
                    "--Testing:IdentityControl:ExpectedEngineId=" + expectedEngineId,
                    "--Testing:IdentityControl:DockerExecutable=" + dockerExecutable,
                    "--Testing:StackOwnership:LockPath=" + lockPath,
                    "--Testing:StackOwnership:StackId=" + firstStackId,
                    "--Testing:Diagnostics:RunId=" + firstRunId,
                    "--Testing:Crane:FixtureManifestPath=" + fixtureManifestPath,
                    "--Testing:Crane:DiagnosticsPath=" + craneDiagnosticsPath,
                ]);
                managedFirstApplication = firstAppHostBuilder.Build();

                using var startupTimeout = new CancellationTokenSource(StackSignalTimeout);
                managedFirstStartAttempted = true;
                await managedFirstApplication.StartAsync(startupTimeout.Token);
                var notifications = managedFirstApplication.Services.GetRequiredService<ResourceNotificationService>();
                foreach (var resource in new[] { "simulator", "duck", "game-controller", "cm4-sim", "crane" })
                {
                    await notifications.WaitForResourceAsync(resource, KnownResourceStates.Running, startupTimeout.Token);
                }
            }

            firstContainerIds = await GetOwnedContainerIdsAsync(firstStackId, dockerExecutable);
            Assert.NotEmpty(firstContainerIds);

            var secondStackId = $"second-owner-{Guid.NewGuid():N}";
            var appHostProject = Path.Combine(repositoryRoot, "Testing", "Duck.Testing.AppHost", "Duck.Testing.AppHost.csproj");
            using var secondAppHost = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    WorkingDirectory = repositoryRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            secondAppHost.StartInfo.ArgumentList.Add("run");
            secondAppHost.StartInfo.ArgumentList.Add("--no-build");
            secondAppHost.StartInfo.ArgumentList.Add("--project");
            secondAppHost.StartInfo.ArgumentList.Add(appHostProject);
            secondAppHost.StartInfo.Environment["Testing__StackOwnership__LockPath"] = lockPath;
            secondAppHost.StartInfo.Environment["Testing__StackOwnership__StackId"] = secondStackId;

            string stdout = string.Empty;
            string stderr = string.Empty;
            var secondExited = false;
            var secondStarted = false;
            try
            {
                Assert.True(secondAppHost.Start(), "Could not start the second AppHost process.");
                secondStarted = true;
                var stdoutTask = secondAppHost.StandardOutput.ReadToEndAsync();
                var stderrTask = secondAppHost.StandardError.ReadToEndAsync();
                using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    await secondAppHost.WaitForExitAsync(exitTimeout.Token);
                    secondExited = true;
                }
                catch (OperationCanceledException)
                {
                    throw new InvalidOperationException("The second AppHost did not exit within 30 seconds; a timeout is not accepted as lock rejection.");
                }

                stdout = await stdoutTask;
                stderr = await stderrTask;
                var combinedOutput = stdout + Environment.NewLine + stderr;
                Assert.NotEqual(0, secondAppHost.ExitCode);
                Assert.Contains($"Another Duck Aspire stack already owns '{Path.GetFullPath(lockPath)}'.", combinedOutput, StringComparison.Ordinal);
                Assert.Empty(await GetOwnedContainerIdsAsync(secondStackId, dockerExecutable));
            }
            finally
            {
                if (secondStarted && !secondExited && !secondAppHost.HasExited)
                {
                    secondAppHost.Kill(entireProcessTree: true);
                    using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await secondAppHost.WaitForExitAsync(cleanupTimeout.Token);
                }

                await RemoveOnlySecondStackContainersAsync(secondStackId, dockerExecutable);
                AssertFirstAppHostRemainsActive();
                Assert.Equal(firstContainerIds, await GetOwnedContainerIdsAsync(firstStackId, dockerExecutable));
            }

            Assert.Empty(await GetOwnedContainerIdsAsync(secondStackId, dockerExecutable));
            AssertFirstAppHostRemainsActive();
            Assert.Equal(firstContainerIds, await GetOwnedContainerIdsAsync(firstStackId, dockerExecutable));
        }
        finally
        {
            try
            {
                if (managedFirstApplication is not null)
                {
                    try
                    {
                        if (managedFirstStartAttempted)
                        {
                            using var stopTimeout = new CancellationTokenSource(StackSignalTimeout);
                            await managedFirstApplication.StopAsync(stopTimeout.Token);
                        }
                    }
                    finally
                    {
                        await managedFirstApplication.DisposeAsync();
                    }

                    Assert.Empty(await GetOwnedContainerIdsAsync(firstStackId, dockerExecutable));
                }

                firstAppHost?.Dispose();
            }
            finally
            {
                try
                {
                    CleanupStagedAppHostAssets(stagedAppHostAssets);
                }
                finally
                {
                    dockerPathScope?.Dispose();
                }
            }
        }

        void AssertFirstAppHostRemainsActive()
        {
            Assert.False(firstAppHost?.HasExited ?? false, "The first AppHost process exited during the second-owner check.");
            if (managedFirstApplication is not null)
            {
                var lifetime = managedFirstApplication.Services.GetRequiredService<IHostApplicationLifetime>();
                Assert.False(lifetime.ApplicationStopping.IsCancellationRequested, "The first AppHost stopped during the second-owner check.");
            }
            else
            {
                Assert.NotNull(firstAppHost);
            }
        }
    }

    [Fact]
    public async Task BaseAppHostAndDebugHostReceiveVisionMulticastDuringTheSameWindow()
    {
        var acceptanceClock = Stopwatch.StartNew();
        var totalBudget = TimeSpan.FromSeconds(300);
        var workBudget = TimeSpan.FromSeconds(240);
        var runId = "issue82-net003-" + Guid.NewGuid().ToString("N");
        var stackId = runId;
        var artifactsDirectory = Path.Combine(Path.GetTempPath(), runId);
        var captureRoot = artifactsDirectory;
        Directory.CreateDirectory(artifactsDirectory);
        var repositoryRoot = FindRepositoryRoot();
        var (dockerExecutable, dockerPathScope) = ResolveAndPrepareDockerExecutable(
            Environment.GetEnvironmentVariable("Testing__IdentityControl__DockerExecutable"),
            Environment.GetEnvironmentVariable("PATH"));
        var stagedAssets = new List<StagedAppHostAsset>();
        IDistributedApplicationTestingBuilder? appHostBuilder = null;
        DistributedApplication? application = null;
        string[] baselineRunningIds = [];
        string debugHostImageId = "";
        var windowStartedAt = DateTimeOffset.MinValue;
        var windowEndedAt = DateTimeOffset.MinValue;
        var receiveWindowStarted = false;

        TimeSpan Remaining(TimeSpan budget, string phase)
        {
            var remaining = budget - acceptanceClock.Elapsed;
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException($"NET-003 {phase} budget expired after {acceptanceClock.Elapsed}.");
            return remaining;
        }

        TimeSpan WorkCommandTimeout()
        {
            var available = Remaining(workBudget, "work") - TimeSpan.FromSeconds(1);
            return available < TimeSpan.FromSeconds(15) ? available : TimeSpan.FromSeconds(15);
        }

        TimeSpan CleanupCommandTimeout()
        {
            var available = Remaining(totalBudget, "cleanup") - TimeSpan.FromSeconds(1);
            return available < TimeSpan.FromSeconds(5) ? available : TimeSpan.FromSeconds(5);
        }

        TimeSpan ProcessKillWaitTimeout() => TimeSpan.FromSeconds(1);

        Task<ProcessResult> DockerAsync(IEnumerable<string> arguments, bool cleanup = false) =>
            RunProcessAsync(
                dockerExecutable,
                arguments,
                cleanup ? CleanupCommandTimeout() : WorkCommandTimeout(),
                ProcessKillWaitTimeout());

        async Task<(HttpStatusCode Status, long PacketCount, long DecodedCount, bool IsReady, string Body)> ReadVisionObservationAsync(string baseUrl)
        {
            using var client = new HttpClient { Timeout = WorkCommandTimeout() };
            using var response = await client.GetAsync(new Uri(new Uri(baseUrl), "/health/vision"));
            var responseBody = await response.Content.ReadAsStringAsync();
            using var document = System.Text.Json.JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            System.Text.Json.JsonElement GetPropertyIgnoreCase(System.Text.Json.JsonElement element, string name)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                        return property.Value;
                }
                throw new System.Text.Json.JsonException($"Health response is missing '{name}': {responseBody}");
            }

            var count = GetPropertyIgnoreCase(root, "VisionPacketsReceived").GetInt64();
            var isReady = GetPropertyIgnoreCase(root, "IsReady").GetBoolean();
            var diagnostics = GetPropertyIgnoreCase(root, "Diagnostics");
            var decodedCount = diagnostics.ValueKind == System.Text.Json.JsonValueKind.Array
                ? diagnostics.EnumerateArray().Sum(snapshot => GetPropertyIgnoreCase(snapshot, "SuccessfullyDecodedCount").GetInt64())
                : GetPropertyIgnoreCase(diagnostics, "SuccessfullyDecodedCount").GetInt64();
            return (response.StatusCode, count, decodedCount, isReady, responseBody);
        }

        try
        {
            var (dockerContext, engineId) = await ResolveDockerIdentityAsync(WorkCommandTimeout, ProcessKillWaitTimeout);
            var requiredImages = new[]
            {
                "ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd",
                "robocupssl/ssl-game-controller:3.20.3",
                "ghcr.io/ibis-ssl/orion-cm4-sim:d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a",
                "ghcr.io/ibis-ssl/crane:scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                "mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3",
                "mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca",
            };
            var imageCheck = await DockerAsync(["image", "inspect", .. requiredImages]);
            Assert.Equal(0, imageCheck.ExitCode);
            baselineRunningIds = (await DockerAsync(["ps", "--quiet", "--no-trunc"]))
                .StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Order(StringComparer.Ordinal)
                .ToArray();

            var helper = ResolveCm4TransportHelperSource(repositoryRoot);
            var overlay = Path.Combine(Projects.Duck_Testing_AppHost.ProjectPath, "CraneLaunchOverlay.py");
            stagedAssets.AddRange(StageLocalAppHostAssets(helper, overlay));
            foreach (var relativePath in new[]
            {
                Path.Combine("Tracker", "Tracker.RuntimeHost", "bin", "Release", "net10.0", "publish", "Tracker.RuntimeHost.dll"),
                Path.Combine("Tracker", "Tracker.DebugHost", "bin", "Release", "net10.0", "publish", "Tracker.DebugHost.dll"),
            })
            {
                Assert.True(File.Exists(Path.Combine(repositoryRoot, relativePath)), $"Required local publish output was not found: {relativePath}");
            }

            var arguments = new[]
            {
                "--Testing:Mode=base",
                "--Testing:DebugHost:VisionObserverEnabled=true",
                "--Testing:DebugHost:CaptureRootPath=" + captureRoot,
                "--Testing:IdentityControl:DesktopBridgeEnabled=true",
                "--Testing:IdentityControl:DockerContext=" + dockerContext,
                "--Testing:IdentityControl:ExpectedEngineId=" + engineId,
                "--Testing:IdentityControl:DockerExecutable=" + dockerExecutable,
                "--Testing:StackOwnership:LockPath=" + Path.Combine(artifactsDirectory, runId + ".lock"),
                "--Testing:StackOwnership:StackId=" + stackId,
                "--Testing:Diagnostics:RunId=" + runId,
                "--Testing:Crane:FixtureManifestPath=" + Path.Combine(artifactsDirectory, "crane-fixture.json"),
                "--Testing:Crane:DiagnosticsPath=" + Path.Combine(artifactsDirectory, "crane-diagnostics.jsonl"),
            };
            using var appHostDeadline = new CancellationTokenSource(Remaining(workBudget, "AppHost creation"));
            appHostBuilder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
                arguments, appHostDeadline.Token);
            application = await appHostBuilder.BuildAsync(appHostDeadline.Token);
            await application.StartAsync(appHostDeadline.Token);

            var notifications = application.Services.GetRequiredService<ResourceNotificationService>();
            foreach (var resource in new[] { "simulator", "duck", "game-controller", "cm4-sim", "crane", "debug-host" })
            {
                using var readinessDeadline = new CancellationTokenSource(Remaining(workBudget, "resource readiness"));
                await notifications.WaitForResourceAsync(resource, KnownResourceStates.Running, readinessDeadline.Token);
            }

            var ownedIds = await GetOwnedContainerIdsAsync(stackId, dockerExecutable, WorkCommandTimeout, ProcessKillWaitTimeout);
            Assert.Equal(6, ownedIds.Length);
            var resourceIds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in ownedIds)
            {
                var labels = await DockerAsync(["inspect", "--format", "{{json .Config.Labels}}", id]);
                Assert.Equal(0, labels.ExitCode);
                using var labelDocument = System.Text.Json.JsonDocument.Parse(labels.StandardOutput);
                resourceIds[labelDocument.RootElement.GetProperty(DockerContainerSpec.ResourceLabel).GetString()!] = id;
            }

            var debugHostImage = await DockerAsync(["inspect", "--format", "{{.Image}}", resourceIds["debug-host"]]);
            Assert.Equal(0, debugHostImage.ExitCode);
            debugHostImageId = debugHostImage.StandardOutput.Trim();

            var endpointPorts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in new[] { ("duck", resourceIds["duck"], "39108/tcp"), ("debug-host", resourceIds["debug-host"], "8080/tcp") })
            {
                var result = await DockerAsync(["inspect", "--format", "{{json .NetworkSettings.Ports}}", entry.Item2]);
                Assert.Equal(0, result.ExitCode);
                using var ports = System.Text.Json.JsonDocument.Parse(result.StandardOutput);
                var mapping = ports.RootElement.GetProperty(entry.Item3)[0];
                Assert.Equal("127.0.0.1", mapping.GetProperty("HostIp").GetString());
                endpointPorts[entry.Item1] = int.Parse(mapping.GetProperty("HostPort").GetString()!);
            }

            var duckUrl = $"http://127.0.0.1:{endpointPorts["duck"]}";
            var debugHostUrl = $"http://127.0.0.1:{endpointPorts["debug-host"]}";
            var readinessDeadlineAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(45);
            (HttpStatusCode Status, long PacketCount, long DecodedCount, bool IsReady, string Body) duckBefore;
            (HttpStatusCode Status, long PacketCount, long DecodedCount, bool IsReady, string Body) debugHostBefore;
            while (true)
            {
                try
                {
                    duckBefore = await ReadVisionObservationAsync(duckUrl);
                    debugHostBefore = await ReadVisionObservationAsync(debugHostUrl);
                    if (duckBefore.Status == HttpStatusCode.OK && duckBefore.PacketCount > 0 && debugHostBefore.DecodedCount > 0)
                        break;
                    if (DateTimeOffset.UtcNow >= readinessDeadlineAt)
                        throw new TimeoutException($"NET-003 receive counters did not become available. Duck={duckBefore.Body}; DebugHost={debugHostBefore.Body}");
                }
                catch (Exception) when (DateTimeOffset.UtcNow < readinessDeadlineAt)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), new CancellationTokenSource(Remaining(workBudget, "health readiness")).Token);
                }
            }

            windowStartedAt = DateTimeOffset.UtcNow;
            receiveWindowStarted = true;
            await Task.Delay(TimeSpan.FromSeconds(5), new CancellationTokenSource(Remaining(workBudget, "5-second receive window")).Token);
            windowEndedAt = DateTimeOffset.UtcNow;
            var duckAfter = await ReadVisionObservationAsync(duckUrl);
            var debugHostAfter = await ReadVisionObservationAsync(debugHostUrl);
            Assert.Equal(HttpStatusCode.OK, duckAfter.Status);
            Assert.True(duckAfter.PacketCount > duckBefore.PacketCount, $"Duck did not receive vision during the window: {duckBefore.PacketCount} -> {duckAfter.PacketCount}.");
            Assert.True(debugHostAfter.DecodedCount > debugHostBefore.DecodedCount, $"DebugHost did not decode vision during the window: {debugHostBefore.DecodedCount} -> {debugHostAfter.DecodedCount}. Health={debugHostAfter.Body}");
            output.WriteLine($"NET003 engine={dockerContext}/{engineId}; stack={stackId}; windowStart={windowStartedAt:O}; windowEnd={windowEndedAt:O}; duckStatus={(int)duckBefore.Status}->{(int)duckAfter.Status}; duckDecoded={duckBefore.DecodedCount}->{duckAfter.DecodedCount}; debugHostStatus={(int)debugHostBefore.Status}->{(int)debugHostAfter.Status}; debugHostReady={debugHostBefore.IsReady}->{debugHostAfter.IsReady}; debugHostDecoded={debugHostBefore.DecodedCount}->{debugHostAfter.DecodedCount}; debugHostRawCapture=await-stop.");

        }
        finally
        {
            try
            {
            Exception? shutdownFailure = null;
            if (application is not null)
            {
                try
                {
                    using var stopDeadline = new CancellationTokenSource(
                        Remaining(totalBudget, "AppHost stop") < TimeSpan.FromSeconds(30)
                            ? Remaining(totalBudget, "AppHost stop")
                            : TimeSpan.FromSeconds(30));
                    await application.StopAsync(stopDeadline.Token);
                }
                catch (Exception exception)
                {
                    shutdownFailure = exception;
                    output.WriteLine($"NET003 AppHost stop failed: {exception}");
                }

                try
                {
                    await application.DisposeAsync().AsTask().WaitAsync(Remaining(totalBudget, "AppHost dispose"));
                }
                catch (Exception exception)
                {
                    shutdownFailure ??= exception;
                    output.WriteLine($"NET003 AppHost dispose failed: {exception}");
                }
            }
            if (appHostBuilder is not null)
            {
                try
                {
                    await appHostBuilder.DisposeAsync().AsTask().WaitAsync(Remaining(totalBudget, "AppHost builder dispose"));
                }
                catch (Exception exception)
                {
                    shutdownFailure ??= exception;
                    output.WriteLine($"NET003 AppHost builder dispose failed: {exception}");
                }
            }

            var remainingOwnedIds = await GetOwnedContainerIdsAsync(stackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
            output.WriteLine($"NET003 pre-rescueOwnedIds={string.Join(",", remainingOwnedIds)}; debugHostImageId={debugHostImageId}");
            foreach (var id in remainingOwnedIds)
            {
                _ = await AssertStackOwnedContainerAsync(id, stackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                var removal = await DockerAsync(["rm", "--force", id], cleanup: true);
                Assert.Equal(0, removal.ExitCode);
            }
            Assert.Empty(await GetOwnedContainerIdsAsync(stackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout));
            var runningAfter = (await DockerAsync(["ps", "--quiet", "--no-trunc"], cleanup: true))
                .StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal);
            Assert.All(baselineRunningIds, id => Assert.Contains(id, runningAfter));

            if (receiveWindowStarted)
            {
                var captureDirectory = Path.Combine(captureRoot, "Duck", "Aspire", runId, "captures");
                var captureFiles = Directory.Exists(captureDirectory)
                    ? Directory.GetFiles(captureDirectory, "*.jsonl.gz", SearchOption.AllDirectories)
                    : [];
                Assert.NotEmpty(captureFiles);
                var packetCount = 0;
                var detectionPacketCount = 0;
                var ballCount = 0;
                var robotCount = 0;
                DateTimeOffset? firstPacket = null;
                DateTimeOffset? lastPacket = null;
                foreach (var file in captureFiles)
                {
                    using var compressed = File.OpenRead(file);
                    using var gzip = new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionMode.Decompress);
                    using var reader = new StreamReader(gzip);
                    string? line;
                    while ((line = await reader.ReadLineAsync()) is not null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        using var record = System.Text.Json.JsonDocument.Parse(line);
                        var timestamp = DateTimeOffset.Parse(record.RootElement.GetProperty("receivedAt").GetString()!);
                        if (timestamp < windowStartedAt || timestamp > windowEndedAt) continue;
                        packetCount++;
                        firstPacket ??= timestamp;
                        lastPacket = timestamp;
                        var payload = Convert.FromBase64String(record.RootElement.GetProperty("payloadBase64").GetString()!);
                        var packet = SSL_WrapperPacket.Parser.ParseFrom(payload);
                        if (packet.Detection is not null)
                        {
                            detectionPacketCount++;
                            ballCount += packet.Detection.Balls.Count;
                            robotCount += packet.Detection.RobotsYellow.Count + packet.Detection.RobotsBlue.Count;
                        }
                    }
                }
                output.WriteLine($"NET003 debugHostRawPacketsInWindow={packetCount}; detectionPackets={detectionPacketCount}; balls={ballCount}; robots={robotCount}; first={firstPacket:O}; last={lastPacket:O}");
                Assert.True(packetCount > 0, "DebugHost did not capture any raw vision datagrams in the same five-second window.");
            }

            if (shutdownFailure is not null)
                throw new InvalidOperationException("NET-003 AppHost shutdown did not complete cleanly.", shutdownFailure);
            CleanupStagedAppHostAssets(stagedAssets);
            output.WriteLine($"NET003 elapsed={acceptanceClock.Elapsed}; completedAt={DateTimeOffset.UtcNow:O}; localCaptureRoot={captureRoot}");
            }
            finally
            {
                dockerPathScope.Dispose();
            }
        }
    }
    [Fact]
    public async Task CancellingAppHostDuringStartupCleansOnlyItsContainers()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var acceptanceClock = Stopwatch.StartNew();
        var workBudget = TimeSpan.FromSeconds(180);
        var totalBudget = TimeSpan.FromSeconds(300);
        using var workDeadline = new CancellationTokenSource(workBudget);
        var artifactsDirectory = Path.Combine(Path.GetTempPath(), "duck-aspire-partial-cancel-artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactsDirectory);
        var repositoryRoot = FindRepositoryRoot();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var (dockerExecutable, preparedDockerPathScope) = ResolveAndPrepareDockerExecutable(
            Environment.GetEnvironmentVariable("Testing__IdentityControl__DockerExecutable"), originalPath);
        IDisposable? dockerPathScope = preparedDockerPathScope;
        var stagedAppHostAssets = new List<StagedAppHostAsset>();
        DistributedApplication? application = null;
        IDistributedApplicationTestingBuilder? appHostBuilder = null;
        var startAttempted = false;
        string stackId = $"cancelled-owner-{Guid.NewGuid():N}";
        var runId = $"cancelled-run-{Guid.NewGuid():N}";
        var sentinelStackId = $"sentinel-owner-{Guid.NewGuid():N}";
        var sentinelRunId = $"sentinel-run-{Guid.NewGuid():N}";
        string? sentinelContainerId = null;
        var sentinelCreationAttempted = false;
        var sentinelName = $"duck82-sentinel-{Guid.NewGuid():N}";
        var baselineRunningIds = Array.Empty<string>();

        TimeSpan Remaining(TimeSpan deadline, string phase)
        {
            var remaining = deadline - acceptanceClock.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException($"Issue #82 cancellation acceptance {phase} budget expired after {acceptanceClock.Elapsed}.");
            }

            return remaining;
        }

        void EnsureWorkBudget() => _ = Remaining(workBudget, "work");

        TimeSpan WorkCommandTimeout()
        {
            var available = Remaining(workBudget, "work") - TimeSpan.FromSeconds(1);
            if (available <= TimeSpan.Zero)
            {
                throw new TimeoutException("Issue #82 cancellation acceptance work budget expired before a Docker command could start.");
            }

            return available < TimeSpan.FromSeconds(15) ? available : TimeSpan.FromSeconds(15);
        }

        TimeSpan CleanupCommandTimeout()
        {
            var available = Remaining(totalBudget, "cleanup") - TimeSpan.FromSeconds(1);
            if (available <= TimeSpan.Zero)
            {
                throw new TimeoutException("Issue #82 cancellation acceptance cleanup budget expired before a Docker command could start.");
            }

            return available < TimeSpan.FromSeconds(3) ? available : TimeSpan.FromSeconds(3);
        }

        TimeSpan ProcessKillWaitTimeout() => TimeSpan.FromSeconds(1);

        TimeSpan CleanupOperationTimeout(TimeSpan cap, TimeSpan reserveAfter)
        {
            var remaining = Remaining(totalBudget, "cleanup") - reserveAfter;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("Issue #82 cancellation acceptance has entered its reserved exact-owner cleanup window.");
            }

            return remaining < cap ? remaining : cap;
        }

        Task<ProcessResult> RunWorkDockerCommandAsync(IEnumerable<string> arguments) =>
            RunProcessAsync(dockerExecutable, arguments, WorkCommandTimeout(), ProcessKillWaitTimeout());

        Task<ProcessResult> RunCleanupDockerCommandAsync(IEnumerable<string> arguments) =>
            RunProcessAsync(dockerExecutable, arguments, CleanupCommandTimeout(), ProcessKillWaitTimeout());

        async Task DisposeAppHostBuilderAsync()
        {
            var builderToDispose = appHostBuilder;
            appHostBuilder = null;
            if (builderToDispose is not null)
            {
                await builderToDispose.DisposeAsync();
            }
        }

        async Task AssertSentinelStillRunningAsync(Func<TimeSpan> commandTimeout)
        {
            if (sentinelContainerId is null)
            {
                return;
            }

            var sentinelIds = await GetOwnedContainerIdsAsync(
                sentinelStackId, dockerExecutable, commandTimeout, ProcessKillWaitTimeout);
            Assert.Equal([sentinelContainerId], sentinelIds);
            var sentinelState = await RunProcessAsync(
                dockerExecutable, ["inspect", "--format", "{{.State.Running}}", sentinelContainerId],
                commandTimeout(), ProcessKillWaitTimeout());
            Assert.Equal(0, sentinelState.ExitCode);
            Assert.Equal("true", sentinelState.StandardOutput.Trim());
            output.WriteLine($"sentinelPreserved={sentinelContainerId}; running={sentinelState.StandardOutput.Trim()}");
        }

        try
        {
            var (dockerContext, expectedEngineId) = await ResolveDockerIdentityAsync(
                WorkCommandTimeout, ProcessKillWaitTimeout);
            output.WriteLine($"Engine={dockerContext}/{expectedEngineId}; startedAt={startedAt:O}");

            var requiredImages = new[]
            {
                "ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd",
                "robocupssl/ssl-game-controller:3.20.3",
                "ghcr.io/ibis-ssl/orion-cm4-sim:d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a",
                "ghcr.io/ibis-ssl/crane:scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                "mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3",
            };
            var imageCheck = await RunWorkDockerCommandAsync(["image", "inspect", .. requiredImages]);
            Assert.Equal(0, imageCheck.ExitCode);

            baselineRunningIds = (await RunWorkDockerCommandAsync(["ps", "--quiet", "--no-trunc"]))
                .StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Order(StringComparer.Ordinal)
                .ToArray();

            var lockPath = Path.Combine(artifactsDirectory, $"{stackId}.lock");
            var fixtureManifestPath = Path.Combine(artifactsDirectory, $"{stackId}-crane-fixture.json");
            var craneDiagnosticsPath = Path.Combine(artifactsDirectory, $"{stackId}-crane-diagnostics.jsonl");
            var cm4HelperSource = ResolveCm4TransportHelperSource(repositoryRoot);
            var craneOverlaySource = Path.Combine(Projects.Duck_Testing_AppHost.ProjectPath, "CraneLaunchOverlay.py");
            stagedAppHostAssets.AddRange(StageLocalAppHostAssets(cm4HelperSource, craneOverlaySource));
            var runtimePublishDirectory = Path.Combine(
                repositoryRoot, "Tracker", "Tracker.RuntimeHost", "bin", "Release", "net10.0", "publish");
            Assert.True(
                File.Exists(Path.Combine(runtimePublishDirectory, "Tracker.RuntimeHost.dll")),
                $"AppHost Dockerfile build context is missing the published runtime at '{runtimePublishDirectory}'.");
            EnsureWorkBudget();
            appHostBuilder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>([
                "--Testing:Mode=base",
                "--Testing:Crane:Planner=visibility_graph",
                "--Testing:Simulator:ImageTag=a52b6bd",
                "--Testing:Crane:ImageTag=scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                "--Testing:Cm4Sim:ImageTag=d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a",
                "--Testing:IdentityControl:DesktopBridgeEnabled=true",
                "--Testing:IdentityControl:DockerContext=" + dockerContext,
                "--Testing:IdentityControl:ExpectedEngineId=" + expectedEngineId,
                "--Testing:IdentityControl:DockerExecutable=" + dockerExecutable,
                "--Testing:StackOwnership:LockPath=" + lockPath,
                "--Testing:StackOwnership:StackId=" + stackId,
                "--Testing:Diagnostics:RunId=" + runId,
                "--Testing:Crane:FixtureManifestPath=" + fixtureManifestPath,
                "--Testing:Crane:DiagnosticsPath=" + craneDiagnosticsPath,
            ], workDeadline.Token);
            EnsureWorkBudget();
            application = await appHostBuilder.BuildAsync(workDeadline.Token);
            EnsureWorkBudget();

            output.WriteLine($"acceptancePreflight=complete; cm4HelperPath={cm4HelperSource}; cm4HelperSha256={GetSha256(cm4HelperSource)}; runtimePublishPath={runtimePublishDirectory}; engine={dockerContext}/{expectedEngineId}; imageCount={requiredImages.Length}");

            sentinelCreationAttempted = true;
            var sentinelCreate = await RunWorkDockerCommandAsync(
            [
                "run", "--pull=never", "--detach", "--name", sentinelName, "--network", "none",
                "--label", $"{DockerContainerSpec.StackLabel}={sentinelStackId}",
                "--label", $"{DockerContainerSpec.ResourceLabel}=sentinel",
                "--label", $"{DockerContainerSpec.RunLabel}={sentinelRunId}",
                "--entrypoint", "/bin/bash",
                requiredImages[3], "-c", "exec sleep 600",
            ]);
            Assert.Equal(0, sentinelCreate.ExitCode);
            var sentinelLookup = await RunWorkDockerCommandAsync(["inspect", "--format", "{{.Id}}", sentinelName]);
            Assert.Equal(0, sentinelLookup.ExitCode);
            sentinelContainerId = sentinelLookup.StandardOutput.Trim();
            Assert.Matches("^[0-9a-f]{64}$", sentinelContainerId);
            Assert.Equal([sentinelContainerId], await GetOwnedContainerIdsAsync(
                sentinelStackId, dockerExecutable, WorkCommandTimeout, ProcessKillWaitTimeout));
            output.WriteLine("sentinelNetwork=none; sentinelPublishedPorts=0; existing AppHost endpoint bindings are not asserted to be zero.");

            using var startupTimeout = new CancellationTokenSource(Remaining(workBudget, "AppHost startup"));
            startAttempted = true;
            var startTask = application.StartAsync(startupTimeout.Token);
            var notifications = application.Services.GetRequiredService<ResourceNotificationService>();
            await notifications.WaitForResourceAsync("simulator", KnownResourceStates.Running, startupTimeout.Token);
            var partialOwnedIds = await GetOwnedContainerIdsAsync(
                stackId, dockerExecutable, WorkCommandTimeout, ProcessKillWaitTimeout);
            Assert.NotEmpty(partialOwnedIds);
            Assert.True(partialOwnedIds.Length < 5, "Cancellation must be observed while the stack is only partially started.");
            Assert.False(startTask.IsCompleted, "AppHost startup completed before the partial-start cancellation point; this run is inconclusive.");

            output.WriteLine($"cancelAt={DateTimeOffset.UtcNow:O}; runningOwnedIds={partialOwnedIds.Length}; sentinel={sentinelContainerId}");
            startupTimeout.Cancel();
            output.WriteLine("startAsyncCancellation=cancelled startup wait; actual AppHost stop follows through StopAsync and DisposeAsync.");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await startTask.WaitAsync(Remaining(workBudget, "cancellation observation")));
            await AssertSentinelStillRunningAsync(WorkCommandTimeout);
        }
        finally
        {
            try
            {
                if (application is not null)
                {
                    try
                    {
                        if (startAttempted)
                        {
                            using var stopTimeout = new CancellationTokenSource(
                                CleanupOperationTimeout(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(90)));
                            await application.StopAsync(stopTimeout.Token);
                        }
                    }
                    finally
                    {
                        try
                        {
                            await application.DisposeAsync().AsTask().WaitAsync(
                                CleanupOperationTimeout(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)));
                        }
                        finally
                        {
                            try
                            {
                                await DisposeAppHostBuilderAsync();
                                string[] remainingOwnedIds = [];
                                var verificationFailures = new List<Exception>();
                                try
                                {
                                    remainingOwnedIds = await GetOwnedContainerIdsAsync(
                                        stackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                                }
                                catch (Exception exception)
                                {
                                    verificationFailures.Add(exception);
                                }

                                output.WriteLine($"cancelledOwnerAfterStopDispose={remainingOwnedIds.Length}; ids={string.Join(',', remainingOwnedIds)}");
                                try
                                {
                                    await AssertSentinelStillRunningAsync(CleanupCommandTimeout);
                                }
                                catch (Exception exception)
                                {
                                    verificationFailures.Add(exception);
                                }

                                if (verificationFailures.Count > 0)
                                {
                                    throw new AggregateException("Post-stop ownership/protection verification failed.", verificationFailures);
                                }

                                Assert.Empty(remainingOwnedIds);
                            }
                            finally
                            {
                                await RemoveOnlySecondStackContainersAsync(
                                    stackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                                var remainingAfterRescue = await GetOwnedContainerIdsAsync(
                                    stackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                                output.WriteLine($"cancelledOwnerAfterRescue={remainingAfterRescue.Length}");
                                Assert.Empty(remainingAfterRescue);
                            }
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    if (sentinelContainerId is null && sentinelCreationAttempted)
                    {
                        var recoveryLookup = await RunCleanupDockerCommandAsync(
                            ["inspect", "--format", "{{.Id}}", sentinelName]);
                        if (recoveryLookup.ExitCode == 0)
                        {
                            sentinelContainerId = recoveryLookup.StandardOutput.Trim();
                        }
                    }

                    if (sentinelContainerId is not null)
                    {
                        await AssertStackOwnedContainerAsync(
                            sentinelContainerId, sentinelStackId, dockerExecutable,
                            CleanupCommandTimeout, ProcessKillWaitTimeout);
                        if (application is null)
                        {
                            await AssertSentinelStillRunningAsync(CleanupCommandTimeout);
                        }

                        await RemoveOnlySecondStackContainersAsync(
                            sentinelStackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                        var remainingSentinelIds = await GetOwnedContainerIdsAsync(
                            sentinelStackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                        output.WriteLine($"sentinelAfterCleanup={remainingSentinelIds.Length}");
                        Assert.Empty(remainingSentinelIds);
                    }

                    var runningAfterCleanup = (await RunCleanupDockerCommandAsync(
                            ["ps", "--quiet", "--no-trunc"]))
                        .StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToHashSet(StringComparer.Ordinal);
                    foreach (var existingId in baselineRunningIds)
                    {
                        Assert.Contains(existingId, runningAfterCleanup);
                    }
                }
                finally
                {
                    try
                    {
                        await DisposeAppHostBuilderAsync();
                        CleanupStagedAppHostAssets(stagedAppHostAssets);
                    }
                    finally
                    {
                        dockerPathScope?.Dispose();
                        output.WriteLine($"finishedAt={DateTimeOffset.UtcNow:O}; elapsed={acceptanceClock.Elapsed}; totalBudget={totalBudget}; workBudget={workBudget}");
                    }
                }
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Testing", "Duck.Testing.AppHost", "Duck.Testing.AppHost.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Duck repository root from the test process.");
    }

    private static string CreateDockerExecutable(string directory)
    {
        var executableName = OperatingSystem.IsWindows() ? "docker.exe" : "docker";
        var executablePath = Path.Combine(directory, executableName);
        File.WriteAllText(executablePath, string.Empty);
        return Path.GetFullPath(executablePath);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("duck-docker-path-tests-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class DockerPathScope(string? originalPath, bool restore) : IDisposable
    {
        public void Dispose()
        {
            if (restore)
            {
                Environment.SetEnvironmentVariable("PATH", originalPath);
            }
        }
    }

    private static string ResolveDockerExecutable(string? configuredPath, string? path)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath);
            Assert.True(File.Exists(fullPath), $"Configured Docker executable does not exist: {fullPath}");
            return fullPath;
        }

        var executableName = OperatingSystem.IsWindows() ? "docker.exe" : "docker";
        foreach (var directory in (path ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.GetFullPath(Path.Combine(directory, executableName));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "Docker CLI could not be resolved from PATH; configure Testing__IdentityControl__DockerExecutable or add Docker to this test process PATH.");
    }

    private static (string DockerExecutable, IDisposable PathScope) ResolveAndPrepareDockerExecutable(
        string? configuredPath,
        string? path)
    {
        string dockerExecutable;
        try
        {
            dockerExecutable = ResolveDockerExecutable(configuredPath, path);
        }
        catch (FileNotFoundException) when (OperatingSystem.IsWindows())
        {
            dockerExecutable = NamespaceProbeDcpAcceptanceTests.GetDockerExecutablePath();
        }

        return (dockerExecutable, AddDockerDirectoryToProcessPath(dockerExecutable));
    }

    private static string PrependDockerDirectoryToPath(string dockerExecutable, string? path)
    {
        var dockerDirectory = Path.GetDirectoryName(Path.GetFullPath(dockerExecutable))
            ?? throw new InvalidOperationException($"Docker executable has no containing directory: {dockerExecutable}");
        if (string.IsNullOrEmpty(path))
        {
            return dockerDirectory;
        }

        var existingDirectories = path.Split(Path.PathSeparator, StringSplitOptions.None);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var normalizedDockerDirectory = Path.TrimEndingDirectorySeparator(dockerDirectory);
        var matchingIndices = existingDirectories
            .Select((directory, index) => (directory, index))
            .Where(item => !string.IsNullOrWhiteSpace(item.directory) && string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(item.directory.Trim())),
                normalizedDockerDirectory,
                comparison))
            .Select(item => item.index)
            .ToHashSet();

        if (matchingIndices.Contains(0) && matchingIndices.Count == 1)
        {
            return path;
        }

        var remainingDirectories = existingDirectories
            .Where((_, index) => !matchingIndices.Contains(index));
        return dockerDirectory + Path.PathSeparator + string.Join(Path.PathSeparator, remainingDirectories);
    }

    private static IDisposable AddDockerDirectoryToProcessPath(string dockerExecutable)
    {
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var pathWithDocker = PrependDockerDirectoryToPath(dockerExecutable, originalPath);
        if (string.Equals(pathWithDocker, originalPath, StringComparison.Ordinal))
        {
            return new DockerPathScope(originalPath, restore: false);
        }

        Environment.SetEnvironmentVariable("PATH", pathWithDocker);
        return new DockerPathScope(originalPath, restore: true);
    }

    private static async Task<(string Context, string EngineId)> ResolveDockerIdentityAsync(
        Func<TimeSpan>? commandTimeout = null,
        Func<TimeSpan>? killWaitTimeout = null)
    {
        var versionResult = await RunProcessAsync(
            "docker", ["--version"], commandTimeout?.Invoke() ?? TimeSpan.FromSeconds(15), killWaitTimeout?.Invoke());
        Assert.True(versionResult.ExitCode == 0,
            $"Docker CLI could not be resolved from the acceptance test process PATH. exit={versionResult.ExitCode}; stderr={versionResult.StandardError.Trim()}");
        var configuredContext = Environment.GetEnvironmentVariable("Testing__IdentityControl__DockerContext");
        var context = configuredContext;
        if (string.IsNullOrWhiteSpace(context))
        {
            var contextResult = await RunProcessAsync(
                "docker", ["context", "show"], commandTimeout?.Invoke() ?? TimeSpan.FromSeconds(15), killWaitTimeout?.Invoke());
            Assert.Equal(0, contextResult.ExitCode);
            context = contextResult.StandardOutput.Trim();
        }

        Assert.False(string.IsNullOrWhiteSpace(context), "Docker context name is missing.");
        var engineResult = await RunProcessAsync("docker",
            ["--context", context!, "info", "--format", "{{.ID}}"],
            commandTimeout?.Invoke() ?? TimeSpan.FromSeconds(15), killWaitTimeout?.Invoke());
        Assert.Equal(0, engineResult.ExitCode);
        var engineId = engineResult.StandardOutput.Trim();
        Assert.False(string.IsNullOrWhiteSpace(engineId), "Docker Engine ID is missing from docker info.");

        var configuredEngineId = Environment.GetEnvironmentVariable("Testing__IdentityControl__ExpectedEngineId");
        if (!string.IsNullOrWhiteSpace(configuredEngineId))
        {
            Assert.Equal(configuredEngineId, engineId);
        }

        return (context!, engineId);
    }

    private static string ResolveCm4TransportHelperSource(string repositoryRoot) =>
        ResolveCm4TransportHelperSource(repositoryRoot, AppContext.BaseDirectory);

    private static string ResolveCm4TransportHelperSource(string repositoryRoot, string appContextBaseDirectory)
    {
        const string helperName = BaseSimulationResourceRegistration.Cm4TransportHelperAssetName;
        var configuredPath = Environment.GetEnvironmentVariable("DUCK_ASPIRE_CM4_TRANSPORT_HELPER");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath);
            Assert.True(File.Exists(fullPath), $"Configured CM4 transport helper does not exist: {fullPath}");
            return fullPath;
        }

        var outputDirectory = new DirectoryInfo(Path.TrimEndingDirectorySeparator(appContextBaseDirectory));
        var targetFramework = outputDirectory.Name;
        var configuration = outputDirectory.Parent?.Name;
        var appHostProject = Path.Combine(repositoryRoot, "Testing", "Duck.Testing.AppHost");
        var runtimeBundleAppHostDirectory = Path.Combine(
            repositoryRoot, "artifacts", "issue81-runtime-bundle", "Duck.Testing.AppHost");
        var candidateRoots = new[]
        {
            appContextBaseDirectory,
            configuration is null ? string.Empty : Path.Combine(appHostProject, "obj", configuration, targetFramework),
            Path.Combine(appHostProject, "obj", "Debug", targetFramework),
            Path.Combine(appHostProject, "obj", "Release", targetFramework),
            configuration is null ? string.Empty : Path.Combine(appHostProject, "bin", configuration, targetFramework),
            runtimeBundleAppHostDirectory,
        };
        foreach (var root in candidateRoots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            var candidate = Path.Combine(root, helperName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new FileNotFoundException(
            $"The existing CM4 transport helper was not found in AppHost outputs. Set DUCK_ASPIRE_CM4_TRANSPORT_HELPER to the locally prepared helper asset. Searched: {string.Join(Path.PathSeparator, candidateRoots.Where(root => !string.IsNullOrWhiteSpace(root)).Select(root => Path.Combine(root, helperName)))}");
    }

    private static IReadOnlyList<StagedAppHostAsset> StageLocalAppHostAssets(string cm4HelperSource, string craneOverlaySource)
    {
        var staged = new List<StagedAppHostAsset>();
        try
        {
            Stage(cm4HelperSource, BaseSimulationResourceRegistration.Cm4TransportHelperAssetName);
            Stage(craneOverlaySource, "CraneLaunchOverlay.py");
            return staged;
        }
        catch
        {
            CleanupStagedAppHostAssets(staged);
            throw;
        }

        void Stage(string sourcePath, string assetName)
        {
            Assert.True(File.Exists(sourcePath), $"AppHost asset source was not found: {sourcePath}");
            var source = Path.GetFullPath(sourcePath);
            var destination = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, assetName));
            if (string.Equals(source, destination, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                return;
            }

            var sourceHash = GetSha256(source);
            if (File.Exists(destination))
            {
                if (GetSha256(destination) != sourceHash)
                {
                    throw new InvalidOperationException($"Refusing to replace a different AppHost asset at '{destination}'.");
                }

                return;
            }

            File.Copy(source, destination, overwrite: false);
            staged.Add(new StagedAppHostAsset(destination, sourceHash));
        }
    }

    private static void CleanupStagedAppHostAssets(IEnumerable<StagedAppHostAsset> stagedAssets)
    {
        foreach (var asset in stagedAssets.Reverse())
        {
            if (File.Exists(asset.Path) && GetSha256(asset.Path) == asset.Sha256)
            {
                File.Delete(asset.Path);
            }
            else if (File.Exists(asset.Path))
            {
                Console.Error.WriteLine($"Refusing to remove a changed staged AppHost asset: {asset.Path}");
            }
        }
    }

    private static string GetSha256(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

    private sealed record StagedAppHostAsset(string Path, string Sha256);

    private static async Task<string[]> GetOwnedContainerIdsAsync(
        string stackId,
        string dockerExecutable,
        Func<TimeSpan>? commandTimeout = null,
        Func<TimeSpan>? killWaitTimeout = null)
    {
        var result = await RunProcessAsync(dockerExecutable, [
            "ps", "-aq", "--no-trunc",
            "--filter", $"label=duck.aspire.stack={stackId}",
        ], commandTimeout?.Invoke() ?? TimeSpan.FromSeconds(15), killWaitTimeout?.Invoke());
        Assert.Equal(0, result.ExitCode);
        var containerIds = result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string? expectedDcpRunId = null;
        foreach (var containerId in containerIds)
        {
            var ownership = await AssertStackOwnedContainerAsync(
                containerId, stackId, dockerExecutable, commandTimeout, killWaitTimeout);
            if (!ownership.IsWrapperOwned)
            {
                expectedDcpRunId ??= ownership.RunId;
                Assert.Equal(expectedDcpRunId, ownership.RunId);
            }
        }

        return containerIds;
    }

    private static async Task RemoveOnlySecondStackContainersAsync(
        string stackId,
        string dockerExecutable,
        Func<TimeSpan>? commandTimeout = null,
        Func<TimeSpan>? killWaitTimeout = null)
    {
        var containerIds = await GetOwnedContainerIdsAsync(stackId, dockerExecutable, commandTimeout, killWaitTimeout);
        foreach (var containerId in containerIds)
        {
            await AssertStackOwnedContainerAsync(
                containerId, stackId, dockerExecutable, commandTimeout, killWaitTimeout);
            var removal = await RunProcessAsync(
                dockerExecutable, ["rm", "--force", containerId],
                commandTimeout?.Invoke() ?? TimeSpan.FromSeconds(15), killWaitTimeout?.Invoke());
            Assert.Equal(0, removal.ExitCode);
        }
    }

    private static async Task<(bool IsWrapperOwned, string RunId)> AssertStackOwnedContainerAsync(
        string containerId,
        string stackId,
        string dockerExecutable,
        Func<TimeSpan>? commandTimeout = null,
        Func<TimeSpan>? killWaitTimeout = null)
    {
        var inspection = await RunProcessAsync(dockerExecutable, [
            "inspect", "--format", "{{json .Config.Labels}}", containerId,
        ], commandTimeout?.Invoke() ?? TimeSpan.FromSeconds(15), killWaitTimeout?.Invoke());
        Assert.Equal(0, inspection.ExitCode);

        using var labelsDocument = System.Text.Json.JsonDocument.Parse(inspection.StandardOutput);
        var labels = labelsDocument.RootElement;
        Assert.Equal(stackId, labels.GetProperty(DockerContainerSpec.StackLabel).GetString());
        var resourceName = labels.GetProperty(DockerContainerSpec.ResourceLabel).GetString();
        Assert.False(string.IsNullOrWhiteSpace(resourceName));
        var runId = labels.GetProperty(DockerContainerSpec.RunLabel).GetString();
        Assert.False(string.IsNullOrWhiteSpace(runId));
        if (labels.TryGetProperty(DockerContainerSpec.OwnerLabel, out var owner))
        {
            Assert.Equal(DockerContainerSpec.OwnerValue, owner.GetString());
            return (true, runId!);
        }

        return (false, runId!);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        TimeSpan? killWaitTimeout = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), $"Could not start {fileName}.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeoutSource = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            using var cleanupTimeout = new CancellationTokenSource(killWaitTimeout ?? TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cleanupTimeout.Token);
            throw;
        }

        return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private static async Task<SSL_WrapperPacket> ReadVisionFrameWithYellowRobotsAsync(
        UdpMulticastReceiver receiver,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var packet = SSL_WrapperPacket.Parser.ParseFrom((await receiver.ReceiveAsync(cancellationToken)).ToArray());
            if (packet.Detection is not null && packet.Detection.RobotsYellow.Count > 0)
            {
                return packet;
            }
        }
    }

    private static async Task<TrackerWrapperPacket> ReadDuckTrackerPacketAsync(
        UdpMulticastReceiver receiver,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var packet = TrackerWrapperPacket.Parser.ParseFrom((await receiver.ReceiveAsync(cancellationToken)).ToArray());
            if (packet.Uuid == "ibis" && packet.SourceName == "ibis" && packet.TrackedFrame is not null)
            {
                return packet;
            }
        }
    }

    private static async Task<TrackerComparisonMeasurementEvidence> MeasureLiveTrackerDifferencePathAsync(
        UdpMulticastReceiver trackerReceiver,
        UdpMulticastReceiver visionReceiver,
        CancellationToken cancellationToken)
    {
        const int measurementWarmupCalls = 100;
        const int measurementIterations = 1_000;
        var requestedInputWindow = TimeSpan.FromSeconds(5);
        var discardedTrackerDatagrams = trackerReceiver.DrainPendingPackets();
        var discardedVisionDatagrams = visionReceiver.DrainPendingPackets();
        var trackerCounts = new Dictionary<string, TrackerTrafficCounter>(StringComparer.Ordinal)
        {
            ["Duck"] = new(),
            ["TIGERs"] = new(),
            ["ER-Force"] = new(),
        };
        var manager = new MultiTrackerManager<TrackerPacketAdapter>();
        using var trackerSnapshotStore = new ExternalTrackerSnapshotStore(manager);
        var visionPacketStore = new VisionPacketStore();
        using var inputWindow = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        inputWindow.CancelAfter(requestedInputWindow);
        var inputTimer = Stopwatch.StartNew();
        var visionCapture = CaptureVisionPacketsForWindowAsync(
            visionReceiver,
            visionPacketStore,
            inputWindow.Token);

        try
        {
            while (true)
            {
                var datagram = await trackerReceiver.ReceiveDatagramAsync(inputWindow.Token);
                var packet = TrackerWrapperPacket.Parser.ParseFrom(datagram.Buffer);
                var role = ResolveComparisonRole(packet.SourceName);
                if (role is null)
                {
                    continue;
                }

                trackerCounts[role].Record(packet.TrackedFrame);
                manager.ProcessPacket(
                    new TrackerPacketAdapter(packet),
                    datagram.RemoteEndPoint,
                    DateTimeOffset.UtcNow);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The fixed live-input sampling window completed.
        }
        finally
        {
            inputWindow.Cancel();
            inputTimer.Stop();
        }

        await visionCapture;
        cancellationToken.ThrowIfCancellationRequested();
        Assert.True(inputTimer.Elapsed > TimeSpan.Zero);
        Assert.All(trackerCounts, entry => Assert.True(
            entry.Value.TrackedFrameCount > 0,
            $"No live {entry.Key} tracker frames were captured during the {inputTimer.Elapsed.TotalMilliseconds:F0} ms input window."));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:Mode"] = "comparison",
                ["TrackerComparison:BallDistanceGateMeters"] = "0.5",
            })
            .Build();

        IResult EvaluateStoreToHealthResult() => DebugHostHealthEndpoints.GetComparisonHealthResult(
            trackerSnapshotStore,
            visionPacketStore,
            configuration);

        IResult? lastResult = null;
        for (var index = 0; index < measurementWarmupCalls; index++)
        {
            lastResult = EvaluateStoreToHealthResult();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var elapsedTicksByCall = new long[measurementIterations];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var evaluationTimer = Stopwatch.StartNew();
        for (var index = 0; index < measurementIterations; index++)
        {
            var callStart = Stopwatch.GetTimestamp();
            lastResult = EvaluateStoreToHealthResult();
            elapsedTicksByCall[index] = Stopwatch.GetTimestamp() - callStart;
        }

        evaluationTimer.Stop();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Array.Sort(elapsedTicksByCall);
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(lastResult);
        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        var response = Assert.IsAssignableFrom<IValueHttpResult>(lastResult).Value;
        Assert.NotNull(response);
        var pairs = Assert.IsAssignableFrom<IEnumerable<TrackerDifferencePairResult>>(
            response.GetType().GetProperty("Pairs")?.GetValue(response));
        Assert.Equal(3, pairs.Count());
        var sources = Assert.IsAssignableFrom<IEnumerable<TrackerComparisonSourceStatus>>(
            response.GetType().GetProperty("Sources")?.GetValue(response));
        var resolvedRoles = sources.Select(source => source.LogicalRole).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("Duck", resolvedRoles);
        Assert.Contains("TIGERs", resolvedRoles);
        Assert.Contains("ER-Force", resolvedRoles);

        var windowSeconds = inputTimer.Elapsed.TotalSeconds;
        var inputs = trackerCounts.Select(entry => new TrackerSourceTrafficEvidence(
            entry.Key,
            entry.Value.PacketCount,
            entry.Value.TrackedFrameCount,
            entry.Value.PacketCount / windowSeconds,
            entry.Value.TrackedFrameCount / windowSeconds,
            entry.Value.TrackedFrameCount == 0 ? 0 : entry.Value.RobotCountTotal / (double)entry.Value.TrackedFrameCount,
            entry.Value.TrackedFrameCount == 0 ? 0 : entry.Value.BallCountTotal / (double)entry.Value.TrackedFrameCount,
            entry.Value.FirstFrameNumber,
            entry.Value.LastFrameNumber)).ToArray();

        return new TrackerComparisonMeasurementEvidence(
            "live multicast packets fed into the comparison endpoint method; includes ExternalTrackerSnapshotStore.GetSnapshot protobuf clones, resolver, evaluator, VisionPacketStore.GetSnapshot clones, and IResult construction; excludes HTTP serialization and network request/response",
            DateTimeOffset.UtcNow,
            requestedInputWindow.TotalMilliseconds,
            inputTimer.Elapsed.TotalMilliseconds,
            discardedTrackerDatagrams,
            discardedVisionDatagrams,
            inputs,
            visionPacketStore.GetSnapshot().PacketCount,
            measurementWarmupCalls,
            measurementIterations,
            evaluationTimer.Elapsed.TotalMilliseconds,
            ToMicroseconds(elapsedTicksByCall[PercentileIndex(elapsedTicksByCall, 0.50)]),
            ToMicroseconds(elapsedTicksByCall[PercentileIndex(elapsedTicksByCall, 0.95)]),
            allocatedBytes / (double)measurementIterations,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount);
    }

    private static async Task CaptureVisionPacketsForWindowAsync(
        UdpMulticastReceiver receiver,
        VisionPacketStore store,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var datagram = await receiver.ReceiveDatagramAsync(cancellationToken);
                var packet = SSL_WrapperPacket.Parser.ParseFrom(datagram.Buffer);
                store.StorePacket(packet, datagram.RemoteEndPoint, DateTimeOffset.UtcNow);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The fixed live-input sampling window completed.
        }
    }

    private static string? ResolveComparisonRole(string sourceName) => sourceName switch
    {
        "ibis" => "Duck",
        "TIGERs" => "TIGERs",
        "ER-FORCE" => "ER-Force",
        _ => null,
    };

    private static int PercentileIndex(long[] sortedTicks, double percentile) =>
        Math.Clamp((int)Math.Ceiling(percentile * sortedTicks.Length) - 1, 0, sortedTicks.Length - 1);

    private static double ToMicroseconds(long stopwatchTicks) =>
        stopwatchTicks * (1_000_000d / Stopwatch.Frequency);

    private static MulticastInterface FindMulticastInterface()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(networkInterface =>
            {
                var properties = networkInterface.GetIPProperties();
                var hasDefaultGateway = properties.GatewayAddresses.Any(gateway =>
                    gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !gateway.Address.Equals(IPAddress.Any));
                return properties.UnicastAddresses
                    .Where(unicast => unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(unicast => new MulticastInterface(
                        networkInterface.Name,
                        unicast.Address,
                        hasDefaultGateway));
            })
            .OrderByDescending(candidate => candidate.HasDefaultGateway)
            .FirstOrDefault();

        return interfaces ?? throw new InvalidOperationException(
            "MULTICAST_INTERFACE_FAILURE: no operational non-loopback IPv4 interface was found.");
    }

    private static string? ReadLinuxMulticastMemberships()
    {
        const string membershipPath = "/proc/net/igmp";
        return File.Exists(membershipPath) ? File.ReadAllText(membershipPath) : null;
    }

    private sealed class RecordingRefereeCommandSource(
        IRefereeCommandSource inner,
        ConcurrentQueue<RefereeCommandSnapshot> snapshots,
        ITestOutputHelper output) : IRefereeCommandSource, IDisposable
    {
        public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
            uint? previousCommandCounter,
            CancellationToken cancellationToken)
        {
            var snapshot = await inner.WaitForChangeAsync(previousCommandCounter, cancellationToken);
            snapshots.Enqueue(snapshot);
            output.WriteLine($"referee command={snapshot.Command} counter={snapshot.CommandCounter}");
            return snapshot;
        }

        public void Dispose()
        {
            (inner as IDisposable)?.Dispose();
        }
    }

    private sealed class InitialSnapshotRefereeCommandSource(
        IRefereeCommandSource inner,
        RefereeCommandSnapshot initial) : IRefereeCommandSource
    {
        private bool initialAvailable = true;

        public ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
            uint? previousCommandCounter,
            CancellationToken cancellationToken)
        {
            if (initialAvailable && previousCommandCounter is null)
            {
                initialAvailable = false;
                return ValueTask.FromResult(initial);
            }

            return inner.WaitForChangeAsync(previousCommandCounter, cancellationToken);
        }
    }

    private sealed class UdpMulticastReceiver : IRefereePacketReceiver, IDisposable
    {
        private readonly UdpClient client;
        private int packetCount;

        public int PacketCount => Volatile.Read(ref packetCount);

        public UdpMulticastReceiver(string address, int port, IPAddress interfaceAddress)
        {
            var group = IPAddress.Parse(address);
            client = new UdpClient(AddressFamily.InterNetwork)
            {
                ExclusiveAddressUse = false,
            };
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            client.JoinMulticastGroup(group, interfaceAddress);
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
        {
            return (await ReceiveDatagramAsync(cancellationToken)).Buffer;
        }

        public async ValueTask<UdpReceiveResult> ReceiveDatagramAsync(CancellationToken cancellationToken)
        {
            var datagram = await client.ReceiveAsync(cancellationToken);
            Interlocked.Increment(ref packetCount);
            return datagram;
        }

        public int DrainPendingPackets()
        {
            var discarded = 0;
            while (client.Available > 0)
            {
                var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                _ = client.Receive(ref remoteEndpoint);
                Interlocked.Increment(ref packetCount);
                discarded++;
            }

            return discarded;
        }

        public void Dispose() => client.Dispose();
    }

    private sealed class TrackerTrafficCounter
    {
        public int PacketCount { get; private set; }
        public int TrackedFrameCount { get; private set; }
        public long RobotCountTotal { get; private set; }
        public long BallCountTotal { get; private set; }
        public uint? FirstFrameNumber { get; private set; }
        public uint? LastFrameNumber { get; private set; }

        public void Record(TrackedFrame? frame)
        {
            PacketCount++;
            if (frame is null)
            {
                return;
            }

            TrackedFrameCount++;
            RobotCountTotal += frame.Robots.Count;
            BallCountTotal += frame.Balls.Count;
            FirstFrameNumber ??= frame.FrameNumber;
            LastFrameNumber = frame.FrameNumber;
        }
    }

    private sealed record TrackerSourceTrafficEvidence(
        string Role,
        int ReceivedPackets,
        int TrackedFrames,
        double PacketsPerSecond,
        double TrackedFramesPerSecond,
        double MeanRobotsPerFrame,
        double MeanBallsPerFrame,
        uint? FirstFrameNumber,
        uint? LastFrameNumber);

    private sealed record TrackerComparisonMeasurementEvidence(
        string MeasurementBoundary,
        DateTimeOffset MeasuredAtUtc,
        double RequestedInputWindowMilliseconds,
        double ActualInputWindowMilliseconds,
        int TrackerDatagramsDiscardedBeforeWindow,
        int VisionDatagramsDiscardedBeforeWindow,
        IReadOnlyList<TrackerSourceTrafficEvidence> TrackerInputs,
        long VisionPacketsReceived,
        int EvaluationWarmupCalls,
        int EvaluationCalls,
        double EvaluationIntervalMilliseconds,
        double EvaluationP50Microseconds,
        double EvaluationP95Microseconds,
        double AllocatedBytesPerEndpointCall,
        string Runtime,
        string OperatingSystem,
        string Architecture,
        int ProcessorCount);

    private sealed record MulticastInterface(string Name, IPAddress Address, bool HasDefaultGateway);
}
