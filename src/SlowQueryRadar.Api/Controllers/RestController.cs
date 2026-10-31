//src/SlowQueryRadar.Api/Controllers/RestController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SlowQueryRadar.Analyzer.Plan;
using SlowQueryRadar.Api.Contracts;
using SlowQueryRadar.Api.Services;
using SlowQueryRadar.Benchmark;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Api.Controllers;

/// <summary>Provides the versioned JSON REST API.</summary>
[ApiController]
[Route("api/v1")]
public sealed class RestController : ControllerBase
{
    private readonly ISnapshotStore _store;
    private readonly ITargetProvider _targets;
    private readonly IRegressionDetector _regressions;
    private readonly QueryStateStore _states;
    private readonly QueryBenchmarker _benchmarker;
    private readonly BenchmarkJobStore _jobs;
    private readonly ExecutionPlanParser _planParser;

    /// <summary>Initializes the REST controller.</summary>
    public RestController(ISnapshotStore store, ITargetProvider targets, IRegressionDetector regressions, QueryStateStore states, QueryBenchmarker benchmarker, BenchmarkJobStore jobs, ExecutionPlanParser planParser)
    { _store = store; _targets = targets; _regressions = regressions; _states = states; _benchmarker = benchmarker; _jobs = jobs; _planParser = planParser; }

    /// <summary>Gets fleet health and slow-query summary.</summary>
    [HttpGet("fleet/summary")]
    public async Task<ActionResult<FleetSummaryDto>> Summary(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow; var runs = await _store.ReadTargetRunsAsync(now.AddHours(-24), now, ct); var samples = await _store.ReadLatestSamplesAsync(ct); var regressions = await _regressions.DetectAsync(null, ct);
        return Ok(new FleetSummaryDto(runs.Count(x => string.Equals(x.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)), runs.Count(x => !string.Equals(x.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)), samples.Count, samples.GroupBy(x => x.Target).OrderByDescending(x => x.Sum(y => y.TotalMs)).Take(5).Select(x => x.Key).ToArray(), samples.Select(x => x.Target).Distinct().Take(10).ToArray(), regressions.Count));
    }

    /// <summary>Gets configured monitoring targets.</summary>
    [HttpGet("targets")]
    public async Task<ActionResult<IReadOnlyList<TargetDto>>> Targets(CancellationToken ct) => Ok((await _targets.GetTargetsAsync(ct)).Select(x => new TargetDto(x.Name, x.Group, x.Tags, x.Enabled)).ToArray());

