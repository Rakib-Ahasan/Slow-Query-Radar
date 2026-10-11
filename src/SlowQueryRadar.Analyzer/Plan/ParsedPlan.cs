//src/SlowQueryRadar.Analyzer/Plan/ParsedPlan.cs
namespace SlowQueryRadar.Analyzer.Plan;

/// <summary>Represents the parsed portions of a SQL Server execution plan.</summary>
public sealed class ParsedPlan
{
    /// <summary>Initializes a parsed plan.</summary>
    /// <param name="operators">The plan operators.</param>
    /// <param name="missingIndexes">The missing-index suggestions.</param>
    /// <param name="hasImplicitConversion">Whether an implicit conversion was found.</param>
    public ParsedPlan(
        IEnumerable<PlanOperator> operators,
        IEnumerable<MissingIndexSuggestion> missingIndexes,
        bool hasImplicitConversion)
    {
        Operators = operators.ToArray();
        MissingIndexes = missingIndexes.ToArray();
        HasImplicitConversion = hasImplicitConversion;
    }

    /// <summary>Gets the parsed plan operators.</summary>
    public IReadOnlyList<PlanOperator> Operators { get; }

    /// <summary>Gets missing-index suggestions.</summary>
    public IReadOnlyList<MissingIndexSuggestion> MissingIndexes { get; }

    /// <summary>Gets a value indicating whether an implicit conversion exists.</summary>
    public bool HasImplicitConversion { get; }

    /// <summary>Gets an empty parsed plan.</summary>
    public static ParsedPlan Empty { get; } = new([], [], false);
}

/// <summary>Represents one relational operator in an execution plan.</summary>
public sealed record PlanOperator(
    string PhysicalOp,
    string LogicalOp,
    double EstimatedRows,
    double EstimatedCpu,
    string? TableName,
    string? IndexName,
    string? Predicate);

/// <summary>Represents a missing-index recommendation from an execution plan.</summary>
public sealed record MissingIndexSuggestion(
    string TableName,
    IReadOnlyList<string> EqualityColumns,
    IReadOnlyList<string> InequalityColumns,
    IReadOnlyList<string> IncludeColumns,
    double EstimatedImpactPercent);
