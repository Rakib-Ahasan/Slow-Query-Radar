//src/SlowQueryRadar.Collector/DependencyInjection.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Collector;

/// <summary>Registers Query Store collector services.</summary>
public static class DependencyInjection
{
    /// <summary>Registers and configures the Query Store collector.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSlowQueryRadarCollector(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<QueryStoreOptions>(configuration.GetSection("QueryStore"));
        services.Configure<MonitoringOptions>(configuration.GetSection("Monitoring"));
        services.Configure<DiscoveryOptions>(configuration.GetSection("Discovery"));
        services.Configure<SnapshotStoreOptions>(configuration.GetSection("SnapshotStore"));
        services.Configure<RegressionDetectionOptions>(configuration.GetSection("RegressionDetection"));
        services.AddSingleton<ITargetProvider, TargetProvider>();
        services.AddSingleton<IFleetCollector, FleetCollector>();
        services.AddSingleton<IQueryStoreCollector, QueryStoreCollector>();
        services.AddSingleton<ISnapshotStore, SqliteSnapshotStore>();
        services.AddSingleton<IRegressionDetector, RegressionDetector>();
        return services;
    }
}
