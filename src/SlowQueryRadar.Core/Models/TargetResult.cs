//src/SlowQueryRadar.Core/Models/TargetResult.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Contains one target's collection outcome.</summary>
/// <param name="Name">Target name.</param>
/// <param name="Group">Target group.</param>
/// <param name="Status">Target status.</param>
/// <param name="ErrorMessage">Error message for failed targets.</param>
/// <param name="Queries">Collected snapshots for this target.</param>
/// <param name="Duration">Collection duration for this target.</param>
public sealed record TargetResult(
    string Name,
    string? Group,
    TargetResultStatus Status,
    string? ErrorMessage,
    IReadOnlyList<SlowQuerySnapshot> Queries,
    TimeSpan Duration);
