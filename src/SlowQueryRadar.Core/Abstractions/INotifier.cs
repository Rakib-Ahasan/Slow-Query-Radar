//src/SlowQueryRadar.Core/Abstractions/INotifier.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Core.Abstractions;

/// <summary>Sends regression notifications to external channels.</summary>
public interface INotifier
{
    /// <summary>Sends regression notifications.</summary>
    /// <param name="regressions">Regressions to notify.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyAsync(IReadOnlyList<Regression> regressions, CancellationToken ct);
}
