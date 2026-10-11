//src/SlowQueryRadar.Cli/Commands/AnalyzeCommand.cs
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;
using SlowQueryRadar.Suggestions;

namespace SlowQueryRadar.Cli.Commands;

/// <summary>Creates the SQL analysis command.</summary>
public static class AnalyzeCommand
{
    /// <summary>Creates the <c>analyze</c> command.</summary>
    /// <param name="services">The application service provider.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(IServiceProvider services)
    {
        var queryIdOption = new Option<long?>("--query-id");
        var sqlOption = new Option<string?>("--sql");
        var command = new Command("analyze", "Analyze a collected query or SQL text")
        {
            queryIdOption,
            sqlOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var queryId = parseResult.GetValue(queryIdOption);
            var sql = parseResult.GetValue(sqlOption);
            if (queryId is null == string.IsNullOrWhiteSpace(sql))
            {
                throw new ArgumentException("Specify exactly one of --query-id or --sql.");
            }

            var snapshot = queryId is not null
                ? await FindQueryAsync(services.GetRequiredService<IQueryStoreCollector>(), queryId.Value, ct).ConfigureAwait(false)
                : new SlowQuerySnapshot { SqlText = sql! };

            var analyzer = services.GetRequiredService<SqlAnalyzer>();
            var result = analyzer.Analyze(snapshot, [], ct);
            var suggestion = await services.GetRequiredService<SuggestionEngine>()
                .BuildSuggestionAsync(result, snapshot.SqlText, ct)
                .ConfigureAwait(false);

            var table = new Table().AddColumn("Severity").AddColumn("Rule").AddColumn("Finding");
            foreach (var finding in result.Findings)
            {
                table.AddRow(
                    SeverityMarkup(finding.Severity),
                    Markup.Escape(finding.RuleId),
                    Markup.Escape(finding.Message));
            }

            AnsiConsole.Write(table);
            AnsiConsole.Write(new Panel(Markup.Escape(suggestion.DeterministicFix))
            {
                Header = new PanelHeader("Suggested fixes"),
                Border = BoxBorder.Rounded
            });
        });

        return command;
    }

    private static async Task<SlowQuerySnapshot> FindQueryAsync(
        IQueryStoreCollector collector,
        long queryId,
        CancellationToken ct)
    {
        var snapshots = await collector.CollectTopSlowQueriesAsync(1000, TimeSpan.FromDays(30), ct).ConfigureAwait(false);
        return snapshots.FirstOrDefault(snapshot => snapshot.QueryId == queryId)
            ?? throw new KeyNotFoundException($"Query Store query {queryId} was not found.");
    }

    private static string SeverityMarkup(Severity severity) => severity switch
    {
        Severity.Critical => "[bold red]Critical[/]",
        Severity.High => "[red]High[/]",
        Severity.Medium => "[yellow]Medium[/]",
        _ => "[green]Low[/]"
    };
}
