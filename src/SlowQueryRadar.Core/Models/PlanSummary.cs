//src/SlowQueryRadar.Core/Models/PlanSummary.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Represents aggregated evidence for one Query Store plan.</summary>
/// <param name="PlanId">The Query Store plan identifier.</param>
/// <param name="IsForcedPlan">Whether the plan is forced in Query Store.</param>
/// <param name="LastExecutionTime">The last execution time for the plan.</param>
/// <param name="ExecutionCount">The total execution count in the window.</param>
/// <param name="AvgDurationMs">The weighted average duration in milliseconds.</param>
/// <param name="MaxDurationMs">The maximum duration in milliseconds.</param>
/// <param name="StandardDeviationMs">The pooled standard deviation in milliseconds.</param>
/// <param name="PlanXml">Optional plan XML when explicitly loaded.</param>
public sealed record PlanSummary(
    long PlanId,
    bool IsForcedPlan,
    DateTimeOffset? LastExecutionTime,
    long ExecutionCount,
    double AvgDurationMs,
    double MaxDurationMs,
    double StandardDeviationMs,
    string? PlanXml);
