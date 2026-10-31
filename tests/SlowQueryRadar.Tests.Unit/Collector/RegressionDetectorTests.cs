// tests/SlowQueryRadar.Tests.Unit/Collector/RegressionDetectorTests.cs
using Microsoft.Extensions.Options;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Tests.Unit.Collector;

/// <summary>Tests regression detector thresholds and boundary behavior.</summary>
public sealed class RegressionDetectorTests
{
    [Fact]
    public async Task DetectAsync_WhenThresholdsMet_ReturnsCriticalRegression()
    {
        var latest = new QuerySample(2, "tenant-a", "Q", "N", "SELECT 1", 20, 150, 3000, 200, 40, 100, 1000, 1, DateTimeOffset.UtcNow);
        var baseline = new QuerySample(1, "tenant-a", "Q", "N", "SELECT 1", 40, 100, 4000, 120, 20, 80, 900, 1, DateTimeOffset.UtcNow.AddDays(-1));
        var detector = new RegressionDetector(new StubSnapshotStore([latest], [baseline]), Options.Create(new RegressionDetectionOptions()));

        var regressions = await detector.DetectAsync(null, CancellationToken.None);

        var regression = Assert.Single(regressions);
        Assert.Equal(RegressionSeverity.Critical, regression.Severity);
    }

    [Fact]
    public async Task DetectAsync_WhenExecutionsBelowBoundary_DoesNotFlagPerformanceRegression()
    {
        var latest = new QuerySample(2, "tenant-a", "Q", "N", "SELECT 1", 19, 150, 2850, 200, 40, 100, 1000, 1, DateTimeOffset.UtcNow);
        var baseline = new QuerySample(1, "tenant-a", "Q", "N", "SELECT 1", 40, 100, 4000, 120, 20, 80, 900, 1, DateTimeOffset.UtcNow.AddDays(-1));
        var detector = new RegressionDetector(new StubSnapshotStore([latest], [baseline]), Options.Create(new RegressionDetectionOptions()));

        var regressions = await detector.DetectAsync(null, CancellationToken.None);

        Assert.DoesNotContain(regressions, row => row.Severity == RegressionSeverity.Critical);
    }

    [Fact]
    public async Task DetectAsync_WhenNoBaseline_ReturnsWarningNewSlowQuery()
    {
        var latest = new QuerySample(2, "tenant-a", "Q", "N", "SELECT 1", 30, 80, 2400, 110, 15, 50, 600, 1, DateTimeOffset.UtcNow);
        var detector = new RegressionDetector(new StubSnapshotStore([latest], []), Options.Create(new RegressionDetectionOptions()));

        var regressions = await detector.DetectAsync(null, CancellationToken.None);

        var regression = Assert.Single(regressions);
        Assert.True(regression.IsNewSlowQuery);
        Assert.Equal(RegressionSeverity.Warning, regression.Severity);
    }

    private sealed class StubSnapshotStore : ISnapshotStore
    {
        private readonly IReadOnlyList<QuerySample> _latest;
        private readonly IReadOnlyList<QuerySample> _baseline;

        public StubSnapshotStore(IReadOnlyList<QuerySample> latest, IReadOnlyList<QuerySample> baseline)
        {
            _latest = latest;
            _baseline = baseline;
        }

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<long> SaveRunAsync(DateTimeOffset startedUtc, TimeSpan duration, FleetCollectResult result, CancellationToken ct) => Task.FromResult(1L);
        public Task<IReadOnlyList<QuerySample>> ReadSamplesAsync(string? target, DateTimeOffset sinceUtc, DateTimeOffset untilUtc, CancellationToken ct) => Task.FromResult(_baseline);
        public Task<IReadOnlyList<QuerySample>> ReadLatestSamplesAsync(CancellationToken ct) => Task.FromResult(_latest);
        public Task<IReadOnlyList<TargetRunRecord>> ReadTargetRunsAsync(DateTimeOffset sinceUtc, DateTimeOffset untilUtc, CancellationToken ct) => Task.FromResult<IReadOnlyList<TargetRunRecord>>([]);
        public Task<int> CleanupAsync(TimeSpan retention, CancellationToken ct) => Task.FromResult(0);
    }
}
