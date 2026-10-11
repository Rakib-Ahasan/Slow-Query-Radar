//src/SlowQueryRadar.Api/Services/ApiMapper.cs
using SlowQueryRadar.Api.Contracts;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Api.Services;

/// <summary>Maps Core models to API-owned JSON contracts.</summary>
public static class ApiMapper
{
    /// <summary>Maps a slow-query snapshot.</summary>
    public static SlowQueryDto ToDto(SlowQuerySnapshot snapshot) => new(
        snapshot.QueryId,
        snapshot.SqlText,
        snapshot.NormalizedSqlHash,
        snapshot.ExecCount,
        snapshot.AvgDurationMs,
        snapshot.TotalCpuMs,
        snapshot.TotalLogicalReads,
        snapshot.ImpactScore,
        snapshot.ExecutionPlanXml,
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
