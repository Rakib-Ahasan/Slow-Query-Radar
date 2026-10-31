//src/SlowQueryRadar.Api/Contracts/RestDtos.cs
namespace SlowQueryRadar.Api.Contracts;

/// <summary>Paginated API response.</summary>
public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>Target status response.</summary>
public sealed record TargetDto(string Name, string? Group, IReadOnlyList<string> Tags, bool Enabled);

/// <summary>Fleet summary response.</summary>
public sealed record FleetSummaryDto(int TargetsOk, int TargetsFailed, int TotalSlowQueries, IReadOnlyList<string> WorstTenants, IReadOnlyList<string> TopFeatures, int Regressions);

/// <summary>Query detail response.</summary>
public sealed record QueryDetailDto(SlowQueryDto Snapshot, IReadOnlyList<FindingDto> Findings, SuggestionDto? Suggestion, IReadOnlyList<QuerySampleDto> History, IReadOnlyList<PlanDto> Plans);

/// <summary>Persisted query sample response.</summary>
public sealed record QuerySampleDto(string Target, string Hash, double AvgMs, double TotalMs, long Executions, DateTimeOffset CapturedUtc);

/// <summary>Plan response.</summary>
public sealed record PlanDto(long PlanId, string? PlanXml, IReadOnlyList<PlanOperatorDto> Operators);

/// <summary>Parsed plan operator response.</summary>
public sealed record PlanOperatorDto(string? NodeId, string PhysicalOp, string LogicalOp, double EstimatedRows, double ActualRows, string? ObjectName, string? Predicate, bool HasSpill, bool HasConversion);

/// <summary>Benchmark submission response.</summary>
public sealed record BenchmarkSubmissionDto(string Target, string OriginalSql, string CandidateSql, int Iterations);
/// <summary>Benchmark job response.</summary>
public sealed record BenchmarkJobDto(Guid Id, string Status, BenchmarkResultDto? Original, BenchmarkResultDto? Candidate, double? ImprovementPercent, string? Error);
/// <summary>Heatmap response.</summary>
public sealed record HeatmapDto(IReadOnlyList<string> Tenants, IReadOnlyList<DateTimeOffset> Buckets, IReadOnlyList<HeatmapCellDto> Cells);
/// <summary>One heatmap cell.</summary>
public sealed record HeatmapCellDto(string Tenant, DateTimeOffset Bucket, double Value);
