//src/SlowQueryRadar.Cli/Commands/BenchmarkCommand.cs
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SlowQueryRadar.Benchmark;

namespace SlowQueryRadar.Cli.Commands;

/// <summary>Creates the benchmark command.</summary>
public static class BenchmarkCommand
{
    /// <summary>Creates the <c>benchmark</c> command.</summary>
    /// <param name="services">The application service provider.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(IServiceProvider services)
    {
        var sqlOption = new Option<string?>("--sql");
        var beforeOption = new Option<long?>("--before-id");
        var afterOption = new Option<long?>("--after-id");
        var command = new Command("benchmark", "Run a safe benchmark for a SELECT statement")
        {
            sqlOption,
            beforeOption,
            afterOption
        };
        command.SetAction(async (parseResult, ct) =>
        {
            var sql = parseResult.GetValue(sqlOption) ?? throw new ArgumentException("--sql is required.");
            var result = await services.GetRequiredService<QueryBenchmarker>()
                .RunAsync(sql, null, 1, 5, ct)
                .ConfigureAwait(false);
            AnsiConsole.Write(new Table()
                .AddColumn("SqlHash")
                .AddColumn("P50Ms")
                .AddColumn("P95Ms")
                .AddColumn("MaxMs")
                .AddColumn("LogicalReads")
                .AddColumn("CpuMs")
                .AddRow(result.SqlHash, result.DurationP50Ms.ToString("F2"), result.DurationP95Ms.ToString("F2"), result.DurationMaxMs.ToString("F2"), result.LogicalReads.ToString(), result.CpuTimeMs.ToString("F2")));
        });
        return command;
    }
}
