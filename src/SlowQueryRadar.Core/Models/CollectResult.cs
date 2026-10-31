//src/SlowQueryRadar.Core/Models/CollectResult.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Represents one collection run and its result set.</summary>
/// <param name="Queries">Collected query snapshots.</param>
/// <param name="Warnings">Collection warnings emitted by the collector.</param>
/// <param name="WindowStartUtc">Collection window start in UTC.</param>
/// <param name="WindowEndUtc">Collection window end in UTC.</param>
/// <param name="QueryStoreState">Query Store state returned from SQL Server.</param>
/// <param name="CapturedAtUtc">Capture timestamp in UTC.</param>
public sealed record CollectResult(
    IReadOnlyList<SlowQuerySnapshot> Queries,
    IReadOnlyList<string> Warnings,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    string QueryStoreState,
    DateTimeOffset CapturedAtUtc);
