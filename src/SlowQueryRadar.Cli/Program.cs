//src/SlowQueryRadar.Cli/Program.cs
using System.CommandLine;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Benchmark;
using SlowQueryRadar.Cli.Commands;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core.Configuration;
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
                SharedConfiguration.AddSharedSettings(configuration);
                configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                configuration.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                    optional: true, reloadOnChange: false);
                configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
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

        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var queryStoreConnectionString = configuration["QueryStore:ConnectionString"];
        if (!IsDoctorCommand(args) && string.IsNullOrWhiteSpace(queryStoreConnectionString))
        {
            AnsiConsole.MarkupLine("[red]Configuration error:[/] QueryStore:ConnectionString is not configured. Copy appsettings.Shared.template.json to appsettings.Shared.json and set QueryStore:ConnectionString.");
            return 2;
        }

        if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogDebug(
                "Loaded QueryStore SQL target {ConnectionSummary}",
                DescribeConnection(queryStoreConnectionString));
        }

        var root = new RootCommand("Slow Query Radar CLI");
        root.Add(CollectCommand.Create(host.Services));
        root.Add(HistoryCommand.Create(host.Services));
        root.Add(RegressionsCommand.Create(host.Services));
        root.Add(DoctorCommand.Create(host.Services));
        root.Add(AnalyzeCommand.Create(host.Services));
        root.Add(ReportCommand.Create(host.Services));
        root.Add(ScanRepoCommand.Create());
        root.Add(BenchmarkCommand.Create(host.Services));
        try
        {
            return await root.Parse(args)
                .InvokeAsync(new InvocationConfiguration())
                .ConfigureAwait(false);
        }
        finally
        {
            await host.StopAsync().ConfigureAwait(false);
        }
    }

    internal static string DescribeConnection(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "<empty>";
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var server = string.IsNullOrWhiteSpace(builder.DataSource) ? "<unknown>" : builder.DataSource;
            var database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "<default>" : builder.InitialCatalog;
            var password = string.IsNullOrWhiteSpace(builder.Password) ? "<not-set>" : "***";
            return $"Server={server};Database={database};Password={password}";
        }
        catch (ArgumentException)
        {
            return "<invalid connection string>";
        }
    }

    private static bool IsDoctorCommand(string[] args)
    {
        return args.Length > 0 && string.Equals(args[0], "doctor", StringComparison.OrdinalIgnoreCase);
    }
}