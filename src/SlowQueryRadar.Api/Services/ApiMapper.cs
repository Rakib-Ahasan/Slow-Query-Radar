//src/SlowQueryRadar.Api/Services/ApiMapper.cs
using SlowQueryRadar.Api.Contracts;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Api.Services;

/// <summary>Maps Core models to API-owned JSON contracts.</summary>
public static class ApiMapper
{
    private const int MaxSqlTextLength = 16_384;
    /// <summary>Maps a slow-query snapshot.</summary>
    public static SlowQueryDto ToDto(SlowQuerySnapshot snapshot) => new(
        snapshot.QueryId,
        LimitSql(snapshot.SqlText) ?? string.Empty,
        snapshot.NormalizedSqlHash,
        snapshot.ExecCount,
        snapshot.AvgDurationMs,
        snapshot.TotalCpuMs,
        snapshot.TotalLogicalReads,
        snapshot.ImpactScore,
        LimitSql(snapshot.ExecutionPlanXml),
        snapshot.LastExecutionUtc,
        snapshot.CapturedAtUtc);

    /// <summary>Maps a finding.</summary>
    public static FindingDto ToDto(Finding finding) => new(
        finding.RuleId,
        finding.Severity.ToString(),
        finding.Message,
        finding.Evidence,
        finding.SuggestedFix,
        finding.EstimatedImpactPercent);

    /// <summary>Maps a suggestion.</summary>
    public static SuggestionDto ToDto(Suggestion suggestion) => new(suggestion.DeterministicFix, suggestion.AiExplanation);

    /// <summary>Limits SQL and plan payloads returned to the UI.</summary>
    public static string? LimitSql(string? value) => string.IsNullOrEmpty(value) || value.Length <= MaxSqlTextLength ? value : value[..MaxSqlTextLength];

    /// <summary>Maps a benchmark result.</summary>
    public static BenchmarkResultDto ToDto(BenchmarkResult result) => new(
        result.SqlHash,
        result.DurationP50Ms,
        result.DurationP95Ms,
        result.DurationMaxMs,
        result.LogicalReads,
        result.CpuTimeMs,
        result.RowCount,
        result.RanAtUtc);
}
