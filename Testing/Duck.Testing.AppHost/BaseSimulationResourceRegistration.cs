using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Duck.Testing.AppHost;

internal sealed record BaseSimulationRegistrationOptions(
    string SimulatorIbisPort,
    string SimulatorImageTag,
    string SimulatorGeometry,
    string SimulatorRealism,
    string SimulatorIbisTeamColor,
    string CraneImageTag,
    string Cm4SimulatorImageTag,
    string CranePlanner,
    string StackId,
    string CraneTeamName,
    string CraneTeamColor,
    string FixtureManifestPath,
    bool UseStandardDcpResources,
    string DuckRunId,
    CraneTargetAddressSettings? CraneTargetAddressSettings,
    string? CraneDiagnosticsPath,
    string? DockerContextName,
    string? ExpectedEngineId,
    string DockerExecutable);

internal sealed record BaseSimulationResources(
    IResourceBuilder<ContainerResource> Duck,
    IResourceBuilder<IResourceWithWaitSupport> Simulator,
    IResourceBuilder<IResourceWithWaitSupport> GameController,
    IResourceBuilder<IResourceWithWaitSupport>? Cm4Simulator,
    IResourceBuilder<IResourceWithWaitSupport> Crane);

/// <summary>通常構成で選択された資源を、AppHostの実登録経路から登録する。</summary>
internal static class BaseSimulationResourceRegistration
{
    internal const string Cm4TransportHelperAssetName = "cm4-transport-control";
    internal const string Cm4TransportHelperContainerPath = "/opt/duck/cm4-transport-control";

    internal static bool UsesCm4Simulator(string planner) =>
        string.Equals(planner, "visibility_graph", StringComparison.Ordinal);

