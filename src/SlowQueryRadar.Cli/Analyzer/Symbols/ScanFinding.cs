//src/SlowQueryRadar.Cli/Analyzer/Symbols/ScanFinding.cs
namespace SlowQueryRadar.Cli.Analyzer.Symbols;

/// <summary>Severity assigned to a repository scan finding.</summary>
public enum ScanSeverity
{
    /// <summary>Informational finding.</summary>
    Info,
    /// <summary>Warning finding.</summary>
    Warning,
    /// <summary>Error finding.</summary>
    Error
}

/// <summary>Represents a repository SQL usage finding.</summary>
public sealed record ScanFinding(string RuleId, ScanSeverity Severity, string FilePath, int Line, string Message);
