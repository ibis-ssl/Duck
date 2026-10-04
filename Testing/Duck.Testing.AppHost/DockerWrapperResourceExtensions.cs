using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Duck.Testing.AppHost;

public static class DockerWrapperResourceExtensions
{
    public static IResourceBuilder<ExecutableResource> AddDockerWrapper(
        this IDistributedApplicationBuilder builder,
        DockerContainerSpec spec,
        int healthPort,
        string expectedProcessName,
        string readinessProfile = "process")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(spec);

        var launch = new DockerWrapperLaunchOptions(
            spec.ResourceName,
            spec.Image,
            spec.ContainerArguments.ToArray(),
            spec.Environment.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            spec.StackId,
            spec.RunId,
            healthPort,
            ExpectedProcessName: expectedProcessName,
            ReadinessProfile: readinessProfile);
        var healthCheckName = $"{spec.ResourceName}-docker-wrapper-ready";
        builder.Services.AddHealthChecks().AddCheck(
            healthCheckName,
            new WrapperReadinessHealthCheck(new Uri($"http://127.0.0.1:{healthPort}/health/ready")));

        var wrapperExecutable = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "Duck.Testing.AppHost.exe" : "Duck.Testing.AppHost");
        var executable = builder.AddExecutable(
                spec.ResourceName,
                wrapperExecutable,
                AppContext.BaseDirectory)
            .WithArgs("--docker-wrapper", JsonSerializer.Serialize(launch))
            .WithHttpEndpoint(port: healthPort, targetPort: healthPort, name: "health")
            .WithHealthCheck(healthCheckName);
        executable.Resource.Annotations.Add(new DockerWrapperLaunchOptionsAnnotation(launch));

        return executable;
    }
}

internal sealed class WrapperReadinessHealthCheck(Uri endpoint) : IHealthCheck
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(2) };

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Client.GetAsync(endpoint, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Docker wrapper reports ready.")
                : HealthCheckResult.Unhealthy($"Docker wrapper readiness returned {(int)response.StatusCode}.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy("Docker wrapper readiness endpoint is unavailable.", exception);
        }
    }
}
