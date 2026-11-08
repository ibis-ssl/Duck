using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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
    public void HttpRefereeAcceptanceSourceExposesLifecycleObservations()
    {
        using var client = new HttpClient();
        var source = new HttpRefereeCommandSource(client);

        Assert.IsAssignableFrom<IRefereeLifecycleObservationSource>(source);
    }

    [Fact]
    public async Task HttpRefereeLifecycleObservationPreservesMatchedUdpAndApiContext()
    {
        const string body = """
            {"isReady":true,"reason":"Matched","refereeCommand":{"command":4,"commandCounter":7,"yellowTeamName":"crane","stage":0,"packetTimestamp":100,"blueScore":null,"yellowScore":1,"nextCommand":5,"designatedPositionX":null,"designatedPositionY":null},"diagnostic":{},"api":{"state":{"command":2,"phase":2,"stage":"NORMAL_FIRST_HALF_PRE","stageTimeLeft":"00:00:25","nextCommand":"KICKOFF","nextCommandForTeam":"BLUE","blueScore":null,"yellowScore":1,"placementX":null,"placementY":null,"commandForTeam":"UNKNOWN","phaseForTeam":"UNKNOWN"},"requestSequence":12,"ageUpperBound":"00:00:00.0100000"}}
            """;
        using var client = new HttpClient(new StaticResponseHandler(body)) { BaseAddress = new Uri("http://localhost/") };
        var source = new HttpRefereeCommandSource(client);

        var observation = await source.WaitForNextAsync(11, CancellationToken.None);

        Assert.Equal(12, observation.RequestSequence);
        Assert.Equal((uint)0, observation.Udp.BlueScore);
        Assert.Equal((uint)1, observation.Udp.YellowScore);
        Assert.Equal(Referee.Types.Command.PrepareKickoffBlue, observation.Udp.NextCommand);
        Assert.Equal("KICKOFF", observation.ApiSnapshot!.NextCommand);
        Assert.Equal("BLUE", observation.ApiSnapshot.NextCommandForTeam);
        Assert.Null(observation.ApiSnapshot.BlueScore);
        Assert.Null(observation.ApiSnapshot.PlacementX);
        Assert.Null(observation.ApiSnapshot.PlacementY);
        Assert.Equal((uint?)1, observation.ApiSnapshot.YellowScore);
        Assert.Equal(TimeSpan.FromSeconds(25), observation.ApiStageTimeLeft);
    }

    [Fact]
    public async Task HttpLifecycleWaitsForFreshMatchedKickoffAndReadyAutoBeforeNormalStart()
    {
        static string Health(
            Referee.Types.Command refereeCommand,
            uint counter,
            long sequence,
            GameControllerCommandKind apiCommand,
            GameControllerMatchPhase apiPhase,
            string apiStage,
            string? nextCommand,
            string? nextCommandForTeam,
            string commandForTeam,
            string phaseForTeam,
            bool ready = true) =>
            System.Text.Json.JsonSerializer.Serialize(new
            {
                isReady = ready,
                reason = ready ? "Matched" : "NoFreshUdp",
                refereeCommand = new
                {
                    command = refereeCommand, commandCounter = counter, yellowTeamName = "crane",
                    stage = apiStage == "NORMAL_FIRST_HALF"
                        ? Referee.Types.Stage.NormalFirstHalf : Referee.Types.Stage.NormalFirstHalfPre,
                    packetTimestamp = (ulong)(100 + counter), blueScore = 2, yellowScore = 1,
                    nextCommand = refereeCommand == Referee.Types.Command.PrepareKickoffYellow
                        ? Referee.Types.Command.PrepareKickoffYellow : (Referee.Types.Command?)null,
                },
                diagnostic = new { },
                api = new
                {
                    requestSequence = sequence, ageUpperBound = "00:00:00.0100000",
                    state = new
                    {
                        command = apiCommand,
                        phase = apiPhase,
                        stage = apiStage, stageTimeLeft = "00:00:25",
                        nextCommand,
                        nextCommandForTeam,
                        blueScore = 2, yellowScore = 1,
                        commandForTeam,
                        phaseForTeam,
                    },
                },
            });

        static string Preparation(uint counter, long sequence, string nextTeam = "YELLOW", bool ready = true) =>
            Health(Referee.Types.Command.PrepareKickoffYellow, counter, sequence,
                GameControllerCommandKind.Kickoff, GameControllerMatchPhase.Kickoff, "NORMAL_FIRST_HALF_PRE",
                "KICKOFF", nextTeam, "YELLOW", "YELLOW", ready);

        using var handler = new QueueResponseHandler(
            (HttpStatusCode.OK, Health(Referee.Types.Command.Stop, 10, 0,
                GameControllerCommandKind.Stop, GameControllerMatchPhase.Stop, "NORMAL_FIRST_HALF_PRE",
                null, null, "UNKNOWN", "UNKNOWN")),
            (HttpStatusCode.OK, Preparation(11, 1)),
            (HttpStatusCode.ServiceUnavailable, Preparation(11, 1, ready: false)),
            (HttpStatusCode.OK, Preparation(11, 1)),
            (HttpStatusCode.OK, Preparation(11, 2, nextTeam: "BLUE")),
            (HttpStatusCode.OK, Preparation(11, 3)),
            (HttpStatusCode.ServiceUnavailable, Preparation(11, 3, ready: false)),
            (HttpStatusCode.OK, Preparation(11, 3)),
            (HttpStatusCode.OK, Preparation(11, 4)),
            (HttpStatusCode.OK, Health(Referee.Types.Command.NormalStart, 12, 5,
                GameControllerCommandKind.NormalStart, GameControllerMatchPhase.Running, "NORMAL_FIRST_HALF",
                null, null, "UNKNOWN", "UNKNOWN")));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var source = new HttpRefereeCommandSource(client, TimeSpan.FromMilliseconds(1));
        var control = new ReadinessSequenceControlClient(() => handler.RequestCount);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var initial = (await source.ReadCurrentAsync(timeout.Token)).Snapshot;
        Assert.NotNull(initial);
        var active = await CreateRefereeDriverFixture(source, initial, control).DriveToActiveAsync(timeout.Token);

        Assert.Equal(Referee.Types.Command.NormalStart, active.Command);
        Assert.Equal(new[] { GameControllerContinueAction.NextCommand, GameControllerContinueAction.NormalStart }, control.SentActions);
        Assert.Equal(3, control.ReadinessReads);
        Assert.Equal(new[] { 4, 6, 9 }, control.ReadinessRequestIndices);
        Assert.Equal(9, control.NormalStartSentAtRequestIndex);
        Assert.True(control.NormalStartSentAfterReady);
    }

    private static RefereeDriverFixture CreateRefereeDriverFixture(
        HttpRefereeCommandSource refereeSource,
        RefereeCommandSnapshot initial,
        IGameControllerControlClient controlClient) =>
        new(new InitialSnapshotRefereeCommandSource(refereeSource, initial), controlClient, refereeSource);

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

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Net003CommandTimeoutRejectsNonPositiveTimeBeforeDockerStart(double availableSeconds)
    {
        Assert.Throws<TimeoutException>(() => GetPositiveCommandTimeout(
            TimeSpan.FromSeconds(availableSeconds),
            TimeSpan.FromSeconds(15),
            "NET-003 work budget expired before a Docker command could start."));
    }

    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(20, 15)]
    public void Net003CommandTimeoutPreservesPositiveBoundedTime(double availableSeconds, double expectedSeconds)
    {
        Assert.Equal(
            TimeSpan.FromSeconds(expectedSeconds),
            GetPositiveCommandTimeout(
                TimeSpan.FromSeconds(availableSeconds),
                TimeSpan.FromSeconds(15),
                "NET-003 work budget expired before a Docker command could start."));
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

        var dockerExecutable = ResolveDockerExecutable(
            Environment.GetEnvironmentVariable("Testing__IdentityControl__DockerExecutable"),
            Environment.GetEnvironmentVariable("PATH"));
        using var dockerPathScope = AddDockerDirectoryToProcessPath(dockerExecutable);
        var manifestPath = Environment.GetEnvironmentVariable("DUCK_ASPIRE_FIXTURE_MANIFEST")
            ?? CraneFixtureManifest.GetDefaultPath(stackId);
        var craneFixtureSettings = CraneFixtureManifest.ReadForStack(manifestPath, stackId);
        var artifactsDirectory = Environment.GetEnvironmentVariable("DUCK_ASPIRE_ARTIFACTS")
            ?? Path.Combine(Path.GetTempPath(), "duck-aspire-full-stack-artifacts");
        Directory.CreateDirectory(artifactsDirectory);

        var refereeSnapshots = new ConcurrentQueue<RefereeCommandSnapshot>();
        var resultWriter = new AspireAcceptanceResultWriter(refereeSnapshots);
        var stageReached = "test_setup";
        RobotMotionDelta? observedMovement = null;
        object? comparisonMeasurement = null;
        Dictionary<string, Uri>? mappedEndpoints = null;

        try
        {
            using var timeout = new CancellationTokenSource(StackSignalTimeout);
            mappedEndpoints = await ResolveComparisonEndpointsAsync(stackId, dockerExecutable);
            using var refereeClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            using var comparisonClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            refereeClient.BaseAddress = mappedEndpoints["referee-driver"];
            comparisonClient.BaseAddress = mappedEndpoints["debug-host"];
            var httpRefereeSource = new HttpRefereeCommandSource(refereeClient);
            using var refereeSource = new RecordingRefereeCommandSource(
                httpRefereeSource,
                refereeSnapshots,
                message => output.WriteLine(message));
            var gameControllerControl = new GameControllerWebSocketControlClient(
                new UriBuilder(mappedEndpoints["game-controller"]) { Scheme = "ws", Path = "/api/control" }.Uri);
            output.WriteLine(
                $"same-run mapped endpoints: game-controller={mappedEndpoints["game-controller"]}; " +
                $"referee-driver={mappedEndpoints["referee-driver"]}; debug-host={mappedEndpoints["debug-host"]}");

            RefereeCommandSnapshot initial;
            stageReached = "initial_referee_wait";
            try
            {
                await gameControllerControl.SendYellowTeamNameAsync(
                    craneFixtureSettings.TeamName, timeout.Token);
                output.WriteLine($"requested Game Controller Yellow team name={craneFixtureSettings.TeamName}");
                initial = await WaitForInitialRefereeWithYellowIdentityAsync(
                    httpRefereeSource, craneFixtureSettings.TeamName, timeout.Token);
                refereeSnapshots.Enqueue(initial);
                output.WriteLine($"referee command={initial.Command} counter={initial.CommandCounter}");
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    $"TEAM_IDENTITY_FAILURE: RefereeDriver did not expose a ready HALT snapshot with Yellow team name '{craneFixtureSettings.TeamName}' after the Game Controller update before the deadline.",
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

            LatestVisionHealthSnapshot initialVision;
            stageReached = "initial_vision_wait";
            try
            {
                initialVision = await WaitForLatestYellowVisionAsync(comparisonClient, timeout.Token);
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    "VISION_INGRESS_FAILURE: DebugHost did not expose a latest SSL-Vision detection frame with Yellow robots before the deadline.",
                    exception);
            }
            stageReached = "active_referee_transition";
            var motionVerifier = new RobotMotionAcceptanceVerifier(minimumDistanceMm: 100);
            motionVerifier.CaptureBaseline(initialVision.Detection);
            output.WriteLine(
                $"baseline referee={initial.Command} counter={initial.CommandCounter}; " +
                $"visionFrame={initialVision.FrameNumber}; " +
                $"yellowRobots={initialVision.Detection.RobotsYellow.Count}");

            RefereeCommandSnapshot active;
            try
            {
                active = await CreateRefereeDriverFixture(
                    httpRefereeSource, initial, gameControllerControl).DriveToActiveAsync(timeout.Token);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"REFEREE_FIXTURE_FAILURE: Game Controller did not complete HALT/STOP/PREPARE/NORMAL_START: {exception.Message}",
                    exception);
            }

            output.WriteLine($"active referee={active.Command} counter={active.CommandCounter}");
            if (!httpRefereeSource.TryGetObservationTime(active.CommandCounter, out var activeObservedAt))
            {
                throw new InvalidOperationException(
                    $"REFEREE_FIXTURE_FAILURE: no client observation timestamp was recorded for active command counter {active.CommandCounter}.");
            }
            output.WriteLine($"active referee client-observed-at={activeObservedAt:O}");

            LatestVisionHealthSnapshot activeVision;
            try
            {
                activeVision = await WaitForYellowVisionAfterActiveObservationAsync(
                    comparisonClient, activeObservedAt, timeout.Token);
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    "VISION_INGRESS_FAILURE: no fresh Yellow detection was available after the active referee transition.",
                    exception);
            }

            motionVerifier.CaptureBaseline(activeVision.Detection);
            output.WriteLine(
                $"post-active baseline referee={active.Command} counter={active.CommandCounter}; " +
                $"activeObservedAt={activeObservedAt:O}; " +
                $"visionFrame={activeVision.FrameNumber}; " +
                $"yellowRobots={activeVision.Detection.RobotsYellow.Count}");

            stageReached = "active_robot_motion_wait";
            var lastVisionReceivedAt = activeVision.DetectionReceivedAt;

            try
            {
                var accepted = await RefereeDriverFixture.WaitForMotionWhileActiveAsync(
                    refereeSource,
                    active!,
                    MotionTimeout,
                    async motionToken =>
                    {
                        while (true)
                        {
                            var vision = await WaitForNewYellowVisionAsync(
                                comparisonClient, lastVisionReceivedAt, motionToken);
                            lastVisionReceivedAt = vision.DetectionReceivedAt;
                            var movement = FindMovementFromFreshVision(
                                motionVerifier, vision, activeObservedAt);
                            if (movement is not null)
                            {
                                var comparison = await WaitForLiveComparisonAsync(comparisonClient, motionToken);
                                var finalReferee = await httpRefereeSource.ReadCurrentAsync(motionToken);
                                ValidateFinalActiveSnapshot(finalReferee, active!);
                                return (Movement: movement, Comparison: comparison, FinalReferee: finalReferee.Snapshot!);
                            }
                        }
                    },
                    timeout.Token);
                observedMovement = accepted.Movement;
                comparisonMeasurement = new
                {
                    transport = "same-run DCP mapped HTTP endpoints",
                    activeRefereeObservedAt = activeObservedAt,
                    visionFrame = accepted.Comparison.LatestVisionFrameNumber,
                    detectionReceivedAt = accepted.Comparison.DetectionReceivedAt,
                    visionPacketsReceived = accepted.Comparison.VisionPacketsReceived,
                    sourceRoles = accepted.Comparison.SourceRoles,
                    pairs = accepted.Comparison.Pairs,
                    finalReferee = new
                    {
                        command = accepted.FinalReferee.Command.ToString(),
                        accepted.FinalReferee.CommandCounter,
                    },
                };
            }
            catch (RefereeStateLostException exception)
            {
                var lostSnapshot = exception.StateLoss.Snapshot;
                resultWriter.RecordRefereeHealthAtFailure(
                    httpRefereeSource.TryGetReadEvidence(lostSnapshot, out var matchedEvidence)
                        ? matchedEvidence
                        : null);
                throw AspireAcceptanceResultWriter.CreateMotionFailure(exception);
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    "DCP_MOTION_FAILURE: no Yellow robot moved at least 100 mm while the referee stayed active and all comparison roles/pairs became live within 60 seconds.",
                    exception);
            }

            output.WriteLine(
                $"DCP motion verified: team={observedMovement.Team}; " +
                $"robotId={observedMovement.RobotId}; distanceMm={observedMovement.DistanceMm:F1}");
            output.WriteLine(
                $"live comparison verified: {System.Text.Json.JsonSerializer.Serialize(comparisonMeasurement)}");
            stageReached = "same_run_acceptance_complete";
        }
        catch (Exception exception)
        {
            resultWriter.RecordFailure(exception);
            throw;
        }
        finally
        {
            var path = Path.Combine(artifactsDirectory, "acceptance-result.json");
            await resultWriter.WriteAsync(
                path,
                stageReached,
                refereeRawDatagramCount: 0,
                visionPacketsConsumed: null,
                trackerPacketsConsumed: null,
                comparisonMeasurement,
                receiverSockets: new
                {
                    transport = "DCP mapped HTTP and WebSocket; no host UDP receiver",
                    endpoints = mappedEndpoints?.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value.ToString(),
                        StringComparer.Ordinal),
                },
                movement: observedMovement);
            output.WriteLine($"Acceptance evidence: {path}");
        }
    }

    [Fact]
    public async Task HttpRefereeCommandSourceFailsClosedWhenCommandCounterSkips()
    {
        using var http = new HttpClient(new QueueHttpMessageHandler(
            """{"isReady":true,"refereeCommand":{"command":3,"commandCounter":41,"yellowTeamName":"crane"}}""",
            """{"isReady":true,"refereeCommand":{"command":4,"commandCounter":43,"yellowTeamName":"crane"},"credential":"must-not-be-retained"}"""))
        {
            BaseAddress = new Uri("http://localhost/"),
        };
        var source = new HttpRefereeCommandSource(http, TimeSpan.Zero);

        var initial = await source.WaitForChangeAsync(null, CancellationToken.None);
        Assert.Equal(41u, initial.CommandCounter);
        Assert.Equal("crane", initial.YellowTeamName);
        var failure = await Assert.ThrowsAsync<RefereeCommandCounterGapException>(async () =>
            await source.WaitForChangeAsync(initial.CommandCounter, CancellationToken.None));
        var currentHealth = Assert.IsType<RefereeHealthReadEvidence>(failure.CurrentHealth);
        Assert.Equal(200, currentHealth.HttpStatusCode);
        Assert.True(currentHealth.IsReady);
        Assert.Equal(nameof(Referee.Types.Command.PrepareKickoffYellow), currentHealth.Command);
        Assert.Equal(43u, currentHealth.CommandCounter);
        Assert.Equal("crane", currentHealth.YellowTeamName);
        Assert.NotNull(currentHealth.ReadStartedAtUtc);
        Assert.NotNull(currentHealth.ReadCompletedAtUtc);
        Assert.True(currentHealth.ReadElapsedMilliseconds >= 0);
        Assert.DoesNotContain("must-not-be-retained", failure.ToString(), StringComparison.Ordinal);

        var snapshots = new ConcurrentQueue<RefereeCommandSnapshot>();
        snapshots.Enqueue(initial);
        var writer = new AspireAcceptanceResultWriter(snapshots);
        writer.RecordFailure(failure);
        using var artifacts = new TemporaryDirectory();
        var resultPath = Path.Combine(artifacts.Path, "acceptance-result.json");
        await writer.WriteAsync(
            resultPath, "active_robot_motion_wait", 0, null, null, null, null, null);
        using var result = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        var retainedHealth = result.RootElement.GetProperty("refereeHealthAtCounterGap");
        Assert.Equal(43u, retainedHealth.GetProperty("commandCounter").GetUInt32());
        Assert.Equal(nameof(Referee.Types.Command.PrepareKickoffYellow), retainedHealth.GetProperty("command").GetString());
        Assert.True(retainedHealth.GetProperty("readElapsedMilliseconds").GetDouble() >= 0);
    }

    [Fact]
    public async Task ActiveRefereeStateLossRetainsMatchedHealthSourceAndApiState()
    {
        using var http = new HttpClient(new QueueHttpMessageHandler(
            """{"isReady":true,"reason":"Matched","refereeCommand":{"command":3,"commandCounter":41,"yellowTeamName":"crane"},"diagnostic":{"sourceKey":"172.18.0.2:11003","remoteEndpoint":"172.18.0.2:11003"},"api":{"state":{"command":"FORCE_START","phase":"RUNNING"}}}""",
            """{"isReady":true,"reason":"Matched","refereeCommand":{"command":0,"commandCounter":42,"yellowTeamName":"crane"},"diagnostic":{"sourceKey":"172.18.0.2:11003","remoteEndpoint":"172.18.0.2:11003"},"api":{"state":{"command":"HALT","phase":"HALT"},"credential":"must-not-be-retained"}}""",
            """{"isReady":true,"reason":"Matched","refereeCommand":{"command":3,"commandCounter":43,"yellowTeamName":"crane"},"diagnostic":{"sourceKey":"172.18.0.2:11003","remoteEndpoint":"172.18.0.2:11003"},"api":{"state":{"command":"FORCE_START","phase":"RUNNING"}}}"""))
        {
            BaseAddress = new Uri("http://localhost/"),
        };
        var source = new HttpRefereeCommandSource(http, TimeSpan.Zero);
        var active = await source.WaitForChangeAsync(null, CancellationToken.None);
        var halted = await source.WaitForChangeAsync(active.CommandCounter, CancellationToken.None);
        var lateRead = await source.ReadCurrentAsync(CancellationToken.None);

        Assert.Equal(Referee.Types.Command.ForceStart, active.Command);
        Assert.Equal(Referee.Types.Command.Halt, halted.Command);
        Assert.Equal(42u, halted.CommandCounter);
        Assert.Equal(43u, lateRead.Snapshot!.CommandCounter);
        var stateLoss = new RefereeStateLostException(halted);
        Assert.Same(halted, stateLoss.StateLoss.Snapshot);
        Assert.True(source.TryGetReadEvidence(stateLoss.StateLoss.Snapshot, out var matchedEvidence));
        var currentHealth = Assert.IsType<RefereeHealthReadEvidence>(matchedEvidence);
        Assert.Equal(200, currentHealth.HttpStatusCode);
        Assert.True(currentHealth.IsReady);
        Assert.Equal("Matched", currentHealth.Reason);
        Assert.Equal("172.18.0.2:11003", currentHealth.UdpSourceKey);
        Assert.Equal("172.18.0.2:11003", currentHealth.UdpRemoteEndpoint);
        Assert.Equal("HALT", currentHealth.ApiCommand);
        Assert.Equal("HALT", currentHealth.ApiPhase);
        Assert.Equal(42u, currentHealth.CommandCounter);
        Assert.Equal("FORCE_START", lateRead.ReadEvidence!.ApiCommand);
        Assert.Equal(43u, lateRead.ReadEvidence.CommandCounter);

        var snapshots = new ConcurrentQueue<RefereeCommandSnapshot>();
        snapshots.Enqueue(active);
        snapshots.Enqueue(halted);
        var writer = new AspireAcceptanceResultWriter(snapshots);
        writer.RecordFailure(stateLoss);
        writer.RecordRefereeHealthAtFailure(currentHealth);
        using var artifacts = new TemporaryDirectory();
        var resultPath = Path.Combine(artifacts.Path, "acceptance-result.json");
        await writer.WriteAsync(
            resultPath, "active_robot_motion_wait", 0, null, null, null, null, null);
        var json = await File.ReadAllTextAsync(resultPath);
        Assert.DoesNotContain("must-not-be-retained", json, StringComparison.Ordinal);
        using var result = System.Text.Json.JsonDocument.Parse(json);
        var retainedHealth = result.RootElement.GetProperty("refereeHealthAtFailure");
        Assert.Equal(42u, retainedHealth.GetProperty("commandCounter").GetUInt32());
        Assert.Equal("HALT", retainedHealth.GetProperty("apiCommand").GetString());
        Assert.Equal("Matched", retainedHealth.GetProperty("reason").GetString());
        Assert.Equal("unavailable", result.RootElement.GetProperty("refereeHealthAtCounterGap").GetString());
    }

    [Fact]
    public async Task InitialRefereeWaitRejectsUnknownYellowTeamUntilExpectedIdentityIsObserved()
    {
        using var http = new HttpClient(new QueueHttpMessageHandler(
            """{"isReady":true,"refereeCommand":{"command":0,"commandCounter":7,"yellowTeamName":"Unknown"}}""",
            """{"isReady":true,"refereeCommand":{"command":0,"commandCounter":7,"yellowTeamName":"crane"}}"""))
        {
            BaseAddress = new Uri("http://localhost/"),
        };
        var source = new HttpRefereeCommandSource(http, TimeSpan.Zero);

        var initial = await WaitForInitialRefereeWithYellowIdentityAsync(
            source, "crane", CancellationToken.None);

        Assert.Equal(Referee.Types.Command.Halt, initial.Command);
        Assert.Equal(7u, initial.CommandCounter);
        Assert.Equal("crane", initial.YellowTeamName);
    }

    [Fact]
    public async Task LiveComparisonWaitIncludesLastHealthResponseWhenPredicateTimesOut()
    {
        const string healthResponse = """
            {"isReady":true,"missingRoles":[],"conflictingRoles":[],"sources":[{"logicalRole":"Duck","sourceName":"ibis","sourceUuid":"duck","receivedAt":"2026-11-01T13:02:30Z"},{"logicalRole":"ER-Force","sourceName":"ER-FORCE","sourceUuid":"erforce","receivedAt":"2026-11-01T13:02:31Z"},{"logicalRole":"TIGERs","sourceName":"TIGERs","sourceUuid":"tigers","receivedAt":"2026-11-01T13:02:32Z"}],"pairs":[{"referenceRole":"Duck","comparisonRole":"ER-Force","status":"Compared"},{"referenceRole":"Duck","comparisonRole":"TIGERs","status":"MissingComparisonFrame"},{"referenceRole":"ER-Force","comparisonRole":"TIGERs","status":"Compared"}],"latestVision":{"packetCount":7,"detectionReceivedAt":"2026-11-01T13:02:33Z","frameNumber":4692,"yellowRobots":[{"robotId":2,"x":220,"y":35}]},"visionPacketsReceived":7}
            """;
        using var http = new HttpClient(new QueueHttpMessageHandler(healthResponse))
        {
            BaseAddress = new Uri("http://localhost/"),
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var failure = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            WaitForLiveComparisonAsync(http, cancellation.Token, TimeSpan.FromHours(1)));

        Assert.Contains("COMPARISON_HEALTH_WAIT_CANCELLED", failure.Message, StringComparison.Ordinal);
        Assert.Contains("lastResponseStatus=200", failure.Message, StringComparison.Ordinal);
        Assert.Contains("\"logicalRole\":\"TIGERs\"", failure.Message, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"MissingComparisonFrame\"", failure.Message, StringComparison.Ordinal);
        Assert.Contains("\"detectionReceivedAt\":\"2026-11-01T13:02:33Z\"", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsesLatestVisionHealthAsYellowDetectionFrame()
    {
        using var document = System.Text.Json.JsonDocument.Parse(
            """{"latestVision":{"packetCount":7,"detectionReceivedAt":"2026-11-01T00:00:00+00:00","frameNumber":9,"yellowRobots":[{"robotId":2,"x":123.5,"y":-44.0}]}}""");

        var snapshot = ParseLatestVisionHealth(document.RootElement);

        Assert.NotNull(snapshot);
        Assert.Equal(9u, snapshot.FrameNumber);
        Assert.Single(snapshot.Detection.RobotsYellow);
        Assert.Equal(2u, snapshot.Detection.RobotsYellow[0].RobotId);
        Assert.Equal(123.5f, snapshot.Detection.RobotsYellow[0].X);
        Assert.Equal(-44f, snapshot.Detection.RobotsYellow[0].Y);
    }

    [Fact]
    public void PreActiveCachedYellowFrameCannotSatisfyMotionAfterActiveBaseline()
    {
        var activeObservedAt = DateTimeOffset.Parse("2026-11-01T00:00:01Z");
        var intermediatePreActiveFrame = CreateYellowVision(11, activeObservedAt.AddMilliseconds(-1), 150);
        var postActiveBaseline = CreateYellowVision(12, activeObservedAt.AddMilliseconds(1), 150);
        var freshPostActiveMovement = CreateYellowVision(13, activeObservedAt.AddSeconds(1), 260);
        var verifier = new RobotMotionAcceptanceVerifier(minimumDistanceMm: 100);
        var selectedBaseline = SelectPostActiveYellowVision(
            [intermediatePreActiveFrame, postActiveBaseline], activeObservedAt);
        Assert.Same(postActiveBaseline, selectedBaseline);
        verifier.CaptureBaseline(selectedBaseline!.Detection);

        Assert.Null(FindMovementFromFreshVision(verifier, intermediatePreActiveFrame, activeObservedAt));
        Assert.Null(FindMovementFromFreshVision(verifier, postActiveBaseline, activeObservedAt));
        var freshMovement = FindMovementFromFreshVision(verifier, freshPostActiveMovement, activeObservedAt);
        Assert.NotNull(freshMovement);
        Assert.Equal(2u, freshMovement.RobotId);
        Assert.Equal(110f, freshMovement.DistanceMm);
    }

    [Fact]
    public void FinalRefereeCheckRejectsNonReadyOrChangedActiveSnapshot()
    {
        var active = new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 42, "crane");

        var nonReady = Assert.Throws<InvalidOperationException>(() => ValidateFinalActiveSnapshot(
            new CurrentRefereeHealthSnapshot(false, active, DateTimeOffset.UtcNow), active));
        Assert.Contains("REFEREE_FINAL_STATE_NOT_READY", nonReady.Message, StringComparison.Ordinal);

        var changedCommand = Assert.Throws<InvalidOperationException>(() => ValidateFinalActiveSnapshot(
            new CurrentRefereeHealthSnapshot(true,
                new RefereeCommandSnapshot(Referee.Types.Command.Stop, 43, "crane"), DateTimeOffset.UtcNow), active));
        Assert.Contains("REFEREE_FINAL_STATE_CHANGED", changedCommand.Message, StringComparison.Ordinal);

        var changedCounter = Assert.Throws<InvalidOperationException>(() => ValidateFinalActiveSnapshot(
            new CurrentRefereeHealthSnapshot(true,
                new RefereeCommandSnapshot(Referee.Types.Command.NormalStart, 44, "crane"), DateTimeOffset.UtcNow), active));
        Assert.Contains("REFEREE_COUNTER_GAP", changedCounter.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HttpRefereeSourceSerializesGuardAndFinalReadAndDetectsPendingChange()
    {
        static string Health(Referee.Types.Command command, uint counter) =>
            System.Text.Json.JsonSerializer.Serialize(new
            {
                isReady = true,
                refereeCommand = new
                {
                    command = (int)command,
                    commandCounter = counter,
                    yellowTeamName = "crane",
                },
            });

        var handler = new BlockingRefereeHttpMessageHandler(
            Health(Referee.Types.Command.NormalStart, 42),
            Health(Referee.Types.Command.Stop, 43));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var source = new HttpRefereeCommandSource(http, TimeSpan.Zero);
        var initial = await source.ReadCurrentAsync(CancellationToken.None);
        Assert.True(initial.IsReady);
        Assert.NotNull(initial.Snapshot);
        Assert.True(source.TryGetObservationTime(initial.Snapshot!.CommandCounter, out _));

        var guardTask = source.WaitForChangeAsync(initial.Snapshot.CommandCounter, CancellationToken.None).AsTask();
        await handler.WaitForSecondRequestAsync(CancellationToken.None);
        var finalReadTask = source.ReadCurrentAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(25));

        Assert.False(finalReadTask.IsCompleted);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(1, handler.MaximumConcurrentRequests);

        handler.ReleaseSecondRequest();
        var guardSnapshot = await guardTask.WaitAsync(TimeSpan.FromSeconds(2));
        var finalSnapshot = await finalReadTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(Referee.Types.Command.Stop, guardSnapshot.Command);
        Assert.Equal(43u, finalSnapshot.Snapshot?.CommandCounter);
        var changed = Assert.Throws<InvalidOperationException>(() => ValidateFinalActiveSnapshot(
            finalSnapshot, initial.Snapshot));
        Assert.Contains("REFEREE_FINAL_STATE_CHANGED", changed.Message, StringComparison.Ordinal);
        Assert.Equal(3, handler.RequestCount);
        Assert.Equal(1, handler.MaximumConcurrentRequests);
    }

    [Fact]
    public async Task HttpRefereeSourceCarriesPostGameStageIntoTheTerminationSnapshot()
    {
        using var http = new HttpClient(new QueueHttpMessageHandler(
            """{"isReady":true,"refereeCommand":{"command":1,"commandCounter":44,"yellowTeamName":"crane","stage":1,"packetTimestamp":103}}""",
            """{"isReady":true,"refereeCommand":{"command":1,"commandCounter":44,"yellowTeamName":"crane","stage":13,"packetTimestamp":104}}"""))
        {
            BaseAddress = new Uri("http://localhost/"),
        };
        var source = new HttpRefereeCommandSource(http, TimeSpan.Zero);

        var initial = await source.WaitForChangeAsync(null, CancellationToken.None);
        var postGame = await source.WaitForChangeAsync(initial.CommandCounter, CancellationToken.None);

        Assert.Equal(Referee.Types.Command.Stop, postGame.Command);
        Assert.Equal(Referee.Types.Stage.PostGame, postGame.Stage);
        Assert.Equal(44u, postGame.CommandCounter);
        Assert.Equal(104ul, postGame.PacketTimestamp);
    }

    private static LatestVisionHealthSnapshot CreateYellowVision(uint frameNumber, DateTimeOffset receivedAt, float x)
    {
        var frame = new SSL_DetectionFrame { FrameNumber = frameNumber };
        frame.RobotsYellow.Add(new SSL_DetectionRobot { RobotId = 2, X = x, Y = 0 });
        return new LatestVisionHealthSnapshot(1, receivedAt, frameNumber, frame);
    }

    private static async Task<RefereeCommandSnapshot> WaitForInitialRefereeWithYellowIdentityAsync(
        HttpRefereeCommandSource source,
        string expectedYellowTeamName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedYellowTeamName);

        while (true)
        {
            var current = await source.ReadCurrentAsync(cancellationToken);
            if (current.IsReady && current.Snapshot is { } snapshot)
            {
                if (snapshot.Command != Referee.Types.Command.Halt)
                {
                    throw new InvalidOperationException(
                        $"REFEREE_FIXTURE_FAILURE: expected initial HALT while confirming Yellow team identity, observed {snapshot.Command}.");
                }

                if (string.Equals(snapshot.YellowTeamName, expectedYellowTeamName, StringComparison.Ordinal))
                {
                    return snapshot;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private static RobotMotionDelta? FindMovementFromFreshVision(
        RobotMotionAcceptanceVerifier verifier,
        LatestVisionHealthSnapshot vision,
        DateTimeOffset strictlyNewerThan) =>
        IsAfterActiveObservation(vision, strictlyNewerThan)
            ? verifier.FindMovement(vision.Detection)
            : null;

    private static void ValidateFinalActiveSnapshot(
        CurrentRefereeHealthSnapshot current,
        RefereeCommandSnapshot active)
    {
        if (!current.IsReady || current.Snapshot is null || current.ObservedAtUtc is null)
        {
            throw new InvalidOperationException(
                "REFEREE_FINAL_STATE_NOT_READY: current referee health did not expose a ready matched snapshot after comparison became live.");
        }

        if (current.Snapshot.CommandCounter != active.CommandCounter)
        {
            var expected = unchecked(active.CommandCounter + 1);
            if (current.Snapshot.CommandCounter != expected)
            {
                throw new InvalidOperationException(
                    $"REFEREE_COUNTER_GAP: expected final command counter {active.CommandCounter} (or its next observed value {expected}), observed {current.Snapshot.CommandCounter}.");
            }

            throw new InvalidOperationException(
                $"REFEREE_FINAL_STATE_CHANGED: active command counter changed from {active.CommandCounter} to {current.Snapshot.CommandCounter}.");
        }

        if (current.Snapshot.Command != active.Command)
        {
            throw new InvalidOperationException(
                $"REFEREE_FINAL_STATE_CHANGED: active command changed from {active.Command} to {current.Snapshot.Command}.");
        }
    }

    private static async Task<Dictionary<string, Uri>> ResolveComparisonEndpointsAsync(
        string stackId,
        string dockerExecutable)
    {
        var (dockerContext, _) = await ResolveDockerIdentityAsync();
        var containers = await RunProcessAsync(dockerExecutable, [
            "--context", dockerContext, "ps", "--quiet", "--no-trunc",
            "--filter", $"label={DockerContainerSpec.StackLabel}={stackId}",
        ], TimeSpan.FromSeconds(15));
        Assert.Equal(0, containers.ExitCode);
        var ids = containers.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.NotEmpty(ids);

        var resources = new Dictionary<string, (string Id, string RunId)>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            var inspection = await RunProcessAsync(dockerExecutable, [
                "--context", dockerContext, "inspect", "--format", "{{json .Config.Labels}}", id,
            ], TimeSpan.FromSeconds(15));
            Assert.Equal(0, inspection.ExitCode);
            using var document = System.Text.Json.JsonDocument.Parse(inspection.StandardOutput);
            var labels = document.RootElement;
            Assert.Equal(stackId, labels.GetProperty(DockerContainerSpec.StackLabel).GetString());
            var role = labels.GetProperty(DockerContainerSpec.ResourceLabel).GetString();
            Assert.False(string.IsNullOrWhiteSpace(role));
            var runId = labels.GetProperty(DockerContainerSpec.RunLabel).GetString();
            Assert.False(string.IsNullOrWhiteSpace(runId));
            if (labels.TryGetProperty(DockerContainerSpec.OwnerLabel, out var owner))
            {
                Assert.Equal(DockerContainerSpec.OwnerValue, owner.GetString());
            }

            Assert.True(resources.TryAdd(role!, (id, runId!)), $"Duplicate same-stack resource label: {role}.");
        }

        Assert.Single(resources.Values.Select(resource => resource.RunId).Distinct(StringComparer.Ordinal));
        foreach (var requiredRole in new[]
        {
            "simulator", "duck", "game-controller", "cm4-sim", "crane",
            "referee-driver", "tracker-tigers", "tracker-erforce", "debug-host",
        })
        {
            Assert.Contains(requiredRole, resources.Keys);
        }

        var endpoints = new Dictionary<string, Uri>(StringComparer.Ordinal);
        foreach (var (resource, targetPort) in new[]
        {
            ("game-controller", "8082/tcp"),
            ("referee-driver", "39110/tcp"),
            ("debug-host", "8080/tcp"),
        })
        {
            var inspection = await RunProcessAsync(dockerExecutable, [
                "--context", dockerContext, "inspect", "--format", "{{json .NetworkSettings.Ports}}", resources[resource].Id,
            ], TimeSpan.FromSeconds(15));
            Assert.Equal(0, inspection.ExitCode);
            using var portsDocument = System.Text.Json.JsonDocument.Parse(inspection.StandardOutput);
            var mappings = portsDocument.RootElement.GetProperty(targetPort);
            var mapping = Assert.Single(mappings.EnumerateArray());
            Assert.Equal("127.0.0.1", mapping.GetProperty("HostIp").GetString());
            var hostPort = mapping.GetProperty("HostPort").GetString();
            Assert.True(int.TryParse(hostPort, out var parsedPort) && parsedPort is > 0 and <= 65535,
                $"Invalid dynamic host port for {resource}:{targetPort}: {hostPort}.");
            endpoints[resource] = new Uri($"http://127.0.0.1:{parsedPort}/");
        }

        return endpoints;
    }

    private static async Task<LatestVisionHealthSnapshot> WaitForLatestYellowVisionAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var document = await ReadComparisonHealthAsync(client, cancellationToken);
            var vision = ParseLatestVisionHealth(document.RootElement);
            if (vision is { Detection.RobotsYellow.Count: > 0 })
            {
                return vision;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
    }

    private static async Task<LatestVisionHealthSnapshot> WaitForNewYellowVisionAsync(
        HttpClient client,
        DateTimeOffset previousDetectionReceivedAt,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var document = await ReadComparisonHealthAsync(client, cancellationToken);
            var vision = ParseLatestVisionHealth(document.RootElement);
            if (vision is { Detection.RobotsYellow.Count: > 0 } &&
                vision.DetectionReceivedAt > previousDetectionReceivedAt)
            {
                return vision;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private static async Task<LatestVisionHealthSnapshot> WaitForYellowVisionAfterActiveObservationAsync(
        HttpClient client,
        DateTimeOffset activeObservedAt,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var document = await ReadComparisonHealthAsync(client, cancellationToken);
            var vision = ParseLatestVisionHealth(document.RootElement);
            var selected = vision is null
                ? null
                : SelectPostActiveYellowVision([vision], activeObservedAt);
            if (selected is not null)
            {
                return selected;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private static LatestVisionHealthSnapshot? SelectPostActiveYellowVision(
        IEnumerable<LatestVisionHealthSnapshot> snapshots,
        DateTimeOffset activeObservedAt) =>
        snapshots
            .Where(snapshot => IsAfterActiveObservation(snapshot, activeObservedAt) &&
                snapshot.Detection.RobotsYellow.Count > 0)
            .OrderBy(snapshot => snapshot.DetectionReceivedAt)
            .LastOrDefault();

    private static bool IsAfterActiveObservation(
        LatestVisionHealthSnapshot snapshot,
        DateTimeOffset activeObservedAt) =>
        snapshot.DetectionReceivedAt > activeObservedAt;

    private static async Task<LiveComparisonHealthSnapshot> WaitForLiveComparisonAsync(
        HttpClient client,
        CancellationToken cancellationToken,
        TimeSpan? pollInterval = null)
    {
        HttpStatusCode? lastResponseStatus = null;
        string? lastResponseBody = null;
        try
        {
            while (true)
            {
                var response = await ReadComparisonHealthResponseAsync(client, cancellationToken);
                lastResponseStatus = response.StatusCode;
                lastResponseBody = response.Body;
                using var document = System.Text.Json.JsonDocument.Parse(response.Body);
                var root = document.RootElement;
                if (root.TryGetProperty("isReady", out var ready) && ready.ValueKind == System.Text.Json.JsonValueKind.True &&
                    root.TryGetProperty("sources", out var sourcesElement) && sourcesElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                    root.TryGetProperty("pairs", out var pairsElement) && pairsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var sourceRoles = sourcesElement.EnumerateArray()
                        .Select(source => source.GetProperty("logicalRole").GetString() ?? string.Empty)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray();
                    var requiredRoles = new[] { "Duck", "ER-Force", "TIGERs" }.Order(StringComparer.Ordinal).ToArray();
                    var pairs = pairsElement.EnumerateArray()
                        .Select(pair => new LiveComparisonPairEvidence(
                            pair.GetProperty("referenceRole").GetString() ?? string.Empty,
                            pair.GetProperty("comparisonRole").GetString() ?? string.Empty,
                            ParseTrackerDifferencePairStatus(pair.GetProperty("status"))))
                        .ToArray();
                    if (sourceRoles.SequenceEqual(requiredRoles, StringComparer.Ordinal) &&
                        pairs.Length == 3 && pairs.All(pair => pair.Status == TrackerDifferencePairStatus.Compared))
                    {
                        var vision = ParseLatestVisionHealth(root);
                        Assert.NotNull(vision);
                        return new LiveComparisonHealthSnapshot(
                            vision.FrameNumber,
                            vision.DetectionReceivedAt,
                            root.GetProperty("visionPacketsReceived").GetInt32(),
                            sourceRoles,
                            pairs);
                    }
                }

                await Task.Delay(pollInterval ?? TimeSpan.FromMilliseconds(200), cancellationToken);
            }
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            var lastResponse = lastResponseStatus is { } status && lastResponseBody is { } body
                ? $"lastResponseStatus={(int)status}; lastResponseBody={body}"
                : "lastResponse=none";
            throw new OperationCanceledException(
                $"COMPARISON_HEALTH_WAIT_CANCELLED: {lastResponse}",
                exception,
                cancellationToken);
        }
    }

    private static async Task<(HttpStatusCode StatusCode, string Body)> ReadComparisonHealthResponseAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("health/comparison", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response.StatusCode, body);
    }

    private static async Task<System.Text.Json.JsonDocument> ReadComparisonHealthAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("health/comparison", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return System.Text.Json.JsonDocument.Parse(body);
    }

    private static LatestVisionHealthSnapshot? ParseLatestVisionHealth(System.Text.Json.JsonElement root)
    {
        if (!root.TryGetProperty("latestVision", out var latestVision) || latestVision.ValueKind is System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined)
        {
            return null;
        }

        var frame = new SSL_DetectionFrame { FrameNumber = latestVision.GetProperty("frameNumber").GetUInt32() };
        foreach (var robot in latestVision.GetProperty("yellowRobots").EnumerateArray())
        {
            frame.RobotsYellow.Add(new SSL_DetectionRobot
            {
                RobotId = robot.GetProperty("robotId").GetUInt32(),
                X = robot.GetProperty("x").GetSingle(),
                Y = robot.GetProperty("y").GetSingle(),
            });
        }

        return new LatestVisionHealthSnapshot(
            latestVision.GetProperty("packetCount").GetInt32(),
            latestVision.GetProperty("detectionReceivedAt").GetDateTimeOffset(),
            latestVision.GetProperty("frameNumber").GetUInt32(),
            frame);
    }

    private static TrackerDifferencePairStatus ParseTrackerDifferencePairStatus(System.Text.Json.JsonElement value)
    {
        var parsed = value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String when Enum.TryParse<TrackerDifferencePairStatus>(value.GetString(), true, out var status) => status,
            System.Text.Json.JsonValueKind.Number when Enum.IsDefined(typeof(TrackerDifferencePairStatus), value.GetInt32()) => (TrackerDifferencePairStatus)value.GetInt32(),
            _ => (TrackerDifferencePairStatus)(-1),
        };
        return parsed;
    }

    private sealed class HttpRefereeCommandSource(HttpClient client, TimeSpan? pollInterval = null) :
        IRefereeCommandSource, IRefereeLifecycleObservationSource
    {
        private readonly TimeSpan pollDelay = pollInterval ?? TimeSpan.FromMilliseconds(100);
        private readonly SemaphoreSlim readGate = new(1, 1);
        private readonly ConcurrentDictionary<uint, DateTimeOffset> observationTimesByCounter = new();
        private readonly ConditionalWeakTable<RefereeCommandSnapshot, RefereeHealthReadEvidence> readEvidenceBySnapshot = new();

        public bool TryGetReadEvidence(
            RefereeCommandSnapshot snapshot,
            out RefereeHealthReadEvidence? evidence) =>
            readEvidenceBySnapshot.TryGetValue(snapshot, out evidence);

        public bool TryGetObservationTime(uint commandCounter, out DateTimeOffset observedAtUtc) =>
            observationTimesByCounter.TryGetValue(commandCounter, out observedAtUtc);

        public async Task<RefereeLifecycleObservation> WaitForNextAsync(
            long previousRequestSequence,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var current = await ReadCurrentAsync(cancellationToken);
                if (current.IsReady && current.Snapshot is { } udp &&
                    current.RequestSequence is { } sequence && sequence > previousRequestSequence &&
                    current.ApiSnapshot is { } api)
                {
                    return new(sequence, udp, api.Command.ToString(), api.Phase.ToString(),
                        api.Stage, api.StageTimeLeft, api);
                }

                await Task.Delay(pollDelay, cancellationToken);
            }
        }

        public async ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
            uint? previousCommandCounter,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var current = await ReadCurrentAsync(cancellationToken);
                if (current.IsReady && current.Snapshot is { } snapshot)
                {
                    if (previousCommandCounter is null)
                    {
                        return snapshot;
                    }

                    if (snapshot.Stage == Referee.Types.Stage.PostGame)
                    {
                        return snapshot;
                    }

                    if (snapshot.CommandCounter != previousCommandCounter.Value)
                    {
                        var expected = unchecked(previousCommandCounter.Value + 1);
                        if (snapshot.CommandCounter != expected)
                        {
                            throw new RefereeCommandCounterGapException(
                                expected,
                                previousCommandCounter.Value,
                                current.ReadEvidence);
                        }

                        return snapshot;
                    }
                }

                await Task.Delay(pollDelay, cancellationToken);
            }
        }

        public async Task<CurrentRefereeHealthSnapshot> ReadCurrentAsync(CancellationToken cancellationToken)
        {
            await readGate.WaitAsync(cancellationToken);
            try
            {
                var readStartedAtUtc = DateTimeOffset.UtcNow;
                var readStartedTimestamp = Stopwatch.GetTimestamp();
                using var response = await client.GetAsync("health/referee", cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var readCompletedAtUtc = DateTimeOffset.UtcNow;
                var readElapsed = Stopwatch.GetElapsedTime(readStartedTimestamp);
                using var document = System.Text.Json.JsonDocument.Parse(body);
                var root = document.RootElement;
                var isReady = response.IsSuccessStatusCode &&
                    root.TryGetProperty("isReady", out var ready) && ready.ValueKind == System.Text.Json.JsonValueKind.True;
                RefereeCommandSnapshot? snapshot = null;
                if (root.TryGetProperty("refereeCommand", out var commandElement) &&
                    commandElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    snapshot = new RefereeCommandSnapshot(
                        ParseRefereeCommand(commandElement.GetProperty("command")),
                        commandElement.GetProperty("commandCounter").GetUInt32(),
                        commandElement.GetProperty("yellowTeamName").GetString() ?? string.Empty,
                        TryParseRefereeStage(commandElement),
                        commandElement.TryGetProperty("packetTimestamp", out var packetTimestamp)
                            ? packetTimestamp.GetUInt64()
                            : null,
                        ReadUInt32(commandElement, "blueScore"),
                        ReadUInt32(commandElement, "yellowScore"),
                        TryParseRefereeCommandProperty(commandElement, "nextCommand"),
                        ReadSingle(commandElement, "designatedPositionX"),
                        ReadSingle(commandElement, "designatedPositionY"));
                }

                var diagnostic = root.TryGetProperty("diagnostic", out var diagnosticElement)
                    ? diagnosticElement
                    : default;
                var apiState = root.TryGetProperty("api", out var apiElement) &&
                    apiElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    apiElement.TryGetProperty("state", out var stateElement)
                        ? stateElement
                        : default;
                var apiSnapshot = TryParseApiSnapshot(apiState);
                var requestSequence = apiElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    apiElement.TryGetProperty("requestSequence", out var sequenceElement) &&
                    sequenceElement.TryGetInt64(out var sequence)
                        ? sequence
                        : (long?)null;
                var readEvidence = new RefereeHealthReadEvidence(
                    (int)response.StatusCode,
                    isReady,
                    snapshot?.Command.ToString(),
                    snapshot?.CommandCounter,
                    snapshot?.YellowTeamName,
                    readStartedAtUtc,
                    readCompletedAtUtc,
                    readElapsed.TotalMilliseconds,
                    ReadScalar(root, "reason"),
                    ReadScalar(diagnostic, "sourceKey"),
                    ReadScalar(diagnostic, "remoteEndpoint"),
                    ReadScalar(apiState, "command"),
                    ReadScalar(apiState, "phase"));
                var result = new CurrentRefereeHealthSnapshot(
                    isReady,
                    snapshot,
                    isReady && snapshot is not null ? readCompletedAtUtc : null,
                    readEvidence);
                result = result with { RequestSequence = requestSequence, ApiSnapshot = apiSnapshot };
                if (result.ObservedAtUtc is { } readyObservedAt && snapshot is not null)
                {
                    observationTimesByCounter[snapshot.CommandCounter] = readyObservedAt;
                    readEvidenceBySnapshot.Add(snapshot, readEvidence);
                }

                return result;
            }
            finally
            {
                readGate.Release();
            }
        }

        private static string? ReadScalar(
            System.Text.Json.JsonElement parent,
            string propertyName)
        {
            if (parent.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !parent.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => value.GetString(),
                System.Text.Json.JsonValueKind.Number => value.GetRawText(),
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                _ => null,
            };
        }

        private static uint ReadUInt32(System.Text.Json.JsonElement parent, string name) =>
            parent.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Number &&
            value.TryGetUInt32(out var result) ? result : 0;

        private static float? ReadSingle(System.Text.Json.JsonElement parent, string name) =>
            parent.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Number &&
            value.TryGetSingle(out var result) ? result : null;

        private static Referee.Types.Command? TryParseRefereeCommandProperty(
            System.Text.Json.JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out var value)) return null;
            if (value.ValueKind == System.Text.Json.JsonValueKind.Number &&
                Enum.IsDefined(typeof(Referee.Types.Command), value.GetInt32()))
                return (Referee.Types.Command)value.GetInt32();
            return value.ValueKind == System.Text.Json.JsonValueKind.String &&
                Enum.TryParse<Referee.Types.Command>(value.GetString()?.Replace("_", string.Empty, StringComparison.Ordinal),
                    true, out var command) ? command : null;
        }

        private static GameControllerMatchSnapshot? TryParseApiSnapshot(System.Text.Json.JsonElement state)
        {
            if (state.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !state.TryGetProperty("command", out var commandElement) ||
                !TryReadEnum(commandElement, out GameControllerCommandKind command) ||
                !state.TryGetProperty("phase", out var phaseElement) ||
                !TryReadEnum(phaseElement, out GameControllerMatchPhase phase))
                return null;

            return new(command, phase,
                ReadScalar(state, "stage"),
                TryReadTimeSpan(state, "stageTimeLeft"),
                ReadScalar(state, "nextCommand"),
                ReadScalar(state, "nextCommandForTeam"),
                TryReadUInt32(state, "blueScore"),
                TryReadUInt32(state, "yellowScore"),
                ReadSingle(state, "placementX"),
                ReadSingle(state, "placementY"),
                ReadScalar(state, "commandForTeam"),
                ReadScalar(state, "phaseForTeam"));
        }

        private static uint? TryReadUInt32(System.Text.Json.JsonElement parent, string name) =>
            parent.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Number &&
            value.TryGetUInt32(out var result) ? result : null;

        private static bool TryReadEnum<TEnum>(System.Text.Json.JsonElement value, out TEnum result)
            where TEnum : struct, Enum
        {
            if (value.ValueKind == System.Text.Json.JsonValueKind.Number &&
                Enum.IsDefined(typeof(TEnum), value.GetInt32()))
            {
                result = (TEnum)Enum.ToObject(typeof(TEnum), value.GetInt32());
                return true;
            }
            return Enum.TryParse(value.GetString(), true, out result);
        }

        private static TimeSpan? TryReadTimeSpan(System.Text.Json.JsonElement parent, string name) =>
            parent.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String &&
            TimeSpan.TryParse(value.GetString(), out var result) ? result : null;

        private static Referee.Types.Command ParseRefereeCommand(System.Text.Json.JsonElement value)
        {
            if (value.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                var numeric = value.GetInt32();
                Assert.True(Enum.IsDefined(typeof(Referee.Types.Command), numeric), $"Unknown referee command value {numeric}.");
                return (Referee.Types.Command)numeric;
            }

            var text = value.GetString()?.Replace("_", string.Empty, StringComparison.Ordinal);
            Assert.True(Enum.TryParse<Referee.Types.Command>(text, true, out var command), $"Unknown referee command value '{text}'.");
            return command;
        }

        private static Referee.Types.Stage? TryParseRefereeStage(System.Text.Json.JsonElement commandElement)
        {
            if (!commandElement.TryGetProperty("stage", out var value))
            {
                return null;
            }

            if (value.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                var numeric = value.GetInt32();
                return Enum.IsDefined(typeof(Referee.Types.Stage), numeric)
                    ? (Referee.Types.Stage)numeric
                    : null;
            }

            return value.ValueKind == System.Text.Json.JsonValueKind.String &&
                Enum.TryParse<Referee.Types.Stage>(value.GetString(), ignoreCase: true, out var stage)
                ? stage
                : null;
        }
    }

    private sealed class QueueHttpMessageHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> responseQueue = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (responseQueue.Count == 0)
            {
                throw new InvalidOperationException("The test HTTP response queue is empty.");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseQueue.Dequeue()),
            });
        }
    }

    private sealed class BlockingRefereeHttpMessageHandler(
        string initialResponse,
        string changedResponse) : HttpMessageHandler
    {
        private readonly TaskCompletionSource<bool> secondRequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> releaseSecondRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int requestCount;
        private int activeRequests;
        private int maximumConcurrentRequests;

        public int RequestCount => Volatile.Read(ref requestCount);

        public int MaximumConcurrentRequests => Volatile.Read(ref maximumConcurrentRequests);

        public Task WaitForSecondRequestAsync(CancellationToken cancellationToken) =>
            secondRequestStarted.Task.WaitAsync(cancellationToken);

        public void ReleaseSecondRequest() => releaseSecondRequest.TrySetResult(true);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var currentCount = Interlocked.Increment(ref activeRequests);
            UpdateMaximum(currentCount);
            var currentRequest = Interlocked.Increment(ref requestCount);
            try
            {
                if (currentRequest == 2)
                {
                    secondRequestStarted.TrySetResult(true);
                    await releaseSecondRequest.Task.WaitAsync(cancellationToken);
                }

                var response = currentRequest == 1 ? initialResponse : changedResponse;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) };
            }
            finally
            {
                Interlocked.Decrement(ref activeRequests);
            }
        }

        private void UpdateMaximum(int current)
        {
            while (true)
            {
                var maximum = Volatile.Read(ref maximumConcurrentRequests);
                if (current <= maximum || Interlocked.CompareExchange(ref maximumConcurrentRequests, current, maximum) == maximum)
                {
                    return;
                }
            }
        }
    }

    private sealed record LatestVisionHealthSnapshot(
        int PacketCount,
        DateTimeOffset DetectionReceivedAt,
        uint FrameNumber,
        SSL_DetectionFrame Detection);

    private sealed record LiveComparisonPairEvidence(
        string ReferenceRole,
        string ComparisonRole,
        TrackerDifferencePairStatus Status);

    private sealed record LiveComparisonHealthSnapshot(
        uint LatestVisionFrameNumber,
        DateTimeOffset DetectionReceivedAt,
        int VisionPacketsReceived,
        IReadOnlyList<string> SourceRoles,
        IReadOnlyList<LiveComparisonPairEvidence> Pairs);

    private sealed record CurrentRefereeHealthSnapshot(
        bool IsReady,
        RefereeCommandSnapshot? Snapshot,
        DateTimeOffset? ObservedAtUtc = null,
        RefereeHealthReadEvidence? ReadEvidence = null,
        long? RequestSequence = null,
        GameControllerMatchSnapshot? ApiSnapshot = null);

    private sealed class StaticResponseHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
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
                var craneOverlaySource = ResolveCraneLaunchOverlaySource(Projects.Duck_Testing_AppHost.ProjectPath);
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

    private sealed class QueueResponseHandler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        private readonly ConcurrentQueue<(HttpStatusCode Status, string Body)> queue = new(responses);
        private (HttpStatusCode Status, string Body) last = responses[^1];
        private int requestsSent;

        internal int RequestCount => Volatile.Read(ref requestsSent);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestsSent);
            if (queue.TryDequeue(out var next)) last = next;
            return Task.FromResult(new HttpResponseMessage(last.Status)
            {
                Content = new StringContent(last.Body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class ReadinessSequenceControlClient(Func<int> requestCount) : IGameControllerControlClient
    {
        private readonly GameControllerContinuationState[] states =
        [
            Continuation("BLOCKED"), Continuation("WAITING"), Continuation("READY_AUTO"),
        ];

        internal List<GameControllerContinueAction> SentActions { get; } = [];
        internal List<int> ReadinessRequestIndices { get; } = [];
        internal int ReadinessReads { get; private set; }
        internal bool NormalStartSentAfterReady { get; private set; }
        internal int? NormalStartSentAtRequestIndex { get; private set; }

        public Task<GameControllerContinuationState?> ReadContinuationStateAsync(CancellationToken cancellationToken)
        {
            ReadinessRequestIndices.Add(requestCount());
            var index = Math.Min(ReadinessReads, states.Length - 1);
            ReadinessReads++;
            return Task.FromResult<GameControllerContinuationState?>(states[index]);
        }

        public Task SendContinueActionAsync(GameControllerContinueAction action, CancellationToken cancellationToken)
        {
            SentActions.Add(action);
            if (action == GameControllerContinueAction.NormalStart)
            {
                NormalStartSentAtRequestIndex = requestCount();
                NormalStartSentAfterReady = ReadinessReads == states.Length && states[^1].ContinueActions[0].State == "READY_AUTO";
            }
            return Task.CompletedTask;
        }

        private static GameControllerContinuationState Continuation(string state) => new(
            nameof(GameControllerCommandKind.Kickoff), nameof(GameControllerMatchPhase.Kickoff),
            "NORMAL_FIRST_HALF_PRE", TimeSpan.FromSeconds(25),
            [new("NORMAL_START", "UNKNOWN", state, false)], "KICKOFF", "YELLOW", 2, 1);
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
            return GetPositiveCommandTimeout(
                available,
                TimeSpan.FromSeconds(15),
                "NET-003 work budget expired before a Docker command could start.");
        }

        TimeSpan CleanupCommandTimeout()
        {
            var available = Remaining(totalBudget, "cleanup") - TimeSpan.FromSeconds(1);
            return GetPositiveCommandTimeout(
                available,
                TimeSpan.FromSeconds(5),
                "NET-003 cleanup budget expired before a Docker command could start.");
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
            var overlay = ResolveCraneLaunchOverlaySource(Projects.Duck_Testing_AppHost.ProjectPath);
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

            string[] remainingOwnedIds = [];
            try
            {
                remainingOwnedIds = await GetOwnedContainerIdsAsync(stackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                output.WriteLine($"NET003 pre-rescueOwnedIds={string.Join(",", remainingOwnedIds)}; debugHostImageId={debugHostImageId}");
                Assert.Empty(remainingOwnedIds);
            }
            finally
            {
                await RemoveOnlySecondStackContainersAsync(stackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                var remainingAfterRescue = await GetOwnedContainerIdsAsync(stackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                output.WriteLine($"NET003 post-rescueOwnedIds={string.Join(",", remainingAfterRescue)}");
                Assert.Empty(remainingAfterRescue);
            }
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
    public async Task CraneStartupExitCleansOnlyItsContainersAndPreservesOtherOwner()
    {
        var acceptanceClock = Stopwatch.StartNew();
        var startedAt = DateTimeOffset.UtcNow;
        var workBudget = TimeSpan.FromSeconds(180);
        var totalBudget = TimeSpan.FromSeconds(300);
        var stackId = $"issue82-failure-{Guid.NewGuid():N}";
        var runId = $"failure-run-{Guid.NewGuid():N}";
        var sentinelStackId = $"issue82-sentinel-{Guid.NewGuid():N}";
        var sentinelRunId = $"sentinel-run-{Guid.NewGuid():N}";
        var sentinelName = $"duck82-sentinel-{Guid.NewGuid():N}";
        var artifactsDirectory = Path.Combine(Path.GetTempPath(), stackId);
        Directory.CreateDirectory(artifactsDirectory);
        var repositoryRoot = FindRepositoryRoot();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var (dockerExecutable, dockerPathScope) = ResolveAndPrepareDockerExecutable(
            Environment.GetEnvironmentVariable("Testing__IdentityControl__DockerExecutable"), originalPath);
        var stagedAssets = new List<StagedAppHostAsset>();
        IDistributedApplicationTestingBuilder? appHostBuilder = null;
        DistributedApplication? application = null;
        Task? startTask = null;
        var startAttempted = false;
        DateTimeOffset? containerEventWindowStartedAt = null;
        var sentinelCreationAttempted = false;
        string? sentinelContainerId = null;
        string[] baselineContainerIds = [];
        var dockerReady = false;
        var engine = "";
        var engineId = "";

        TimeSpan Remaining(TimeSpan deadline, string phase)
        {
            var remaining = deadline - acceptanceClock.Elapsed;
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException($"Issue #82 failure acceptance {phase} budget expired after {acceptanceClock.Elapsed}.");
            return remaining;
        }

        TimeSpan WorkCommandTimeout() => GetPositiveCommandTimeout(
            Remaining(workBudget, "work") - TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            "Issue #82 failure acceptance work budget expired before a Docker command could start.");

        TimeSpan CleanupCommandTimeout() => GetPositiveCommandTimeout(
            Remaining(totalBudget, "cleanup") - TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            "Issue #82 failure acceptance cleanup budget expired before a Docker command could start.");

        TimeSpan ProcessKillWaitTimeout() => TimeSpan.FromSeconds(1);

        TimeSpan CleanupOperationTimeout(TimeSpan cap, TimeSpan reserveAfter)
        {
            var available = Remaining(totalBudget, "cleanup") - reserveAfter;
            if (available <= TimeSpan.Zero)
                throw new TimeoutException("Issue #82 failure acceptance has entered its reserved exact-owner cleanup window.");
            return available < cap ? available : cap;
        }

        Task<ProcessResult> DockerAsync(IEnumerable<string> arguments, bool cleanup = false) =>
            RunProcessAsync(
                dockerExecutable,
                arguments,
                cleanup ? CleanupCommandTimeout() : WorkCommandTimeout(),
                ProcessKillWaitTimeout());

        async Task<string[]> GetAllContainerIdsAsync(bool cleanup = false)
        {
            var result = await DockerAsync(["ps", "--all", "--quiet", "--no-trunc"], cleanup);
            Assert.Equal(0, result.ExitCode);
            return result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        async Task<(string Status, int ExitCode, bool Running)> InspectStateAsync(string id, bool cleanup = false)
        {
            var result = await DockerAsync(["inspect", "--format", "{{json .State}}", id], cleanup);
            Assert.Equal(0, result.ExitCode);
            using var document = System.Text.Json.JsonDocument.Parse(result.StandardOutput);
            var state = document.RootElement;
            return (
                state.GetProperty("Status").GetString() ?? "unknown",
                state.GetProperty("ExitCode").GetInt32(),
                state.GetProperty("Running").GetBoolean());
        }

        async Task<Dictionary<string, string?>> ReadLabelsAsync(string id, bool cleanup = false)
        {
            var result = await DockerAsync(["inspect", "--format", "{{json .Config.Labels}}", id], cleanup);
            Assert.Equal(0, result.ExitCode);
            using var document = System.Text.Json.JsonDocument.Parse(result.StandardOutput);
            return document.RootElement.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.ValueKind == System.Text.Json.JsonValueKind.String ? property.Value.GetString() : null,
                StringComparer.Ordinal);
        }

        async Task<bool> IsSentinelRunningAsync(bool cleanup = false)
        {
            if (sentinelContainerId is null)
                return false;
            var ids = await GetOwnedContainerIdsAsync(
                sentinelStackId, dockerExecutable, cleanup ? CleanupCommandTimeout : WorkCommandTimeout, ProcessKillWaitTimeout);
            if (!ids.SequenceEqual([sentinelContainerId], StringComparer.Ordinal))
                return false;
            var state = await InspectStateAsync(sentinelContainerId, cleanup);
            return state.Running;
        }

        try
        {
            (engine, engineId) = await ResolveDockerIdentityAsync(WorkCommandTimeout, ProcessKillWaitTimeout);
            var requiredImages = new[]
            {
                "ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd",
                "robocupssl/ssl-game-controller:3.20.3",
                "ghcr.io/ibis-ssl/orion-cm4-sim:d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a",
                "ghcr.io/ibis-ssl/crane:scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                "mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3",
            };
            var imageCheck = await DockerAsync(["image", "inspect", .. requiredImages]);
            Assert.Equal(0, imageCheck.ExitCode);
            baselineContainerIds = await GetAllContainerIdsAsync();
            Assert.Empty(await GetOwnedContainerIdsAsync(stackId, dockerExecutable, WorkCommandTimeout, ProcessKillWaitTimeout));
            Assert.Empty(await GetOwnedContainerIdsAsync(sentinelStackId, dockerExecutable, WorkCommandTimeout, ProcessKillWaitTimeout));
            dockerReady = true;
            output.WriteLine($"failurePreflight=complete; startedAt={startedAt:O}; engine={engine}/{engineId}; localImages={requiredImages.Length}; baselineContainers={baselineContainerIds.Length}; pull=none; workBudget={workBudget}; totalBudget={totalBudget}");

            var helper = ResolveCm4TransportHelperSource(repositoryRoot);
            var overlay = ResolveCraneLaunchOverlaySource(Projects.Duck_Testing_AppHost.ProjectPath);
            stagedAssets.AddRange(StageLocalAppHostAssets(helper, overlay));
            Assert.True(File.Exists(Path.Combine(repositoryRoot, "Tracker", "Tracker.RuntimeHost", "bin", "Release", "net10.0", "publish", "Tracker.RuntimeHost.dll")));

            var lockPath = Path.Combine(artifactsDirectory, $"{stackId}.lock");
            appHostBuilder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>([
                "--Testing:Mode=base",
                "--Testing:Crane:Planner=visibility_graph",
                "--Testing:Simulator:ImageTag=a52b6bd",
                "--Testing:Crane:ImageTag=scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                "--Testing:Cm4Sim:ImageTag=d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a",
                "--Testing:IdentityControl:DesktopBridgeEnabled=true",
                "--Testing:IdentityControl:DockerContext=" + engine,
                "--Testing:IdentityControl:ExpectedEngineId=" + engineId,
                "--Testing:IdentityControl:DockerExecutable=" + dockerExecutable,
                "--Testing:StackOwnership:LockPath=" + lockPath,
                "--Testing:StackOwnership:StackId=" + stackId,
                "--Testing:Diagnostics:RunId=" + runId,
                "--Testing:Crane:FixtureManifestPath=" + Path.Combine(artifactsDirectory, "crane-fixture.json"),
                "--Testing:Crane:DiagnosticsPath=" + Path.Combine(artifactsDirectory, "crane-diagnostics.jsonl"),
            ], new CancellationTokenSource(Remaining(workBudget, "AppHost creation")).Token);

            sentinelCreationAttempted = true;
            var sentinelCreate = await DockerAsync([
                "run", "--pull=never", "--detach", "--name", sentinelName, "--network", "none",
                "--label", $"{DockerContainerSpec.StackLabel}={sentinelStackId}",
                "--label", $"{DockerContainerSpec.ResourceLabel}=sentinel",
                "--label", $"{DockerContainerSpec.RunLabel}={sentinelRunId}",
                "--entrypoint", "/bin/bash",
                requiredImages[3], "-c", "exec sleep 600",
            ]);
            Assert.Equal(0, sentinelCreate.ExitCode);
            var sentinelLookup = await DockerAsync(["inspect", "--format", "{{.Id}}", sentinelName]);
            Assert.Equal(0, sentinelLookup.ExitCode);
            sentinelContainerId = sentinelLookup.StandardOutput.Trim();
            Assert.Matches("^[0-9a-f]{64}$", sentinelContainerId);
            Assert.True(await IsSentinelRunningAsync());
            output.WriteLine($"failureSentinelBeforeBuild={sentinelContainerId}; network=none; publishedPorts=0");

            var craneResource = Assert.IsType<ContainerResource>(appHostBuilder.Resources.Single(resource => resource.Name == "crane"));
            appHostBuilder.CreateResourceBuilder<ContainerResource>(craneResource)
                .WithEntrypoint("/bin/bash")
                .WithArgs(context =>
                {
                    context.Args.Clear();
                    context.Args.Add("-c");
                    context.Args.Add("exit 82");
                    return Task.CompletedTask;
                });
            Assert.Equal("/bin/bash", craneResource.Entrypoint);
            output.WriteLine("failureInjection=test-only Crane entrypoint=/bin/bash args=-c,exit 82; image/dependencies/labels/network/health unchanged");

            containerEventWindowStartedAt = DateTimeOffset.UtcNow;
            application = await appHostBuilder.BuildAsync(new CancellationTokenSource(Remaining(workBudget, "AppHost build")).Token);
            startAttempted = true;
            startTask = application.StartAsync(new CancellationTokenSource(Remaining(workBudget, "AppHost start")).Token);
            var notifications = application.Services.GetRequiredService<ResourceNotificationService>();
            foreach (var resource in new[] { "simulator", "duck", "game-controller", "cm4-sim" })
            {
                await notifications.WaitForResourceAsync(resource, KnownResourceStates.Running, new CancellationTokenSource(Remaining(workBudget, $"{resource} Running")).Token);
            }

            string[] ownedIds = [];
            string? craneId = null;
            (string Status, int ExitCode, bool Running)? craneState = null;
            var craneDependencyWait = Stopwatch.StartNew();
            var craneDependencyWaitBudget = TimeSpan.FromSeconds(45);
            while (craneState is null || (craneState.Value.Status != "exited" && craneState.Value.Status != "dead"))
            {
                ownedIds = await GetOwnedContainerIdsAsync(stackId, dockerExecutable, WorkCommandTimeout, ProcessKillWaitTimeout);
                var craneCandidates = new List<string>();
                foreach (var id in ownedIds)
                {
                    var labels = await ReadLabelsAsync(id);
                    if (labels.GetValueOrDefault(DockerContainerSpec.ResourceLabel) == "crane")
                        craneCandidates.Add(id);
                }

                Assert.True(craneCandidates.Count <= 1,
                    $"DCP produced more than one Crane container for stack {stackId}: {string.Join(',', craneCandidates)}.");
                if (craneCandidates.Count == 1)
                {
                    var observedId = craneCandidates[0];
                    if (craneId is not null)
                        Assert.Equal(craneId, observedId);
                    craneId = observedId;
                    craneState = await InspectStateAsync(craneId);
                }

                if (craneState is { Status: "exited" or "dead" })
                    break;

                if (craneDependencyWait.Elapsed >= craneDependencyWaitBudget)
                    break;

                var waitRemaining = craneDependencyWaitBudget - craneDependencyWait.Elapsed;
                await Task.Delay(
                    waitRemaining < TimeSpan.FromMilliseconds(250) ? waitRemaining : TimeSpan.FromMilliseconds(250),
                    new CancellationTokenSource(Remaining(workBudget, "Crane failure observation")).Token);
            }

            if (craneId is null || craneState is null || craneState.Value.Status is not ("exited" or "dead"))
            {
                var startOutcome = "still-pending";
                if (startTask.IsCompleted)
                {
                    try
                    {
                        await startTask;
                        startOutcome = "completed";
                    }
                    catch (Exception exception)
                    {
                        startOutcome = $"faulted:{exception.GetType().FullName}:{exception.Message}";
                    }
                }
                output.WriteLine($"failureNotObserved=true; craneFullId={craneId}; craneState={craneState}; appHostStartOutcome={startOutcome}; ownedIds={string.Join(',', ownedIds)}");
            }

            var otherResources = new Dictionary<string, (string Id, string Status, bool Running)>(StringComparer.Ordinal);
            foreach (var id in ownedIds)
            {
                var labels = await ReadLabelsAsync(id);
                var resource = labels.GetValueOrDefault(DockerContainerSpec.ResourceLabel);
                if (resource == "crane")
                {
                    continue;
                }

                Assert.Contains(resource, new[] { "simulator", "duck", "game-controller", "cm4-sim" });
                var state = await InspectStateAsync(id);
                Assert.True(state.Running, $"Expected resource {resource} ({id}) to be Running before AppHost stop; actual={state.Status}/{state.ExitCode}.");
                Assert.False(otherResources.ContainsKey(resource!), $"Duplicate resource container for {resource} indicates DCP recreation.");
                otherResources.Add(resource!, (id, state.Status, state.Running));
            }

            Assert.Equal(4, otherResources.Count);
            Assert.NotNull(craneId);
            Assert.NotNull(craneState);
            var craneNotification = notifications.TryGetCurrentState("crane", out var craneCurrent)
                ? craneCurrent.Snapshot.State?.Text ?? "unknown"
                : "unavailable";
            async Task<(string? Action, string? Resource, string? ExitCode, string? ContainerId)[]> ReadCraneEventsAsync()
            {
                var eventQuery = await DockerAsync([
                    "events",
                    "--since", containerEventWindowStartedAt!.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    "--until", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    "--filter", "type=container",
                    "--filter", $"label={DockerContainerSpec.StackLabel}={stackId}",
                    "--format", "{{json .}}",
                ]);
                Assert.Equal(0, eventQuery.ExitCode);
                return eventQuery.StandardOutput
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(line =>
                    {
                        using var eventDocument = System.Text.Json.JsonDocument.Parse(line);
                        var eventRoot = eventDocument.RootElement;
                        var action = eventRoot.TryGetProperty("Action", out var actionElement)
                            ? actionElement.GetString()
                            : eventRoot.GetProperty("status").GetString();
                        var actor = eventRoot.GetProperty("Actor");
                        var attributes = actor.GetProperty("Attributes");
                        var resource = attributes.TryGetProperty(DockerContainerSpec.ResourceLabel, out var resourceElement)
                            ? resourceElement.GetString()
                            : null;
                        var exitCode = attributes.TryGetProperty("exitCode", out var exitCodeElement)
                            ? exitCodeElement.GetString()
                            : null;
                        var containerId = actor.TryGetProperty("ID", out var idElement) ? idElement.GetString() : null;
                        return (Action: action, Resource: resource, ExitCode: exitCode, ContainerId: containerId);
                    })
                    .Where(entry => entry.Resource == "crane")
                    .ToArray();
            }

            var craneEvents = await ReadCraneEventsAsync();
            while (!craneEvents.Any(entry => entry.Action == "die" && entry.ContainerId == craneId))
            {
                var waitRemaining = Remaining(workBudget, "Crane die event");
                await Task.Delay(waitRemaining < TimeSpan.FromMilliseconds(250) ? waitRemaining : TimeSpan.FromMilliseconds(250));
                craneEvents = await ReadCraneEventsAsync();
            }

            Assert.Single(craneEvents, entry => entry.Action == "create");
            Assert.Single(craneEvents, entry => entry.Action == "start");
            var dieEvent = Assert.Single(craneEvents, entry => entry.Action == "die");
            Assert.Equal(craneId, dieEvent.ContainerId);
            output.WriteLine($"failureObservedBeforeStop=true; stack={stackId}; containerEventWindowStartedAt={containerEventWindowStartedAt:O}; craneFullId={craneId}; craneDockerState={craneState.Value.Status}; craneExitCode={craneState.Value.ExitCode}; craneRunning={craneState.Value.Running}; craneAspireState={craneNotification}; craneEvents={System.Text.Json.JsonSerializer.Serialize(craneEvents.Select(entry => new { entry.Action, entry.Resource, entry.ExitCode, entry.ContainerId }))}; otherResources={System.Text.Json.JsonSerializer.Serialize(otherResources.Select(entry => new { Resource = entry.Key, entry.Value.Id, entry.Value.Status, entry.Value.Running }))}; startTaskCompleted={startTask.IsCompleted}");
            Assert.Equal("82", dieEvent.ExitCode);
            Assert.Equal("exited", craneState.Value.Status);
            Assert.Equal(82, craneState.Value.ExitCode);
            Assert.False(craneState.Value.Running);
            Assert.True(await IsSentinelRunningAsync());

            if (startTask.IsCompleted)
            {
                try
                {
                    await startTask;
                    output.WriteLine("appHostStartTask=completed; Crane resource failure is proven separately by exited/82 state");
                }
                catch (Exception exception)
                {
                    output.WriteLine($"appHostStartTask=faulted; exception={exception.GetType().FullName}: {exception.Message}");
                }
            }
            else
            {
                output.WriteLine("appHostStartTask=pending when Crane exit 82 was observed; no cancellation was requested");
            }
        }
        finally
        {
            var cleanupFailures = new List<string>();

            async Task AttemptCleanupAsync(string name, Func<Task> action)
            {
                try
                {
                    await action();
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add($"{name}: {exception.GetType().FullName}: {exception.Message}");
                    output.WriteLine($"cleanupAttemptFailed={name}; exception={exception.GetType().FullName}: {exception.Message}");
                }
            }

            try
            {
                if (application is not null)
                {
                    if (startAttempted)
                    {
                        await AttemptCleanupAsync("AppHost.StopAsync", async () =>
                        {
                            using var stopTimeout = new CancellationTokenSource(CleanupOperationTimeout(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(90)));
                            await application.StopAsync(stopTimeout.Token);
                        });
                    }

                    await AttemptCleanupAsync("AppHost.DisposeAsync", async () =>
                        await application.DisposeAsync().AsTask().WaitAsync(CleanupOperationTimeout(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60))));
                }

                if (startTask is not null)
                    await AttemptCleanupAsync("AppHost.StartTaskJoin", async () =>
                    {
                        try
                        {
                            await startTask.WaitAsync(CleanupOperationTimeout(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(45)));
                        }
                        catch (Exception exception)
                        {
                            output.WriteLine($"appHostStartTaskAfterStop={exception.GetType().FullName}: {exception.Message}");
                        }
                    });

                if (appHostBuilder is not null)
                    await AttemptCleanupAsync("AppHostBuilder.DisposeAsync", async () =>
                        await appHostBuilder.DisposeAsync().AsTask().WaitAsync(CleanupOperationTimeout(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30))));

                if (dockerReady)
                {
                    if (sentinelContainerId is not null)
                    {
                        await AttemptCleanupAsync("Pre-rescue ownership observations", async () =>
                        {
                            var remainingOwnedIds = await GetOwnedContainerIdsAsync(stackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                            var sentinelStillRunning = await IsSentinelRunningAsync(cleanup: true);
                            output.WriteLine($"failurePostStopPreRescueOwnedIds={string.Join(',', remainingOwnedIds)}; sentinelSameFullIdRunning={sentinelStillRunning}; sentinelFullId={sentinelContainerId}");
                            Assert.Empty(remainingOwnedIds);
                            Assert.True(sentinelStillRunning, "The same full-ID sentinel must still be running before its own cleanup.");
                        });
                    }

                    await AttemptCleanupAsync("Stack-owned rescue cleanup", async () =>
                        await RemoveOnlySecondStackContainersAsync(stackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout));
                    await AttemptCleanupAsync("Stack-owned final inventory", async () =>
                    {
                        var remainingAfterRescue = await GetOwnedContainerIdsAsync(stackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                        output.WriteLine($"failurePostRescueOwnedIds={string.Join(',', remainingAfterRescue)}");
                        Assert.Empty(remainingAfterRescue);
                    });

                    if (sentinelContainerId is null && sentinelCreationAttempted)
                    {
                        await AttemptCleanupAsync("Sentinel identity lookup", async () =>
                        {
                            var lookup = await DockerAsync(["inspect", "--format", "{{.Id}}", sentinelName], cleanup: true);
                            if (lookup.ExitCode == 0)
                                sentinelContainerId = lookup.StandardOutput.Trim();
                        });
                    }
                    if (sentinelContainerId is not null)
                    {
                        await AttemptCleanupAsync("Sentinel ownership verification", async () =>
                        {
                            await AssertStackOwnedContainerAsync(sentinelContainerId, sentinelStackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                        });
                    }

                    if (sentinelCreationAttempted)
                        await AttemptCleanupAsync("Sentinel-owned rescue cleanup", async () =>
                            await RemoveOnlySecondStackContainersAsync(sentinelStackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout));

                    await AttemptCleanupAsync("Sentinel final inventory", async () =>
                    {
                        var remainingSentinelIds = await GetOwnedContainerIdsAsync(sentinelStackId, dockerExecutable, CleanupCommandTimeout, ProcessKillWaitTimeout);
                        output.WriteLine($"failureSentinelAfterCleanup={remainingSentinelIds.Length}; finishedAt={DateTimeOffset.UtcNow:O}; elapsed={acceptanceClock.Elapsed}; totalBudget={totalBudget}");
                        Assert.Empty(remainingSentinelIds);
                    });

                    await AttemptCleanupAsync("Baseline inventory verification", async () =>
                    {
                        var afterIds = await GetAllContainerIdsAsync(cleanup: true);
                        Assert.All(baselineContainerIds, id => Assert.Contains(id, afterIds));
                    });
                }
            }
            finally
            {
                try
                {
                    await AttemptCleanupAsync("Staged AppHost asset cleanup", () =>
                    {
                        CleanupStagedAppHostAssets(stagedAssets);
                        return Task.CompletedTask;
                    });
                }
                finally
                {
                    await AttemptCleanupAsync("Docker process PATH restoration", () =>
                    {
                        dockerPathScope.Dispose();
                        return Task.CompletedTask;
                    });
                }
            }

            Assert.True(cleanupFailures.Count == 0, string.Join(Environment.NewLine, cleanupFailures));
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
            var craneOverlaySource = ResolveCraneLaunchOverlaySource(Projects.Duck_Testing_AppHost.ProjectPath);
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

    private static string ResolveCraneLaunchOverlaySource(string appHostProjectPath)
    {
        var testOutput = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        var targetFramework = testOutput.Name;
        var configuration = testOutput.Parent?.Name;
        var configurations = new[] { configuration, "Debug", "Release" }
            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal);
        foreach (var candidateConfiguration in configurations)
        {
            var candidate = Path.Combine(appHostProjectPath, "bin", candidateConfiguration!, targetFramework,
                "crane-launch-overlay");
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }
        throw new FileNotFoundException(
            $"The Crane launch helper was not found in AppHost output for {targetFramework}/{configuration}.");
    }

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
            Stage(craneOverlaySource, "crane-launch-overlay");
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
            if (assetName == "crane-launch-overlay" && !OperatingSystem.IsWindows())
                File.SetUnixFileMode(destination, File.GetUnixFileMode(source));
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

    private static TimeSpan GetPositiveCommandTimeout(TimeSpan available, TimeSpan maximum, string expiredMessage)
    {
        if (available <= TimeSpan.Zero)
        {
            throw new TimeoutException(expiredMessage);
        }

        return available < maximum ? available : maximum;
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
