//src/SlowQueryRadar.Core/Abstractions/ISqlRule.cs
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Core.Abstractions;

/// <summary>Evaluates one SQL performance rule against an analysis context.</summary>
public interface ISqlRule
{
    /// <summary>Gets the stable rule identifier.</summary>
    string RuleId { get; }

    /// <summary>Evaluates the rule synchronously.</summary>
    /// <param name="context">The SQL and execution-plan context.</param>
    /// <returns>Findings produced by the rule.</returns>
    IEnumerable<Finding> Evaluate(SqlAnalysisContext context);
}

/// <summary>Provides SQL text, parsed SQL, and optional plan XML to rules.</summary>
public sealed record SqlAnalysisContext(
    string SqlText,
    TSqlFragment ParsedStatement,
    string? PlanXml);
