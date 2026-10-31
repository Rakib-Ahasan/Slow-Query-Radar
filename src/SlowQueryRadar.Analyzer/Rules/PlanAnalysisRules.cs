//src/SlowQueryRadar.Analyzer/Rules/PlanAnalysisRules.cs
using SlowQueryRadar.Analyzer.Plan;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Analyzer.Rules;

/// <summary>Provides shared safe plan parsing behavior for plan analysis rules.</summary>
public abstract class PlanAnalysisRule : IAnalysisRule
{
    private readonly ExecutionPlanParser _parser;

    protected PlanAnalysisRule(ExecutionPlanParser parser) => _parser = parser;

    public abstract string Id { get; }

    public abstract Severity Severity { get; }

    /// <inheritdoc />
    public abstract IEnumerable<Finding> Analyze(AnalysisContext context);

    protected ParsedPlan Parse(AnalysisContext context) =>
        string.IsNullOrWhiteSpace(context.PlanXml) ? ParsedPlan.Empty : _parser.Parse(context.PlanXml.AsSpan());

    protected static Finding AnalysisFailed(ParsedPlan plan) => new(
        "AnalysisFailed",
        Severity.Low,
        "Execution plan analysis could not be completed.",
        plan.ParseError ?? "The execution plan XML was malformed or unavailable.",
        "Capture a valid SQL Server showplan XML document and analyze it again.",
        0)
    {
        WhyItMatters = "Plan-based findings may be incomplete without valid showplan XML.",
        Confidence = Confidence.High,
        DocsLink = "docs/rules.md"
    };

    protected Finding Finding(
        string title,
        string evidence,
        string why,
        string fix,
        Confidence confidence = Confidence.High,
        double impact = 0) => new(Id, Severity, title, evidence, fix, impact)
        {
            WhyItMatters = why,
            Confidence = confidence,
            DocsLink = "docs/rules.md"
        };
}

/// <summary>Finds large filtered scans in execution plans.</summary>
public sealed class LargeScanAnalysisRule : PlanAnalysisRule
{
    /// <summary>Initializes the large-scan rule.</summary>
    public LargeScanAnalysisRule(ExecutionPlanParser parser) : base(parser) { }

    /// <inheritdoc />
    public override string Id => "PLAN-001";

    /// <inheritdoc />
    public override Severity Severity => Severity.High;

    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var plan = Parse(context);
        if (plan.IsMalformed) { yield return AnalysisFailed(plan); yield break; }
        foreach (var op in plan.Operators.Where(op => op.EstimatedRows > 10_000
            && !string.IsNullOrWhiteSpace(op.Predicate)
            && (op.PhysicalOp.Equals("Table Scan", StringComparison.OrdinalIgnoreCase)
                || op.PhysicalOp.Equals("Clustered Index Scan", StringComparison.OrdinalIgnoreCase))))
        {
            yield return Finding("Large filtered scan detected", $"Node {op.NodeId ?? "?"}; object {op.TableName ?? "unknown"}; estimated rows {op.EstimatedRows:N0}; predicate {op.Predicate}", "A large filtered scan can consume substantial IO and CPU.", "Review selective indexes and predicate column data types before changing production schema.");
        }
    }
}

