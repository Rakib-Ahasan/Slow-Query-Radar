//src/SlowQueryRadar.Core/Models/Severity.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>
/// Represents the severity level of a log entry or finding.
/// </summary>
public enum Severity
{
    /// <summary>Low severity.</summary>
    Low,
    /// <summary>Medium severity.</summary>
    Medium,
    /// <summary>High severity.</summary>
    High,
    /// <summary>Critical severity.</summary>
    Critical
}