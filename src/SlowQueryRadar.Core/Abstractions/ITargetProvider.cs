//src/SlowQueryRadar.Core/Abstractions/ITargetProvider.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Core.Abstractions;

/// <summary>Provides monitoring targets for fleet collection.</summary>
public interface ITargetProvider
{
    /// <summary>Gets enabled monitoring targets.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The configured or discovered targets.</returns>
    Task<IReadOnlyList<MonitoringTarget>> GetTargetsAsync(CancellationToken ct);
}
