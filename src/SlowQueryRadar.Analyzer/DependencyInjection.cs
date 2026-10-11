//src/SlowQueryRadar.Analyzer/DependencyInjection.cs
using Microsoft.Extensions.DependencyInjection;
using SlowQueryRadar.Analyzer.Plan;
using SlowQueryRadar.Analyzer.Rules;
using SlowQueryRadar.Core.Abstractions;

namespace SlowQueryRadar.Analyzer;

/// <summary>Registers SQL analysis services.</summary>
public static class DependencyInjection
{
    /// <summary>Registers the analyzer, parser, and all built-in rules.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSlowQueryRadarAnalyzer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ExecutionPlanParser>();
        services.AddSingleton<SqlAnalyzer>();
        services.AddSingleton<ISqlRule, MissingIndexRule>();
        services.AddSingleton<ISqlRule, TableScanRule>();
        services.AddSingleton<ISqlRule, KeyLookupRule>();
        services.AddSingleton<ISqlRule, ImplicitConversionRule>();
        services.AddSingleton<ISqlRule, NonSargableRule>();
        services.AddSingleton<ISqlRule, SelectStarRule>();
        services.AddSingleton<ISqlRule, NPlus1Rule>();
        return services;
    }
}
