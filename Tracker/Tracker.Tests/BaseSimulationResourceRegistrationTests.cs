using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Duck.Testing.AppHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Tracker.Tests;

public sealed class BaseSimulationResourceRegistrationTests
{
    [Fact]
    public void StandardCraneHealthRegistrationUsesTheCraneServiceConsumer()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "duck-base-health-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureRoot);

        try
        {
            _ = BaseSimulationResourceRegistration.Add(
                builder,
                new BaseSimulationRegistrationOptions(
                    "12346", "a52b6bd", "2020B", "None", "yellow",
                    "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                    "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a", "visibility_graph",
                    "base-health-test", "crane", "yellow", Path.Combine(fixtureRoot, "fixture.json"),
                    UseStandardDcpResources: true, DuckRunId: "run-base-health-test",
                    CraneTargetAddressSettings: null, CraneDiagnosticsPath: null,
                    DockerContextName: null, ExpectedEngineId: null, DockerExecutable: "docker.exe"),
                _ => builder.AddContainer("duck", "mcr.microsoft.com/dotnet/aspnet"),
                _ => { });

            using var services = builder.Services.BuildServiceProvider();
            var registration = Assert.Single(
                services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations,
                item => item.Name == "dcp-crane-service-ready");
            var healthCheck = registration.Factory(services);

            var dcpHealthCheck = Assert.IsType<DcpBaseResourceReadinessHealthCheck>(healthCheck);
            Assert.NotNull(dcpHealthCheck.CraneReadinessProbe);
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BaseRegistrationUsesDcpOwnedContainerResourcesForTheSelectedSimulationServices()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "duck-base-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureRoot);

        try
        {
            var resources = BaseSimulationResourceRegistration.Add(
                builder,
                new BaseSimulationRegistrationOptions(
                    "12346",
                    "a52b6bd",
                    "2020B",
                    "None",
                    "yellow",
                    "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                    "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a",
                    "visibility_graph",
                    "base-test-" + Guid.NewGuid().ToString("N"),
                    "crane",
                    "yellow",
                    Path.Combine(fixtureRoot, "fixture.json"),
                    UseStandardDcpResources: true,
                    DuckRunId: "run-base-test",
                    CraneTargetAddressSettings: null,
                    CraneDiagnosticsPath: null,
                    DockerContextName: null,
                    ExpectedEngineId: null,
                    DockerExecutable: "docker.exe"),
                _ => builder.AddContainer("duck", "mcr.microsoft.com/dotnet/aspnet"),
                _ => { });

            Assert.IsType<ContainerResource>(resources.Simulator.Resource);
            Assert.IsType<ContainerResource>(resources.GameController.Resource);
            Assert.IsType<ContainerResource>(resources.Cm4Simulator!.Resource);
            Assert.IsType<ContainerResource>(resources.Crane.Resource);
            Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(DcpBaseResourceReadinessRegistry));
            Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(DcpBaseResourceReadinessEventConsumer));
            Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(IHostedService));
            Assert.Contains(builder.Services, descriptor =>
                descriptor.ServiceType == typeof(DcpBaseResourceOwnershipEventConsumer));
            var resourcesByRole = new Dictionary<string, ContainerResource>(StringComparer.Ordinal)
            {
                ["simulator"] = Assert.IsType<ContainerResource>(resources.Simulator.Resource),
                ["game-controller"] = Assert.IsType<ContainerResource>(resources.GameController.Resource),
                ["cm4-sim"] = Assert.IsType<ContainerResource>(resources.Cm4Simulator.Resource),
                ["crane"] = Assert.IsType<ContainerResource>(resources.Crane.Resource),
            };
            string? sharedRunLabel = null;
            foreach (var (role, resource) in resourcesByRole)
            {
                Assert.Contains(resource.Annotations.OfType<HealthCheckAnnotation>(),
                    annotation => annotation.Key == $"dcp-{role}-service-ready");
                Assert.Contains(resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>(),
                    annotation => annotation.Callback is not null);
                var runtimeArguments = await GetRuntimeArgumentsAsync(resource);
                Assert.DoesNotContain("--network", runtimeArguments);
                Assert.Contains($"{ContainerIdentityExecHandoff.RoleLabel}={role}", runtimeArguments);
                var runLabel = Assert.Single(runtimeArguments, argument =>
                    argument.StartsWith($"{ContainerIdentityExecHandoff.RunIdLabel}=", StringComparison.Ordinal));
                sharedRunLabel ??= runLabel;
                Assert.Equal(sharedRunLabel, runLabel);
            }

            Assert.Equal("ghcr.io/ibis-ssl/framework-simulatorcli", resourcesByRole["simulator"].Annotations.OfType<ContainerImageAnnotation>().Single().Image);
            Assert.Equal("a52b6bd", resourcesByRole["simulator"].Annotations.OfType<ContainerImageAnnotation>().Single().Tag);
            Assert.Equal(
                ["tini", "--", "./bin/simulator-cli", "-g", "2020B", "--realism", "None", "--ibis-port", "12346", "--ibis-team-color", "yellow"],
                await GetArgumentsAsync(resourcesByRole["simulator"]));
            Assert.Equal(
                ["-visionAddress", "224.5.23.2:10020", "-trackerAddress", "224.5.23.2:11010", "-publishAddress", "224.5.23.1:11003", "-address", ":8082"],
                await GetArgumentsAsync(resourcesByRole["game-controller"]));
            Assert.Contains("--robot-ids", await GetArgumentsAsync(resourcesByRole["cm4-sim"]));
            Assert.Contains("bash", await GetArgumentsAsync(resourcesByRole["crane"]));
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }

    private static async Task<string[]> GetRuntimeArgumentsAsync(ContainerResource resource)
    {
        var values = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
        {
            await annotation.Callback(new ContainerRuntimeArgsCallbackContext(values, CancellationToken.None));
        }

        return values.Select(value => Assert.IsType<string>(value)).ToArray();
    }

    private static async Task<string[]> GetArgumentsAsync(IResourceWithArgs resource)
    {
        var values = new List<object>();
        foreach (var annotation in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
        {
            await annotation.Callback(new CommandLineArgsCallbackContext(values, resource, CancellationToken.None));
        }

        return values.Select(value => value?.ToString() ?? string.Empty).ToArray();
    }

    [Fact]
    public void ComparisonRegistrationRetainsTheLegacyWrapperResources()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "duck-comparison-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureRoot);

        try
        {
            var resources = BaseSimulationResourceRegistration.Add(
                builder,
                new BaseSimulationRegistrationOptions(
                    "12346",
                    "a52b6bd",
                    "2020B",
                    "None",
                    "yellow",
                    "scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d",
                    "d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a",
                    "visibility_graph",
                    "comparison-test-" + Guid.NewGuid().ToString("N"),
                    "crane",
                    "yellow",
                    Path.Combine(fixtureRoot, "fixture.json"),
                    UseStandardDcpResources: false,
                    DuckRunId: "run-comparison-test",
                    CraneTargetAddressSettings: null,
                    CraneDiagnosticsPath: null,
                    DockerContextName: null,
                    ExpectedEngineId: null,
                    DockerExecutable: "docker.exe"),
                _ => builder.AddContainer("duck", "mcr.microsoft.com/dotnet/aspnet"),
                _ => { });

            Assert.IsType<ExecutableResource>(resources.Simulator.Resource);
            Assert.IsType<ExecutableResource>(resources.GameController.Resource);
            Assert.IsType<ExecutableResource>(resources.Cm4Simulator!.Resource);
            Assert.IsType<ExecutableResource>(resources.Crane.Resource);
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }
}
