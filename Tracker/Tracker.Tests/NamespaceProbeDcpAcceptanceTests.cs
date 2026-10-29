using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Tracker.Tests;

public sealed class NamespaceProbeDcpAcceptanceTests(ITestOutputHelper output)
{
    private static readonly TimeSpan ProbeDeadline = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan OperationBudget = TimeSpan.FromSeconds(45);
    private const string ExpectedContext = "desktop-linux";
    internal const string GateVariableName = "DUCK_I81_NAMESPACE_DCP_PROBE_ENABLED";
    private const string EngineVariable = "DUCK_I81_NAMESPACE_DCP_EXPECTED_ENGINE_ID";
    private const string RunIdVariable = "DUCK_I81_NAMESPACE_DCP_RUN_ID";

    [Theory]
    [InlineData(null)]
    public void MissingNativeProbeGateDoesNotEnableTheFixture(string? value)
    {
        Assert.False(NamespaceProbeTestSettings.Parse(value, null, null).Enabled);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("enabled-ish")]
    public void NativeProbeGateRejectsPresentValuesOtherThanTrue(string value)
    {
        Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeTestSettings.Parse(value, null, null));
    }

    [Theory]
    [InlineData(null, "run_01")]
    [InlineData("engine", null)]
    [InlineData("engine", "bad/run")]
    public void EnabledNativeProbeRequiresAnExactTargetAndSafeRunId(
        string? engineId,
        string? runId)
    {
        Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeTestSettings.Parse("true", engineId, runId));
    }

    [Fact]
    public void EnabledNativeProbeAcceptsOnlyTheApprovedContextAndExplicitIdentity()
    {
        var settings = NamespaceProbeTestSettings.Parse("true", "expected-engine", "run_01");

        Assert.True(settings.Enabled);
        Assert.Equal(ExpectedContext, settings.Context);
        Assert.Equal("expected-engine", settings.ExpectedEngineId);
        Assert.Equal("run_01", settings.RunId);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(44, 16)]
    [InlineData(45, 15)]
    [InlineData(60, 0)]
    public void CleanupBudgetReservesOnlyTheRemainingProbeDeadline(int elapsedSeconds, int expectedSeconds)
    {
        Assert.Equal(
            TimeSpan.FromSeconds(expectedSeconds),
            NamespaceProbeCleanupBudget.Remaining(TimeSpan.FromSeconds(elapsedSeconds)));
    }

    [Fact]
    public async Task ReadOnlyPreflightRejectsAnEffectiveEngineMismatchBeforeInspectingImagesOrContainers()
    {
        var docker = new FakeReadOnlyDockerCli(
            (true, ["info", "--format", "{{.ID}}"], "expected-engine"),
            (false, ["info", "--format", "{{.ID}}"], "other-engine"),
            (false, ["context", "show"], "desktop-linux"));
        var settings = NamespaceProbeTestSettings.Parse("true", "expected-engine", "run_01");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VerifyReadOnlyPreflightAsync(docker, settings, null, null, CancellationToken.None));

        Assert.DoesNotContain(docker.Calls, call => call.Arguments.Contains("image", StringComparer.Ordinal));
        Assert.DoesNotContain(docker.Calls, call => call.Arguments.Contains("ps", StringComparer.Ordinal));
        Assert.All(docker.Calls, call => Assert.True(FakeReadOnlyDockerCli.IsReadOnly(call.Arguments)));
    }

    [Fact]
    public async Task ReadOnlyPreflightRejectsMissingPinnedCacheBeforeCheckingRunIdCollision()
    {
        var docker = new FakeReadOnlyDockerCli(
            (true, ["info", "--format", "{{.ID}}"], "expected-engine"),
            (false, ["info", "--format", "{{.ID}}"], "expected-engine"),
            (false, ["context", "show"], "desktop-linux"),
            (true, ["image", "inspect", "--format", "{{json .RepoDigests}}", ProbeTestArgument.Any], ""));
        var settings = NamespaceProbeTestSettings.Parse("true", "expected-engine", "run_01");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VerifyReadOnlyPreflightAsync(docker, settings, null, null, CancellationToken.None));

        Assert.DoesNotContain(docker.Calls, call => call.Arguments.Contains("ps", StringComparer.Ordinal));
        Assert.All(docker.Calls, call => Assert.True(FakeReadOnlyDockerCli.IsReadOnly(call.Arguments)));
    }

    [Fact]
    public async Task ReadOnlyPreflightRejectsRunIdCollisionWithoutAnyMutationCommand()
    {
        var docker = new FakeReadOnlyDockerCli(
            (true, ["info", "--format", "{{.ID}}"], "expected-engine"),
            (false, ["info", "--format", "{{.ID}}"], "expected-engine"),
            (false, ["context", "show"], "desktop-linux"),
            (true, ["image", "inspect", "--format", "{{json .RepoDigests}}", ProbeTestArgument.Any], $"[{System.Text.Json.JsonSerializer.Serialize($"{NamespaceProbeModelFactory.AspNetImage}@sha256:{NamespaceProbeModelFactory.AspNetImageSha256}")}]"),
            (true, ["ps", "--all", "--quiet", "--filter", $"label={ContainerIdentityExecHandoff.RunIdLabel}=run_01"], "existing-id"));
        var settings = NamespaceProbeTestSettings.Parse("true", "expected-engine", "run_01");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VerifyReadOnlyPreflightAsync(docker, settings, null, null, CancellationToken.None));

        Assert.All(docker.Calls, call => Assert.True(FakeReadOnlyDockerCli.IsReadOnly(call.Arguments)));
    }

    [Theory]
    [InlineData("unix:///somewhere", null)]
    [InlineData(null, "default")]
    public void DockerEnvironmentOverridesAreRejectedWithoutChangingTheEnvironment(string? dockerHost, string? dockerContext)
    {
        Assert.Throws<InvalidOperationException>(() =>
            NamespaceProbeDockerSelection.ValidateOverrides(ExpectedContext, dockerHost, dockerContext));
    }

    [Fact]
    public async Task CleanupDoesNotStopOrDisposeWhenTheApprovedEngineCheckFails()
    {
        var stopCalls = 0;
        var disposeCalls = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var result = await NamespaceProbeCleanup.RunOnlyAfterEngineCheckAsync(
            _ => Task.FromException(new InvalidOperationException("wrong engine")),
            _ =>
            {
                stopCalls++;
                return Task.CompletedTask;
            },
            _ =>
            {
                disposeCalls++;
                return Task.CompletedTask;
            },
            timeout.Token);

        Assert.False(result.EngineVerified);
        Assert.IsType<InvalidOperationException>(result.EngineCheckFailure);
        Assert.Equal(0, stopCalls);
        Assert.Equal(0, disposeCalls);
    }

    [Fact]
    public async Task CleanupDeadlineExpiryDoesNotInvokeReadOrLifecycleCallbacks()
    {
        var verifyCalls = 0;
        var stopCalls = 0;
        var disposeCalls = 0;
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        var result = await NamespaceProbeCleanup.RunOnlyAfterEngineCheckAsync(
            _ =>
            {
                verifyCalls++;
                return Task.CompletedTask;
            },
            _ =>
            {
                stopCalls++;
                return Task.CompletedTask;
            },
            _ =>
            {
                disposeCalls++;
                return Task.CompletedTask;
            },
            timeout.Token);

        Assert.False(result.EngineVerified);
        Assert.IsAssignableFrom<OperationCanceledException>(result.EngineCheckFailure);
        Assert.Equal(0, verifyCalls);
        Assert.Equal(0, stopCalls);
        Assert.Equal(0, disposeCalls);
    }

    [Fact]
    public async Task NotificationMonitorKeepsOtherResourceEventsSeenBeforeTheRequestedOwner()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var owner = builder.AddContainer("monitor-owner", "example/image").Resource;
        var follower = builder.AddContainer("monitor-follower", "example/image").Resource;
        var followerId = new string('b', 64);
        var ownerId = new string('a', 64);
        var adapter = new SequenceOwnedReconfigurationAdapter(
        [
            new OwnedResourceObservation(follower, "follower-key", KnownResourceStates.Running, followerId),
            new OwnedResourceObservation(owner, "owner-key", KnownResourceStates.Running, ownerId),
        ]);
        await using var monitor = new NamespaceProbeNotificationMonitor(adapter);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var ownerObservation = await monitor.WaitForRunningAsync(owner, null, timeout.Token);
        var followerObservation = await monitor.WaitForRunningAsync(follower, null, timeout.Token);

        Assert.Equal(ownerId, ownerObservation.ContainerId);
        Assert.Equal(followerId, followerObservation.ContainerId);
    }

    [Fact]
    public async Task NotificationMonitorRejectsThePriorGenerationAndReturnsTheNextFullId()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var owner = builder.AddContainer("monitor-owner", "example/image").Resource;
        var previousId = new string('a', 64);
        var nextId = new string('b', 64);
        var adapter = new SequenceOwnedReconfigurationAdapter(
        [
            new OwnedResourceObservation(owner, "owner-key", KnownResourceStates.Running, previousId),
            new OwnedResourceObservation(owner, "owner-key", KnownResourceStates.Stopping, previousId),
            new OwnedResourceObservation(owner, "owner-next-key", KnownResourceStates.Running, nextId),
        ]);
        await using var monitor = new NamespaceProbeNotificationMonitor(adapter);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var observation = await monitor.WaitForRunningAsync(owner, previousId, timeout.Token);

        Assert.Equal(nextId, observation.ContainerId);
        Assert.Equal("owner-next-key", observation.ResourceId);
    }

    [NativeNamespaceProbeFact]
    public async Task NativeNamespaceProbeRecreatesOnlyItsTwoOwnedContainersOnce()
    {
        var settings = NamespaceProbeTestSettings.Parse(
            Environment.GetEnvironmentVariable(GateVariableName),
            Environment.GetEnvironmentVariable(EngineVariable),
            Environment.GetEnvironmentVariable(RunIdVariable));
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("The native probe fact was not skipped even though its gate is absent.");
        }

        await RunNativeProbeAsync(settings);
    }

    private async Task RunNativeProbeAsync(NamespaceProbeTestSettings settings)
    {
        var stopwatch = Stopwatch.StartNew();
        using var operationDeadline = new CancellationTokenSource(OperationBudget);
        var cancellationToken = operationDeadline.Token;
        var ownedContainerIds = new HashSet<string>(StringComparer.Ordinal);
        DistributedApplication? application = null;
        StackOwnershipLease? ownershipLease = null;
        ResourceNotificationService? notifications = null;
        ResourceCommandService? commands = null;
        NamespaceProbeNotificationMonitor? monitor = null;
        Exception? operationFailure = null;
        Exception? cleanupFailure = null;
        var dockerExecutable = GetDockerExecutablePath();
        var publishDirectory = GetExistingPublishDirectory();

        try
        {
            var docker = new ReadOnlyDockerCli(dockerExecutable, settings.Context);
            Record("preflight.started", new { context = settings.Context, runId = settings.RunId });
            await VerifyReadOnlyPreflightAsync(
                docker,
                settings,
                Environment.GetEnvironmentVariable("DOCKER_HOST"),
                Environment.GetEnvironmentVariable("DOCKER_CONTEXT"),
                cancellationToken).ConfigureAwait(false);
            Record("preflight.passed", new { engineId = settings.ExpectedEngineId, imageDigest = NamespaceProbeModelFactory.AspNetImageSha256 });

            ownershipLease = StackOwnershipLease.Acquire(
                Path.Combine(Path.GetTempPath(), "duck-aspire-stack.lock"));

            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                Args = [],
                AssemblyName = typeof(Projects.Duck_Testing_AppHost).Assembly.GetName().Name,
                ProjectDirectory = Projects.Duck_Testing_AppHost.ProjectPath,
                DisableDashboard = true,
            });
            var model = await NamespaceProbeModelFactory.AddResourcesAndProjectAsync(
                builder,
                settings.RunId,
                publishDirectory,
                cancellationToken).ConfigureAwait(false);
            Assert.Equal(2, model.Resources.Count);

            var owner = Assert.IsType<ContainerResource>(Assert.Single(
                builder.Resources,
                resource => resource.Name == NamespaceProbeModelFactory.OwnerResourceName));
            var follower = Assert.IsType<ContainerResource>(Assert.Single(
                builder.Resources,
                resource => resource.Name == NamespaceProbeModelFactory.FollowerResourceName));
            application = builder.Build();
            notifications = application.Services.GetRequiredService<ResourceNotificationService>();
            commands = application.Services.GetRequiredService<ResourceCommandService>();
            var identityAdapter = new AspireOwnedReconfigurationAdapter(
                notifications,
                commands,
                new DockerCliCommandRunner(dockerExecutable),
                settings.Context);
            monitor = new NamespaceProbeNotificationMonitor(identityAdapter);

            Record("apphost.starting", new { resources = 2, dashboardDisabled = true });
            await application.StartAsync(cancellationToken).ConfigureAwait(false);
            Record("apphost.started", new { });

            var ownerInitial = await monitor.WaitForRunningAsync(owner, null, cancellationToken).ConfigureAwait(false);
            ownedContainerIds.Add(ownerInitial.ContainerId!);
            Record("resource.running", new { role = NamespaceProbeModelFactory.OwnerRole, resourceId = ownerInitial.ResourceId, containerId = ownerInitial.ContainerId });
            var followerInitial = await monitor.WaitForRunningAsync(follower, null, cancellationToken).ConfigureAwait(false);
            ownedContainerIds.Add(followerInitial.ContainerId!);
            Record("resource.running", new { role = NamespaceProbeModelFactory.FollowerRole, resourceId = followerInitial.ResourceId, containerId = followerInitial.ContainerId });
            await AssertOwnedRunningAsync(docker, settings, ownerInitial, NamespaceProbeModelFactory.OwnerRole, cancellationToken)
                .ConfigureAwait(false);
            await AssertOwnedRunningAsync(docker, settings, followerInitial, NamespaceProbeModelFactory.FollowerRole, cancellationToken)
                .ConfigureAwait(false);
            await AssertOwnerEndpointIsLoopbackOnlyAsync(docker, ownerInitial.ContainerId!, cancellationToken).ConfigureAwait(false);
            await AssertFollowerSharesCurrentOwnerNamespaceAsync(
                docker,
                followerInitial.ContainerId!,
                ownerInitial.ContainerId!,
                $"duck-namespace-probe-owner-{settings.RunId}",
                cancellationToken).ConfigureAwait(false);
            await AssertFollowerHasNoPublishedHostPortsAsync(docker, followerInitial.ContainerId!, cancellationToken)
                .ConfigureAwait(false);

            Record("follower.stop.started", new { containerId = followerInitial.ContainerId });
            await AssertEngineIdentityAsync(docker, settings, cancellationToken).ConfigureAwait(false);
            await ExecuteResourceCommandAsync(commands, follower, KnownResourceCommands.StopCommand, cancellationToken)
                .ConfigureAwait(false);
            Record("follower.stop.accepted", new { });
            var followerStopped = await monitor.WaitForStoppedAsync(
                follower,
                followerInitial.ContainerId!,
                cancellationToken).ConfigureAwait(false);
            Record("follower.stopped", new { resourceId = followerStopped.ResourceId, containerId = followerStopped.ContainerId, state = followerStopped.State });
            if (!string.Equals(followerStopped.ContainerId, followerInitial.ContainerId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The follower did not reach a terminal state for its originally owned container ID.");
            }

            Record("owner.restart.started", new { containerId = ownerInitial.ContainerId });
            await AssertEngineIdentityAsync(docker, settings, cancellationToken).ConfigureAwait(false);
            await ExecuteResourceCommandAsync(commands, owner, KnownResourceCommands.RestartCommand, cancellationToken)
                .ConfigureAwait(false);
            Record("owner.restart.accepted", new { });
            var ownerRestarted = await monitor.WaitForRunningAsync(
                owner,
                ownerInitial.ContainerId,
                cancellationToken).ConfigureAwait(false);
            ownedContainerIds.Add(ownerRestarted.ContainerId!);
            Record("resource.running", new { role = NamespaceProbeModelFactory.OwnerRole, resourceId = ownerRestarted.ResourceId, containerId = ownerRestarted.ContainerId });
            await AssertOwnedRunningAsync(docker, settings, ownerRestarted, NamespaceProbeModelFactory.OwnerRole, cancellationToken)
                .ConfigureAwait(false);
            await AssertOwnerEndpointIsLoopbackOnlyAsync(docker, ownerRestarted.ContainerId!, cancellationToken).ConfigureAwait(false);
            await VerifyOwnerTcpConnectionAsync(docker, ownerRestarted.ContainerId!, cancellationToken).ConfigureAwait(false);

            Record("follower.start.started", new { previousContainerId = followerInitial.ContainerId, ownerContainerId = ownerRestarted.ContainerId });
            await AssertEngineIdentityAsync(docker, settings, cancellationToken).ConfigureAwait(false);
            await ExecuteResourceCommandAsync(commands, follower, KnownResourceCommands.StartCommand, cancellationToken)
                .ConfigureAwait(false);
            Record("follower.start.accepted", new { });
            var followerRestarted = await monitor.WaitForRunningAsync(
                follower,
                followerInitial.ContainerId,
                cancellationToken).ConfigureAwait(false);
            ownedContainerIds.Add(followerRestarted.ContainerId!);
            Record("resource.running", new { role = NamespaceProbeModelFactory.FollowerRole, resourceId = followerRestarted.ResourceId, containerId = followerRestarted.ContainerId });
            await AssertOwnedRunningAsync(docker, settings, followerRestarted, NamespaceProbeModelFactory.FollowerRole, cancellationToken)
                .ConfigureAwait(false);
            await AssertFollowerSharesCurrentOwnerNamespaceAsync(
                docker,
                followerRestarted.ContainerId!,
                ownerRestarted.ContainerId!,
                $"duck-namespace-probe-owner-{settings.RunId}",
                cancellationToken).ConfigureAwait(false);
            await AssertFollowerHasNoPublishedHostPortsAsync(docker, followerRestarted.ContainerId!, cancellationToken)
                .ConfigureAwait(false);

            Assert.Equal(4, ownedContainerIds.Count);
        }
        catch (Exception exception)
        {
            operationFailure = exception;
            Record("operation.failed", new { exceptionType = exception.GetType().FullName, message = exception.Message, elapsedMilliseconds = stopwatch.ElapsedMilliseconds });
        }
        finally
        {
            var remaining = NamespaceProbeCleanupBudget.Remaining(stopwatch.Elapsed);
            if (remaining <= TimeSpan.Zero)
            {
                cleanupFailure ??= new TimeoutException("The 60-second probe deadline expired before DCP cleanup could be confirmed.");
            }
            else
            {
                using var cleanupDeadline = new CancellationTokenSource(remaining);
                Record("cleanup.started", new { remainingMilliseconds = (long)remaining.TotalMilliseconds, observedContainerGenerations = ownedContainerIds.Count });

                if (monitor is not null)
                {
                    try
                    {
                        await monitor.DisposeAsync().AsTask().WaitAsync(cleanupDeadline.Token).ConfigureAwait(false);
                        Record("monitor.stopped", new { });
                    }
                    catch (Exception exception)
                    {
                        cleanupFailure ??= exception;
                        Record("monitor.stop-failed", new { exceptionType = exception.GetType().FullName, message = exception.Message });
                    }
                }

                if (cleanupDeadline.IsCancellationRequested)
                {
                    cleanupFailure ??= new TimeoutException("The cleanup deadline expired before the approved Engine check and DCP cleanup.");
                    Record("cleanup.not-started-deadline", new { elapsedMilliseconds = stopwatch.ElapsedMilliseconds });
                }
                else if (application is not null)
                {
                    var cleanupResult = await NamespaceProbeCleanup.RunOnlyAfterEngineCheckAsync(
                        token => AssertEngineIdentityAsync(
                            new ReadOnlyDockerCli(dockerExecutable, settings.Context),
                            settings,
                            token),
                        async token =>
                        {
                            try
                            {
                                await application.StopAsync(token).WaitAsync(token).ConfigureAwait(false);
                                Record("apphost.stopped", new { });
                            }
                            catch (Exception exception)
                            {
                                cleanupFailure ??= exception;
                                Record("apphost.stop-failed", new { exceptionType = exception.GetType().FullName, message = exception.Message });
                            }
                        },
                        async token =>
                        {
                            try
                            {
                                await application.DisposeAsync().AsTask().WaitAsync(token).ConfigureAwait(false);
                                Record("apphost.disposed", new { });
                            }
                            catch (Exception exception)
                            {
                                cleanupFailure ??= exception;
                                Record("apphost.dispose-failed", new { exceptionType = exception.GetType().FullName, message = exception.Message });
                            }
                        },
                        cleanupDeadline.Token).ConfigureAwait(false);

                    if (cleanupResult.EngineVerified && !cleanupDeadline.IsCancellationRequested)
                    {
                        try
                        {
                            var docker = new ReadOnlyDockerCli(dockerExecutable, settings.Context);
                            await AssertEngineIdentityAsync(docker, settings, cleanupDeadline.Token).ConfigureAwait(false);

                            var inventory = await docker.RunAsync(
                            [
                                "ps", "--all", "--quiet",
                                "--filter", $"label={ContainerIdentityExecHandoff.RunIdLabel}={settings.RunId}",
                            ], cleanupDeadline.Token).ConfigureAwait(false);
                            if (inventory.ExitCode != 0)
                            {
                                throw new IOException("The run-label inventory could not verify DCP cleanup.");
                            }

                            var remainingIds = inventory.StandardOutput
                                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            Record("cleanup.inventory", new { exitCode = inventory.ExitCode, remainingContainerIds = remainingIds, observedContainerGenerations = ownedContainerIds.Count });
                            foreach (var remainingId in remainingIds)
                            {
                                var inspect = await docker.RunAsync(["inspect", "--format", "{{json .}}", remainingId], cleanupDeadline.Token)
                                    .ConfigureAwait(false);
                                if (!OwnedContainerInspection.IsFullId(remainingId) ||
                                    inspect.ExitCode != 0 ||
                                    !HasOwnedProbeLabels(inspect.StandardOutput, remainingId, settings.RunId))
                                {
                                    throw new InvalidOperationException("A remaining run-label container could not be confirmed as an owned probe resource.");
                                }

                                Record("cleanup.remaining-owned-resource", new { containerId = remainingId });
                            }

                            if (remainingIds.Length > 0)
                            {
                                throw new InvalidOperationException("DCP cleanup left run-labeled probe resources behind; no manual removal was attempted.");
                            }

                            Record("cleanup.verified", new { remainingOwnedResources = 0 });
                        }
                        catch (Exception exception)
                        {
                            cleanupFailure ??= exception;
                            Record("cleanup.verification-failed", new { exceptionType = exception.GetType().FullName, message = exception.Message, elapsedMilliseconds = stopwatch.ElapsedMilliseconds });
                        }
                    }
                    else if (!cleanupResult.EngineVerified)
                    {
                        cleanupFailure ??= cleanupResult.EngineCheckFailure ?? new InvalidOperationException("The approved Engine/context changed before cleanup.");
                        Record("cleanup.skipped-engine-mismatch", new { exceptionType = cleanupResult.EngineCheckFailure?.GetType().FullName, message = cleanupResult.EngineCheckFailure?.Message, elapsedMilliseconds = stopwatch.ElapsedMilliseconds });
                    }
                    else
                    {
                        cleanupFailure ??= new TimeoutException("The cleanup deadline expired before owned-resource inventory verification.");
                        Record("cleanup.inventory-not-verified", new { elapsedMilliseconds = stopwatch.ElapsedMilliseconds });
                    }
                }
                else if (monitor is not null)
                {
                    cleanupFailure ??= new TimeoutException("The cleanup deadline expired before owned-resource inventory verification.");
                    Record("cleanup.inventory-not-verified", new { elapsedMilliseconds = stopwatch.ElapsedMilliseconds });
                }
            }

            ownershipLease?.Dispose();
            Record("probe.finished", new { elapsedMilliseconds = stopwatch.ElapsedMilliseconds, observedContainerGenerations = ownedContainerIds.Count, cleanupVerified = cleanupFailure is null });
        }

        if (operationFailure is not null || cleanupFailure is not null)
        {
            throw new InvalidOperationException(
                $"The bounded native namespace probe did not complete cleanly. Owned container generations observed: {ownedContainerIds.Count}; elapsed: {stopwatch.Elapsed}. Cleanup must be reviewed before any retry.",
                operationFailure ?? cleanupFailure);
        }
    }

    internal async Task VerifyReadOnlyPreflightAsync(
        IReadOnlyDockerCli docker,
        NamespaceProbeTestSettings settings,
        string? dockerHostOverride,
        string? dockerContextOverride,
        CancellationToken cancellationToken)
    {
        var engineId = await AssertEngineIdentityAsync(docker, settings, cancellationToken).ConfigureAwait(false);
        NamespaceProbeDockerSelection.ValidateOverrides(settings.Context, dockerHostOverride, dockerContextOverride);
        Record("preflight.engine-identity", new { engineId, context = settings.Context });

        var imageReference = $"{NamespaceProbeModelFactory.AspNetImage}@sha256:{NamespaceProbeModelFactory.AspNetImageSha256}";
        var image = await docker.RunAsync(
            ["image", "inspect", "--format", "{{json .RepoDigests}}", imageReference],
            cancellationToken).ConfigureAwait(false);
        if (image.ExitCode != 0 || !image.StandardOutput.Contains(imageReference, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The exact pinned ASP.NET image digest is not available in the local cache.");
        }
        Record("preflight.cached-image", new { exitCode = image.ExitCode, imageReference });

        var collisions = await docker.RunAsync(
            [
                "ps", "--all", "--quiet",
                "--filter", $"label={ContainerIdentityExecHandoff.RunIdLabel}={settings.RunId}",
            ],
            cancellationToken).ConfigureAwait(false);
        if (collisions.ExitCode != 0 || !string.IsNullOrWhiteSpace(collisions.StandardOutput))
        {
            throw new InvalidOperationException("The probe run ID is already present or its owner-label preflight failed.");
        }
        Record("preflight.run-id-collision-check", new { exitCode = collisions.ExitCode, matchedContainers = 0 });
    }

    private static async Task ExecuteResourceCommandAsync(
        ResourceCommandService commands,
        IResource resource,
        string command,
        CancellationToken cancellationToken)
    {
        var result = await commands.ExecuteCommandAsync(resource, command, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException($"The DCP resource command '{command}' failed for the selected probe resource.");
        }
    }

    private async Task AssertOwnedRunningAsync(
        IReadOnlyDockerCli docker,
        NamespaceProbeTestSettings settings,
        OwnedResourceObservation observation,
        string role,
        CancellationToken cancellationToken)
    {
        if (!OwnedContainerInspection.IsFullId(observation.ContainerId))
        {
            throw new InvalidOperationException("The current probe resource did not expose a full container ID.");
        }

        var inspection = await docker.RunAsync(
            ["inspect", "--format", "{{json .}}", observation.ContainerId!],
            cancellationToken).ConfigureAwait(false);
        if (inspection.ExitCode != 0 ||
            !OwnedContainerInspection.MatchesOwner(inspection.StandardOutput, observation.ContainerId!, settings.RunId, role, requireRunning: true))
        {
            throw new InvalidOperationException("The inspected container was not the running resource owned by this probe run.");
        }

        var expectedImage = $"{NamespaceProbeModelFactory.AspNetImage}@sha256:{NamespaceProbeModelFactory.AspNetImageSha256}";
        using (var document = System.Text.Json.JsonDocument.Parse(inspection.StandardOutput))
        {
            if (!document.RootElement.TryGetProperty("Config", out var config) ||
                !config.TryGetProperty("Image", out var image) ||
                !string.Equals(image.GetString(), expectedImage, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The running owned resource did not use the exact pinned image reference.");
            }
        }
        Record("container.inspect-verified", new { role, containerId = observation.ContainerId, runId = settings.RunId, image = expectedImage });

        await AssertEngineIdentityAsync(docker, settings, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> AssertEngineIdentityAsync(
        IReadOnlyDockerCli docker,
        NamespaceProbeTestSettings settings,
        CancellationToken cancellationToken)
    {
        var selected = await docker.RunAsync(["info", "--format", "{{.ID}}"], cancellationToken).ConfigureAwait(false);
        var effective = await docker.RunDefaultAsync(["info", "--format", "{{.ID}}"], cancellationToken).ConfigureAwait(false);
        var context = await docker.RunDefaultAsync(["context", "show"], cancellationToken).ConfigureAwait(false);
        if (selected.ExitCode != 0 || effective.ExitCode != 0 || context.ExitCode != 0 ||
            !string.Equals(selected.StandardOutput.Trim(), settings.ExpectedEngineId, StringComparison.Ordinal) ||
            !string.Equals(effective.StandardOutput.Trim(), settings.ExpectedEngineId, StringComparison.Ordinal) ||
            !string.Equals(context.StandardOutput.Trim(), settings.Context, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The explicit and effective Docker selections no longer identify the approved Engine and context.");
        }

        return selected.StandardOutput.Trim();
    }

    private async Task AssertOwnerEndpointIsLoopbackOnlyAsync(
        IReadOnlyDockerCli docker,
        string containerId,
        CancellationToken cancellationToken)
    {
        using var document = await InspectJsonAsync(docker, containerId, cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (!root.TryGetProperty("HostConfig", out var hostConfig) ||
            !hostConfig.TryGetProperty("PortBindings", out var bindings) ||
            !bindings.TryGetProperty($"{NamespaceProbeModelFactory.OwnerPort}/tcp", out var entries) ||
            entries.ValueKind != System.Text.Json.JsonValueKind.Array || entries.GetArrayLength() != 1)
        {
            throw new InvalidOperationException("The owner container did not have exactly one dynamically published TCP endpoint.");
        }

        var binding = entries[0];
        if (binding.GetProperty("HostIp").GetString() != "127.0.0.1" ||
            !int.TryParse(binding.GetProperty("HostPort").GetString(), out var hostPort) ||
            hostPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("The owner endpoint was not bound to a dynamic localhost TCP port.");
        }

        Record("owner.endpoint-inspected", new { containerId, hostIp = "127.0.0.1", hostPort, targetPort = NamespaceProbeModelFactory.OwnerPort, protocol = "TCP" });
    }

    private async Task AssertFollowerSharesCurrentOwnerNamespaceAsync(
        IReadOnlyDockerCli docker,
        string followerId,
        string currentOwnerId,
        string ownerContainerName,
        CancellationToken cancellationToken)
    {
        var ownerNameInspection = await docker.RunAsync(["inspect", "--format", "{{.Id}}", ownerContainerName], cancellationToken)
            .ConfigureAwait(false);
        if (ownerNameInspection.ExitCode != 0 ||
            !string.Equals(ownerNameInspection.StandardOutput.Trim(), currentOwnerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The stable owner name did not resolve to the currently observed owner full ID.");
        }

        using var document = await InspectJsonAsync(docker, followerId, cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("HostConfig", out var hostConfig) ||
            !hostConfig.TryGetProperty("NetworkMode", out var modeElement) ||
            modeElement.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            throw new InvalidOperationException("The follower container did not report its network mode.");
        }

        var mode = modeElement.GetString();
        if (!string.Equals(mode, $"container:{currentOwnerId}", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mode, $"container:{ownerContainerName}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The follower was not attached to the current owner container network namespace.");
        }

        var ownerJson = await docker.RunAsync(["inspect", "--format", "{{json .NetworkSettings.Networks}}", currentOwnerId], cancellationToken)
            .ConfigureAwait(false);
        var followerNetworks = document.RootElement.GetProperty("NetworkSettings").GetProperty("Networks");
        if (ownerJson.ExitCode != 0 || followerNetworks.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            throw new InvalidOperationException("The owner/follower network settings could not be read for this generation.");
        }

        Record("follower.network-namespace-verified", new
        {
            followerContainerId = followerId,
            ownerContainerId = currentOwnerId,
            ownerName = ownerContainerName,
            networkMode = mode,
            ownerNetworks = ownerJson.StandardOutput,
            followerNetworks = followerNetworks.ToString(),
        });
    }

    private static async Task AssertFollowerHasNoPublishedHostPortsAsync(
        IReadOnlyDockerCli docker,
        string containerId,
        CancellationToken cancellationToken)
    {
        using var document = await InspectJsonAsync(docker, containerId, cancellationToken).ConfigureAwait(false);
        if (document.RootElement.TryGetProperty("HostConfig", out var hostConfig) &&
            hostConfig.TryGetProperty("PortBindings", out var bindings) &&
            bindings.ValueKind == System.Text.Json.JsonValueKind.Object &&
            bindings.EnumerateObject().Any())
        {
            throw new InvalidOperationException("The follower unexpectedly published a host port.");
        }
    }

    private static async Task<System.Text.Json.JsonDocument> InspectJsonAsync(
        IReadOnlyDockerCli docker,
        string containerId,
        CancellationToken cancellationToken)
    {
        var result = await docker.RunAsync(["inspect", "--format", "{{json .}}", containerId], cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new IOException("The owned probe container could not be inspected.");
        }

        return System.Text.Json.JsonDocument.Parse(result.StandardOutput);
    }

    private async Task VerifyOwnerTcpConnectionAsync(
        IReadOnlyDockerCli docker,
        string containerId,
        CancellationToken cancellationToken)
    {
        using var document = await InspectJsonAsync(docker, containerId, cancellationToken).ConfigureAwait(false);
        var bindings = document.RootElement.GetProperty("HostConfig").GetProperty("PortBindings");
        var hostPort = int.Parse(bindings.GetProperty($"{NamespaceProbeModelFactory.OwnerPort}/tcp")[0].GetProperty("HostPort").GetString()!);
        var deadline = Stopwatch.StartNew();
        Exception? lastFailure = null;
        while (deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync(System.Net.IPAddress.Loopback, hostPort, cancellationToken).ConfigureAwait(false);
                Record("owner.localhost-tcp-connected", new { containerId, hostIp = "127.0.0.1", hostPort, protocol = "TCP", applicationProtocolTested = false });
                return;
            }
            catch (System.Net.Sockets.SocketException exception)
            {
                lastFailure = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException("A localhost TCP connection to the dynamically published owner endpoint was not established.", lastFailure);
    }

    private void Record(string stage, object details) => output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        timestampUtc = DateTimeOffset.UtcNow,
        stage,
        details,
    }));

    private static bool HasOwnedProbeLabels(string json, string containerId, string runId)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var root = document.RootElement;
            var labels = root.GetProperty("Config").GetProperty("Labels");
            return string.Equals(root.GetProperty("Id").GetString(), containerId, StringComparison.Ordinal) &&
                labels.TryGetProperty(ContainerIdentityExecHandoff.RunIdLabel, out var actualRunId) &&
                string.Equals(actualRunId.GetString(), runId, StringComparison.Ordinal) &&
                labels.TryGetProperty(ContainerIdentityExecHandoff.RoleLabel, out var role) &&
                (string.Equals(role.GetString(), NamespaceProbeModelFactory.OwnerRole, StringComparison.Ordinal) ||
                 string.Equals(role.GetString(), NamespaceProbeModelFactory.FollowerRole, StringComparison.Ordinal));
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return false;
        }
    }

    private static string GetDockerExecutablePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var path = Path.Combine(localAppData, "Programs", "DockerDesktop", "resources", "bin", "docker.exe");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("The existing Docker Desktop CLI executable was not found at its installed path.");
        }

        return path;
    }

    private static string GetExistingPublishDirectory()
    {
        var repositoryRoot = FindRepositoryRoot();
        return Path.Combine(
            repositoryRoot,
            "Testing", "Duck.Testing.RefereeDriver.Host", "bin", "Release", "net10.0", "publish");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Testing", "Duck.Testing.AppHost", "Duck.Testing.AppHost.csproj")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root could not be located from the test output directory.");
    }
}

