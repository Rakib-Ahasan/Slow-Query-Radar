//src/SlowQueryRadar.Analyzer/Rules/KeyLookupRule.cs
using SlowQueryRadar.Analyzer.Plan;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Analyzer.Rules;

/// <summary>Reports key lookups that may benefit from covering indexes.</summary>
public sealed class KeyLookupRule : ISqlRule
{
    private readonly ExecutionPlanParser _parser;

    /// <summary>Initializes the key-lookup rule.</summary>
    /// <param name="parser">The execution-plan parser.</param>
    public KeyLookupRule(ExecutionPlanParser parser) => _parser = parser;

    /// <inheritdoc />
    public string RuleId => "SQLR-003";

    /// <inheritdoc />
    public IEnumerable<Finding> Evaluate(SqlAnalysisContext context)
    {
        foreach (var operation in RuleSupport.ParsePlan(context, _parser).Operators.Where(IsKeyLookup))
        {
            var table = operation.TableName ?? "the referenced table";
            var evidence = string.IsNullOrWhiteSpace(operation.Predicate)
                ? $"Key Lookup on {table}."
                : $"Key Lookup on {table}; predicate: {operation.Predicate}";
            yield return new Finding(
                RuleId,
                Severity.Medium,
                $"A Key Lookup was found on {table}.",
                evidence,
                $"Consider a covering index on {table} that includes the lookup key and required output columns.",
                0);
        }
    }

    private static bool IsKeyLookup(PlanOperator operation) =>
        operation.PhysicalOp.Equals("Key Lookup", StringComparison.OrdinalIgnoreCase)
        || operation.LogicalOp.Equals("Key Lookup", StringComparison.OrdinalIgnoreCase);
}
