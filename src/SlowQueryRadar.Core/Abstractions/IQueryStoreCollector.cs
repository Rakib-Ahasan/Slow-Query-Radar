//src/SlowQueryRadar.Core/Abstractions/IQueryStoreCollector.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Core.Abstractions;

/// <summary>
/// Interface for collecting slow queries from SQL Server Query Store.
/// </summary>
public interface IQueryStoreCollector
{
    /// <summary>
    /// Collects slow-query snapshots using the specified collection options.
    /// </summary>
    /// <param name="options">The collection options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The collection result with snapshots and warnings.</returns>
    Task<CollectResult> CollectAsync(CollectOptions options, CancellationToken ct);

    /// <summary>
    /// Collects the top N slow queries from Query Store.
    /// </summary>
    /// <param name="topN">Number of top queries to collect.</param>
    /// <param name="lookback">Time span to look back from now.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of slow query snapshots.</returns>
    Task<IReadOnlyList<SlowQuerySnapshot>> CollectTopSlowQueriesAsync(
        int topN, TimeSpan lookback, CancellationToken ct);

    /// <summary>Gets a single query snapshot by its Query Store query ID.</summary>
    /// <param name="queryId">The Query Store query ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching snapshot, or <see langword="null"/> when not found.</returns>
    Task<SlowQuerySnapshot?> GetByQueryIdAsync(long queryId, CancellationToken ct);
}