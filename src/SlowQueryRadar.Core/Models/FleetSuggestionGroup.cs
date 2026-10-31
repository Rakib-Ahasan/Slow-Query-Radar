//src/SlowQueryRadar.Core/Models/FleetSuggestionGroup.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Groups one remediation across tenants sharing the same normalized query.</summary>
public sealed record FleetSuggestionGroup(
    string QueryKey,
    string? FeatureName,
    IReadOnlyList<string> Tenants,
    IReadOnlyList<SuggestionAction> Actions,
    string Summary);