    internal static BaseSimulationResources Add(
        IDistributedApplicationBuilder builder,
        BaseSimulationRegistrationOptions options,
        Func<IResourceBuilder<IResource>, IResourceBuilder<ContainerResource>> addDuck,
        Action<IResourceBuilder<IResource>> addGameControllerDependentResource,
        ContainerIdentityHandoffStateStore? handoffStates = null,
        Func<EndpointReference, CancellationToken, Task<Uri>>? gameControllerControlEndpointResolver = null,
        Func<Uri, CancellationToken, Task<bool>>? gameControllerControlApiProbe = null,
        string? appHostAssetRoot = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(addDuck);
        ArgumentNullException.ThrowIfNull(addGameControllerDependentResource);
        var useCm4Simulator = UsesCm4Simulator(options.CranePlanner);

        CraneFixtureManifest.WriteAtomically(options.FixtureManifestPath, options.StackId,
            new CraneFixtureSettings(options.CraneTeamName, options.CraneTeamColor));

        IResourceBuilder<IResourceWithWaitSupport> simulator;
        if (options.UseStandardDcpResources)
        {
            simulator = AsResourceBuilder(builder, AddStandardContainer(
                builder,
                new DockerContainerSpec(
                    "simulator",
                    $"ghcr.io/ibis-ssl/framework-simulatorcli:{options.SimulatorImageTag}",
                    [
                        "tini", "--", "./bin/simulator-cli", "-g", options.SimulatorGeometry,
                        "--realism", options.SimulatorRealism, "--ibis-port", options.SimulatorIbisPort,
                        "--ibis-team-color", options.SimulatorIbisTeamColor,
                    ],
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    options.StackId),
                options.DuckRunId,
                "simulator"));
        }
        else
        {
            simulator = AsResourceBuilder(builder, builder.AddDockerWrapper(
                new DockerContainerSpec(
                    "simulator",
                    $"ghcr.io/ibis-ssl/framework-simulatorcli:{options.SimulatorImageTag}",
                    [
                        "tini", "--", "./bin/simulator-cli", "-g", options.SimulatorGeometry,
                        "--realism", options.SimulatorRealism, "--ibis-port", options.SimulatorIbisPort,
                        "--ibis-team-color", options.SimulatorIbisTeamColor,
                    ],
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    options.StackId),
                39101,
                "simulator-cli",
                "simulator"));
        }

        var simulatorDependency = builder.CreateResourceBuilder<IResource>(simulator.Resource);
        var duck = addDuck(simulatorDependency)
            .WithEnvironment("VisionReceiver__MulticastAddress", "224.5.23.2")
            .WithEnvironment("VisionReceiver__Port", "10020");
        duck = options.UseStandardDcpResources
            ? duck.WaitForStart(simulatorDependency)
            : duck.WaitFor(simulatorDependency);

        IResourceBuilder<IResourceWithWaitSupport> gameController;
        if (options.UseStandardDcpResources)
        {
            var gameControllerContainer = AddStandardContainer(
                builder,
                new DockerContainerSpec(
                    "game-controller",
                    "robocupssl/ssl-game-controller:3.20.3",
                    [
                        "-visionAddress", "224.5.23.2:10020",
                        "-trackerAddress", "224.5.23.2:11010",
                        "-publishAddress", "224.5.23.1:11003",
                        "-address", ":8082",
                    ],
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    options.StackId),
                options.DuckRunId,
                "game-controller");
            var controlEndpoint = gameControllerContainer
                .WithHttpEndpoint(targetPort: 8082, name: "game-controller-control", isProxied: false);
            var controlEndpointAnnotation = controlEndpoint.Resource.Annotations
                .OfType<EndpointAnnotation>()
                .Single(endpoint => endpoint.Name == "game-controller-control");
            controlEndpointAnnotation.Port = null;
            controlEndpointAnnotation.TargetHost = "127.0.0.1";
            gameController = AsResourceBuilder(builder, gameControllerContainer);
        }
        else
        {
            gameController = AsResourceBuilder(builder, builder.AddDockerWrapper(
                new DockerContainerSpec(
                    "game-controller",
                    "robocupssl/ssl-game-controller:3.20.3",
                    [
                        "-visionAddress", "224.5.23.2:10020",
                        "-trackerAddress", "224.5.23.2:11010",
                        "-publishAddress", "224.5.23.1:11003",
                        "-address", ":8082",
                    ],
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    options.StackId),
                39102,
                "app",
                "game-controller",
                expectedCraneTeamName: options.CraneTeamName));
        }

        addGameControllerDependentResource(builder.CreateResourceBuilder<IResource>(gameController.Resource));

        IResourceBuilder<IResourceWithWaitSupport>? cm4Simulator = null;
        if (useCm4Simulator)
        {
            var cm4HelperSource = Path.GetFullPath(Path.Combine(
                appHostAssetRoot ?? AppContext.BaseDirectory,
                Cm4TransportHelperAssetName));
            var cm4Spec = new DockerContainerSpec(
                "cm4-sim",
                $"ghcr.io/ibis-ssl/orion-cm4-sim:{options.Cm4SimulatorImageTag}",
                [
                    "tini", "--", "cm4_sim", "--robot-ids", "0,1,2,3,4,5,6,7,8,9,10",
                    "--in-port", "12345", "--out-addr", "127.0.0.1", "--out-port", options.SimulatorIbisPort,
                    "--feedback-port-base", "50100", "--multicast-if",
                    options.UseStandardDcpResources ? "0.0.0.0" : "127.0.0.1", "--rate-hz", "1000",
                    "--rx-delay-ms", "0", "--rx-jitter-ms", "0", "--rx-loss-rate", "0.0", "--seed", "0",
                ],
                new Dictionary<string, string>(StringComparer.Ordinal),
                options.StackId,
                mounts: options.UseStandardDcpResources
                    ? [new DockerContainerMount(cm4HelperSource, Cm4TransportHelperContainerPath, ReadOnly: true)]
                    : null);
            if (options.UseStandardDcpResources)
            {
                cm4Simulator = AsResourceBuilder(builder, AddStandardContainer(
                    builder, cm4Spec, options.DuckRunId, "cm4-sim"));
                var cm4Resource = cm4Simulator.Resource;
                var standardSimulator = builder.CreateResourceBuilder<ContainerResource>(
                    (ContainerResource)simulator.Resource);
                standardSimulator.WaitForStart(builder.CreateResourceBuilder<IResource>(cm4Resource));
                standardSimulator.WithArgs(context =>
                    AppendCurrentCm4FeedbackAddressAsync(context, cm4Resource));
                simulator = AsResourceBuilder(builder, standardSimulator);
                builder.Eventing.Subscribe<BeforeResourceStartedEvent>((@event, _) =>
                {
                    if (ReferenceEquals(@event.Resource, cm4Resource) && !File.Exists(cm4HelperSource))
                    {
                        throw new InvalidOperationException(
                            $"The CM4 transport helper is missing from the AppHost output: '{cm4HelperSource}'.");
                    }

                    return Task.CompletedTask;
                });
            }
            else
            {
                cm4Simulator = AsResourceBuilder(builder, builder.AddDockerWrapper(
                    cm4Spec,
                    39103,
                    "cm4_sim",
                    "cm4-sim").WaitFor(simulator));
            }
        }

        var craneEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PLANNER"] = options.CranePlanner,
            ["CRANE_TARGET_PORT"] = useCm4Simulator ? "12345" : options.SimulatorIbisPort,
        };
        var craneCommand =
            $"source /root/ibis_ws/install/setup.bash && ros2 launch crane_bringup crane.launch.xml sim:=true speak:=false team:={options.CraneTeamName} team_color:={options.CraneTeamColor} planner:=${{PLANNER}}";
        IEnumerable<DockerContainerMount>? craneMounts = null;
        if (options.CraneTargetAddressSettings is not null)
        {
            craneEnvironment[CraneTargetAddressSettings.TargetAddressEnvironmentKey] =
                options.CraneTargetAddressSettings.TargetAddress;
            craneEnvironment[CraneTargetAddressSettings.ExpectedResourceEnvironmentKey] =
                options.CraneTargetAddressSettings.ExpectedResourceName;
            craneCommand =
                $"source /root/ibis_ws/install/setup.bash && exec python3 {CraneTargetAddressSettings.ScriptMountTarget} speak:=false team:={options.CraneTeamName} team_color:={options.CraneTeamColor} planner:=${{PLANNER}}";
            craneMounts =
            [
                new DockerContainerMount(
                    options.CraneTargetAddressSettings.ScriptSource,
                    CraneTargetAddressSettings.ScriptMountTarget,
                    ReadOnly: true),
            ];
        }
        if (!useCm4Simulator)
        {
            craneEnvironment["FEEDBACK_SIM_MODE"] = "true";
        }

