using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Duck.Testing.Diagnostics;

namespace Tracker.Tests;

public sealed class DcpBaseResourceOwnershipEventConsumerTests
{
    private const string EngineId = "expected-engine";
    private const string StackId = "base-stack";
    private const string RunId = "base-run";
    private const string NetworkName = "aspire-default-network";

    [Fact]
    public async Task VerifiesStandardDcpBridgeMembershipWithoutClaimingServiceReadiness()
    {
        var fixture = new OwnershipFixture();

        var result = await fixture.VerifyAsync();

        Assert.Equal(DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown, result.Disposition);
        Assert.Equal(4, result.InspectCount);
        Assert.Equal(NetworkName, result.ObservedNetworkName);
        Assert.Equal(1, fixture.Runner.EngineReads);
        Assert.Equal(4, fixture.Runner.InspectReads);
        Assert.All(fixture.Runner.Calls, call =>
        {
            Assert.Equal("desktop-linux", call.Arguments[1]);
            Assert.InRange(call.Timeout, TimeSpan.FromTicks(1), TimeSpan.FromSeconds(3));
            Assert.InRange(call.MaximumOutputCharacters, 1, 64 * 1024);
        });
        Assert.Contains(fixture.Runner.Calls, call => call.Arguments.SequenceEqual(["--context", "desktop-linux", "info", "--format", "{{.ID}}"]));
        Assert.All(fixture.Registry.GetCandidates(), candidate =>
            Assert.Contains(fixture.Runner.Calls, call =>
                call.Arguments.SequenceEqual(["--context", "desktop-linux", "inspect", "--format", "{{json .}}", candidate.ContainerId!])));
        Assert.All(fixture.Registry.GetCandidates(), candidate =>
        {
            Assert.True(candidate.IdentityVerified);
            Assert.True(candidate.NetworkVerified);
            Assert.True(candidate.OwnershipVerified);
            Assert.Equal(NetworkName, candidate.ObservedNetworkName);
            Assert.False(fixture.Registry.Get(candidate.Resource)!.IsReady);
        });
    }

    [Theory]
    [InlineData("default")]
    [InlineData("network-id")]
    [InlineData("mixed")]
    public async Task AcceptsDefaultOrAttachedNetworkIdModeForTheSameSingleBridge(string mode)
    {
        var fixture = new OwnershipFixture();
        foreach (var (candidate, index) in fixture.Registry.GetCandidates().Select((candidate, index) => (candidate, index)))
        {
            fixture.Runner.NetworkModes[candidate.ContainerId!] = mode switch
            {
                "default" => "default",
                "network-id" => new string('b', 64),
                _ => index switch
                {
                    0 => "default",
                    1 => new string('b', 64),
                    2 => NetworkName,
                    _ => "default",
                },
            };
        }

        var result = await fixture.VerifyAsync();

        Assert.Equal(DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown, result.Disposition);
        Assert.Equal(NetworkName, result.ObservedNetworkName);
        Assert.All(fixture.Registry.GetCandidates(), candidate =>
        {
            Assert.True(candidate.IdentityVerified);
            Assert.True(candidate.NetworkVerified);
            Assert.True(candidate.OwnershipVerified);
            Assert.False(fixture.Registry.Get(candidate.Resource)!.IsReady);
        });
    }

    [Theory]
    [InlineData("host")]
    [InlineData("none")]
    [InlineData("container:other")]
    [InlineData("unknown-network")]
    public async Task RejectsNonStandardNetworkModes(string mode)
    {
        var fixture = new OwnershipFixture();
        var first = fixture.Registry.GetCandidates()[0];
        fixture.Runner.NetworkModes[first.ContainerId!] = mode;

        var result = await fixture.VerifyAsync();

        Assert.Equal(DcpBaseResourceOwnershipDisposition.NetworkObservationRejected, result.Disposition);
        Assert.Null(result.ObservedNetworkName);
        Assert.False(fixture.Registry.Get(first.Resource)!.NetworkVerified);
        Assert.False(fixture.Registry.Get(first.Resource)!.OwnershipVerified);
    }

