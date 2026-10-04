using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Duck.Testing.AppHost;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tracker.Tests;

public sealed class AppHostApplicationModelTests
{
    [Fact]
    public void StackOwnershipLeaseRejectsSecondOwnerAndAllowsReuseWithExistingMarker()
    {
        var lockPath = Path.Combine(Path.GetTempPath(), $"duck-aspire-stack-{Guid.NewGuid():N}.lock");
        File.WriteAllText(lockPath, "stale-owner=previous-run");

        using (StackOwnershipLease.Acquire(lockPath))
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => StackOwnershipLease.Acquire(lockPath));

            Assert.Contains("already owns", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        using var nextOwner = StackOwnershipLease.Acquire(lockPath);
    }

    [Fact]
    public async Task BaseModelContainsDuckRuntimeHostProject()
    {
        using var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>([CreateIsolatedOwnershipLockArgument()]);

        var duck = Assert.Single(appHost.Resources, resource => resource.Name == "duck");
        var project = Assert.IsType<ProjectResource>(duck);
        var metadata = Assert.Single(project.Annotations.OfType<IProjectMetadata>());

        Assert.Equal("Tracker.RuntimeHost.csproj", Path.GetFileName(metadata.ProjectPath));
        Assert.Equal("Tracker.RuntimeHost", Path.GetFileName(Path.GetDirectoryName(metadata.ProjectPath)));
    }