        var craneSpec = new DockerContainerSpec(
            "crane",
            $"ghcr.io/ibis-ssl/crane:{options.CraneImageTag}",
            ["bash", "-c", craneCommand],
            craneEnvironment,
            options.StackId,
            mounts: craneMounts);
        IResourceBuilder<IResourceWithWaitSupport> craneBuilder;
        if (options.UseStandardDcpResources)
        {
            craneBuilder = AsResourceBuilder(builder, AddStandardContainer(
                builder, craneSpec, options.DuckRunId, "crane"));
        }
        else
        {
            craneBuilder = AsResourceBuilder(builder, builder.AddDockerWrapper(
                craneSpec,
                39104,
                "ros2",
                "crane",
                startupTimeoutSeconds: 360,
                expectedCraneTeamName: options.CraneTeamName));
        }

        var crane = craneBuilder
            .WaitForStart(builder.CreateResourceBuilder<IResource>(duck.Resource))
            .WaitFor(builder.CreateResourceBuilder<IResource>(gameController.Resource));
        if (cm4Simulator is not null)
        {
            var cm4Dependency = builder.CreateResourceBuilder<IResource>(cm4Simulator.Resource);
            if (options.UseStandardDcpResources)
            {
                crane.WaitForStart(cm4Dependency);
            }
            else
            {
                crane.WaitFor(cm4Dependency);
            }
        }

