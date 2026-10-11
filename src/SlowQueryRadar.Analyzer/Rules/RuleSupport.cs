//src/SlowQueryRadar.Analyzer/Rules/RuleSupport.cs
using SlowQueryRadar.Analyzer.Plan;
using SlowQueryRadar.Core.Abstractions;

namespace SlowQueryRadar.Analyzer.Rules;

internal static class RuleSupport
{
    public static ParsedPlan ParsePlan(SqlAnalysisContext context, ExecutionPlanParser parser) =>
        string.IsNullOrWhiteSpace(context.PlanXml)
            ? ParsedPlan.Empty
            : parser.Parse(context.PlanXml.AsSpan());

    public static string CleanIdentifier(string value) =>
        value.Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal)
            .Replace(".", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);
}
