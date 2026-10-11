//src/SlowQueryRadar.Analyzer/Rules/MissingIndexRule.cs
using SlowQueryRadar.Analyzer.Plan;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Analyzer.Rules;

/// <summary>Reports missing indexes identified by the execution plan.</summary>
public sealed class MissingIndexRule : ISqlRule
{
    private readonly ExecutionPlanParser _parser;

    /// <summary>Initializes the missing-index rule.</summary>
    /// <param name="parser">The execution-plan parser.</param>
    public MissingIndexRule(ExecutionPlanParser parser) => _parser = parser;

    /// <inheritdoc />
    public string RuleId => "SQLR-001";

    /// <inheritdoc />
    public IEnumerable<Finding> Evaluate(SqlAnalysisContext context)
    {
        foreach (var suggestion in RuleSupport.ParsePlan(context, _parser).MissingIndexes)
        {
            var impact = suggestion.EstimatedImpactPercent;
            var table = suggestion.TableName;
            var columns = suggestion.EqualityColumns.Concat(suggestion.InequalityColumns).ToArray();
            var indexName = $"IX_{RuleSupport.CleanIdentifier(table)}_{string.Join("_", columns.Select(RuleSupport.CleanIdentifier))}";
            var tableName = table.Contains('.', StringComparison.Ordinal) ? table : $"dbo.{table}";
            var keyColumns = string.Join(", ", columns);
            var include = suggestion.IncludeColumns.Count == 0
                ? string.Empty
                : $"\nINCLUDE ({string.Join(", ", suggestion.IncludeColumns)});";
            var script = $"CREATE NONCLUSTERED INDEX {indexName}\nON {tableName} ({keyColumns}){include}";
            yield return new Finding(
                RuleId,
                impact > 30 ? Severity.High : Severity.Medium,
                $"The plan recommends an index on {table}.",
                $"Estimated impact: {impact:F2}%.",
                script,
                impact);
        }
    }
}