/// <summary>Finds key lookups with high estimated or actual work.</summary>
public sealed class KeyLookupAnalysisRule : PlanAnalysisRule
{
    /// <summary>Initializes the key-lookup rule.</summary>
    public KeyLookupAnalysisRule(ExecutionPlanParser parser) : base(parser) { }
    /// <inheritdoc />
    public override string Id => "PLAN-002";
    /// <inheritdoc />
    public override Severity Severity => Severity.High;
    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var plan = Parse(context);
        if (plan.IsMalformed) { yield return AnalysisFailed(plan); yield break; }
        foreach (var op in plan.Operators.Where(op => (op.PhysicalOp.Equals("Key Lookup", StringComparison.OrdinalIgnoreCase) || op.LogicalOp.Equals("Key Lookup", StringComparison.OrdinalIgnoreCase))
            && Math.Max(op.ActualRows * Math.Max(1d, op.ActualExecutions), op.EstimatedRows * op.EstimatedExecutions) > 10_000))
        {
            var executions = op.ActualExecutions > 0 ? op.ActualExecutions : op.EstimatedExecutions;
            var rows = op.ActualRows > 0 ? op.ActualRows : op.EstimatedRows;
            yield return Finding("High-cost key lookup detected", $"Node {op.NodeId ?? "?"}; object {op.TableName ?? "unknown"}; rows {rows:N0}; executions {executions:N0}; total rows {rows * executions:N0}", "Repeated lookups can turn a selective access path into random IO.", "Review a covering index suggestion; review before applying any index change.");
        }
    }
}

/// <summary>Finds plan-affecting implicit conversions.</summary>
public sealed class ImplicitConversionAnalysisRule : PlanAnalysisRule
{
    /// <summary>Initializes the implicit-conversion rule.</summary>
    public ImplicitConversionAnalysisRule(ExecutionPlanParser parser) : base(parser) { }
    /// <inheritdoc />
    public override string Id => "PLAN-003";
    /// <inheritdoc />
    public override Severity Severity => Severity.High;
    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var plan = Parse(context);
        if (plan.IsMalformed) { yield return AnalysisFailed(plan); yield break; }
        if (plan.HasImplicitConversion)
            yield return Finding("Plan-affecting implicit conversion detected", "PlanAffectingConvert or CONVERT_IMPLICIT was found in the showplan XML.", "A conversion on an indexed expression can prevent efficient seeks.", "Align parameter and column data types, then compare the revised plan.");
    }
}

/// <summary>Reports missing-index metadata from a plan as a reviewable suggestion.</summary>
public sealed class MissingIndexAnalysisRule : PlanAnalysisRule
{
    /// <summary>Initializes the missing-index rule.</summary>
    public MissingIndexAnalysisRule(ExecutionPlanParser parser) : base(parser) { }
    /// <inheritdoc />
    public override string Id => "PLAN-004";
    /// <inheritdoc />
    public override Severity Severity => Severity.Medium;
    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var plan = Parse(context);
        if (plan.IsMalformed) { yield return AnalysisFailed(plan); yield break; }
        foreach (var suggestion in plan.MissingIndexes)
        {
            var keys = suggestion.EqualityColumns.Concat(suggestion.InequalityColumns).ToArray();
            var name = $"IX_{RuleSupport.CleanIdentifier(suggestion.TableName)}_{string.Join("_", keys.Select(RuleSupport.CleanIdentifier))}";
            var table = suggestion.TableName.Contains('.', StringComparison.Ordinal) ? suggestion.TableName : $"dbo.{suggestion.TableName}";
            var include = suggestion.IncludeColumns.Count == 0 ? string.Empty : $" INCLUDE ({string.Join(", ", suggestion.IncludeColumns)})";
            var script = $"CREATE NONCLUSTERED INDEX {name} ON {table} ({string.Join(", ", keys)}){include}; -- review before applying";
            yield return Finding("Missing index suggested by the plan", $"Table {suggestion.TableName}; equality [{string.Join(", ", suggestion.EqualityColumns)}]; inequality [{string.Join(", ", suggestion.InequalityColumns)}]; impact {suggestion.EstimatedImpactPercent:F2}%", "The optimizer identified an access path that may reduce estimated work.", script, Confidence.Medium, suggestion.EstimatedImpactPercent);
        }
    }
}

