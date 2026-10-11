//src/SlowQueryRadar.Cli/Analyzer/Symbols/ISqlUsageRule.cs
using Microsoft.CodeAnalysis;

namespace SlowQueryRadar.Cli.Analyzer.Symbols;

/// <summary>Analyzes a C# syntax tree for one SQL usage rule.</summary>
public interface ISqlUsageRule
{
    /// <summary>Finds violations in the supplied syntax tree.</summary>
    /// <param name="root">The syntax tree root.</param>
    /// <param name="filePath">The source file path.</param>
    /// <returns>Detected findings.</returns>
    IEnumerable<ScanFinding> Analyze(SyntaxNode root, string filePath);
}
