//src/SlowQueryRadar.Core/Models/SlowQuerySnapshot.cs
using System;

namespace SlowQueryRadar.Core.Models;

/// <summary>
/// Represents a snapshot of a slow query captured from SQL Server Query Store.
/// </summary>
public class SlowQuerySnapshot
{
    /// <summary>Gets or sets the Query Store query identifier.</summary>
    public long QueryId { get; set; }
    /// <summary>Gets or sets the Query Store query hash text value.</summary>
    public string QueryHash { get; set; } = string.Empty;
    /// <summary>Gets or sets the object identifier, when the query is object-bound.</summary>
    public int? ObjectId { get; set; }
    /// <summary>Gets or sets the schema-qualified object name, when available.</summary>
    public string? ObjectName { get; set; }
    /// <summary>Gets or sets the original SQL text.</summary>
    public string SqlText { get; set; } = string.Empty;
    /// <summary>Gets or sets the normalized SQL text hash.</summary>
    public string NormalizedSqlHash { get; set; } = string.Empty;
    /// <summary>Gets or sets the execution count.</summary>
    public long ExecCount { get => TotalExecutions; set => TotalExecutions = value; }
    /// <summary>Gets or sets the total execution count in the selected window.</summary>
    public long TotalExecutions { get; set; }
    /// <summary>Gets or sets the average duration in milliseconds.</summary>
    public double AvgDurationMs { get; set; }
    /// <summary>Gets or sets the total duration in milliseconds.</summary>
    public double TotalDurationMs { get; set; }
    /// <summary>Gets or sets the minimum duration in milliseconds.</summary>
    public double MinDurationMs { get; set; }
    /// <summary>Gets or sets the maximum duration in milliseconds.</summary>
    public double MaxDurationMs { get; set; }
    /// <summary>Gets or sets the pooled duration standard deviation in milliseconds.</summary>
    public double StandardDeviationMs { get; set; }
    /// <summary>Gets or sets the total CPU time in milliseconds.</summary>
    public double TotalCpuMs { get; set; }
    /// <summary>Gets or sets the average CPU time in milliseconds.</summary>
    public double AvgCpuMs { get; set; }
    /// <summary>Gets or sets the total logical reads.</summary>
    public long TotalLogicalReads { get; set; }
    /// <summary>Gets or sets the average logical reads.</summary>
    public double AvgLogicalReads { get; set; }
    /// <summary>Gets or sets the total physical reads.</summary>
    public long TotalPhysicalReads { get; set; }
    /// <summary>Gets or sets the total logical writes.</summary>
    public long TotalLogicalWrites { get; set; }
    /// <summary>Gets or sets the total row count.</summary>
    public long TotalRows { get; set; }
    /// <summary>Gets or sets the number of plans seen for the query.</summary>
    public int PlanCount { get; set; }
    /// <summary>Gets or sets the latest execution time.</summary>
    public DateTimeOffset? LastExecutionTime { get; set; }
    /// <summary>Gets or sets the latest compile start time.</summary>
    public DateTimeOffset? LastCompileStartTime { get; set; }
    /// <summary>Gets or sets per-plan summary metrics.</summary>
    public IReadOnlyList<PlanSummary> Plans { get; set; } = [];
    /// <summary>Gets or sets whether the query may have plan regression.</summary>
    public bool PlanRegressionSuspected { get; set; }
    /// <summary>Gets or sets whether parameter sensitivity may be present.</summary>
    public bool ParameterSensitivitySuspected { get; set; }
    /// <summary>Gets or sets whether multiple plans were observed.</summary>
    public bool HasMultiplePlans { get; set; }
    /// <summary>Gets or sets the impact score in CPU microseconds.</summary>
    public double ImpactScore { get; set; }
    /// <summary>Gets or sets the execution plan XML, when available.</summary>
    public string? ExecutionPlanXml { get; set; }
    /// <summary>Gets or sets the last execution time in UTC.</summary>
    public DateTimeOffset LastExecutionUtc { get => LastExecutionTime ?? default; set => LastExecutionTime = value; }
    /// <summary>Gets or sets the capture time in UTC.</summary>
    public DateTimeOffset CapturedAtUtc { get; set; }
}