//src/SlowQueryRadar.Cli/Commands/ReportCommand.cs
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using RazorLight;
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;
using SlowQueryRadar.Suggestions;

namespace SlowQueryRadar.Cli.Commands;

/// <summary>Creates the standalone HTML report command.</summary>
public static class ReportCommand
{
    /// <summary>Creates the <c>report</c> command.</summary>
    /// <param name="services">The application service provider.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(IServiceProvider services)
    {
        var formatOption = new Option<string>("--format") { DefaultValueFactory = _ => "html" };
        var outputOption = new Option<FileInfo?>("--output");
        var command = new Command("report", "Generate a standalone performance report") { formatOption, outputOption };
        command.SetAction(async (parseResult, ct) =>
        {
            var format = parseResult.GetValue(formatOption) ?? "html";
            if (!string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only the html report format is supported.");
            }

            var output = parseResult.GetValue(outputOption) ?? throw new ArgumentException("--output is required.");
            var collector = services.GetRequiredService<IQueryStoreCollector>();
            var snapshots = await collector.CollectTopSlowQueriesAsync(10, TimeSpan.FromDays(7), ct).ConfigureAwait(false);
            var analyzer = services.GetRequiredService<SqlAnalyzer>();
            var suggestions = services.GetRequiredService<SuggestionEngine>();
            var rows = new List<ReportQuery>();
            foreach (var snapshot in snapshots)
            {
                var analysis = analyzer.Analyze(snapshot, [], ct);
                var suggestion = await suggestions.BuildSuggestionAsync(analysis, snapshot.SqlText, ct).ConfigureAwait(false);
                rows.Add(new ReportQuery(snapshot, analysis, suggestion));
            }

            var model = new ReportModel(
                DateTime.UtcNow,
                rows,
                rows.Sum(row => row.Snapshot.ExecCount),
                rows.Sum(row => row.Snapshot.TotalCpuMs),
                rows.SelectMany(row => row.Analysis.Findings).Count(),
                rows.SelectMany(row => row.Analysis.Findings).Count(finding => finding.Severity >= Severity.High));
            var templateRoot = Path.Combine(AppContext.BaseDirectory, "Templates");
            var engine = new RazorLightEngineBuilder().UseFileSystemProject(templateRoot).UseMemoryCachingProvider().Build();
            var html = await engine.CompileRenderAsync("report.cshtml", model).ConfigureAwait(false);
            await File.WriteAllTextAsync(output.FullName, html, ct).ConfigureAwait(false);
        });
        return command;
    }
}

/// <summary>Data rendered into the HTML report.</summary>
public sealed record ReportModel(DateTime GeneratedAtUtc, IReadOnlyList<ReportQuery> Queries, long TotalExecutions, double TotalCpuMs, int FindingCount, int HighSeverityCount);

/// <summary>A query and its analysis rendered in the HTML report.</summary>
public sealed record ReportQuery(SlowQuerySnapshot Snapshot, AnalysisResult Analysis, Suggestion Suggestion);
