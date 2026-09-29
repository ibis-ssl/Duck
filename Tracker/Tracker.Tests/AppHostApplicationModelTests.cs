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
    public async Task BaseModelContainsSingleGameControllerRefereeProducer()
    {
        using var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Duck_Testing_AppHost>();

        var gameController =
            Assert.IsType<ContainerResource>(
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
            args.Select(value => value.ToString()).ToArray());

        var runtimeArgs = new List<object>();
        foreach (var annotation in gameController.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
        {
            await annotation.Callback(
                new ContainerRuntimeArgsCallbackContext(runtimeArgs, CancellationToken.None));
        }

        Assert.Equal(
            ["--network", "host"],
            runtimeArgs.Select(value => value?.ToString() ?? string.Empty).ToArray());

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
}
