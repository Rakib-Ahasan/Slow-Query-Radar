//src/SlowQueryRadar.Core/Models/AnalysisResult.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Contains the findings produced for a query analysis.</summary>
public sealed record AnalysisResult(
    long QueryId,
    IReadOnlyList<Finding> Findings,
    DateTime AnalyzedAtUtc);
