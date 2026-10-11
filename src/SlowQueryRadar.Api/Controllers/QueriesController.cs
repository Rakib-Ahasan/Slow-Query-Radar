//src/SlowQueryRadar.Api/Controllers/QueriesController.cs
using Microsoft.AspNetCore.Mvc;
using SlowQueryRadar.Api.Contracts;
using SlowQueryRadar.Api.Services;
using SlowQueryRadar.Core.Abstractions;

namespace SlowQueryRadar.Api.Controllers;

/// <summary>Provides slow-query collection and cached-query endpoints.</summary>
[ApiController]
[Route("api/queries")]
public sealed class QueriesController : ControllerBase
{
    private readonly IQueryStoreCollector _collector;
    private readonly QueryStateStore _stateStore;

    /// <summary>Initializes the queries controller.</summary>
    public QueriesController(IQueryStoreCollector collector, QueryStateStore stateStore)
    {
        _collector = collector;
        _stateStore = stateStore;
    }

    /// <summary>Collects and caches the top slow queries.</summary>
    [HttpGet("top")]
    public async Task<ActionResult<IReadOnlyList<SlowQueryDto>>> GetTopAsync(
        [FromQuery] int top = 20,
        [FromQuery] int lookbackHours = 24,
        CancellationToken ct = default)
    {
        var snapshots = await _collector.CollectTopSlowQueriesAsync(
            top,
            TimeSpan.FromHours(lookbackHours),
            ct).ConfigureAwait(false);
        foreach (var snapshot in snapshots)
        {
            _stateStore.StoreSnapshot(snapshot);
        }

        return Ok(snapshots.Select(ApiMapper.ToDto).ToArray());
    }

    /// <summary>Gets one query from the in-memory cache.</summary>
    [HttpGet("{queryId:long}")]
    public ActionResult<SlowQueryDto> Get(long queryId)
    {
        return _stateStore.TryGet(queryId, out var state)
            ? Ok(ApiMapper.ToDto(state.Snapshot))
            : NotFound();
    }
}
