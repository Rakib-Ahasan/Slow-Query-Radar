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
        @"CONVERT_IMPLICIT\((?<target>[^,\)]+),\s*(?<source>[^,\)]+)(?:,\s*(?<style>[^\)]+))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ParameterTypePattern = new(
        @"@(?<name>\w+)\s+(?<type>[^,\)]+(?:\([^)]*\))?)",
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
        var targetType = match.Success ? match.Groups["target"].Value.Trim() : "the target type";
        var sourceExpression = match.Success ? match.Groups["source"].Value.Trim() : "the parameter";
        var parameterName = Regex.Match(sourceExpression, @"@\w+", RegexOptions.IgnoreCase).Value;
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            parameterName = Regex.Matches(context.SqlText, @"@\w+", RegexOptions.IgnoreCase)
                .Select(match => match.Value)
                .FirstOrDefault() ?? "@parameter";
        }

        var sourceType = FindParameterType(context.SqlText, parameterName);
        var column = sourceExpression;
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            sourceType = "the source type";
        }

        yield return new Finding(
            RuleId,
            Severity.High,
            $"An implicit conversion was detected for {column}.",
            "The execution plan contains CONVERT_IMPLICIT.",
            $"Implicit conversion detected: parameter {parameterName} ({sourceType}) is being converted to {targetType} by the server. Consider using {parameterName} as {targetType} in the calling code, or cast the parameter in the query: WHERE [CustomerId] = CAST({parameterName} AS {targetType}).",
            0);
    }

    private static string? FindParameterType(string sql, string parameterName)
    {
        var match = ParameterTypePattern.Matches(sql)
            .FirstOrDefault(candidate => string.Equals(
                "@" + candidate.Groups["name"].Value,
                parameterName,
                StringComparison.OrdinalIgnoreCase));
        return match?.Groups["type"].Value.Trim();
    }

}