        if (options.UseStandardDcpResources)
        {
            var readinessResources = new List<(IResource Resource, string Role)>
            {
                (simulator.Resource, "simulator"),
                (gameController.Resource, "game-controller"),
                (crane.Resource, "crane"),
            };
            if (cm4Simulator is not null)
            {
                readinessResources.Add((cm4Simulator.Resource, "cm4-sim"));
            }

            DcpBaseResourceReadinessRegistration.Add(
                builder,
                options.DuckRunId,
                options.StackId,
                options.DockerContextName,
                options.ExpectedEngineId,
                options.DockerExecutable,
                duck.Resource,
                duck.Resource.GetEndpoint("vision-health"),
                handoffStates ?? new ContainerIdentityHandoffStateStore(),
                options.CraneTeamName,
                gameControllerControlEndpointResolver,
                gameControllerControlApiProbe,
                readinessResources.ToArray());

            if (cm4Simulator is not null)
            {
                var simulatorResource = simulator.Resource;
                var cm4Resource = cm4Simulator.Resource;
                builder.Services.AddSingleton<IOwnedCm4SimulatorReconfigurationAdapter>(services =>
                    new DcpOwnedCm4SimulatorReconfigurationAdapter(
                        services.GetRequiredService<DcpBaseResourceReadinessRegistry>(),
                        services.GetRequiredService<DcpBaseResourceOwnershipEventConsumer>(),
                        services.GetRequiredService<DcpBaseResourceReadinessEventConsumer>(),
                        services.GetRequiredService<ResourceNotificationService>(),
                        simulatorResource,
                        cm4Resource,
                        options.ExpectedEngineId,
                        () => services.GetService<OwnedCm4TransportServerSessionManager>()));
                if (!string.IsNullOrWhiteSpace(options.DockerContextName) &&
                    !string.IsNullOrWhiteSpace(options.ExpectedEngineId))
                {
                    builder.Services.AddSingleton<IOwnedCm4TransportTargetController>(services =>
                        new OwnedCm4TransportTargetController(
                            services.GetRequiredService<IDockerStdioCommandRunner>(),
                            options.DockerContextName,
                            options.ExpectedEngineId,
                            options.DuckRunId,
                            services.GetRequiredService<IOwnedCm4SimulatorReconfigurationAdapter>().GetCurrentOwnedPair,
                            () => services.GetService<OwnedCm4TransportServerSessionManager>()?.GetCurrentSessionIdentity()));
                }
                builder.Services.AddSingleton<OwnedCm4SimulatorReconfigurationCoordinator>(services =>
                {
                    var adapter = services.GetRequiredService<IOwnedCm4SimulatorReconfigurationAdapter>();
                    return new OwnedCm4SimulatorReconfigurationCoordinator(
                        adapter,
                        services.GetService<IOwnedCm4TransportTargetController>(),
                        options.DuckRunId);
                });
                builder.Services.AddSingleton<OwnedCm4SimulatorRestartCommandHandler>();
                builder.Eventing.Subscribe<BeforeResourceStartedEvent>((@event, _) =>
                {
                    var restartHandler = @event.Services.GetRequiredService<OwnedCm4SimulatorRestartCommandHandler>();
                    if (ReferenceEquals(@event.Resource, cm4Resource))
                    {
                        restartHandler.InstallCm4(cm4Resource, simulatorResource);
                    }
                    else if (ReferenceEquals(@event.Resource, simulatorResource))
                    {
                        restartHandler.Install(simulatorResource);
                    }

                    return Task.CompletedTask;
                });
                builder.Eventing.Subscribe<AfterResourcesCreatedEvent>((@event, _) =>
                {
                    var restartHandler = @event.Services.GetRequiredService<OwnedCm4SimulatorRestartCommandHandler>();
                    restartHandler.InstallCm4(cm4Resource, simulatorResource);
                    restartHandler.Install(simulatorResource);
                    return Task.CompletedTask;
                });

                builder.CreateResourceBuilder<ContainerResource>(
                        (ContainerResource)simulator.Resource)
                    .WithCommand(
                        "reconfigure-cm4-transport",
                        "無効化確認後に Simulator を再作成し、現世代へ送信先を適用します",
                        async context =>
                        {
                            return await context.Services
                                .GetRequiredService<OwnedCm4SimulatorRestartCommandHandler>()
                                .ExecuteAsync(context)
                                .ConfigureAwait(false);
                        });
            }
        }

