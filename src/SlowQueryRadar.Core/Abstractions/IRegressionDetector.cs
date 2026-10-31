//src/SlowQueryRadar.Core/Abstractions/IRegressionDetector.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Core.Abstractions;

/// <summary>Detects query regressions from persisted samples.</summary>
public interface IRegressionDetector
{
    /// <summary>Detects regressions for the latest samples against historical baseline.</summary>
    /// <param name="target">Optional target filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Detected regressions.</returns>
    Task<IReadOnlyList<Regression>> DetectAsync(string? target, CancellationToken ct);
}
