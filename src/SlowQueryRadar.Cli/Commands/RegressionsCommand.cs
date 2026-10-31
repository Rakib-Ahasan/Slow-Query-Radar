// src/SlowQueryRadar.Cli/Commands/RegressionsCommand.cs
using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Cli.Commands;

/// <summary>Creates command for regression detection output.</summary>
public static class RegressionsCommand
{
    /// <summary>Creates the <c>regressions</c> command.</summary>
    /// <param name="services">The application service provider.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(IServiceProvider services)
    {
        var targetOption = new Option<string?>("--target");
        var command = new Command("regressions", "Show detected regressions from persisted snapshots")
        {
            targetOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var detector = services.GetRequiredService<IRegressionDetector>();
            var target = parseResult.GetValue(targetOption);
            var regressions = await detector.DetectAsync(target, ct).ConfigureAwait(false);
            if (regressions.Count == 0)
            {
                AnsiConsole.MarkupLine("No regressions detected.");
                return;
            }

            var table = new Table()
                .AddColumn("Severity")
                .AddColumn("Target")
                .AddColumn("Hash")
                .AddColumn("Avg/Baseline")
                .AddColumn("Executions")
                .AddColumn("Evidence");

            foreach (var regression in regressions)
            {
                table.AddRow(
                    SeverityMarkup(regression.Severity),
                    Markup.Escape(regression.Target),
                    Markup.Escape(regression.NormalizedHash),
                    $"{regression.LatestAvgMs.ToString("F1", CultureInfo.InvariantCulture)} / {regression.BaselineMedianMs.ToString("F1", CultureInfo.InvariantCulture)}",
                    regression.LatestExecutions.ToString(CultureInfo.InvariantCulture),
                    Markup.Escape(string.Join(" | ", regression.Evidence)));
            }

            AnsiConsole.Write(table);
        });

        return command;
    }

    private static string SeverityMarkup(RegressionSeverity severity) => severity switch
    {
        RegressionSeverity.Critical => "[red]Critical[/]",
        RegressionSeverity.Warning => "[yellow]Warning[/]",
        _ => "[blue]Info[/]"
    };
}
