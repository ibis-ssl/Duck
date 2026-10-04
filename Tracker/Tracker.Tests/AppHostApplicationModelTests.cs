using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Duck.Testing.AppHost;

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
            var exception = Assert.Throws<InvalidOperationException>(() => StackOwnershipLease.Acquire(lockPath));
            Assert.Contains("already owns", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        using var nextOwner = StackOwnershipLease.Acquire(lockPath);
    }

    [Fact]
    public async Task BaseModelKeepsDuckAsProjectAndEachDockerServiceAsHealthyExecutableWrapper()
    {
        using var appHost = await CreateAppHostAsync();

        Assert.IsType<ProjectResource>(Assert.Single(appHost.Resources, resource => resource.Name == "duck"));
        foreach (var name in new[] { "simulator", "game-controller", "crane", "cm4-sim" })
        {
            var resource = Assert.IsType<ExecutableResource>(Assert.Single(appHost.Resources, item => item.Name == name));
            Assert.Single(resource.Annotations.OfType<HealthCheckAnnotation>());
            Assert.Contains(resource.Annotations.OfType<EndpointAnnotation>(), endpoint => endpoint.Name == "health");

            var launch = await GetLaunchOptionsAsync(resource);
            Assert.Equal(name, launch.ResourceName);
            Assert.NotEmpty(launch.RunId);
            Assert.InRange(launch.HealthPort, 1, 65535);
            var spec = ToSpec(launch);
            Assert.Contains("--network", spec.CreateRunArguments());
            Assert.Contains("host", spec.CreateRunArguments());
            Assert.Contains("duck.aspire.run=" + launch.RunId, spec.CreateRunArguments());
        }
    }

    [Fact]
    public async Task BaseModelKeepsPinnedSimulatorAndCraneArguments()
    {
        using var appHost = await CreateAppHostAsync();
        var simulator = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "simulator")));
        Assert.Equal("ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd", simulator.Image);
        Assert.Equal(
            ["tini", "--", "./bin/simulator-cli", "-g", "2020B", "--realism", "None", "--ibis-port", "12346", "--ibis-team-color", "yellow"],
            simulator.ContainerArguments);

        var crane = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "crane")));
        Assert.Equal("ghcr.io/ibis-ssl/crane:scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d", crane.Image);
        Assert.Contains("team:=Yellow", crane.ContainerArguments[^1]);
        Assert.Equal("visibility_graph", crane.Environment["PLANNER"]);
        Assert.Equal("12345", crane.Environment["CRANE_TARGET_PORT"]);
        Assert.DoesNotContain(crane.Environment, item => item.Key == "FEEDBACK_SIM_MODE");
    }

    [Fact]
    public async Task GameControllerIsTheOnlyRefereeProducerAndUsesPinnedHostNetworkContainerSpec()
    {
        using var appHost = await CreateAppHostAsync();
        var gameController = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "game-controller")));

        Assert.Equal("robocupssl/ssl-game-controller:3.20.3", gameController.Image);
        Assert.Equal(
            ["-visionAddress", "224.5.23.2:10020", "-trackerAddress", "224.5.23.2:11010", "-publishAddress", "224.5.23.1:11003", "-address", ":8082"],
            gameController.ContainerArguments);

        var producers = new List<string>();
        foreach (var resource in appHost.Resources.OfType<ExecutableResource>()
                     .Where(resource => resource.Name is "simulator" or "game-controller" or "crane" or "cm4-sim"))
        {
            var launch = await GetLaunchOptionsAsync(resource);
            if (launch.ContainerArguments.Contains("-publishAddress") && launch.ContainerArguments.Contains("224.5.23.1:11003"))
            {
                producers.Add(resource.Name);
            }
        }

        Assert.Equal(["game-controller"], producers);
    }

    [Fact]
    public async Task WrapperHealthDependenciesWaitForHealthyServicesAndDuckProjectStart()
    {
        using var appHost = await CreateAppHostAsync();
        var simulator = Find(appHost, "simulator");
        var cm4 = Find(appHost, "cm4-sim");
        var duck = Find(appHost, "duck");
        var gameController = Find(appHost, "game-controller");
        var crane = Find(appHost, "crane");

        AssertSingleWait(duck, simulator, WaitType.WaitUntilHealthy);
        AssertWaits(crane, (duck, WaitType.WaitUntilStarted), (gameController, WaitType.WaitUntilHealthy), (cm4, WaitType.WaitUntilHealthy));
        AssertSingleWait(cm4, simulator, WaitType.WaitUntilHealthy);
    }

    [Fact]
    public async Task ConfigOverridesAndNonVisibilityPlannerChangeWrapperSpecAndDependencies()
    {
        using var appHost = await CreateAppHostAsync(
            "--Testing:Simulator:ImageTag=custom-tag",
            "--Testing:Simulator:Geometry=custom-geometry",
            "--Testing:Simulator:Realism=Custom",
            "--Testing:Simulator:IbisPort=22346",
            "--Testing:Simulator:IbisTeamColor=blue",
            "--Testing:Crane:Planner=rvo2");

        Assert.DoesNotContain(appHost.Resources, resource => resource.Name == "cm4-sim");
        var simulator = await GetLaunchOptionsAsync(Assert.IsType<ExecutableResource>(Find(appHost, "simulator")));
        Assert.Equal("ghcr.io/ibis-ssl/framework-simulatorcli:custom-tag", simulator.Image);
        Assert.Contains("custom-geometry", simulator.ContainerArguments);
        Assert.Contains("Custom", simulator.ContainerArguments);
        Assert.Contains("22346", simulator.ContainerArguments);
        Assert.Contains("blue", simulator.ContainerArguments);

        var craneResource = Assert.IsType<ExecutableResource>(Find(appHost, "crane"));
        var crane = await GetLaunchOptionsAsync(craneResource);
        Assert.Equal("rvo2", crane.Environment["PLANNER"]);
        Assert.Equal("22346", crane.Environment["CRANE_TARGET_PORT"]);
        Assert.Equal("true", crane.Environment["FEEDBACK_SIM_MODE"]);
        AssertWaits(craneResource, (Find(appHost, "duck"), WaitType.WaitUntilStarted), (Find(appHost, "game-controller"), WaitType.WaitUntilHealthy));
    }

    private static async Task<IDistributedApplicationTestingBuilder> CreateAppHostAsync(params string[] additionalArgs) =>
        await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>(
            [.. additionalArgs, CreateIsolatedOwnershipLockArgument()]);

    private static IResource Find(IDistributedApplicationTestingBuilder appHost, string name) =>
        Assert.Single(appHost.Resources, resource => resource.Name == name);

    private static async Task<DockerWrapperLaunchOptions> GetLaunchOptionsAsync(ExecutableResource resource)
    {
        var launch = Assert.Single(resource.Annotations.OfType<DockerWrapperLaunchOptionsAnnotation>()).Options;
        var configuration = await ExecutionConfigurationBuilder.Create(resource)
            .WithArgumentsConfig()
            .BuildAsync(new(DistributedApplicationOperation.Run), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, CancellationToken.None);
        var arguments = configuration.Arguments.Select(value => value.Value?.ToString() ?? string.Empty).ToArray();
        var marker = Array.IndexOf(arguments, "--docker-wrapper");
        Assert.True(marker >= 0 && marker + 1 < arguments.Length, $"Wrapper argv was missing from {resource.Name}: {string.Join(" | ", arguments)}");
        var passedOptions = System.Text.Json.JsonSerializer.Deserialize<DockerWrapperLaunchOptions>(arguments[marker + 1]);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(launch), System.Text.Json.JsonSerializer.Serialize(passedOptions));
        return launch;
    }

    private static DockerContainerSpec ToSpec(DockerWrapperLaunchOptions launch) =>
        new(launch.ResourceName, launch.Image, launch.ContainerArguments, launch.Environment, launch.StackId, launch.RunId);

    private static string CreateIsolatedOwnershipLockArgument() =>
        $"--Testing:StackOwnership:LockPath={Path.Combine(Path.GetTempPath(), $"duck-aspire-model-{Guid.NewGuid():N}.lock")}";

    private static void AssertSingleWait(IResource resource, IResource dependency, WaitType type) =>
        AssertWaits(resource, (dependency, type));

    private static void AssertWaits(IResource resource, params (IResource Dependency, WaitType Type)[] dependencies)
    {
        var waits = resource.Annotations.OfType<WaitAnnotation>().ToArray();
        Assert.Equal(dependencies.Length, waits.Length);
        foreach (var (dependency, type) in dependencies)
        {
            var wait = Assert.Single(waits, annotation => ReferenceEquals(annotation.Resource, dependency));
            Assert.Equal(type, wait.WaitType);
        }
    }

}
