//src/SlowQueryRadar.Api/Contracts/SlowQueryDto.cs
namespace SlowQueryRadar.Api.Contracts;

/// <summary>JSON representation of a slow-query snapshot.</summary>
public sealed record SlowQueryDto(
    long QueryId,
    string SqlText,
    string NormalizedSqlHash,
    long ExecCount,
    double AvgDurationMs,
    double TotalCpuMs,
    long TotalLogicalReads,
    double ImpactScore,
    string? ExecutionPlanXml,
    DateTime LastExecutionUtc,
    DateTime CapturedAtUtc);

/// <summary>JSON representation of an analyzer finding.</summary>
public sealed record FindingDto(
    string RuleId,
    string Severity,
    string Message,
    string? Evidence,
    string? SuggestedFix,
    double EstimatedImpactPercent);

/// <summary>JSON representation of a deterministic and optional AI suggestion.</summary>
public sealed record SuggestionDto(string DeterministicFix, string? AiExplanation);

/// <summary>JSON representation of an analysis response.</summary>
public sealed record AnalyzeResponseDto(
    SlowQueryDto Snapshot,
    IReadOnlyList<FindingDto> Findings,
    SuggestionDto Suggestion);

/// <summary>JSON representation of a benchmark result.</summary>
public sealed record BenchmarkResultDto(
    string SqlHash,
    double DurationP50Ms,
    double DurationP95Ms,
    double DurationMaxMs,
    long LogicalReads,
    double CpuTimeMs,
    long RowCount,
    DateTime RanAtUtc);
