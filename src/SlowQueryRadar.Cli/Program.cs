//src/SlowQueryRadar.Cli/Program.cs
using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Benchmark;
using SlowQueryRadar.Cli.Commands;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Suggestions;

namespace SlowQueryRadar.Cli;

/// <summary>Entry point for the Slow Query Radar command-line application.</summary>
public static class Program
{
    /// <summary>Runs the configured CLI command.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, configuration) =>
            {
                configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                configuration.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false);
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

        var root = new RootCommand("Slow Query Radar CLI");
        root.Add(CollectCommand.Create(host.Services));
        root.Add(AnalyzeCommand.Create(host.Services));
        root.Add(ReportCommand.Create(host.Services));
        root.Add(ScanRepoCommand.Create());
        root.Add(BenchmarkCommand.Create(host.Services));
        return await root.Parse(args).InvokeAsync(new InvocationConfiguration()).ConfigureAwait(false);
    }
}