    /// <summary>Gets paginated query samples for a target.</summary>
    [HttpGet("targets/{name}/queries")]
    public async Task<ActionResult<PageDto<SlowQueryDto>>> TargetQueries(string name, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] int top = 0, [FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || page < 1 || pageSize is < 1 or > 200) return BadRequest(new ProblemDetails { Title = "Invalid pagination or target." });
        var rows = await _store.ReadSamplesAsync(name, from ?? DateTimeOffset.UtcNow.AddDays(-1), to ?? DateTimeOffset.UtcNow, ct); var selected = rows.OrderByDescending(x => x.TotalMs); if (top > 0) selected = selected.Take(Math.Min(top, 200)).OrderByDescending(x => x.TotalMs); var all = selected.ToArray(); var items = all.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new SlowQueryDto(0, ApiMapper.LimitSql(x.SqlText) ?? string.Empty, x.NormalizedHash, x.Executions, x.AvgMs, x.Cpu, x.LogicalReads, x.TotalMs, null, x.CapturedUtc, x.CapturedUtc)).ToArray(); return Ok(new PageDto<SlowQueryDto>(items, page, pageSize, all.Length));
    }

    /// <summary>Gets a query detail from cached state or persisted samples.</summary>
    [HttpGet("queries/{target}/{hash}")]
    public async Task<ActionResult<QueryDetailDto>> Query(string target, string hash, CancellationToken ct)
    {
        var sample = (await _store.ReadSamplesAsync(target, DateTimeOffset.UtcNow.AddYears(-1), DateTimeOffset.UtcNow, ct)).FirstOrDefault(x => string.Equals(x.NormalizedHash, hash, StringComparison.OrdinalIgnoreCase) || string.Equals(x.QueryHash, hash, StringComparison.OrdinalIgnoreCase));
        if (sample is null) return NotFound();
        var state = _states.GetAll().FirstOrDefault(x => string.Equals(x.Snapshot.TargetName, target, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Snapshot.NormalizedSqlHash, hash, StringComparison.OrdinalIgnoreCase));
        return Ok(new QueryDetailDto(new SlowQueryDto(0, ApiMapper.LimitSql(sample.SqlText) ?? string.Empty, sample.NormalizedHash, sample.Executions, sample.AvgMs, sample.Cpu, sample.LogicalReads, sample.TotalMs, null, sample.CapturedUtc, sample.CapturedUtc), state?.Analysis?.Findings.Select(ApiMapper.ToDto).ToArray() ?? [], state?.Suggestion is null ? null : ApiMapper.ToDto(state.Suggestion), [new QuerySampleDto(sample.Target, sample.NormalizedHash, sample.AvgMs, sample.TotalMs, sample.Executions, sample.CapturedUtc)], []));
    }

    /// <summary>Gets a parsed execution plan from cached query state.</summary>
    [HttpGet("queries/{target}/{hash}/plan")]
    public ActionResult<PlanDto> Plan(string target, string hash, [FromQuery] long? planId)
    {
        var state = _states.GetAll().FirstOrDefault(x => string.Equals(x.Snapshot.TargetName, target, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Snapshot.NormalizedSqlHash, hash, StringComparison.OrdinalIgnoreCase)); if (state?.Snapshot.ExecutionPlanXml is null) return NotFound(); var plan = _planParser.Parse(state.Snapshot.ExecutionPlanXml); return Ok(new PlanDto(planId ?? 0, ApiMapper.LimitSql(state.Snapshot.ExecutionPlanXml), plan.Operators.Select(x => new PlanOperatorDto(x.NodeId, x.PhysicalOp, x.LogicalOp, x.EstimatedRows, x.ActualRows, x.TableName, x.Predicate, x.HasSpill, x.HasPlanAffectingConvert)).ToArray()));
    }

    /// <summary>Gets detected regressions.</summary>
    [HttpGet("regressions")]
    public async Task<ActionResult<IReadOnlyList<Regression>>> Regressions([FromQuery] string? severity, CancellationToken ct) { var rows = await _regressions.DetectAsync(null, ct); return Ok(string.IsNullOrWhiteSpace(severity) ? rows : rows.Where(x => string.Equals(x.Severity.ToString(), severity, StringComparison.OrdinalIgnoreCase)).ToArray()); }

    /// <summary>Starts a benchmark job.</summary>
    [HttpPost("benchmarks")]
    [EnableRateLimiting("benchmarks")]
    public ActionResult<BenchmarkJobDto> StartBenchmark([FromBody] BenchmarkSubmissionDto request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.OriginalSql) || string.IsNullOrWhiteSpace(request.CandidateSql) || request.Iterations is < 1 or > 100) return BadRequest(new ProblemDetails { Title = "Invalid benchmark request." }); QueryBenchmarker.ValidateSql(request.OriginalSql); QueryBenchmarker.ValidateSql(request.CandidateSql); var id = _jobs.Create(); _ = RunBenchmarkAsync(id, request, HttpContext.RequestAborted); return Accepted($"/api/v1/benchmarks/{id}", _jobs.TryGet(id, out var job) ? job : null);
    }

    /// <summary>Gets a benchmark job.</summary>
    [HttpGet("benchmarks/{id:guid}")]
    public ActionResult<BenchmarkJobDto> GetBenchmark(Guid id) => _jobs.TryGet(id, out var job) ? Ok(job) : NotFound();

    /// <summary>Gets a tenant/time heatmap.</summary>
    [HttpGet("heatmap")]
    public async Task<ActionResult<HeatmapDto>> Heatmap([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string metric = "total") { var rows = await _store.ReadSamplesAsync(null, from ?? DateTimeOffset.UtcNow.AddDays(-1), to ?? DateTimeOffset.UtcNow, HttpContext.RequestAborted); var cells = rows.GroupBy(x => (Tenant: x.Target, Bucket: new DateTimeOffset(x.CapturedUtc.UtcDateTime.Date, TimeSpan.Zero))).Select(x => new HeatmapCellDto(x.Key.Tenant, x.Key.Bucket, metric.Equals("avg", StringComparison.OrdinalIgnoreCase) ? x.Average(y => y.AvgMs) : x.Sum(y => y.TotalMs))).ToArray(); return Ok(new HeatmapDto(cells.Select(x => x.Tenant).Distinct().ToArray(), cells.Select(x => x.Bucket).Distinct().Order().ToArray(), cells)); }

    private async Task RunBenchmarkAsync(Guid id, BenchmarkSubmissionDto request, CancellationToken ct)
    { try { _jobs.Set(new(id, "running", null, null, null, null)); var original = await _benchmarker.RunAsync(request.OriginalSql, null, 1, request.Iterations, ct); var candidate = await _benchmarker.RunAsync(request.CandidateSql, null, 1, request.Iterations, ct); var improvement = original.DurationP50Ms <= 0 ? 0 : (original.DurationP50Ms - candidate.DurationP50Ms) / original.DurationP50Ms * 100; _jobs.Set(new(id, "completed", ApiMapper.ToDto(original), ApiMapper.ToDto(candidate), improvement, null)); } catch (Exception ex) { _jobs.Set(new(id, "failed", null, null, null, ex.Message)); } }
}
