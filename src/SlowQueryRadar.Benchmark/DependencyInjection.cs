//src/SlowQueryRadar.Benchmark/DependencyInjection.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SlowQueryRadar.Benchmark;

/// <summary>Registers benchmark verification services.</summary>
public static class DependencyInjection
{
    /// <summary>Binds and validates Benchmark options and registers benchmark services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSlowQueryRadarBenchmark(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection("Benchmark");
        var options = new BenchmarkOptions
        {
            ConnectionString = section["ConnectionString"] ?? string.Empty,
            Environment = section["Environment"] ?? "Dev",
            BlockedServerNames = section.GetSection("BlockedServerNames")
                .GetChildren()
                .Select(child => child.Value ?? string.Empty)
                .Where(value => value.Length > 0)
                .ToArray()
        };
        options.Validate();

        services.AddSingleton<IOptions<BenchmarkOptions>>(Options.Create(options));
        services.AddSingleton<QueryBenchmarker>();
        services.AddSingleton<BenchmarkHistoryStore>();
        return services;
    }
}
