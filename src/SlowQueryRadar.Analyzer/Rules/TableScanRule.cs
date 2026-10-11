//src/SlowQueryRadar.Analyzer/Rules/TableScanRule.cs
using SlowQueryRadar.Analyzer.Plan;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;
using Serilog;

namespace SlowQueryRadar.Analyzer.Rules;

/// <summary>Reports large table and clustered-index scans.</summary>
public sealed class TableScanRule : ISqlRule
{
    private readonly ExecutionPlanParser _parser;

    /// <summary>Initializes the table-scan rule.</summary>
    /// <param name="parser">The execution-plan parser.</param>
    public TableScanRule(ExecutionPlanParser parser) => _parser = parser;

    /// <inheritdoc />
    public string RuleId => "SQLR-002";

    /// <inheritdoc />
    public IEnumerable<Finding> Evaluate(SqlAnalysisContext context)
    {
        if (string.IsNullOrWhiteSpace(context.PlanXml))
        {
            Log.Warning("Skipping {RuleId}: execution plan XML is missing", RuleId);
            yield break;
        }

        foreach (var operation in RuleSupport.ParsePlan(context, _parser).Operators.Where(IsLargeScan))
        {
            yield return new Finding(
                RuleId,
                Severity.High,
                $"A large {operation.PhysicalOp} was found on {operation.TableName ?? "an unknown table"}.",
                $"Table: {operation.TableName ?? "unknown"}; estimated rows: {operation.EstimatedRows:N0}.",
                "Add a selective access path or review the query predicates and indexes.",
                0);
        }
    }

    private static bool IsLargeScan(PlanOperator operation) =>
        operation.EstimatedRows > 10_000
        && (operation.PhysicalOp.Equals("Table Scan", StringComparison.OrdinalIgnoreCase)
            || operation.PhysicalOp.Equals("Clustered Index Scan", StringComparison.OrdinalIgnoreCase));
}