    [Fact]
    public async Task RejectsAnUnrelatedFullNetworkIdAsNetworkMode()
    {
        var fixture = new OwnershipFixture();
        var first = fixture.Registry.GetCandidates()[0];
        fixture.Runner.NetworkModes[first.ContainerId!] = new string('c', 64);

        var result = await fixture.VerifyAsync();

        Assert.Equal(DcpBaseResourceOwnershipDisposition.NetworkObservationRejected, result.Disposition);
        Assert.Null(result.ObservedNetworkName);
        Assert.False(fixture.Registry.Get(first.Resource)!.NetworkVerified);
    }

    [Fact]
    public async Task StandardDcpOwnershipRequiresACompleteNetworkId()
    {
        var fixture = new OwnershipFixture();
        fixture.Runner.OmitNetworkId = true;

        var result = await fixture.VerifyAsync();

        Assert.Equal(DcpBaseResourceOwnershipDisposition.NetworkObservationRejected, result.Disposition);
        Assert.Null(result.ObservedNetworkName);
        Assert.All(fixture.Registry.GetCandidates(), candidate =>
        {
            Assert.False(fixture.Registry.Get(candidate.Resource)!.NetworkVerified);
            Assert.False(fixture.Registry.Get(candidate.Resource)!.OwnershipVerified);
        });
    }

    [Fact]
    public async Task SameIdStartingStateInvalidatesPreviouslyVerifiedOwnership()
    {
        var fixture = new OwnershipFixture();
        var initial = await fixture.VerifyAsync();
        Assert.Equal(DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown, initial.Disposition);

        fixture.SetFirstCandidateState("Starting");

        var candidate = fixture.Registry.GetCandidates()[0];
        Assert.False(candidate.IdentityVerified);
        Assert.False(candidate.NetworkVerified);
        Assert.False(candidate.OwnershipVerified);
    }

    [Fact]
    public async Task SameIdRunningAfterStartingIsInspectedAgain()
    {
        var fixture = new OwnershipFixture();
        var initial = await fixture.VerifyAsync();
        Assert.Equal(DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown, initial.Disposition);

        fixture.SetFirstCandidateState("Starting");
        fixture.SetFirstCandidateState("Running");
        var result = await fixture.VerifyAsync();

        Assert.Equal(DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown, result.Disposition);
        Assert.Equal(2, fixture.Runner.EngineReads);
        Assert.Equal(8, fixture.Runner.InspectReads);
    }

    [Fact]
    public async Task CachedOwnershipProofIsNotReturnedForAChangedCurrentFullId()
    {
        var fixture = new OwnershipFixture();
        var initial = await fixture.VerifyAsync();
        Assert.Equal(DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown, initial.Disposition);
        var candidate = fixture.Registry.GetCandidates()[0];

        var result = await fixture.VerifyAsync(fixture.CurrentWithFirstCandidateReplacement());

        Assert.Equal(DcpBaseResourceOwnershipDisposition.CandidateChanged, result.Disposition);
        Assert.Equal(1, fixture.Runner.EngineReads);
        Assert.Equal(4, fixture.Runner.InspectReads);
        Assert.False(fixture.Registry.Get(candidate.Resource)!.OwnershipVerified);
    }

    [Fact]
    public async Task CachedOwnershipProofIsNotReturnedWhenCurrentStateIsStopped()
    {
        var fixture = new OwnershipFixture();
        var initial = await fixture.VerifyAsync();
        Assert.Equal(DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown, initial.Disposition);
        var candidate = fixture.Registry.GetCandidates()[0];
        var stopped = fixture.FirstCandidateStateEvent("Stopped");

        var result = await fixture.VerifyAsync(resourceId => resourceId == candidate.ResourceId
            ? stopped
            : fixture.GetCurrent(resourceId));

        Assert.Equal(DcpBaseResourceOwnershipDisposition.CandidateChanged, result.Disposition);
        Assert.Equal(1, fixture.Runner.EngineReads);
        Assert.Equal(4, fixture.Runner.InspectReads);
        Assert.False(fixture.Registry.Get(candidate.Resource)!.OwnershipVerified);
    }

