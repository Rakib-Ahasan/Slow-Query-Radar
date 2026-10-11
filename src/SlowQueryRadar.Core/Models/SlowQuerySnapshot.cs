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
    /// <summary>Gets or sets the original SQL text.</summary>
    public string SqlText { get; set; } = string.Empty;
    /// <summary>Gets or sets the normalized SQL text hash.</summary>
    public string NormalizedSqlHash { get; set; } = string.Empty;
    /// <summary>Gets or sets the execution count.</summary>
    public long ExecCount { get; set; }
    /// <summary>Gets or sets the average duration in milliseconds.</summary>
    public double AvgDurationMs { get; set; }
    /// <summary>Gets or sets the total CPU time in milliseconds.</summary>
    public double TotalCpuMs { get; set; }
    /// <summary>Gets or sets the total logical reads.</summary>
    public long TotalLogicalReads { get; set; }
    /// <summary>Gets or sets the impact score in CPU microseconds.</summary>
    public double ImpactScore { get; set; }
    /// <summary>Gets or sets the execution plan XML, when available.</summary>
    public string? ExecutionPlanXml { get; set; }
    /// <summary>Gets or sets the last execution time in UTC.</summary>
    public DateTimeOffset LastExecutionUtc { get; set; }
    /// <summary>Gets or sets the capture time in UTC.</summary>
    public DateTimeOffset CapturedAtUtc { get; set; }
}