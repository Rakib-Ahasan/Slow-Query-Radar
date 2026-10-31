//src/SlowQueryRadar.Core/Models/FleetCollectResult.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Contains fleet-wide collection outcomes by target.</summary>
/// <param name="Results">Per-target collection results.</param>
/// <param name="CapturedAtUtc">Capture timestamp in UTC.</param>
public sealed record FleetCollectResult(
    IReadOnlyList<TargetResult> Results,
    DateTimeOffset CapturedAtUtc);