    [Fact]
    public async Task RejectsWrongEngineBeforeInspectingAnyContainer()
    {
        var fixture = new OwnershipFixture();
        fixture.Runner.EngineId = "other-engine";

        var result = await fixture.VerifyAsync();

        Assert.Equal(DcpBaseResourceOwnershipDisposition.EngineRejected, result.Disposition);
        Assert.Equal(0, result.InspectCount);
        Assert.Equal(1, fixture.Runner.EngineReads);
        Assert.Equal(0, fixture.Runner.InspectReads);
        Assert.All(fixture.Registry.GetCandidates(), candidate => Assert.False(candidate.IdentityVerified));
    }

    [Fact]
    public async Task RejectsWrongOwnerLabelsAndNetworkDisagreementWithoutPublishingEvidence()
    {
        var wrongLabels = new OwnershipFixture();
        wrongLabels.Runner.WrongRunRole = "crane";
        var labelResult = await wrongLabels.VerifyAsync();
        Assert.Equal(DcpBaseResourceOwnershipDisposition.ContainerRejected, labelResult.Disposition);
        Assert.Equal("crane", labelResult.ResourceName);
        Assert.All(wrongLabels.Registry.GetCandidates(), candidate => Assert.False(candidate.IdentityVerified));

        var wrongNetwork = new OwnershipFixture();
        wrongNetwork.Runner.DifferentNetworkRole = "crane";
        var networkResult = await wrongNetwork.VerifyAsync();
        Assert.Equal(DcpBaseResourceOwnershipDisposition.NetworkObservationRejected, networkResult.Disposition);
        Assert.Null(networkResult.ObservedNetworkName);
        Assert.All(wrongNetwork.Registry.GetCandidates(), candidate =>
        {
            Assert.True(candidate.IdentityVerified);
            Assert.False(candidate.NetworkVerified);
            Assert.False(candidate.OwnershipVerified);
        });

        var wrongNetworkId = new OwnershipFixture();
        wrongNetworkId.Runner.DifferentNetworkIdRole = "crane";
        var networkIdResult = await wrongNetworkId.VerifyAsync();
        Assert.Equal(DcpBaseResourceOwnershipDisposition.NetworkObservationRejected, networkIdResult.Disposition);
        Assert.Null(networkIdResult.ObservedNetworkName);
        Assert.All(wrongNetworkId.Registry.GetCandidates(), candidate =>
        {
            Assert.True(candidate.IdentityVerified);
            Assert.False(candidate.NetworkVerified);
            Assert.False(candidate.OwnershipVerified);
        });
    }

    [Fact]
    public async Task RejectsInspectResultWhenTheCurrentCandidateChangesBeforeCommit()
    {
        var fixture = new OwnershipFixture();
        fixture.Runner.AfterFirstInspect = fixture.ReplaceFirstCandidate;

        var result = await fixture.VerifyAsync();

        Assert.Equal(DcpBaseResourceOwnershipDisposition.CandidateChanged, result.Disposition);
        Assert.Equal(4, result.InspectCount);
        Assert.All(fixture.Registry.GetCandidates(), candidate => Assert.False(candidate.IdentityVerified));
    }

    [Fact]
    public async Task CallerCancellationLeavesEveryCandidateUnverified()
    {
        var fixture = new OwnershipFixture();
        fixture.Runner.CaptureOverride = async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new OperationCanceledException(token);
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.VerifyAsync(cancellation.Token));

