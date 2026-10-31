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

    /// <summary>Gets a value indicating whether the XML was malformed.</summary>
    public bool IsMalformed { get; init; }

    /// <summary>Gets the parse error, when the XML was malformed.</summary>
    public string? ParseError { get; init; }

    /// <summary>Gets the granted memory in kilobytes when present.</summary>
    public double GrantedMemoryKb { get; init; }

    /// <summary>Gets the maximum used memory in kilobytes when present.</summary>
    public double MaxUsedMemoryKb { get; init; }

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
    string? Predicate)
{
    /// <summary>Gets the showplan node identifier.</summary>
    public string? NodeId { get; init; }

    /// <summary>Gets actual rows per execution when runtime data exists.</summary>
    public double ActualRows { get; init; }

    /// <summary>Gets actual execution count when runtime data exists.</summary>
    public double ActualExecutions { get; init; }

    /// <summary>Gets estimated execution count when available.</summary>
    public double EstimatedExecutions { get; init; } = 1;

    /// <summary>Gets a value indicating whether a spill warning exists.</summary>
    public bool HasSpill { get; init; }

    /// <summary>Gets a value indicating whether a plan-affecting conversion warning exists.</summary>
    public bool HasPlanAffectingConvert { get; init; }

    /// <summary>Gets a value indicating whether the operator has parallelism runtime data.</summary>
    public bool HasParallelismSkew { get; init; }
}

/// <summary>Represents a missing-index recommendation from an execution plan.</summary>
public sealed record MissingIndexSuggestion(
    string TableName,
    IReadOnlyList<string> EqualityColumns,
    IReadOnlyList<string> InequalityColumns,
    IReadOnlyList<string> IncludeColumns,
    double EstimatedImpactPercent);
