//src/SlowQueryRadar.Core/Models/RankBy.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Defines the rank metric used to order collected queries.</summary>
public enum RankBy
{
    /// <summary>Order by total duration descending.</summary>
    TotalDuration,
    /// <summary>Order by average duration descending.</summary>
    AvgDuration,
    /// <summary>Order by total CPU descending.</summary>
    TotalCpu,
    /// <summary>Order by total logical reads descending.</summary>
    TotalLogicalReads,
    /// <summary>Order by total execution count descending.</summary>
    ExecutionCount
}