        Assert.All(fixture.Registry.GetCandidates(), candidate =>
        {
            Assert.False(candidate.IdentityVerified);
            Assert.False(candidate.OwnershipVerified);
        });
    }

    [Fact]
    public async Task SharedDeadlineStopsMetadataInspectionWithoutPublishingIdentity()
    {
        var fixture = new OwnershipFixture();
        fixture.Runner.CaptureOverride = async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new OperationCanceledException(token);
        };

        var result = await fixture.VerifyAsync();

        Assert.Equal(DcpBaseResourceOwnershipDisposition.DeadlineExceeded, result.Disposition);
        Assert.Equal(0, result.InspectCount);
        Assert.All(fixture.Registry.GetCandidates(), candidate =>
        {
            Assert.False(candidate.IdentityVerified);
            Assert.False(candidate.NetworkVerified);
            Assert.False(candidate.OwnershipVerified);
        });
    }

    [Fact]
    public async Task DoesNotStartInspectionWhenContextOrExpectedEngineIsMissing()
    {
        var fixture = new OwnershipFixture(contextName: null);
        await fixture.VerifyAsync();

        Assert.Equal(0, fixture.Runner.EngineReads);
        Assert.Equal(0, fixture.Runner.InspectReads);
        Assert.All(fixture.Registry.GetCandidates(), candidate => Assert.False(candidate.IdentityVerified));

        var missingEngine = new OwnershipFixture(expectedEngineId: null);
        await missingEngine.VerifyAsync();
        Assert.Equal(0, missingEngine.Runner.EngineReads);
        Assert.Equal(0, missingEngine.Runner.InspectReads);
    }

    [Fact]
    public async Task InspectsCurrentRunningCandidatesWithoutMarkingPendingResourcesReady()
    {
        var fixture = new OwnershipFixture();
        fixture.SetFirstCandidateState("Starting");

        var result = await fixture.VerifyAsync();

        Assert.Equal(DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown, result.Disposition);
        Assert.Equal(1, fixture.Runner.EngineReads);
        Assert.Equal(fixture.Registry.GetCandidates().Count - 1, fixture.Runner.InspectReads);
        var candidates = fixture.Registry.GetCandidates();
        Assert.False(candidates[0].IdentityVerified);
        Assert.False(candidates[0].NetworkVerified);
        Assert.False(fixture.Registry.Get(candidates[0].Resource)!.IsReady);
        Assert.All(candidates.Skip(1), candidate =>
        {
            Assert.True(candidate.IdentityVerified);
            Assert.True(candidate.NetworkVerified);
            Assert.True(candidate.OwnershipVerified);
            Assert.False(fixture.Registry.Get(candidate.Resource)!.IsReady);
        });
    }

    [Fact]
    public async Task VerifiesSimulatorAndDuckBeforeDownstreamContainersStart()
    {
        var fixture = new OwnershipFixture(
            includeDuck: true,
            runningRoles: ["simulator", "duck"]);

        var result = await fixture.VerifyAsync();

        Assert.Equal(DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown, result.Disposition);
        Assert.Equal(1, fixture.Runner.EngineReads);
        Assert.Equal(2, fixture.Runner.InspectReads);
        foreach (var candidate in fixture.Registry.GetCandidates())
        {
            var expectedOwned = candidate.Role is "simulator" or "duck";
            Assert.Equal(expectedOwned, candidate.OwnershipVerified);
            Assert.False(fixture.Registry.Get(candidate.Resource)!.IsReady);
        }
    }

    internal sealed class OwnershipFixture
    {
        private readonly Dictionary<string, ResourceEvent> currentEvents = new(StringComparer.Ordinal);
        private readonly DcpBaseResourceReadinessEventConsumer readinessConsumer;
        private readonly Dictionary<IResource, string> roles = new(ReferenceEqualityComparer.Instance);

        internal OwnershipFixture(
            string? contextName = "desktop-linux",
            string? expectedEngineId = EngineId,
            bool includeDuck = false,
            IReadOnlyCollection<string>? runningRoles = null,
            string runId = RunId,
            string stackId = StackId,
            IReadOnlyDictionary<string, string>? duckLabels = null)
        {
            var resources = new List<(IResource Resource, string Role, string RunId)>
            {
                (new ContainerResource("simulator", "example/simulator:fixed"), "simulator", runId),
                (new ContainerResource("game-controller", "example/controller:fixed"), "game-controller", runId),
                (new ContainerResource("cm4-sim", "example/cm4:fixed"), "cm4-sim", runId),
                (new ContainerResource("crane", "example/crane:fixed"), "crane", runId),
            };
            if (includeDuck)
            {
                resources.Add((new ContainerResource("duck", "example/duck:fixed"), "duck", runId));
            }
            Registry = new DcpBaseResourceReadinessRegistry(resources, stackId);
            readinessConsumer = new DcpBaseResourceReadinessEventConsumer(Registry);
            Runner = new FakeDockerStdioCommandRunner();
            OwnershipConsumer = new DcpBaseResourceOwnershipEventConsumer(
                Registry,
                Runner,
                contextName,
                expectedEngineId);

            var idNumber = 1;
            foreach (var (resource, role, _) in resources)
            {
                roles.Add(resource, role);
                var containerId = idNumber++.ToString("x64");
                Runner.Inspections[containerId] = InspectJson(
                    containerId,
                    role,
                    NetworkName,
                    $"172.28.0.{idNumber}",
                    runId,
                    stackId,
                    role == "duck" ? duckLabels : null);
                var state = runningRoles is null || runningRoles.Contains(role) ? "Running" : "NotStarted";
                var resourceEvent = Event(resource, role, containerId, state);
                currentEvents.Add(resource.Name, resourceEvent);
                readinessConsumer.Observe(resourceEvent, resourceEvent, role, GetCurrent);
            }
        }

        internal DcpBaseResourceReadinessRegistry Registry { get; }
        internal FakeDockerStdioCommandRunner Runner { get; }
        internal DcpBaseResourceOwnershipEventConsumer OwnershipConsumer { get; }

        internal Task<DcpBaseResourceOwnershipResult> VerifyAsync(CancellationToken cancellationToken = default) => OwnershipConsumer.VerifyCurrentAsync(
            resourceId => currentEvents.Values.SingleOrDefault(resourceEvent => resourceEvent.ResourceId == resourceId),
            cancellationToken);

        internal Task<DcpBaseResourceOwnershipResult> VerifyAsync(
            Func<string, ResourceEvent?> getCurrentByResourceId,
            CancellationToken cancellationToken = default) => OwnershipConsumer.VerifyCurrentAsync(
                getCurrentByResourceId,
                cancellationToken);

        internal Func<string, ResourceEvent?> CurrentWithFirstCandidateReplacement()
        {
            var candidate = Registry.GetCandidates()[0];
            var replacement = Event(candidate.Resource, candidate.Role, new string('f', 64));
            return resourceId => resourceId == candidate.ResourceId
                ? replacement
                : currentEvents.Values.SingleOrDefault(resourceEvent => resourceEvent.ResourceId == resourceId);
        }

        internal ResourceEvent FirstCandidateStateEvent(string state)
        {
            var candidate = Registry.GetCandidates()[0];
            return Event(candidate.Resource, candidate.Role, candidate.ContainerId!, state);
        }

        internal void ReplaceFirstCandidate()
        {
            var resource = Registry.GetCandidates()[0].Resource;
            var role = roles[resource];
            var replacementId = new string('f', 64);
            var replacement = Event(resource, role, replacementId);
            currentEvents[resource.Name] = replacement;
            readinessConsumer.Observe(replacement, replacement, role, GetCurrent);
        }

        internal void SetFirstCandidateState(string state)
        {
            var candidate = Registry.GetCandidates()[0];
            var replacement = Event(candidate.Resource, candidate.Role, candidate.ContainerId!, state);
            currentEvents[candidate.Resource.Name] = replacement;
            readinessConsumer.Observe(replacement, replacement, candidate.Role, GetCurrent);
        }

        internal ResourceEvent? GetCurrent(string resourceId) =>
            currentEvents.Values.SingleOrDefault(resourceEvent => resourceEvent.ResourceId == resourceId);

        private static ResourceEvent Event(IResource resource, string role, string containerId, string state = "Running") =>
            new(resource, resource.Name, new CustomResourceSnapshot
            {
                ResourceType = "container",
                State = new ResourceStateSnapshot(state, "fixture"),
                Properties = [new ResourcePropertySnapshot("container.id", containerId)],
            });

        private static string InspectJson(
            string containerId,
            string role,
            string networkName,
            string address,
            string runId,
            string stackId,
            IReadOnlyDictionary<string, string>? suppliedLabels) =>
            JsonSerializer.Serialize(new
            {
                Id = containerId,
                State = new { Status = "running" },
                Config = new
                {
                    Labels = suppliedLabels ?? new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [ContainerIdentityExecHandoff.RunIdLabel] = runId,
                        [ContainerIdentityExecHandoff.RoleLabel] = role,
                        [DockerContainerSpec.StackLabel] = stackId,
                        [DockerContainerSpec.ResourceLabel] = role,
                        [DockerContainerSpec.RunLabel] = runId,
                    },
                },
                HostConfig = new { NetworkMode = networkName },
                NetworkSettings = new
                {
                    Networks = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        [networkName] = new { IPAddress = address, NetworkID = new string('b', 64) },
                    },
                },
            });
    }

    internal sealed class FakeDockerStdioCommandRunner : IDockerStdioCommandRunner
    {
        internal Dictionary<string, string> Inspections { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, string> NetworkModes { get; } = new(StringComparer.Ordinal);
        internal string EngineId { get; set; } = DcpBaseResourceOwnershipEventConsumerTests.EngineId;
        internal string? WrongRunRole { get; set; }
        internal string? DifferentNetworkRole { get; set; }
        internal string? DifferentNetworkIdRole { get; set; }
        internal bool OmitNetworkId { get; set; }
        internal Action? AfterFirstInspect { get; set; }
        internal Func<IReadOnlyList<string>, CancellationToken, Task<DockerStdioCommandResult>>? CaptureOverride { get; set; }
        internal int EngineReads { get; private set; }
        internal int InspectReads { get; private set; }
        internal List<(IReadOnlyList<string> Arguments, int MaximumOutputCharacters, TimeSpan Timeout)> Calls { get; } = [];

        public Task<DockerStdioCommandResult> CaptureBoundedAsync(
            IReadOnlyList<string> arguments,
            int maximumOutputCharacters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Calls.Add((arguments.ToArray(), maximumOutputCharacters, timeout));
            if (CaptureOverride is not null)
            {
                return CaptureOverride(arguments, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (arguments.Contains("info", StringComparer.Ordinal))
            {
                EngineReads++;
                return Task.FromResult(new DockerStdioCommandResult(0, EngineId, string.Empty));
            }

            InspectReads++;
            var containerId = arguments[^1];
            var json = Inspections[containerId];
            if (NetworkModes.TryGetValue(containerId, out var networkMode))
            {
                json = json.Replace($"\"NetworkMode\":\"{NetworkName}\"", $"\"NetworkMode\":\"{networkMode}\"", StringComparison.Ordinal);
            }
            if (WrongRunRole is not null && json.Contains($"\"{ContainerIdentityExecHandoff.RoleLabel}\":\"{WrongRunRole}\"", StringComparison.Ordinal))
            {
                json = json.Replace(RunId, "foreign-run", StringComparison.Ordinal);
            }
            if (DifferentNetworkRole is not null && json.Contains($"\"{ContainerIdentityExecHandoff.RoleLabel}\":\"{DifferentNetworkRole}\"", StringComparison.Ordinal))
            {
                json = json.Replace(NetworkName, "other-bridge", StringComparison.Ordinal);
            }
            if (DifferentNetworkIdRole is not null && json.Contains($"\"{ContainerIdentityExecHandoff.RoleLabel}\":\"{DifferentNetworkIdRole}\"", StringComparison.Ordinal))
            {
                json = json.Replace(new string('b', 64), new string('c', 64), StringComparison.Ordinal);
            }
            if (OmitNetworkId)
            {
                json = json.Replace($",\"NetworkID\":\"{new string('b', 64)}\"", string.Empty, StringComparison.Ordinal);
            }

            if (InspectReads == 1)
            {
                AfterFirstInspect?.Invoke();
            }
            return Task.FromResult(new DockerStdioCommandResult(0, json, string.Empty));
        }

        public Task<DockerStdioCommandResult> ExecuteWithStdioAsync(
            IReadOnlyList<string> arguments,
            string standardInput,
            int maximumOutputCharacters,
            TimeSpan timeout,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string> CaptureAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ContainerIdentityControlExecResult> RelayAsync(
            IReadOnlyList<string> arguments,
            ResourceDiagnosticIdentity identity,
            string roleName,
            string? expectedBootId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
