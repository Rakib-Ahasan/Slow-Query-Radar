//src/SlowQueryRadar.Api/Health/ApiHealthChecks.cs
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SlowQueryRadar.Core.Abstractions;

namespace SlowQueryRadar.Api.Health;

/// <summary>Checks that the snapshot store is available.</summary>
public sealed class SnapshotStoreHealthCheck : IHealthCheck
{
    private readonly ISnapshotStore _store;
    /// <summary>Initializes the check.</summary>
    public SnapshotStoreHealthCheck(ISnapshotStore store) => _store = store;
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    { try { await _store.InitializeAsync(cancellationToken); return HealthCheckResult.Healthy(); } catch (Exception ex) { return HealthCheckResult.Unhealthy("Snapshot store unavailable", ex); } }
}

/// <summary>Checks that at least one configured target can be resolved.</summary>
public sealed class TargetAvailabilityHealthCheck : IHealthCheck
{
    private readonly ITargetProvider _provider;
    /// <summary>Initializes the check.</summary>
    public TargetAvailabilityHealthCheck(ITargetProvider provider) => _provider = provider;
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    { try { var targets = await _provider.GetTargetsAsync(cancellationToken); return targets.Count > 0 ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("No monitoring targets available"); } catch (Exception ex) { return HealthCheckResult.Unhealthy("Targets unavailable", ex); } }
}
