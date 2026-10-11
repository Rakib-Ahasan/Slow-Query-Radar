//src/SlowQueryRadar.Core/Models/BenchmarkResult.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Contains measured metrics for one benchmark run.</summary>
public sealed partial record BenchmarkResult(
    string SqlHash,
    double DurationP50Ms,
    double DurationP95Ms,
    double DurationMaxMs,
    long LogicalReads,
    double CpuTimeMs,
    long RowCount,
    DateTime RanAtUtc);

/// <summary>Provides the deterministic checksum used for result-set comparison.</summary>
public sealed partial record BenchmarkResult
{
    /// <summary>Gets the MD5 checksum of the first 100 result rows, when available.</summary>
    public string? FirstRowChecksum { get; init; }
}

/// <summary>Compares before and after benchmark results.</summary>
public sealed record BenchmarkComparison(
    BenchmarkResult Before,
    BenchmarkResult After,
    double DurationImprovementPercent,
    double LogicalReadsImprovementPercent,
    bool ResultSetsMatch,
    bool Validated);
