//src/SlowQueryRadar.Benchmark/BenchmarkComparisonResult.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Benchmark;

/// <summary>Contains before/after benchmark metrics and interpretation confidence.</summary>
public sealed record BenchmarkComparisonResult(
    BenchmarkResult Original,
    BenchmarkResult Proposed,
    double DurationImprovementPercent,
    double LogicalReadsImprovementPercent,
    string ConfidenceNote,
    bool ResultSetsMatch);
