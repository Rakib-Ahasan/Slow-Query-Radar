//src/SlowQueryRadar.Analyzer/IAnalysisRule.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Analyzer;

/// <summary>Evaluates one analysis rule against a query snapshot.</summary>
public interface IAnalysisRule
{
    /// <summary>Gets the stable rule identifier.</summary>
    string Id { get; }

    /// <summary>Gets the default severity of the rule.</summary>
    Severity Severity { get; }

    /// <summary>Analyzes the supplied query context.</summary>
    /// <param name="context">The query, plan, and Query Store metrics.</param>
    /// <returns>The findings produced by the rule.</returns>
    IEnumerable<Finding> Analyze(AnalysisContext context);
}
