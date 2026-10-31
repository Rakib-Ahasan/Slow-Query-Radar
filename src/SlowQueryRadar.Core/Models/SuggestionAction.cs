//src/SlowQueryRadar.Core/Models/SuggestionAction.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Represents one prioritized deterministic remediation action.</summary>
public sealed record SuggestionAction(
    string QueryKey,
    string? Tenant,
    string RuleId,
    string Title,
    string Action,
    double ImpactScore,
    Severity Severity,
    Confidence Confidence,
    string? FeatureName = null,
    int TenantCount = 1);
