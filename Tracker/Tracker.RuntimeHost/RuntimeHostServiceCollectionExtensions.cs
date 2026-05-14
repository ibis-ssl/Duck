using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tracker.Core;

namespace Tracker.RuntimeHost;

/// <summary>
/// Tracker.RuntimeHost の headless host 用 service 登録。
/// </summary>
public static class RuntimeHostServiceCollectionExtensions
{
    /// <summary>
    /// RuntimeHost scaffold に必要な options と hosted service を登録する。
    /// </summary>
    public static IServiceCollection AddRuntimeHost(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<RuntimeHostOptions>()
            .Bind(configuration.GetSection(RuntimeHostOptions.SectionName))
            .Validate(
                options => options.OperationLoopIntervalMilliseconds > 0,
                "RuntimeHost:OperationLoopIntervalMilliseconds must be greater than 0.")
            .ValidateOnStart();
        services
            .AddOptions<RuntimeVisionReceiverOptions>()
            .Bind(configuration.GetSection(RuntimeVisionReceiverOptions.SectionName))
            .Validate(
                options => options.Port > 0,
                "VisionReceiver:Port must be greater than 0.")
            .ValidateOnStart();
        services
            .AddOptions<RuntimeTrackerOptions>()
            .Bind(configuration.GetSection(RuntimeTrackerOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
            RuntimeTrackerConfigurationResolver.Resolve(
                serviceProvider.GetRequiredService<IOptions<RuntimeTrackerOptions>>().Value));
        services.AddSingleton(serviceProvider =>
        {
            var resolvedOptions = serviceProvider.GetRequiredService<TrackerRuntimeResolvedOptions>();
            return new TrackerPacketGenerator(
                resolvedOptions.PublisherOptions.SourceName,
                resolvedOptions.PublisherOptions.Uuid);
        });
        services.AddSingleton<ITrackerEngine, TrackerEngine>();
        services.AddSingleton(serviceProvider =>
        {
            var resolvedOptions = serviceProvider.GetRequiredService<TrackerRuntimeResolvedOptions>();
            return new TrackedSnapshotStore(resolvedOptions.EngineSettings.ProfileName);
        });
        services.AddSingleton<ITrackerPacketPublisher>(serviceProvider =>
        {
            var resolvedOptions = serviceProvider.GetRequiredService<TrackerRuntimeResolvedOptions>();
            return new UdpTrackerPacketPublisher(resolvedOptions.PublisherOptions);
        });
        services.AddSingleton<TrackerCoordinator>();
        services.AddSingleton<RuntimeVisionPacketBuffer>();
        services.AddSingleton<RuntimeHostOperationLoop>();
        services.AddSingleton<IRuntimeHostTickSource, RuntimeHostPeriodicTickSource>();
        services.AddHostedService<RuntimeVisionReceiverService>();
        services.AddHostedService<RuntimeHostLifecycleService>();

        return services;
    }
}
