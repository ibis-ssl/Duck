using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Tracker.Tests;

public sealed class AppHostApplicationModelTests
{
    [Fact]
    public async Task BaseModelContainsDuckRuntimeHostProject()
    {
        using var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>();

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
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>();

        var simulator = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "simulator"));
        var image = Assert.Single(simulator.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal("ghcr.io", image.Registry);
        Assert.Equal("ibis-ssl/framework-simulatorcli", image.Image);
        Assert.Equal("a52b6bd", image.Tag);
        Assert.Equal("tini", simulator.Entrypoint);

        var args = await simulator.GetArgumentValuesAsync(DistributedApplicationOperation.Run);
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
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>();

        var simulator = Assert.Single(appHost.Resources, resource => resource.Name == "simulator");
        var duck = Assert.IsType<ProjectResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "duck"));
        var environment = await duck.GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Run);
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
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(args);

        var simulator = Assert.IsType<ContainerResource>(
            Assert.Single(appHost.Resources, resource => resource.Name == "simulator"));
        var image = Assert.Single(simulator.Annotations.OfType<ContainerImageAnnotation>());
        var containerArgs = await simulator.GetArgumentValuesAsync(DistributedApplicationOperation.Run);

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
