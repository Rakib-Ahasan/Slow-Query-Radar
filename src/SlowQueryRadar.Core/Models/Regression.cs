//src/SlowQueryRadar.Core/Models/Regression.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Describes a detected query regression on a target.</summary>
public sealed record Regression(
    string Target,
    string NormalizedHash,
    string SqlText,
    RegressionSeverity Severity,
    IReadOnlyList<string> Evidence,
    double LatestAvgMs,
    double BaselineMedianMs,
    long LatestExecutions,
    DateTimeOffset DetectedAtUtc,
    bool IsNewSlowQuery,
    bool ParameterSniffingSuspected);
