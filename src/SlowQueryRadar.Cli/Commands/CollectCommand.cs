// src/SlowQueryRadar.Cli/Commands/CollectCommand.cs
using System.Globalization;
using System.Text;
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
        var allOption = new Option<bool>("--all");
        var groupOption = new Option<string?>("--group");
        var tagOption = new Option<string?>("--tag");
        var topOption = new Option<int>("--top") { DefaultValueFactory = _ => 20 };
        var lookbackOption = new Option<string>("--lookback") { DefaultValueFactory = _ => "24h" };
        var rankOption = new Option<string>("--rank-by") { DefaultValueFactory = _ => "total-duration" };
        var minExecutionsOption = new Option<long>("--min-executions") { DefaultValueFactory = _ => 2 };
        var includeNoiseOption = new Option<bool>("--include-noise");
        var formatOption = new Option<string>("--format") { DefaultValueFactory = _ => "table" };
        var outputOption = new Option<string>("--output") { DefaultValueFactory = _ => "table" };
        var outOption = new Option<FileInfo?>("--out");
        var legacyOutputOption = new Option<FileInfo?>("--output-json");
        var command = new Command("collect", "Collect top slow queries from Query Store")
        {
            allOption, groupOption, tagOption,
            topOption, lookbackOption, rankOption, minExecutionsOption, includeNoiseOption,
            formatOption, outputOption, outOption, legacyOutputOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var outputMode = (parseResult.GetValue(outputOption) ?? parseResult.GetValue(formatOption) ?? "table").Trim().ToLowerInvariant();
            if (outputMode is not "table" and not "json" and not "html")
            {
                throw new ArgumentException("Output must be table, json, or html.");
            }

            var writeToFile = parseResult.GetValue(outOption) ?? parseResult.GetValue(legacyOutputOption);
            var allTargets = parseResult.GetValue(allOption);
            var options = new CollectOptions
            {
                Top = parseResult.GetValue(topOption), Lookback = ParseLookback(parseResult.GetValue(lookbackOption) ?? "24h"),
                RankBy = ParseRankBy(parseResult.GetValue(rankOption) ?? "total-duration"), MinExecutions = parseResult.GetValue(minExecutionsOption),
                ExcludeNoise = !parseResult.GetValue(includeNoiseOption)
            };

            if (allTargets)
            {
                var fleetCollector = services.GetRequiredService<IFleetCollector>();
                var fleetResult = await fleetCollector
                    .CollectAsync(options, parseResult.GetValue(groupOption), parseResult.GetValue(tagOption), ct)
                    .ConfigureAwait(false);

                var aggregates = FleetAggregator.AggregateByNormalizedHash(fleetResult);
                var failures = fleetResult.Results.Where(result => result.Status != TargetResultStatus.Ok).ToArray();

                if (outputMode == "json")
                {
                    var payload = JsonSerializer.Serialize(new { fleetResult, aggregates }, JsonOptions);
                    if (writeToFile is null) AnsiConsole.WriteLine(payload); else await File.WriteAllTextAsync(writeToFile.FullName, payload, ct).ConfigureAwait(false);
                    return;
                }

                if (outputMode == "html")
                {
                    var html = RenderFleetHtml(aggregates, failures);
                    if (writeToFile is null) AnsiConsole.WriteLine(html); else await File.WriteAllTextAsync(writeToFile.FullName, html, ct).ConfigureAwait(false);
                    return;
                }

                RenderFleetTable(aggregates);
                RenderFailureTable(failures);
                return;
            }

            var result = await services.GetRequiredService<IQueryStoreCollector>().CollectAsync(options, ct).ConfigureAwait(false);
            foreach (var warning in result.Warnings) AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(warning)}");
            if (outputMode == "json")
            {
                var json = JsonSerializer.Serialize(result, JsonOptions);
                if (writeToFile is null) AnsiConsole.WriteLine(json); else await File.WriteAllTextAsync(writeToFile.FullName, json, ct).ConfigureAwait(false);
                return;
            }

            if (outputMode == "html")
            {
                var html = RenderSingleTargetHtml(result.Queries);
                if (writeToFile is null) AnsiConsole.WriteLine(html); else await File.WriteAllTextAsync(writeToFile.FullName, html, ct).ConfigureAwait(false);
                return;
            }

            if (result.Queries.Count == 0)
            {
                AnsiConsole.MarkupLine("No queries matched. Check Query Store state, lookback, and --min-executions.");
            }
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

    private static void RenderFleetTable(IReadOnlyList<FleetQueryAggregate> aggregates)
    {
        if (aggregates.Count == 0)
        {
            AnsiConsole.MarkupLine("No fleet queries matched.");
            return;
        }

        var table = new Table()
            .AddColumn("Pattern")
            .AddColumn("Tenants")
            .AddColumn("Total ms")
            .AddColumn("Worst")
            .AddColumn("Best");
        foreach (var item in aggregates)
        {
            table.AddRow(
                Markup.Escape(Truncate(item.QueryPattern, 70)),
                item.AffectedTenants.ToString(CultureInfo.InvariantCulture),
                item.TotalDurationMs.ToString("F2", CultureInfo.InvariantCulture),
                Markup.Escape(item.WorstTenant),
                Markup.Escape(item.BestTenant));
        }

        AnsiConsole.Write(table);
    }

    private static void RenderFailureTable(IReadOnlyList<TargetResult> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        var table = new Table()
            .Title("Target Failures")
            .AddColumn("Target")
            .AddColumn("Group")
            .AddColumn("Status")
            .AddColumn("Error");

        foreach (var failure in failures)
        {
            table.AddRow(
                Markup.Escape(failure.Name),
                Markup.Escape(failure.Group ?? string.Empty),
                failure.Status.ToString(),
                Markup.Escape(failure.ErrorMessage ?? string.Empty));
        }

        AnsiConsole.Write(table);
    }

    private static string RenderFleetHtml(IReadOnlyList<FleetQueryAggregate> aggregates, IReadOnlyList<TargetResult> failures)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<html><body><h1>Slow Query Radar Fleet Report</h1>");
        builder.AppendLine("<h2>Aggregates</h2><table border='1'><tr><th>Pattern</th><th>Tenants</th><th>Total ms</th><th>Worst</th><th>Best</th></tr>");
        foreach (var item in aggregates)
        {
            builder.Append("<tr><td>").Append(System.Net.WebUtility.HtmlEncode(item.QueryPattern))
                .Append("</td><td>").Append(item.AffectedTenants.ToString(CultureInfo.InvariantCulture))
                .Append("</td><td>").Append(item.TotalDurationMs.ToString("F2", CultureInfo.InvariantCulture))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(item.WorstTenant))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(item.BestTenant))
                .AppendLine("</td></tr>");
        }

        builder.AppendLine("</table><h2>Failures</h2><table border='1'><tr><th>Target</th><th>Group</th><th>Status</th><th>Error</th></tr>");
        foreach (var failure in failures)
        {
            builder.Append("<tr><td>").Append(System.Net.WebUtility.HtmlEncode(failure.Name))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(failure.Group ?? string.Empty))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(failure.Status.ToString()))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(failure.ErrorMessage ?? string.Empty))
                .AppendLine("</td></tr>");
        }

        builder.AppendLine("</table></body></html>");
        return builder.ToString();
    }

    private static string RenderSingleTargetHtml(IReadOnlyList<SlowQuerySnapshot> queries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<html><body><h1>Slow Query Radar Report</h1><table border='1'><tr><th>QueryId</th><th>SQL</th><th>Total ms</th><th>Avg ms</th><th>Executions</th></tr>");
        foreach (var query in queries)
        {
            builder.Append("<tr><td>").Append(query.QueryId.ToString(CultureInfo.InvariantCulture))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(query.SqlText))
                .Append("</td><td>").Append(query.TotalDurationMs.ToString("F2", CultureInfo.InvariantCulture))
                .Append("</td><td>").Append(query.AvgDurationMs.ToString("F2", CultureInfo.InvariantCulture))
                .Append("</td><td>").Append(query.TotalExecutions.ToString(CultureInfo.InvariantCulture))
                .AppendLine("</td></tr>");
        }

        builder.AppendLine("</table></body></html>");
        return builder.ToString();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
}
