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
}
