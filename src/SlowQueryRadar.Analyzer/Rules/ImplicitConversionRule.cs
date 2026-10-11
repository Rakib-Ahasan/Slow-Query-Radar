//src/SlowQueryRadar.Analyzer/Rules/ImplicitConversionRule.cs
using System.Text.RegularExpressions;
using SlowQueryRadar.Analyzer.Plan;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Analyzer.Rules;

/// <summary>Reports implicit conversions that can prevent index seeks.</summary>
public sealed class ImplicitConversionRule : ISqlRule
{
    private readonly ExecutionPlanParser _parser;
    private static readonly Regex ConversionPattern = new(
        @"CONVERT_IMPLICIT\(([^,\)]+),\s*([^,\)]+)(?:,\s*([^\)]+))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Initializes the implicit-conversion rule.</summary>
    /// <param name="parser">The execution-plan parser.</param>
    public ImplicitConversionRule(ExecutionPlanParser parser) => _parser = parser;

    /// <inheritdoc />
    public string RuleId => "SQLR-004";

    /// <inheritdoc />
    public IEnumerable<Finding> Evaluate(SqlAnalysisContext context)
    {
        var plan = RuleSupport.ParsePlan(context, _parser);
        if (!plan.HasImplicitConversion)
        {
            yield break;
        }

        var match = ConversionPattern.Match(context.PlanXml ?? string.Empty);
        var targetType = match.Success ? match.Groups[1].Value : "the target type";
        var column = match.Success ? match.Groups[2].Value : "the column";
        var sourceType = match.Success ? match.Groups[3].Value : "the parameter type";
        yield return new Finding(
            RuleId,
            Severity.High,
            $"An implicit conversion was detected for {column}.",
            "The execution plan contains CONVERT_IMPLICIT.",
            $"Check parameter types — the query is converting {column} ({sourceType}) to {targetType}. Fix the parameter type in C#.",
            0);
    }

}
