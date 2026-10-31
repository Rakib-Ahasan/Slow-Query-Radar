//src/SlowQueryRadar.Core/Models/Finding.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Describes a detected SQL performance finding.</summary>
public sealed record Finding(
    string RuleId,
    Severity Severity,
    string Message,
    string? Evidence,
    string? SuggestedFix,
    double EstimatedImpactPercent)
{
    /// <summary>Gets the concise finding title.</summary>
    public string Title => Message;

    /// <summary>Gets why the finding matters.</summary>
    public string WhyItMatters { get; init; } = string.Empty;

    /// <summary>Gets the confidence in the finding.</summary>
    public Confidence Confidence { get; init; } = Confidence.Medium;

    /// <summary>Gets a documentation link for the rule.</summary>
    public string? DocsLink { get; init; }
}
