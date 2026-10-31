// src/SlowQueryRadar.Cli/Commands/HistoryCommand.cs
using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SlowQueryRadar.Core.Abstractions;

namespace SlowQueryRadar.Cli.Commands;

/// <summary>Creates the history command for persisted snapshots.</summary>
public static class HistoryCommand
{
    /// <summary>Creates the <c>history</c> command.</summary>
    /// <param name="services">The application service provider.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(IServiceProvider services)
    {
        var targetOption = new Option<string?>("--target");
        var daysOption = new Option<int>("--days") { DefaultValueFactory = _ => 7 };
        var command = new Command("history", "Show persisted snapshot history")
        {
            targetOption,
            daysOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var days = parseResult.GetValue(daysOption);
            if (days <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(daysOption), "Days must be greater than zero.");
            }

            var target = parseResult.GetValue(targetOption);
            var store = services.GetRequiredService<ISnapshotStore>();
            await store.InitializeAsync(ct).ConfigureAwait(false);
            var until = DateTimeOffset.UtcNow;
            var since = until.AddDays(-days);
            var samples = await store.ReadSamplesAsync(target, since, until, ct).ConfigureAwait(false);

            if (samples.Count == 0)
            {
                AnsiConsole.MarkupLine("No persisted samples found for the requested filter.");
                return;
            }

            var table = new Table()
                .AddColumn("Captured")
                .AddColumn("Target")
                .AddColumn("Hash")
                .AddColumn("Exec")
                .AddColumn("Avg ms")
                .AddColumn("Total ms");

            foreach (var sample in samples.OrderByDescending(sample => sample.CapturedUtc).Take(200))
            {
                table.AddRow(
                    sample.CapturedUtc.UtcDateTime.ToString("u", CultureInfo.InvariantCulture),
                    Markup.Escape(sample.Target),
                    Markup.Escape(sample.NormalizedHash),
                    sample.Executions.ToString(CultureInfo.InvariantCulture),
                    sample.AvgMs.ToString("F2", CultureInfo.InvariantCulture),
                    sample.TotalMs.ToString("F2", CultureInfo.InvariantCulture));
            }

            AnsiConsole.Write(table);
        });

        return command;
    }
}
