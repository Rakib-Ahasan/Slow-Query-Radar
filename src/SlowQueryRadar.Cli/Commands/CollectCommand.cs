// src/SlowQueryRadar.Cli/Commands/CollectCommand.cs
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

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
        var rankOption = new Option<string>("--rank-by") { DefaultValueFactory = _ => "total-duration" };
        var minExecutionsOption = new Option<long>("--min-executions") { DefaultValueFactory = _ => 2 };
        var includeNoiseOption = new Option<bool>("--include-noise");
        var formatOption = new Option<string>("--format") { DefaultValueFactory = _ => "table" };
        var outOption = new Option<FileInfo?>("--out");
        var legacyOutputOption = new Option<FileInfo?>("--output-json");
        var command = new Command("collect", "Collect top slow queries from Query Store")
        {
            topOption, lookbackOption, rankOption, minExecutionsOption, includeNoiseOption, formatOption, outOption, legacyOutputOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var format = (parseResult.GetValue(formatOption) ?? "table").Trim().ToLowerInvariant();
            if (format is not "table" and not "json") throw new ArgumentException("Format must be table or json.");
            var output = parseResult.GetValue(outOption) ?? parseResult.GetValue(legacyOutputOption);
            var options = new CollectOptions
            {
                Top = parseResult.GetValue(topOption), Lookback = ParseLookback(parseResult.GetValue(lookbackOption) ?? "24h"),
                RankBy = ParseRankBy(parseResult.GetValue(rankOption) ?? "total-duration"), MinExecutions = parseResult.GetValue(minExecutionsOption),
                ExcludeNoise = !parseResult.GetValue(includeNoiseOption)
            };
            var result = await services.GetRequiredService<IQueryStoreCollector>().CollectAsync(options, ct).ConfigureAwait(false);
            foreach (var warning in result.Warnings) AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(warning)}");
            if (format == "json")
            {
                var json = JsonSerializer.Serialize(result, JsonOptions);
                if (output is null) AnsiConsole.WriteLine(json); else await File.WriteAllTextAsync(output.FullName, json, ct).ConfigureAwait(false);
                return;
            }
            if (result.Queries.Count == 0) AnsiConsole.MarkupLine("No queries matched. Check Query Store state, lookback, and --min-executions.");
            else
            {
                var table = new Table().AddColumn("Rank").AddColumn("QueryId").AddColumn("Object / SQL").AddColumn("Executions").AddColumn("Total ms").AddColumn("Avg ms").AddColumn("Max ms").AddColumn("Total CPU ms").AddColumn("Avg reads").AddColumn("Plans").AddColumn("Flags");
                var rank = 0;
                foreach (var query in result.Queries)
                {
                    var flags = string.Join(" ", new[] { query.PlanRegressionSuspected ? "REGRESSION" : null, query.ParameterSensitivitySuspected ? "SNIFFING" : null, query.HasMultiplePlans ? "MULTI-PLAN" : null }.Where(flag => flag is not null));
                    table.AddRow((++rank).ToString(CultureInfo.InvariantCulture), query.QueryId.ToString(CultureInfo.InvariantCulture), Markup.Escape(Truncate(query.ObjectName ?? query.SqlText, 60)), query.TotalExecutions.ToString(CultureInfo.InvariantCulture), query.TotalDurationMs.ToString("F2", CultureInfo.InvariantCulture), query.AvgDurationMs.ToString("F2", CultureInfo.InvariantCulture), query.MaxDurationMs.ToString("F2", CultureInfo.InvariantCulture), query.TotalCpuMs.ToString("F2", CultureInfo.InvariantCulture), query.AvgLogicalReads.ToString("F2", CultureInfo.InvariantCulture), query.PlanCount.ToString(CultureInfo.InvariantCulture), flags);
                }
                AnsiConsole.Write(table);
            }
            if (output is not null) await File.WriteAllTextAsync(output.FullName, JsonSerializer.Serialize(result, JsonOptions), ct).ConfigureAwait(false);
        });
        return command;
    }

    /// <summary>Parses a duration expressed in hours, minutes, or days.</summary>
    public static TimeSpan ParseLookback(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours) && hours > 0) return TimeSpan.FromHours(hours);
        if (normalized.Length < 2 || !double.TryParse(normalized[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || amount <= 0) throw new ArgumentException($"Invalid lookback format: {value}. Use 24, 24h, 90m, or 7d.", nameof(value));
        return normalized[^1] switch { 'h' => TimeSpan.FromHours(amount), 'd' => TimeSpan.FromDays(amount), 'm' => TimeSpan.FromMinutes(amount), _ => throw new ArgumentException($"Invalid lookback format: {value}. Use 24, 24h, 90m, or 7d.", nameof(value)) };
    }

    /// <summary>Maps a CLI rank value to the fixed rank enum.</summary>
    public static RankBy ParseRankBy(string value) => value.Trim().ToLowerInvariant() switch
    {
        "total-duration" => RankBy.TotalDuration, "avg-duration" => RankBy.AvgDuration, "total-cpu" => RankBy.TotalCpu, "total-reads" => RankBy.TotalLogicalReads, "executions" => RankBy.ExecutionCount,
        _ => throw new ArgumentException("Unknown rank-by value. Use total-duration, avg-duration, total-cpu, total-reads, or executions.", nameof(value))
    };

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..(length - 3)] + "...";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
}
