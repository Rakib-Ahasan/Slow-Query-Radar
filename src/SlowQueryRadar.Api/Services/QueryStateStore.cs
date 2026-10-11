//src/SlowQueryRadar.Api/Services/QueryStateStore.cs
using System.Collections.Concurrent;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Api.Services;

/// <summary>Stores the latest query snapshots and analysis data for the API session.</summary>
public sealed class QueryStateStore
{
    private readonly ConcurrentDictionary<long, QueryState> _states = new();

    /// <summary>Stores a snapshot and returns its current state.</summary>
    public QueryState StoreSnapshot(SlowQuerySnapshot snapshot) =>
        _states.AddOrUpdate(
            snapshot.QueryId,
            _ => new QueryState(snapshot),
            (_, existing) => existing with { Snapshot = snapshot });

    /// <summary>Gets a cached query state.</summary>
    public bool TryGet(long queryId, out QueryState state) => _states.TryGetValue(queryId, out state!);

    /// <summary>Stores analyzer results and suggestions for a query.</summary>
    public QueryState StoreAnalysis(long queryId, AnalysisResult analysis, Suggestion suggestion)
    {
        var state = _states.AddOrUpdate(
            queryId,
            _ => throw new KeyNotFoundException($"Query {queryId} is not cached."),
            (_, existing) => existing with { Analysis = analysis, Suggestion = suggestion });
        return state;
    }

    /// <summary>Gets all cached query states.</summary>
    public IReadOnlyList<QueryState> GetAll() => _states.Values.OrderByDescending(state => state.Snapshot.ImpactScore).ToArray();
}

/// <summary>Contains a cached snapshot and optional analysis data.</summary>
public sealed record QueryState(
    SlowQuerySnapshot Snapshot,
    AnalysisResult? Analysis = null,
    Suggestion? Suggestion = null);
