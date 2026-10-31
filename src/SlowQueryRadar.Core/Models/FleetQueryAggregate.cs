//src/SlowQueryRadar.Core/Models/FleetQueryAggregate.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Represents a normalized query pattern aggregated across targets.</summary>
/// <param name="NormalizedSqlHash">Normalized SQL hash.</param>
/// <param name="QueryPattern">Representative query text pattern.</param>
/// <param name="AffectedTenants">Number of targets affected by this query pattern.</param>
/// <param name="TotalDurationMs">Total duration across the fleet for this pattern.</param>
/// <param name="WorstTenant">Target name with highest total duration for this pattern.</param>
/// <param name="BestTenant">Target name with lowest total duration for this pattern.</param>
public sealed record FleetQueryAggregate(
    string NormalizedSqlHash,
    string QueryPattern,
    int AffectedTenants,
    double TotalDurationMs,
    string WorstTenant,
    string BestTenant);
