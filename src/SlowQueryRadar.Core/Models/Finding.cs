//src/SlowQueryRadar.Core/Models/Finding.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Describes a detected SQL performance finding.</summary>
public sealed record Finding(
    string RuleId,
    Severity Severity,
    string Message,
    string? Evidence,
    string? SuggestedFix,
    double EstimatedImpactPercent);
