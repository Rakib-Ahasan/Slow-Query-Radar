//src/SlowQueryRadar.Collector/RegressionDetector.cs
using Microsoft.Extensions.Options;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Collector;

/// <summary>Detects regressions by comparing latest samples with historical baselines.</summary>
public sealed class RegressionDetector : IRegressionDetector
{
    private readonly ISnapshotStore _snapshotStore;
    private readonly RegressionDetectionOptions _options;

    /// <summary>Initializes a regression detector.</summary>
    public RegressionDetector(ISnapshotStore snapshotStore, IOptions<RegressionDetectionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(snapshotStore);
        ArgumentNullException.ThrowIfNull(options);

        _snapshotStore = snapshotStore;
        _options = options.Value ?? new RegressionDetectionOptions();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Regression>> DetectAsync(string? target, CancellationToken ct)
    {
        var latestSamples = await _snapshotStore.ReadLatestSamplesAsync(ct).ConfigureAwait(false);
        if (latestSamples.Count == 0)
        {
            return Array.Empty<Regression>();
        }

        var filteredLatest = string.IsNullOrWhiteSpace(target)
            ? latestSamples
            : latestSamples.Where(sample => string.Equals(sample.Target, target, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (filteredLatest.Count == 0)
        {
            return Array.Empty<Regression>();
        }

        var latestRunId = filteredLatest.Max(sample => sample.RunId);
        var latestCapturedUtc = filteredLatest.Max(sample => sample.CapturedUtc);
        var baselineFrom = latestCapturedUtc.AddDays(-Math.Max(1, _options.BaselineDays));
        var baselineRows = await _snapshotStore.ReadSamplesAsync(target, baselineFrom, latestCapturedUtc, ct).ConfigureAwait(false);
        var baseline = baselineRows.Where(sample => sample.RunId != latestRunId).ToArray();

        var regressions = new List<Regression>();
        var detectedAt = DateTimeOffset.UtcNow;
        foreach (var latest in filteredLatest)
        {
            var keyBaseline = baseline
                .Where(row => string.Equals(row.Target, latest.Target, StringComparison.OrdinalIgnoreCase)
                              && string.Equals(row.NormalizedHash, latest.NormalizedHash, StringComparison.Ordinal))
                .ToArray();

            var evidence = new List<string>();
            var isNewSlowQuery = keyBaseline.Length == 0;
            if (isNewSlowQuery)
            {
                evidence.Add("Query hash not seen in previous baseline window.");
            }

            var baselineMedianAvg = keyBaseline.Length == 0 ? 0 : Median(keyBaseline.Select(row => row.AvgMs));
            var baselineMedianStd = keyBaseline.Length == 0 ? 0 : Median(keyBaseline.Select(row => row.StdDevMs));
            var baselineMedianPlanCount = keyBaseline.Length == 0 ? 0 : (int)Math.Round(Median(keyBaseline.Select(row => (double)row.PlanCount)));

            var relativeIncrease = baselineMedianAvg > 0
                ? (latest.AvgMs - baselineMedianAvg) / baselineMedianAvg
                : 0;
            var absoluteIncrease = latest.AvgMs - baselineMedianAvg;
            var isPerfRegression = baselineMedianAvg > 0
                                   && relativeIncrease >= _options.RelativeIncreaseThreshold
                                   && absoluteIncrease >= _options.AbsoluteIncreaseMsThreshold
                                   && latest.Executions >= _options.MinimumExecutions;
            if (isPerfRegression)
            {
                evidence.Add($"Average duration increased by {(relativeIncrease * 100):F1}% ({absoluteIncrease:F1} ms) against baseline median.");
            }

            var planCountIncreased = latest.PlanCount > baselineMedianPlanCount;
            var varianceIncreased = latest.StdDevMs > baselineMedianStd;
            var parameterSniffingSuspected = keyBaseline.Length > 0 && planCountIncreased && varianceIncreased;
            if (parameterSniffingSuspected)
            {
                evidence.Add("Plan count and duration variance increased versus baseline, indicating parameter sensitivity risk.");
            }

            if (!isNewSlowQuery && !isPerfRegression && !parameterSniffingSuspected)
            {
                continue;
            }

            var severity = isPerfRegression
                ? RegressionSeverity.Critical
                : isNewSlowQuery
                    ? RegressionSeverity.Warning
                    : RegressionSeverity.Info;

            regressions.Add(new Regression(
                latest.Target,
                latest.NormalizedHash,
                latest.SqlText,
                severity,
                evidence,
                latest.AvgMs,
                baselineMedianAvg,
                latest.Executions,
                detectedAt,
                isNewSlowQuery,
                parameterSniffingSuspected));
        }

        return regressions
            .OrderByDescending(regression => regression.Severity)
            .ThenByDescending(regression => regression.LatestAvgMs)
            .ToArray();
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        var mid = sorted.Length / 2;
        if (sorted.Length % 2 == 0)
        {
            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        return sorted[mid];
    }
}