internal sealed record NamespaceProbeTestSettings(
    bool Enabled,
    string Context,
    string ExpectedEngineId,
    string RunId)
{
    internal static NamespaceProbeTestSettings Parse(string? enabledValue, string? expectedEngineId, string? runId)
    {
        if (enabledValue is null)
        {
            return new NamespaceProbeTestSettings(false, string.Empty, string.Empty, string.Empty);
        }

        if (!bool.TryParse(enabledValue, out var enabled) || !enabled)
        {
            throw new InvalidOperationException($"{NamespaceProbeDcpAcceptanceTests.GateVariableName} must be true when specified.");
        }

        if (string.IsNullOrWhiteSpace(expectedEngineId) ||
            string.IsNullOrWhiteSpace(runId))
        {
            throw new InvalidOperationException("The native probe requires its exact Docker context, expected engine identity, and run ID.");
        }

        return new NamespaceProbeTestSettings(
            true,
            "desktop-linux",
            expectedEngineId,
            NamespaceProbeModelFactory.ValidateRunId(runId));
    }
}

public sealed class NativeNamespaceProbeFactAttribute : FactAttribute
{
    public NativeNamespaceProbeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(NamespaceProbeDcpAcceptanceTests.GateVariableName) is null)
        {
            Skip = $"Set {NamespaceProbeDcpAcceptanceTests.GateVariableName}=true and provide the required target values to run this native probe.";
        }
    }
}

