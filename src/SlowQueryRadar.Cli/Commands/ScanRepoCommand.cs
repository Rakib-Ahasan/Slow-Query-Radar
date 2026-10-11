//src/SlowQueryRadar.Cli/Commands/ScanRepoCommand.cs
using System.CommandLine;
using System.Text.Json;
using Spectre.Console;
using SlowQueryRadar.Cli.Analyzer;

namespace SlowQueryRadar.Cli.Commands;

/// <summary>Creates the repository scan command.</summary>
public static class ScanRepoCommand
{
    /// <summary>Creates the <c>scan-repo</c> command.</summary>
    /// <returns>The configured command.</returns>
    public static Command Create()
    {
        var pathOption = new Option<DirectoryInfo?>("--path");
        var outputOption = new Option<FileInfo?>("--output-json");
        var command = new Command("scan-repo", "Scan C# source for SQL usage issues") { pathOption, outputOption };
        command.SetAction(async (parseResult, ct) =>
        {
            var path = parseResult.GetValue(pathOption) ?? throw new ArgumentException("--path is required.");
            var findings = await new RepositoryScanner().ScanAsync(path.FullName, ct).ConfigureAwait(false);
            var table = new Table().AddColumn("Severity").AddColumn("Rule").AddColumn("File").AddColumn("Line").AddColumn("Message");
            foreach (var finding in findings)
            {
                table.AddRow(finding.Severity.ToString(), finding.RuleId, finding.FilePath, finding.Line.ToString(), finding.Message);
            }

            AnsiConsole.Write(table);
            var output = parseResult.GetValue(outputOption);
            if (output is not null)
            {
                await File.WriteAllTextAsync(output.FullName, JsonSerializer.Serialize(findings, new JsonSerializerOptions { WriteIndented = true }), ct).ConfigureAwait(false);
            }
        });
        return command;
    }
}
