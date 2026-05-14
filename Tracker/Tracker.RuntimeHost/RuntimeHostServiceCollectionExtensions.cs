using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddHostedService<RuntimeHostLifecycleService>();

        return services;
    }
}
