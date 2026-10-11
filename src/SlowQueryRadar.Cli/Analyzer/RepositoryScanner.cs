//src/SlowQueryRadar.Cli/Analyzer/RepositoryScanner.cs
using Microsoft.CodeAnalysis.CSharp;
using SlowQueryRadar.Cli.Analyzer.Symbols;

namespace SlowQueryRadar.Cli.Analyzer;

/// <summary>Scans C# source files for SQL usage findings.</summary>
public sealed class RepositoryScanner
{
    private readonly IReadOnlyList<ISqlUsageRule> _rules =
    [
        new DapperSyncQueryRule(),
        new SqlStringConcatenationRule(),
        new SelectStarRule(),
        new MissingCommandTimeoutRule()
    ];

    /// <summary>Scans all C# files below a directory.</summary>
    /// <param name="path">The directory to scan.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Detected findings.</returns>
    public async Task<IReadOnlyList<ScanFinding>> ScanAsync(string path, CancellationToken ct)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(path);
        }

        var findings = new List<ScanFinding>();
        foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            var root = CSharpSyntaxTree.ParseText(text).GetRoot(ct);
            foreach (var rule in _rules)
            {
                findings.AddRange(rule.Analyze(root, Path.GetRelativePath(Environment.CurrentDirectory, file)));
            }
        }

        return findings;
    }
}