        return new BaseSimulationResources(duck, simulator, gameController, cm4Simulator, crane);
    }

    private static IResourceBuilder<ContainerResource> AddStandardContainer(
        IDistributedApplicationBuilder builder,
        DockerContainerSpec spec,
        string runId,
        string role)
    {
        var resource = builder.AddContainer(spec.ResourceName, spec.Image)
            .WithContainerName(spec.ContainerName)
            .WithArgs(spec.ContainerArguments.ToArray());
        foreach (var (key, value) in spec.Environment)
        {
            resource.WithEnvironment(key, value);
        }

        foreach (var mount in spec.Mounts)
        {
            resource.WithBindMount(mount.Source, mount.Target, isReadOnly: mount.ReadOnly);
        }

        resource.WithContainerRuntimeArgs(
            "--label", $"{DockerContainerSpec.StackLabel}={spec.StackId}",
            "--label", $"{DockerContainerSpec.ResourceLabel}={spec.ResourceName}",
            "--label", $"{DockerContainerSpec.RunLabel}={runId}",
            "--label", $"{ContainerIdentityExecHandoff.RunIdLabel}={runId}",
            "--label", $"{ContainerIdentityExecHandoff.RoleLabel}={role}");
        return resource;
    }

    /// <summary>現行CM4の所有済みIPv4だけを標準Simulatorの引数へ追加します。</summary>
    private static async Task AppendCurrentCm4FeedbackAddressAsync(
        CommandLineArgsCallbackContext context,
        IResource cm4Resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(cm4Resource);
        var cancellationToken = context.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        var services = context.ExecutionContext.Services;
        var notifications = services.GetRequiredService<ResourceNotificationService>();
        var registry = services.GetRequiredService<DcpBaseResourceReadinessRegistry>();
        var readinessEvents = services.GetRequiredService<DcpBaseResourceReadinessEventConsumer>();
        var ownership = services.GetRequiredService<DcpBaseResourceOwnershipEventConsumer>();

        if (!notifications.TryGetCurrentState(cm4Resource.Name, out var currentByName) ||
            !ReferenceEquals(currentByName.Resource, cm4Resource) ||
            currentByName.Snapshot.State?.Text != "Running")
        {
            throw new InvalidOperationException("The current CM4 container identity is unavailable before Simulator start.");
        }

        ResourceEvent? GetCurrent(string resourceId) =>
            notifications.TryGetCurrentState(resourceId, out var current) ? current : null;

        readinessEvents.Observe(currentByName, currentByName, "cm4-sim", GetCurrent);
        var before = GetCurrentCm4Candidate(registry, cm4Resource);
        if (!IsCurrentCandidate(before, currentByName, registry, GetCurrent, requireOwnershipProof: false))
        {
            throw new InvalidOperationException("The current CM4 resource key or full container identity is not verified.");
        }

        var verification = await ownership.VerifyCurrentAsync(GetCurrent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (verification.Disposition != DcpBaseResourceOwnershipDisposition.OwnershipVerifiedServiceReadinessUnknown)
        {
            throw new InvalidOperationException("The current standard DCP container ownership and shared network membership could not be verified.");
        }

        if (!notifications.TryGetCurrentState(cm4Resource.Name, out var currentAfter) ||
            !ReferenceEquals(currentAfter.Resource, cm4Resource) ||
            currentAfter.Snapshot.State?.Text != "Running")
        {
            throw new InvalidOperationException("The CM4 resource changed while its current feedback address was being verified.");
        }

        var after = GetCurrentCm4Candidate(registry, cm4Resource);
        if (before is null || after is null ||
            before.ResourceId != after.ResourceId ||
            before.ContainerId != after.ContainerId ||
            before.Generation != after.Generation ||
            !IsCurrentCandidate(after, currentAfter, registry, GetCurrent, requireOwnershipProof: true) ||
            !System.Net.IPAddress.TryParse(after.ObservedNetworkAddress, out var address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new InvalidOperationException("The current owned CM4 IPv4 feedback address changed or is unavailable.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        context.Args.Add("--ibis-feedback-addr");
        context.Args.Add(address.ToString());
    }

    /// <summary>登録済みCM4の通知キーに対応する候補を取得します。</summary>
    private static DcpBaseResourceIdentityCandidate? GetCurrentCm4Candidate(
        DcpBaseResourceReadinessRegistry registry,
        IResource cm4Resource) =>
        registry.GetCandidates().SingleOrDefault(candidate =>
            ReferenceEquals(candidate.Resource, cm4Resource) && candidate.Role == "cm4-sim");

    /// <summary>現在通知と所有証明が同じCM4候補を示すことを確認します。</summary>
    private static bool IsCurrentCandidate(
        DcpBaseResourceIdentityCandidate? candidate,
        ResourceEvent current,
        DcpBaseResourceReadinessRegistry registry,
        Func<string, ResourceEvent?> getCurrent,
        bool requireOwnershipProof)
    {
        if (candidate is null || candidate.State != "Running" || candidate.ResourceKeyAmbiguous ||
            string.IsNullOrWhiteSpace(candidate.ResourceId) ||
            !OwnedContainerInspection.IsFullId(candidate.ContainerId) ||
            requireOwnershipProof &&
                (!candidate.IdentityVerified || !candidate.NetworkVerified || !candidate.OwnershipVerified) ||
            !ReferenceEquals(current.Resource, candidate.Resource) ||
            current.ResourceId != candidate.ResourceId || current.Snapshot.State?.Text != "Running" ||
            !RegisteredContainerEventProjection.TryProject(
                candidate.Resource, candidate.ResourceId, "cm4-sim", current, current, out var projected) ||
            projected is null || projected.ContainerId != candidate.ContainerId)
        {
            return false;
        }

        var currentById = getCurrent(candidate.ResourceId);
        return currentById is not null &&
            ReferenceEquals(currentById.Resource, candidate.Resource) &&
            currentById.ResourceId == candidate.ResourceId &&
            currentById.Snapshot.State?.Text == "Running" &&
            RegisteredContainerEventProjection.TryProject(
                candidate.Resource, candidate.ResourceId, "cm4-sim", currentById, currentById, out var latest) &&
            latest is not null && latest.ContainerId == candidate.ContainerId &&
            (!requireOwnershipProof || registry.AreOwnershipProofsCurrent([candidate]));
    }

    private static IResourceBuilder<IResourceWithWaitSupport> AsResourceBuilder<TResource>(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<TResource> resource)
        where TResource : IResourceWithWaitSupport => builder.CreateResourceBuilder<IResourceWithWaitSupport>(resource.Resource);
}
