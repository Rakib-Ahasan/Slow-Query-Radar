//src/SlowQueryRadar.Cli/Program.cs
using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Benchmark;
using SlowQueryRadar.Cli.Commands;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Suggestions;

namespace SlowQueryRadar.Cli;

/// <summary>Entry point for the Slow Query Radar command-line application.</summary>
public class Program
{
    private const string UserSecretsId = "c9278960-ab43-45b4-a164-f3c9567dc29f";

    /// <summary>Runs the configured CLI command.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.Sources.Clear();
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false);

        if (builder.Environment.IsDevelopment())
        {
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
            builder.Configuration.AddUserSecrets(UserSecretsId);
        }

        builder.Configuration.AddEnvironmentVariables();
        builder.Services.AddSlowQueryRadarCollector(builder.Configuration);
        builder.Services.AddSlowQueryRadarAnalyzer();
        builder.Services.AddSlowQueryRadarSuggestions(builder.Configuration);
        builder.Services.AddSlowQueryRadarBenchmark(builder.Configuration);

        using var host = builder.Build();
        if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogDebug(
                "Loaded QueryStore connection string: {ConnectionString}",
                MaskConnectionString(builder.Configuration["QueryStore:ConnectionString"]));
        }

        var root = new RootCommand("Slow Query Radar CLI");
        root.Add(CollectCommand.Create(host.Services));
        root.Add(AnalyzeCommand.Create(host.Services));
        root.Add(ReportCommand.Create(host.Services));
        root.Add(ScanRepoCommand.Create());
        root.Add(BenchmarkCommand.Create(host.Services));
        return await root.Parse(args).InvokeAsync(new InvocationConfiguration()).ConfigureAwait(false);
    }

    private static string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "<empty>";
        }

        return Regex.Replace(
            connectionString,
            "(?i)(Password|Pwd|ApiKey)\\s*=\\s*[^;]*",
            "$1=***");
    }
}