internal sealed class NamespaceProbeNotificationMonitor : IAsyncDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentDictionary<IResource, OwnedResourceObservation> latest = new(ReferenceEqualityComparer.Instance);
    private readonly Channel<OwnedResourceObservation> observations = Channel.CreateUnbounded<OwnedResourceObservation>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly Task pump;

    internal NamespaceProbeNotificationMonitor(IOwnedReconfigurationAdapter adapter)
    {
        pump = Task.Run(async () =>
        {
            try
            {
                await foreach (var observation in adapter.WatchAsync(stop.Token).ConfigureAwait(false))
                {
                    latest[observation.Resource] = observation;
                    await observations.Writer.WriteAsync(observation, stop.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
            }
            finally
            {
                observations.Writer.TryComplete();
            }
        });
    }

    internal async Task<OwnedResourceObservation> WaitForRunningAsync(
        IResource resource,
        string? previousContainerId,
        CancellationToken cancellationToken)
    {
        if (latest.TryGetValue(resource, out var current) && IsNewRunningContainer(current, previousContainerId))
        {
            return current;
        }

        await foreach (var observation in observations.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (ReferenceEquals(observation.Resource, resource) && IsNewRunningContainer(observation, previousContainerId))
            {
                return observation;
            }
        }

        throw new InvalidOperationException($"No current running container identity was observed for resource '{resource.Name}'.");
    }

    internal async Task<OwnedResourceObservation> WaitForStoppedAsync(
        IResource resource,
        string expectedContainerId,
        CancellationToken cancellationToken)
    {
        if (latest.TryGetValue(resource, out var current) && IsExpectedStopped(current, expectedContainerId))
        {
            return current;
        }

        await foreach (var observation in observations.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (ReferenceEquals(observation.Resource, resource) && IsExpectedStopped(observation, expectedContainerId))
            {
                return observation;
            }
        }

        throw new InvalidOperationException($"No terminal observation for the owned container was observed for resource '{resource.Name}'.");
    }

    private static bool IsNewRunningContainer(OwnedResourceObservation observation, string? previousContainerId) =>
        string.Equals(observation.State, KnownResourceStates.Running, StringComparison.Ordinal) &&
        OwnedContainerInspection.IsFullId(observation.ContainerId) &&
        !string.Equals(observation.ContainerId, previousContainerId, StringComparison.Ordinal);

    private static bool IsExpectedStopped(OwnedResourceObservation observation, string expectedContainerId) =>
        observation.IsStopped && string.Equals(observation.ContainerId, expectedContainerId, StringComparison.Ordinal);

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        await pump.ConfigureAwait(false);
        stop.Dispose();
    }
}

