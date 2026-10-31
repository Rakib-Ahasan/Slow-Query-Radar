//src/SlowQueryRadar.Core/Models/FleetAggregator.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Aggregates collected snapshots across monitoring targets.</summary>
public static class FleetAggregator
{
    /// <summary>Groups snapshots by normalized hash across targets.</summary>
    /// <param name="results">Fleet collection result.</param>
    /// <returns>Aggregated query-pattern metrics across tenants.</returns>
    public static IReadOnlyList<FleetQueryAggregate> AggregateByNormalizedHash(FleetCollectResult results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return results.Results
            .Where(result => result.Status == TargetResultStatus.Ok)
            .SelectMany(result => result.Queries)
            .Where(query => !string.IsNullOrWhiteSpace(query.NormalizedSqlHash))
            .GroupBy(query => query.NormalizedSqlHash, StringComparer.Ordinal)
            .Select(group =>
            {
                var byTenant = group
                    .GroupBy(query => query.TargetName ?? "<unknown>", StringComparer.Ordinal)
                    .Select(tenant => new
                    {
                        Tenant = tenant.Key,
                        TotalDurationMs = tenant.Sum(row => row.TotalDurationMs)
                    })
                    .OrderByDescending(tenant => tenant.TotalDurationMs)
                    .ToArray();

                var worstTenant = byTenant.FirstOrDefault()?.Tenant ?? "<unknown>";
                var bestTenant = byTenant.LastOrDefault()?.Tenant ?? "<unknown>";
                var pattern = group.FirstOrDefault()?.SqlText ?? string.Empty;

                return new FleetQueryAggregate(
                    group.Key,
                    pattern,
                    byTenant.Length,
                    group.Sum(row => row.TotalDurationMs),
                    worstTenant,
                    bestTenant);
            })
            .OrderByDescending(item => item.TotalDurationMs)
            .ToArray();
    }
}
