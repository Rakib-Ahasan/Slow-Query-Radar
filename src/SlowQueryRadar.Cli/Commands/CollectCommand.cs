//src/SlowQueryRadar.Cli/Commands/CollectCommand.cs
using System.Text.Json;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SlowQueryRadar.Core.Abstractions;

namespace SlowQueryRadar.Cli.Commands;

/// <summary>Creates the Query Store collection command.</summary>
public static class CollectCommand
{
    /// <summary>Creates the <c>collect</c> command.</summary>
    /// <param name="services">The application service provider.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(IServiceProvider services)
    {
        var topOption = new Option<int>("--top") { DefaultValueFactory = _ => 20 };
        var lookbackOption = new Option<string>("--lookback") { DefaultValueFactory = _ => "24h" };
        var outputOption = new Option<FileInfo?>("--output-json");
        var command = new Command("collect", "Collect top slow queries from Query Store")
        {
            topOption,
            lookbackOption,
            outputOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var top = parseResult.GetValue(topOption);
            var lookback = ParseLookback(parseResult.GetValue(lookbackOption) ?? "24h");
            var output = parseResult.GetValue(outputOption);
            var collector = services.GetRequiredService<IQueryStoreCollector>();
            var snapshots = await collector.CollectTopSlowQueriesAsync(top, lookback, ct).ConfigureAwait(false);

            var table = new Table()
                .AddColumn("QueryId")
                .AddColumn("ExecCount")
                .AddColumn("AvgMs")
                .AddColumn("TotalCpuMs")
                .AddColumn("ImpactScore")
                .AddColumn("Sql (80 chars)");

            foreach (var snapshot in snapshots)
            {
                table.AddRow(
                    snapshot.QueryId.ToString(),
                    snapshot.ExecCount.ToString(),
                    snapshot.AvgDurationMs.ToString("F2"),
                    snapshot.TotalCpuMs.ToString("F2"),
                    snapshot.ImpactScore.ToString("F2"),
                    Markup.Escape(Truncate(snapshot.SqlText, 80)));
            }

            AnsiConsole.Write(table);
            if (output is not null)
            {
                await File.WriteAllTextAsync(
                    output.FullName,
                    JsonSerializer.Serialize(snapshots, new JsonSerializerOptions { WriteIndented = true }),
                    ct).ConfigureAwait(false);
            }
        });

        return command;
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 3)] + "...";

    private static TimeSpan ParseLookback(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length < 2 || !int.TryParse(normalized[..^1], out var amount) || amount <= 0)
        {
            throw new ArgumentException($"Invalid lookback format: {value}. Use formats like 24h, 7d, or 30m.");
        }

        return normalized[^1] switch
        {
            'h' => TimeSpan.FromHours(amount),
            'd' => TimeSpan.FromDays(amount),
            'm' => TimeSpan.FromMinutes(amount),
            _ => throw new ArgumentException($"Invalid lookback format: {value}. Use formats like 24h, 7d, or 30m.")
        };
    }
}
