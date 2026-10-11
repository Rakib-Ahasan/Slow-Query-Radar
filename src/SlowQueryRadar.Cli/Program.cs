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
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Debug))
            .ConfigureAppConfiguration((context, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.SetBasePath(AppContext.BaseDirectory);
                configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                configuration.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                    optional: true, reloadOnChange: false);
                configuration.AddUserSecrets<Program>(optional: true);
                configuration.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSlowQueryRadarCollector(context.Configuration);
                services.AddSlowQueryRadarAnalyzer();
                services.AddSlowQueryRadarSuggestions(context.Configuration);
                services.AddSlowQueryRadarBenchmark(context.Configuration);
            })
            .Build();

        if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogDebug(
                "Loaded QueryStore connection string: {ConnectionString}",
                MaskConnectionString(host.Services.GetRequiredService<IConfiguration>()["QueryStore:ConnectionString"]));
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