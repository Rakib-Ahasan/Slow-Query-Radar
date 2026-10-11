//src/SlowQueryRadar.Profiler/DependencyInjection.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Profiler.Sinks;

namespace SlowQueryRadar.Profiler;

/// <summary>Registers Slow Query Radar profiler services.</summary>
public static class DependencyInjection
{
    /// <summary>Registers profiler options and the default performance sink.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSlowQueryRadarProfiler(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ProfilerOptions>(configuration.GetSection("Profiler"));
        services.AddSingleton<IPerformanceSink, SerilogPerformanceSink>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ProfilerOptions>>().Value);
        return services;
    }
}
