//src/SlowQueryRadar.Suggestions/DependencyInjection.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlowQueryRadar.Suggestions.Ai;
using SlowQueryRadar.Suggestions.Deterministic;

namespace SlowQueryRadar.Suggestions;

/// <summary>Registers Slow Query Radar suggestion services.</summary>
public static class DependencyInjection
{
    /// <summary>Registers deterministic suggestions and optional AI services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSlowQueryRadarSuggestions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AiOptions>(configuration.GetSection("Ai"));
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddSingleton<IndexScriptGenerator>();
        services.AddSingleton<DapperSnippetGenerator>();
        services.AddSingleton<DeterministicSuggester>();
        services.AddSingleton<SqlLiteralStripper>();

        if (bool.TryParse(configuration["Ai:Enabled"], out var aiEnabled) && aiEnabled)
        {
            services.AddHttpClient(nameof(ClaudeExplainer));
            services.AddSingleton<IAiExplainer, ClaudeExplainer>();
        }

        services.AddSingleton<SuggestionEngine>();
        services.AddSingleton<Core.Abstractions.ISuggestionEngine>(provider =>
            provider.GetRequiredService<SuggestionEngine>());
        return services;
    }
}