/// <summary>Reports spills to tempdb and excessive memory grants.</summary>
public sealed class SpillAndMemoryGrantAnalysisRule : PlanAnalysisRule
{
    /// <summary>Initializes the spill and memory grant rule.</summary>
    public SpillAndMemoryGrantAnalysisRule(ExecutionPlanParser parser) : base(parser) { }
    /// <inheritdoc />
    public override string Id => "PLAN-005";
    /// <inheritdoc />
    public override Severity Severity => Severity.High;
    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var plan = Parse(context);
        if (plan.IsMalformed) { yield return AnalysisFailed(plan); yield break; }
        foreach (var op in plan.Operators.Where(op => op.HasSpill))
            yield return Finding("Operator spills to tempdb", $"Node {op.NodeId ?? "?"}; object {op.TableName ?? "unknown"}; spill warning SpillToTempDb", "Spills add tempdb IO and can cause latency under concurrency.", "Review memory grants, cardinality estimates, and sort/hash inputs.");

        if (plan.GrantedMemoryKb > 0 && plan.MaxUsedMemoryKb > 0 && plan.GrantedMemoryKb >= plan.MaxUsedMemoryKb * 2)
            yield return Finding("Memory grant substantially exceeds usage", $"Granted memory {plan.GrantedMemoryKb:N0} KB; maximum used {plan.MaxUsedMemoryKb:N0} KB; ratio {plan.GrantedMemoryKb / plan.MaxUsedMemoryKb:F1}x", "Excessive grants can reduce concurrency even when the query does not use the memory.", "Investigate cardinality estimates and memory-intensive operators before changing workload settings.", Confidence.Medium);
    }
}

/// <summary>Reports large estimated-to-actual row discrepancies.</summary>
public sealed class CardinalityMismatchAnalysisRule : PlanAnalysisRule
{
    /// <summary>Initializes the cardinality mismatch rule.</summary>
    public CardinalityMismatchAnalysisRule(ExecutionPlanParser parser) : base(parser) { }
    /// <inheritdoc />
    public override string Id => "PLAN-006";
    /// <inheritdoc />
    public override Severity Severity => Severity.Medium;
    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var plan = Parse(context);
        if (plan.IsMalformed) { yield return AnalysisFailed(plan); yield break; }
        foreach (var op in plan.Operators.Where(op => op.ActualRows > 0 && op.EstimatedRows > 0 && Math.Max(op.ActualRows / op.EstimatedRows, op.EstimatedRows / op.ActualRows) >= 10))
            yield return Finding("Large cardinality estimate mismatch", $"Node {op.NodeId ?? "?"}; estimated rows {op.EstimatedRows:N0}; actual rows {op.ActualRows:N0}; object {op.TableName ?? "unknown"}", "Incorrect cardinality estimates can lead to unsuitable join, memory, and access choices.", "Update statistics or investigate predicates and data distribution.", Confidence.High);
    }
}

/// <summary>Reports possible parallelism skew and large sorts.</summary>
public sealed class ParallelismAndSortAnalysisRule : PlanAnalysisRule
{
    /// <summary>Initializes the parallelism and sort rule.</summary>
    public ParallelismAndSortAnalysisRule(ExecutionPlanParser parser) : base(parser) { }
    /// <inheritdoc />
    public override string Id => "PLAN-007";
    /// <inheritdoc />
    public override Severity Severity => Severity.Medium;
    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var plan = Parse(context);
        if (plan.IsMalformed) { yield return AnalysisFailed(plan); yield break; }
        foreach (var op in plan.Operators.Where(op => op.HasParallelismSkew || (op.PhysicalOp.Equals("Sort", StringComparison.OrdinalIgnoreCase) && op.EstimatedRows > 10_000)))
        {
            var kind = op.HasParallelismSkew ? "parallelism skew" : "large sort";
            yield return Finding($"Possible {kind} detected", $"Node {op.NodeId ?? "?"}; operator {op.PhysicalOp}; estimated rows {op.EstimatedRows:N0}; object {op.TableName ?? "unknown"}", "Uneven parallel work or sorting a large rowset can limit scalability.", "Review distribution, ordering requirements, and supporting indexes.", Confidence.Medium);
        }
    }
}
