//src/SlowQueryRadar.Api/Controllers/AnalyzeController.cs
using Microsoft.AspNetCore.Mvc;
using SlowQueryRadar.Api.Contracts;
using SlowQueryRadar.Api.Services;
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Benchmark;
using SlowQueryRadar.Core.Abstractions;

namespace SlowQueryRadar.Api.Controllers;

/// <summary>Provides query analysis and benchmark endpoints.</summary>
[ApiController]
[Route("api/analyze")]
public sealed class AnalyzeController : ControllerBase
{
    private readonly IQueryStoreCollector _collector;
    private readonly SqlAnalyzer _analyzer;
    private readonly ISuggestionEngine _suggestionEngine;
    private readonly QueryBenchmarker _benchmarker;
    private readonly QueryStateStore _stateStore;

    /// <summary>Initializes the analysis controller.</summary>
    public AnalyzeController(
        IQueryStoreCollector collector,
        SqlAnalyzer analyzer,
        ISuggestionEngine suggestionEngine,
        QueryBenchmarker benchmarker,
        QueryStateStore stateStore)
    {
        _collector = collector;
        _analyzer = analyzer;
        _suggestionEngine = suggestionEngine;
        _benchmarker = benchmarker;
        _stateStore = stateStore;
    }

    /// <summary>Collects, analyzes, and suggests improvements for a query.</summary>
    [HttpPost]
    public async Task<ActionResult<AnalyzeResponseDto>> AnalyzeAsync(
        [FromBody] AnalyzeRequestDto request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var snapshots = await _collector.CollectTopSlowQueriesAsync(
            request.TopN,
            TimeSpan.FromHours(request.LookbackHours),
            ct).ConfigureAwait(false);
        var snapshot = snapshots.FirstOrDefault(candidate =>
                string.Equals(candidate.SqlText, request.SqlText, StringComparison.Ordinal))
            ?? snapshots.FirstOrDefault();
        if (snapshot is null)
        {
            return NotFound("No query snapshot was collected.");
        }

        _stateStore.StoreSnapshot(snapshot);
        var analysis = await _analyzer.AnalyzeAsync(snapshot, [], ct).ConfigureAwait(false);
        var suggestion = await _suggestionEngine.BuildSuggestionAsync(analysis, request.SqlText, ct).ConfigureAwait(false);
        _stateStore.StoreAnalysis(snapshot.QueryId, analysis, suggestion);

        return Ok(new AnalyzeResponseDto(
            ApiMapper.ToDto(snapshot),
            analysis.Findings.Select(ApiMapper.ToDto).ToArray(),
            ApiMapper.ToDto(suggestion)));
    }

    /// <summary>Benchmarks a cached query after applying QueryBenchmarker safety checks.</summary>
    [HttpPost("{queryId:long}/benchmark")]
    public async Task<ActionResult<BenchmarkResultDto>> BenchmarkAsync(
        long queryId,
        [FromBody] BenchmarkRequestDto? request,
        CancellationToken ct)
    {
        if (!_stateStore.TryGet(queryId, out var state))
        {
            return NotFound();
        }

        var options = request ?? new BenchmarkRequestDto();
        var result = await _benchmarker.RunAsync(
            state.Snapshot.SqlText,
            null,
            options.WarmupRuns,
            options.MeasuredRuns,
            ct).ConfigureAwait(false);
        return Ok(ApiMapper.ToDto(result));
    }
}
