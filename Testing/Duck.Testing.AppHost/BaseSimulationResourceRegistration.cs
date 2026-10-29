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
    CraneTargetAddressSettings? CraneTargetAddressSettings,
    string? CraneDiagnosticsPath);

internal sealed record BaseSimulationResources(
    IResourceBuilder<ContainerResource> Duck,
    IResourceBuilder<ExecutableResource> Simulator,
    IResourceBuilder<ExecutableResource> GameController,
    IResourceBuilder<ExecutableResource>? Cm4Simulator,
    IResourceBuilder<ExecutableResource> Crane);

/// <summary>通常構成で選択された資源を、AppHostの実登録経路から登録する。</summary>
internal static class BaseSimulationResourceRegistration
{
    internal static bool UsesCm4Simulator(string planner) =>
        string.Equals(planner, "visibility_graph", StringComparison.Ordinal);

    internal static BaseSimulationResources Add(
        IDistributedApplicationBuilder builder,
        BaseSimulationRegistrationOptions options,
        Func<IResourceBuilder<ExecutableResource>, IResourceBuilder<ContainerResource>> addDuck,
        Action<IResourceBuilder<ExecutableResource>> addGameControllerDependentResource)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(addDuck);
        ArgumentNullException.ThrowIfNull(addGameControllerDependentResource);
        var useCm4Simulator = UsesCm4Simulator(options.CranePlanner);

        CraneFixtureManifest.WriteAtomically(options.FixtureManifestPath, options.StackId,
            new CraneFixtureSettings(options.CraneTeamName, options.CraneTeamColor));

        var simulator = builder.AddDockerWrapper(
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
            "simulator");

        var duck = addDuck(simulator)
            .WithEnvironment("VisionReceiver__MulticastAddress", "224.5.23.2")
            .WithEnvironment("VisionReceiver__Port", "10020")
            .WaitFor(simulator);

        var gameController = builder.AddDockerWrapper(
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
            expectedCraneTeamName: options.CraneTeamName);

        addGameControllerDependentResource(gameController);

        IResourceBuilder<ExecutableResource>? cm4Simulator = null;
        if (useCm4Simulator)
        {
            cm4Simulator = builder.AddDockerWrapper(
                new DockerContainerSpec(
                    "cm4-sim",
                    $"ghcr.io/ibis-ssl/orion-cm4-sim:{options.Cm4SimulatorImageTag}",
                    [
                        "tini", "--", "cm4_sim", "--robot-ids", "0,1,2,3,4,5,6,7,8,9,10",
                        "--in-port", "12345", "--out-addr", "127.0.0.1", "--out-port", options.SimulatorIbisPort,
                        "--feedback-port-base", "50100", "--multicast-if", "127.0.0.1", "--rate-hz", "1000",
                        "--rx-delay-ms", "0", "--rx-jitter-ms", "0", "--rx-loss-rate", "0.0", "--seed", "0",
                    ],
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    options.StackId),
                39103,
                "cm4_sim",
                "cm4-sim")
                .WaitFor(simulator);
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

        var craneBuilder = builder
            .AddDockerWrapper(
                new DockerContainerSpec(
                    "crane",
                    $"ghcr.io/ibis-ssl/crane:{options.CraneImageTag}",
                    ["bash", "-c", craneCommand],
                    craneEnvironment,
                    options.StackId,
                    mounts: craneMounts),
                39104,
                "ros2",
                "crane",
                startupTimeoutSeconds: 360,
                expectedCraneTeamName: options.CraneTeamName);

        var crane = craneBuilder
            .WaitForStart(duck)
            .WaitFor(gameController);
        if (cm4Simulator is not null)
        {
            crane.WaitFor(cm4Simulator);
        }

        return new BaseSimulationResources(duck, simulator, gameController, cm4Simulator, crane);
    }
}