    [Fact]
    public async Task BaseModelContainsPinnedSimulatorWithCraneScenarioDefaults()
    {
        using var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>([CreateIsolatedOwnershipLockArgument()]);

        var simulator = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "simulator"));
        var image = Assert.Single(simulator.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal("ghcr.io", image.Registry);
        Assert.Equal("ibis-ssl/framework-simulatorcli", image.Image);
        Assert.Equal("a52b6bd", image.Tag);
        Assert.Equal("tini", simulator.Entrypoint);

        var executionConfiguration = await ExecutionConfigurationBuilder.Create(simulator)
            .WithArgumentsConfig()
            .BuildAsync(
                new(DistributedApplicationOperation.Run),
                NullLogger.Instance,
                CancellationToken.None);
        var args = executionConfiguration.Arguments
            .Select(argument => argument.Value?.ToString() ?? string.Empty)
            .ToArray();
        Assert.Equal(
            [
                "--",
                "./bin/simulator-cli",
                "-g",
                "2020B",
                "--realism",
                "None",
                "--ibis-port",
                "12346",
                "--ibis-team-color",
                "yellow",
            ],
            args);

        var runtimeArgs = await GetContainerRuntimeArgsAsync(simulator);
        Assert.Equal(["--network", "host"], runtimeArgs);
    }

    [Fact]
    public async Task BaseModelPassesSimVisionSettingsToDuckAndWaitsForSimulatorStart()
    {
        using var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>([CreateIsolatedOwnershipLockArgument()]);

        var simulator = Assert.Single(appHost.Resources, resource => resource.Name == "simulator");
        var duck = Assert.IsType<ProjectResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "duck"));
        var executionConfiguration = await ExecutionConfigurationBuilder.Create(duck)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(
                new(DistributedApplicationOperation.Run),
                NullLogger.Instance,
                CancellationToken.None);
        var environment = executionConfiguration.EnvironmentVariables.ToDictionary();
        var wait = Assert.Single(duck.Annotations.OfType<WaitAnnotation>());

        Assert.Equal("sim", environment["Tracker__ActiveProfileName"]);
        Assert.Equal("224.5.23.2", environment["VisionReceiver__MulticastAddress"]);
        Assert.Equal("10020", environment["VisionReceiver__Port"]);
        Assert.Same(simulator, wait.Resource);
        Assert.Equal(WaitType.WaitUntilStarted, wait.WaitType);
    }

    [Fact]
    public async Task SimulatorSettingsCanBeOverriddenFromAppHostConfiguration()
    {
        string[] args =
        [
            "--Testing:Simulator:ImageTag=custom-tag",
            "--Testing:Simulator:Geometry=custom-geometry",
            "--Testing:Simulator:Realism=Custom",
            "--Testing:Simulator:IbisPort=22346",
            "--Testing:Simulator:IbisTeamColor=blue",
        ];
        using var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>([.. args, CreateIsolatedOwnershipLockArgument()]);

        var simulator = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "simulator"));
        var image = Assert.Single(simulator.Annotations.OfType<ContainerImageAnnotation>());
        var executionConfiguration = await ExecutionConfigurationBuilder.Create(simulator)
            .WithArgumentsConfig()
            .BuildAsync(
                new(DistributedApplicationOperation.Run),
                NullLogger.Instance,
                CancellationToken.None);
        var containerArgs = executionConfiguration.Arguments
            .Select(argument => argument.Value?.ToString() ?? string.Empty)
            .ToArray();

        Assert.Equal("custom-tag", image.Tag);
        Assert.Equal(
            [
                "--",
                "./bin/simulator-cli",
                "-g",
                "custom-geometry",
                "--realism",
                "Custom",
                "--ibis-port",
                "22346",
                "--ibis-team-color",
                "blue",
            ],
            containerArgs);
    }

    [Fact]
    public async Task BaseModelContainsSingleGameControllerRefereeProducer()
    {
        using var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>([CreateIsolatedOwnershipLockArgument()]);

        var gameController = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "game-controller"));
        var image = Assert.Single(gameController.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal("robocupssl/ssl-game-controller", image.Image);
        Assert.Equal("3.20.3", image.Tag);

        var args = await GetArgumentsAsync(gameController);
        Assert.Equal(
            [
                "-visionAddress",
                "224.5.23.2:10020",
                "-trackerAddress",
                "224.5.23.2:11010",
                "-publishAddress",
                "224.5.23.1:11003",
                "-address",
                ":8082",
            ],
            args);

        var runtimeArgs = await GetContainerRuntimeArgsAsync(gameController);
        Assert.Equal(["--network", "host"], runtimeArgs);

        var refereeProducers = new List<string>();
        foreach (var resource in appHost.Resources.OfType<IResourceWithArgs>())
        {
            var values = await GetArgumentsAsync(resource);
            if (PublishesRefereeEndpoint(values))
            {
                refereeProducers.Add(resource.Name);
            }
        }

        Assert.Equal(["game-controller"], refereeProducers);
    }

    [Fact]
    public async Task BaseModelContainsPinnedCraneAndCm4SimulatorWithHostNetworking()
    {
        using var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>([CreateIsolatedOwnershipLockArgument()]);

        var crane = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "crane"));
        var craneImage = Assert.Single(crane.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io", craneImage.Registry);
        Assert.Equal("ibis-ssl/crane", craneImage.Image);
        Assert.Equal("scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d", craneImage.Tag);
        Assert.Equal(["--network", "host"], await GetContainerRuntimeArgsAsync(crane));

        var cm4Simulator = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "cm4-sim"));
        var cm4Image = Assert.Single(cm4Simulator.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io", cm4Image.Registry);
        Assert.Equal("ibis-ssl/orion-cm4-sim", cm4Image.Image);
        Assert.Matches("^[0-9a-f]{7,40}$", cm4Image.Tag);
        Assert.Equal(["--network", "host"], await GetContainerRuntimeArgsAsync(cm4Simulator));
    }

    [Fact]
    public async Task VisibilityGraphCraneUsesModeFourPathAndWaitsForItsPrerequisites()
    {
        using var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>([CreateIsolatedOwnershipLockArgument()]);

        var crane = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "crane"));
        var cm4Simulator = Assert.Single(appHost.Resources, resource => resource.Name == "cm4-sim");
        var duck = Assert.Single(appHost.Resources, resource => resource.Name == "duck");
        var gameController = Assert.Single(appHost.Resources, resource => resource.Name == "game-controller");

        var craneArgs = await GetArgumentsAsync(crane);
        Assert.Contains(craneArgs, argument =>
            argument.Contains("planner:=${PLANNER}", StringComparison.Ordinal));
        Assert.Contains(craneArgs, argument => argument.Contains("team:=Yellow", StringComparison.Ordinal));
        var craneEnvironment = await GetEnvironmentVariablesAsync(crane);
        Assert.Equal("visibility_graph", craneEnvironment["PLANNER"]);
        Assert.Equal("12345", craneEnvironment["CRANE_TARGET_PORT"]);

        var cm4Args = await GetArgumentsAsync(Assert.IsAssignableFrom<IResourceWithArgs>(cm4Simulator));
        Assert.Contains("12345", cm4Args);
        Assert.Contains("12346", cm4Args);

        AssertWaitsFor(crane, cm4Simulator, duck, gameController);
        AssertWaitsFor(cm4Simulator, Assert.Single(appHost.Resources, resource => resource.Name == "simulator"));
    }

    [Fact]
    public async Task PlannerWithoutCm4SimulatorDoesNotCreateThatResourceOrDependency()
    {
        string[] args = ["--Testing:Crane:Planner=rvo2", CreateIsolatedOwnershipLockArgument()];
        using var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(args);

        Assert.DoesNotContain(appHost.Resources, resource => resource.Name == "cm4-sim");
        var crane = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "crane"));
        var duck = Assert.Single(appHost.Resources, resource => resource.Name == "duck");
        var gameController = Assert.Single(appHost.Resources, resource => resource.Name == "game-controller");

        var craneArgs = await GetArgumentsAsync(crane);
        Assert.Contains(craneArgs, argument => argument.Contains("planner:=${PLANNER}", StringComparison.Ordinal));
        var craneEnvironment = await GetEnvironmentVariablesAsync(crane);
        Assert.Equal("rvo2", craneEnvironment["PLANNER"]);
        Assert.Equal("12346", craneEnvironment["CRANE_TARGET_PORT"]);
        Assert.Equal("true", craneEnvironment["FEEDBACK_SIM_MODE"]);
        AssertWaitsFor(crane, duck, gameController);
    }

    [Theory]
    [InlineData("visibility_graph")]
    [InlineData("rvo2")]
    public async Task SimulatorIbisPortOverridePropagatesToCranePath(string planner)
    {
        string[] args =
        [
            "--Testing:Crane:Planner=" + planner,
            "--Testing:Simulator:IbisPort=22346",
            CreateIsolatedOwnershipLockArgument(),
        ];
        using var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(args);

        var simulator = Assert.Single(appHost.Resources, resource => resource.Name == "simulator");
        var simulatorArgs = await GetArgumentsAsync(Assert.IsAssignableFrom<IResourceWithArgs>(simulator));
        var ibisPortIndex = Array.IndexOf(simulatorArgs, "--ibis-port");
        Assert.True(ibisPortIndex >= 0 && ibisPortIndex + 1 < simulatorArgs.Length);
        Assert.Equal("22346", simulatorArgs[ibisPortIndex + 1]);

        var crane = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "crane"));
        var craneEnvironment = await GetEnvironmentVariablesAsync(crane);

        if (planner == "visibility_graph")
        {
            var cm4Simulator = Assert.Single(appHost.Resources, resource => resource.Name == "cm4-sim");
            var cm4Args = await GetArgumentsAsync(Assert.IsAssignableFrom<IResourceWithArgs>(cm4Simulator));
            var outPortIndex = Array.IndexOf(cm4Args, "--out-port");
            Assert.True(outPortIndex >= 0 && outPortIndex + 1 < cm4Args.Length);
            Assert.Equal("22346", cm4Args[outPortIndex + 1]);
            Assert.Equal("12345", craneEnvironment["CRANE_TARGET_PORT"]);
        }
        else
        {
            Assert.DoesNotContain(appHost.Resources, resource => resource.Name == "cm4-sim");
            Assert.Equal("22346", craneEnvironment["CRANE_TARGET_PORT"]);
        }
    }

    private static async Task<Dictionary<string, string>> GetEnvironmentVariablesAsync(IResourceWithEnvironment resource)
    {
        var executionConfiguration = await ExecutionConfigurationBuilder.Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(
                new(DistributedApplicationOperation.Run),
                NullLogger.Instance,
                CancellationToken.None);
        return executionConfiguration.EnvironmentVariables.ToDictionary();
    }

    private static string CreateIsolatedOwnershipLockArgument() =>
        $"--Testing:StackOwnership:LockPath={Path.Combine(Path.GetTempPath(), $"duck-aspire-model-{Guid.NewGuid():N}.lock")}";

    private static void AssertWaitsFor(IResource resource, params IResource[] dependencies)
    {
        var waits = resource.Annotations.OfType<WaitAnnotation>().ToArray();
        Assert.Equal(dependencies.Length, waits.Length);
        foreach (var dependency in dependencies)
        {
            var wait = Assert.Single(waits, annotation => ReferenceEquals(annotation.Resource, dependency));
            Assert.Equal(WaitType.WaitUntilStarted, wait.WaitType);
        }
    }

    private static async Task<string[]> GetArgumentsAsync(IResourceWithArgs resource)
    {
        var args = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
        {
            await annotation.Callback(
                new CommandLineArgsCallbackContext(args, resource, CancellationToken.None));
        }

        return args.Select(value => value?.ToString() ?? string.Empty).ToArray();
    }

    private static bool PublishesRefereeEndpoint(IReadOnlyList<string> args)
    {
        for (var index = 0; index + 1 < args.Count; index++)
        {
            if (args[index] == "-publishAddress" && args[index + 1] == "224.5.23.1:11003")
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<IReadOnlyList<string>> GetContainerRuntimeArgsAsync(ContainerResource resource)
    {
        var args = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
        {
            await annotation.Callback(
                new ContainerRuntimeArgsCallbackContext(args, CancellationToken.None));
        }

        return args.Select(value => Assert.IsType<string>(value)).ToArray();
    }
}
