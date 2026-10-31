//src/SlowQueryRadar.Suggestions/Deterministic/DeterministicSuggester.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Suggestions.Deterministic;

/// <summary>Converts findings and snapshots into prioritized deterministic actions.</summary>
public sealed class DeterministicSuggester
{
    /// <summary>Builds actions ordered by total-duration share, severity, and confidence.</summary>
    /// <param name="snapshot">The analyzed query snapshot.</param>
    /// <param name="result">The associated findings.</param>
    /// <returns>Prioritized actions.</returns>
    public IReadOnlyList<SuggestionAction> BuildActions(SlowQuerySnapshot snapshot, AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(result);
        var totalDuration = Math.Max(snapshot.TotalDurationMs, snapshot.AvgDurationMs * snapshot.TotalExecutions);
        var durationShare = totalDuration <= 0 ? 1d : Math.Clamp(totalDuration / Math.Max(totalDuration, 1d), 0d, 1d);
        var queryKey = string.IsNullOrWhiteSpace(snapshot.NormalizedSqlHash) ? snapshot.QueryHash : snapshot.NormalizedSqlHash;
        return BuildActions(queryKey, result, snapshot.TenantId, snapshot.FeatureName, durationShare);
    }

    /// <summary>Builds actions for a known query key and optional context.</summary>
    /// <param name="queryKey">The normalized query key.</param>
    /// <param name="result">The associated findings.</param>
    /// <param name="tenantId">Optional tenant identifier.</param>
    /// <param name="featureName">Optional feature name.</param>
    /// <param name="durationShare">Optional duration share used for impact scoring.</param>
    /// <returns>Prioritized actions.</returns>
    public IReadOnlyList<SuggestionAction> BuildActions(
        string queryKey,
        AnalysisResult result,
        string? tenantId = null,
        string? featureName = null,
        double durationShare = 1d)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryKey);
        ArgumentNullException.ThrowIfNull(result);
        var clampedDurationShare = Math.Clamp(durationShare, 0d, 1d);
        return result.Findings
            .Where(finding => !string.Equals(finding.RuleId, "AnalysisFailed", StringComparison.OrdinalIgnoreCase))
            .Select(finding => new SuggestionAction(
                queryKey,
                tenantId,
                finding.RuleId,
                finding.Title,
                finding.SuggestedFix ?? finding.Message,
                clampedDurationShare * SeverityWeight(finding.Severity) * ConfidenceWeight(finding.Confidence),
                finding.Severity,
                finding.Confidence,
                featureName))
            .OrderByDescending(action => action.ImpactScore)
            .ThenByDescending(action => action.Severity)
            .ToArray();
    }

    /// <summary>Groups actions from multiple tenants by normalized query and feature.</summary>
    /// <param name="items">Snapshots and their analysis results.</param>
    /// <returns>Fleet-wide grouped actions.</returns>
    public IReadOnlyList<FleetSuggestionGroup> GroupFleet(IEnumerable<(SlowQuerySnapshot Snapshot, AnalysisResult Result)> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items
            .SelectMany(item => BuildActions(item.Snapshot, item.Result).Select(action => (item.Snapshot, Action: action)))
            .GroupBy(item => (item.Action.QueryKey, item.Action.FeatureName), StringTupleComparer.Instance)
            .Select(group =>
            {
                var tenants = group.Select(item => item.Snapshot.TenantId).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var actions = group.Select(item => item.Action with { TenantCount = Math.Max(1, tenants.Length) }).OrderByDescending(action => action.ImpactScore).ToArray();
                return new FleetSuggestionGroup(group.Key.QueryKey, group.Key.FeatureName, tenants!, actions, $"Fix once, benefits {Math.Max(1, tenants.Length)} tenants.");
            })
            .OrderByDescending(group => group.Actions.FirstOrDefault()?.ImpactScore ?? 0)
            .ToArray();
    }

    private static double SeverityWeight(Severity severity) => severity switch
    {
        Severity.Critical => 4,
        Severity.High => 3,
        Severity.Medium => 2,
        _ => 1
    };

    private static double ConfidenceWeight(Confidence confidence) => confidence switch
    {
        Confidence.High => 1,
        Confidence.Medium => 0.75,
        _ => 0.5
    };

    private sealed class StringTupleComparer : IEqualityComparer<(string QueryKey, string? FeatureName)>
    {
        public static StringTupleComparer Instance { get; } = new();
        public bool Equals((string QueryKey, string? FeatureName) x, (string QueryKey, string? FeatureName) y) =>
            string.Equals(x.QueryKey, y.QueryKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.FeatureName, y.FeatureName, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string QueryKey, string? FeatureName) value) => HashCode.Combine(value.QueryKey.ToUpperInvariant(), value.FeatureName?.ToUpperInvariant());
    }
}