internal sealed class SequenceOwnedReconfigurationAdapter(IReadOnlyList<OwnedResourceObservation> sequence)
    : IOwnedReconfigurationAdapter
{
    public OwnedResourceObservation? GetCurrent(IResource resource) => null;

    public OwnedResourceObservation? GetCurrent(string resourceId) => null;

    public async IAsyncEnumerable<OwnedResourceObservation> WatchAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var observation in sequence)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return observation;
            await Task.Yield();
        }
    }

    public Task<bool> ExecuteLifecycleCommandAsync(IResource resource, string commandName, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<string> CaptureDockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal sealed record ReadOnlyDockerResult(int ExitCode, string StandardOutput, string StandardError);

internal interface IReadOnlyDockerCli
{
    Task<ReadOnlyDockerResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    Task<ReadOnlyDockerResult> RunDefaultAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

internal sealed class ReadOnlyDockerCli(string executablePath, string context) : IReadOnlyDockerCli
{
    public async Task<ReadOnlyDockerResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        await RunCoreAsync(arguments, includeContext: true, cancellationToken).ConfigureAwait(false);

    public async Task<ReadOnlyDockerResult> RunDefaultAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        await RunCoreAsync(arguments, includeContext: false, cancellationToken).ConfigureAwait(false);

    private async Task<ReadOnlyDockerResult> RunCoreAsync(
        IReadOnlyList<string> arguments,
        bool includeContext,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (includeContext)
        {
            startInfo.ArgumentList.Add("--context");
            startInfo.ArgumentList.Add(context);
        }
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new IOException("The read-only Docker metadata command did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(8), cancellationToken)
            .ConfigureAwait(false);
        return new ReadOnlyDockerResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }
}

internal sealed class FakeReadOnlyDockerCli(params (bool ExplicitContext, string[] ExpectedArguments, string Output)[] responses)
    : IReadOnlyDockerCli
{
    private readonly Queue<(bool ExplicitContext, string[] ExpectedArguments, string Output)> queue = new(responses);
    internal List<(bool ExplicitContext, IReadOnlyList<string> Arguments)> Calls { get; } = [];

    public Task<ReadOnlyDockerResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        RespondAsync(true, arguments, cancellationToken);

    public Task<ReadOnlyDockerResult> RunDefaultAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        RespondAsync(false, arguments, cancellationToken);

    private Task<ReadOnlyDockerResult> RespondAsync(
        bool explicitContext,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add((explicitContext, arguments.ToArray()));
        if (queue.Count == 0)
        {
            throw new InvalidOperationException("The fake read-only Docker plan received an unexpected command.");
        }

        var expected = queue.Dequeue();
        if (expected.ExplicitContext != explicitContext || !ArgumentsMatch(expected.ExpectedArguments, arguments))
        {
            throw new InvalidOperationException("The fake read-only Docker plan received an unexpected command shape.");
        }

        return Task.FromResult(new ReadOnlyDockerResult(
            expected.Output.Length == 0 ? 1 : 0,
            expected.Output,
            expected.Output.Length == 0 ? "planned read-only command failure" : string.Empty));
    }

    private static bool ArgumentsMatch(IReadOnlyList<string> expected, IReadOnlyList<string> actual) =>
        expected.Count == actual.Count && expected.Zip(actual).All(pair =>
            pair.First == ProbeTestArgument.Any || string.Equals(pair.First, pair.Second, StringComparison.Ordinal));

    internal static bool IsReadOnly(IReadOnlyList<string> arguments) =>
        arguments.Count > 0 &&
        arguments[0] is "info" or "context" or "image" or "ps" or "inspect";
}

internal static class ProbeTestArgument
{
    internal const string Any = "<any>";
}

internal static class NamespaceProbeDockerSelection
{
    internal static void ValidateOverrides(string expectedContext, string? dockerHost, string? dockerContext)
    {
        if (!string.IsNullOrWhiteSpace(dockerHost) ||
            (!string.IsNullOrWhiteSpace(dockerContext) &&
             !string.Equals(dockerContext, expectedContext, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Docker environment overrides did not prove the same approved Engine and context.");
        }
    }
}

internal static class NamespaceProbeCleanupBudget
{
    internal static TimeSpan Remaining(TimeSpan elapsed)
    {
        var deadline = TimeSpan.FromSeconds(60);
        return elapsed >= deadline ? TimeSpan.Zero : deadline - elapsed;
    }
}

internal sealed record NamespaceProbeCleanupResult(bool EngineVerified, Exception? EngineCheckFailure);

internal static class NamespaceProbeCleanup
{
    internal static async Task<NamespaceProbeCleanupResult> RunOnlyAfterEngineCheckAsync(
        Func<CancellationToken, Task> verifyEngine,
        Func<CancellationToken, Task> stopApplication,
        Func<CancellationToken, Task> disposeApplication,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await verifyEngine(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return new NamespaceProbeCleanupResult(false, exception);
        }

        await stopApplication(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        await disposeApplication(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        return new NamespaceProbeCleanupResult(true, null);
    }
}
