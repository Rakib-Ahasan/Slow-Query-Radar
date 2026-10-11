//src/SlowQueryRadar.Analyzer/Rules/NonSargableRule.cs
using System.Text.RegularExpressions;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Analyzer.Rules;

/// <summary>Reports common non-SARGable predicates.</summary>
public sealed class NonSargableRule : ISqlRule
{
    private static readonly Regex FunctionOnColumnPattern = new(
        @"\b(YEAR|MONTH|DAY|CAST|CONVERT|LTRIM|RTRIM|LOWER|UPPER|ISNULL)\s*\(\s*([\[\]\w.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LeadingWildcardPattern = new(
        @"\bLIKE\s+(?:N)?'%'",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <inheritdoc />
    public string RuleId => "SQLR-005";

    /// <inheritdoc />
    public IEnumerable<Finding> Evaluate(SqlAnalysisContext context)
    {
        var functionMatch = FunctionOnColumnPattern.Match(context.SqlText);
        if (functionMatch.Success)
        {
            var function = functionMatch.Groups[1].Value;
            var column = functionMatch.Groups[2].Value;
            yield return new Finding(
                RuleId,
                Severity.Medium,
                $"The {function} function is applied to column {column} in a predicate.",
                functionMatch.Value,
                $"Rewrite the predicate so {column} remains searchable; for date ranges use a half-open range such as {column} >= '2024-01-01' AND {column} < '2025-01-01'.",
                0);
        }

        if (LeadingWildcardPattern.IsMatch(context.SqlText))
        {
            yield return new Finding(
                RuleId,
                Severity.Medium,
                "A LIKE predicate starts with a wildcard.",
                "LIKE '%...'",
                "Use full-text search or a reversed column with a trailing-wildcard predicate.",
                0);
        }
    }
}
