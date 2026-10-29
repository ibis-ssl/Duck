using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

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
    internal static bool UsesCm4Simulator(string planner) =>
        string.Equals(planner, "visibility_graph", StringComparison.Ordinal);

    internal static BaseSimulationResources Add(
        IDistributedApplicationBuilder builder,
        BaseSimulationRegistrationOptions options,
        Func<IResourceBuilder<IResource>, IResourceBuilder<ContainerResource>> addDuck,
        Action<IResourceBuilder<IResource>> addGameControllerDependentResource,
        ContainerIdentityHandoffStateStore? handoffStates = null)
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
            var cm4Spec = new DockerContainerSpec(
                "cm4-sim",
                $"ghcr.io/ibis-ssl/orion-cm4-sim:{options.Cm4SimulatorImageTag}",
                [
                    "tini", "--", "cm4_sim", "--robot-ids", "0,1,2,3,4,5,6,7,8,9,10",
                    "--in-port", "12345", "--out-addr", "127.0.0.1", "--out-port", options.SimulatorIbisPort,
                    "--feedback-port-base", "50100", "--multicast-if", "127.0.0.1", "--rate-hz", "1000",
                    "--rx-delay-ms", "0", "--rx-jitter-ms", "0", "--rx-loss-rate", "0.0", "--seed", "0",
                ],
                new Dictionary<string, string>(StringComparer.Ordinal),
                options.StackId);
            if (options.UseStandardDcpResources)
            {
                cm4Simulator = AsResourceBuilder(builder, AddStandardContainer(
                    builder, cm4Spec, options.DuckRunId, "cm4-sim").WaitFor(simulator));
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
            crane.WaitFor(builder.CreateResourceBuilder<IResource>(cm4Simulator.Resource));
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
                readinessResources.ToArray());
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

    private static IResourceBuilder<IResourceWithWaitSupport> AsResourceBuilder<TResource>(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<TResource> resource)
        where TResource : IResourceWithWaitSupport => builder.CreateResourceBuilder<IResourceWithWaitSupport>(resource.Resource);
}
