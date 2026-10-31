//src/SlowQueryRadar.Analyzer/AnalysisContext.cs
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Analyzer;

/// <summary>Provides SQL, parsed syntax, plan XML, and Query Store metrics to rules.</summary>
public sealed class AnalysisContext
{
    /// <summary>Initializes an analysis context.</summary>
    /// <param name="snapshot">The Query Store snapshot.</param>
    /// <param name="parsedStatement">The first parsed SQL statement.</param>
    /// <param name="recentLogs">Recent profiler entries.</param>
    public AnalysisContext(
        SlowQuerySnapshot snapshot,
        TSqlFragment parsedStatement,
        IReadOnlyList<DapperLogEntry>? recentLogs = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(parsedStatement);
        Snapshot = snapshot;
        ParsedStatement = parsedStatement;
        RecentLogs = recentLogs ?? [];
    }

    /// <summary>Gets the Query Store snapshot.</summary>
    public SlowQuerySnapshot Snapshot { get; }

    /// <summary>Gets the SQL text being analyzed.</summary>
    public string SqlText => Snapshot.SqlText;

    /// <summary>Gets the parsed SQL statement.</summary>
    public TSqlFragment ParsedStatement { get; }

    /// <summary>Gets the optional execution plan XML.</summary>
    public string? PlanXml => Snapshot.ExecutionPlanXml;

    /// <summary>Gets recent profiler entries.</summary>
    public IReadOnlyList<DapperLogEntry> RecentLogs { get; }
}
