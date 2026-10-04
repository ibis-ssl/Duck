using Duck.Testing.AppHost;
using System.Text.Json;

namespace Tracker.Tests;

public sealed class DockerContainerSpecTests
{
    [Fact]
    public void RunArgumentsUseOneFixedHostNetworkContainerAndExplicitOwnerLabels()
    {
        var spec = new DockerContainerSpec(
            "simulator",
            "ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd",
            ["tini", "--", "./bin/simulator-cli", "-g", "2020B"],
            new Dictionary<string, string> { ["SIM_MODE"] = "base" },
            "stack-17");

        Assert.Equal(
            [
                "run", "--detach", "--name", "duck-stack-17-simulator",
                "--network", "host",
                "--label", "duck.aspire.owner=duck-apphost",
                "--label", "duck.aspire.stack=stack-17",
                "--label", "duck.aspire.resource=simulator",
                "--label", $"duck.aspire.run={spec.RunId}",
                "--env", "SIM_MODE=base",
                "ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd",
                "tini", "--", "./bin/simulator-cli", "-g", "2020B",
            ],
            spec.CreateRunArguments());
    }

    [Theory]
    [InlineData("--rm")]
    [InlineData("--name")]
    [InlineData("-v")]
    [InlineData("--network")]
    public void ContainerArgumentsCannotOverrideOwnershipOrLifecycle(string forbidden)
    {
        var exception = Assert.Throws<ArgumentException>(() => new DockerContainerSpec(
            "simulator",
            "image:tag",
            [forbidden, "value"],
            new Dictionary<string, string>(),
            "stack-17"));

        Assert.Contains("reserved", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellMetacharactersRemainIndividualArguments()
    {
        var spec = new DockerContainerSpec(
            "crane",
            "image:tag",
            ["bash", "-c", "source setup && ros2 launch crane.launch.xml"],
            new Dictionary<string, string>(),
            "stack-17");

        var args = spec.CreateRunArguments();

        Assert.Contains("bash", args);
        Assert.Contains("-c", args);
        Assert.Contains("source setup && ros2 launch crane.launch.xml", args);
    }

    [Fact]
    public void OwnerDiscoveryIsScopedToOneStackResourceAndRun()
    {
        var spec = new DockerContainerSpec("simulator", "image:tag", [], new Dictionary<string, string>(), "stack-17");

        Assert.Equal(
            [
                "ps", "--all", "--quiet",
                "--filter", "label=duck.aspire.owner=duck-apphost",
                "--filter", "label=duck.aspire.stack=stack-17",
                "--filter", "label=duck.aspire.resource=simulator",
                "--filter", $"label=duck.aspire.run={spec.RunId}",
            ],
            spec.CreateOwnerDiscoveryArguments());
    }

    [Fact]
    public void ExactOwnerInspectionRejectsForeignImageNetworkOrLabels()
    {
        var spec = new DockerContainerSpec("simulator", "image:tag", [], new Dictionary<string, string>(), "stack-17");
        using var exact = JsonDocument.Parse(Inspection(spec));
        using var foreign = JsonDocument.Parse(Inspection(spec, image: "foreign:tag"));
        using var bridged = JsonDocument.Parse(Inspection(spec, networkMode: "bridge"));
        using var wrongRun = JsonDocument.Parse(Inspection(spec, runId: "another-run"));

        Assert.True(spec.IsExactOwner(exact.RootElement, "container-id"));
        Assert.False(spec.IsExactOwner(exact.RootElement, "other-container-id"));
        Assert.False(spec.IsExactOwner(foreign.RootElement, "container-id"));
        Assert.False(spec.IsExactOwner(bridged.RootElement, "container-id"));
        Assert.False(spec.IsExactOwner(wrongRun.RootElement, "container-id"));
    }

    [Fact]
    public void DestructiveDockerArgumentsRequireExactContainerId()
    {
        Assert.Equal(["inspect", "--format", "{{json .}}", "container-id"], DockerContainerSpec.CreateInspectArguments("container-id"));
        Assert.Equal(["rm", "--force", "container-id"], DockerContainerSpec.CreateRemoveArguments("container-id"));
        Assert.Throws<ArgumentException>(() => DockerContainerSpec.CreateRemoveArguments(" "));
    }

    [Fact]
    public void DockerCliUsesArgumentListInsteadOfAHostShellString()
    {
        var info = DockerCliProcess.CreateStartInfo("docker", ["run", "image:tag", "bash", "-c", "echo one && echo two"]);

        Assert.Equal("docker", info.FileName);
        Assert.False(info.UseShellExecute);
        Assert.Equal(["run", "image:tag", "bash", "-c", "echo one && echo two"], info.ArgumentList);
        Assert.Empty(info.Arguments);
    }

    private static string Inspection(
        DockerContainerSpec spec,
        string? image = null,
        string networkMode = "host",
        string? runId = null) => JsonSerializer.Serialize(new
        {
            Id = "container-id",
            Name = $"/{spec.ContainerName}",
            Config = new
            {
                Image = image ?? spec.Image,
                Labels = new Dictionary<string, string>
                {
                    [DockerContainerSpec.OwnerLabel] = DockerContainerSpec.OwnerValue,
                    [DockerContainerSpec.StackLabel] = spec.StackId,
                    [DockerContainerSpec.ResourceLabel] = spec.ResourceName,
                    [DockerContainerSpec.RunLabel] = runId ?? spec.RunId,
                },
            },
            HostConfig = new { NetworkMode = networkMode },
        });
}
