//src/SlowQueryRadar.Core/Abstractions/IFleetCollector.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Core.Abstractions;

/// <summary>Collects query metrics across multiple monitoring targets.</summary>
public interface IFleetCollector
{
    /// <summary>Collects snapshots across enabled targets with optional filters.</summary>
    /// <param name="options">Per-target collection options.</param>
    /// <param name="group">Optional target group filter.</param>
    /// <param name="tag">Optional target tag filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Per-target collection outcomes.</returns>
    Task<FleetCollectResult> CollectAsync(CollectOptions options, string? group, string? tag, CancellationToken ct);
}
