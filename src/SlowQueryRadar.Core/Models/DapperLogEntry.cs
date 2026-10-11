//src/SlowQueryRadar.Core/Models/DapperLogEntry.cs
using System;

namespace SlowQueryRadar.Core.Models;

/// <summary>
/// Represents a log entry from Dapper profiler.
/// </summary>
public class DapperLogEntry
{
    /// <summary>Gets or sets the normalized SQL hash.</summary>
    public string SqlHash { get; set; } = string.Empty;
    /// <summary>Gets or sets the SQL text.</summary>
    public string SqlText { get; set; } = string.Empty;
    /// <summary>Gets or sets the serialized parameters, when logging is enabled.</summary>
    public string? ParametersJson { get; set; }
    /// <summary>Gets or sets the duration in milliseconds.</summary>
    public double DurationMs { get; set; }
    /// <summary>Gets or sets the caller member name.</summary>
    public string? CallerMemberName { get; set; }
    /// <summary>Gets or sets the caller file path.</summary>
    public string? CallerFilePath { get; set; }
    /// <summary>Gets or sets the caller line number.</summary>
    public int? CallerLineNumber { get; set; }
    /// <summary>Gets or sets whether execution succeeded.</summary>
    public bool Success { get; set; }
    /// <summary>Gets or sets the exception message, when execution failed.</summary>
    public string? ExceptionMessage { get; set; }
    /// <summary>Gets or sets the log timestamp in UTC.</summary>
    public DateTime TimestampUtc { get; set; }
}