//src/SlowQueryRadar.Core/Abstractions/ISnapshotStore.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Core.Abstractions;

/// <summary>Persists fleet snapshot runs and query samples.</summary>
public interface ISnapshotStore
{
    /// <summary>Ensures store schema is initialized.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task InitializeAsync(CancellationToken ct);

    /// <summary>Saves one fleet collection run.</summary>
    /// <param name="startedUtc">Run start time in UTC.</param>
    /// <param name="duration">Run duration.</param>
    /// <param name="result">Fleet collection result.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The persisted run identifier.</returns>
    Task<long> SaveRunAsync(DateTimeOffset startedUtc, TimeSpan duration, FleetCollectResult result, CancellationToken ct);

    /// <summary>Reads historical query samples for a target and time window.</summary>
    /// <param name="target">Optional target filter.</param>
    /// <param name="sinceUtc">Inclusive start time in UTC.</param>
    /// <param name="untilUtc">Inclusive end time in UTC.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching query samples.</returns>
    Task<IReadOnlyList<QuerySample>> ReadSamplesAsync(string? target, DateTimeOffset sinceUtc, DateTimeOffset untilUtc, CancellationToken ct);

    /// <summary>Reads the latest successful samples per target/hash.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Latest query samples.</returns>
    Task<IReadOnlyList<QuerySample>> ReadLatestSamplesAsync(CancellationToken ct);

    /// <summary>Reads per-target run outcomes in a time window.</summary>
    /// <param name="sinceUtc">Inclusive start time in UTC.</param>
    /// <param name="untilUtc">Inclusive end time in UTC.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The target run statuses.</returns>
    Task<IReadOnlyList<TargetRunRecord>> ReadTargetRunsAsync(DateTimeOffset sinceUtc, DateTimeOffset untilUtc, CancellationToken ct);

    /// <summary>Deletes persisted data older than the retention window.</summary>
    /// <param name="retention">Retention duration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of deleted query sample rows.</returns>
    Task<int> CleanupAsync(TimeSpan retention, CancellationToken ct);
}
