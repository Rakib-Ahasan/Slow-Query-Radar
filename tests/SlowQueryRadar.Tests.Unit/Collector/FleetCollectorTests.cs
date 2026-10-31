// tests/SlowQueryRadar.Tests.Unit/Collector/FleetCollectorTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Tests.Unit.Collector;

/// <summary>Tests fleet collection orchestration semantics.</summary>
public sealed class FleetCollectorTests
{
    [Fact]
    public async Task CollectAsync_IsolatesTargetFailure()
    {
        var targets = new[]
        {
            new MonitoringTarget("tenant-ok", "g", "c1", [], true),
            new MonitoringTarget("tenant-fail", "g", "c2", [], true)
        };
        var provider = new StubTargetProvider(targets);
        var collector = new FleetCollector(
            provider,
            Options.Create(new MonitoringOptions { MaxParallelTargets = 2, PerTargetTimeoutSeconds = 30 }),
            NullLoggerFactory.Instance,
            NullLogger<FleetCollector>.Instance,
            target => target.Name == "tenant-fail"
                ? new StubQueryStoreCollector(_ => throw new InvalidOperationException("boom"))
                : new StubQueryStoreCollector(_ => Task.FromResult(new CollectResult(
                    [new SlowQuerySnapshot { QueryId = 1, SqlText = "select 1", NormalizedSqlHash = "A", TotalDurationMs = 12 }],
                    [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "READ_WRITE", DateTimeOffset.UtcNow))));

        var result = await collector.CollectAsync(new CollectOptions { Top = 5 }, null, null, CancellationToken.None);

        Assert.Equal(2, result.Results.Count);
        Assert.Contains(result.Results, row => row.Name == "tenant-ok" && row.Status == TargetResultStatus.Ok && row.Queries.Count == 1);
        Assert.Contains(result.Results, row => row.Name == "tenant-fail" && row.Status == TargetResultStatus.Failed && row.ErrorMessage == "boom");
    }

    [Fact]
    public async Task CollectAsync_TimeoutMarksTargetFailedWithoutStoppingOthers()
    {
        var targets = new[]
        {
            new MonitoringTarget("tenant-fast", "g", "c1", [], true),
            new MonitoringTarget("tenant-slow", "g", "c2", [], true)
        };
        var provider = new StubTargetProvider(targets);
        var collector = new FleetCollector(
            provider,
            Options.Create(new MonitoringOptions { MaxParallelTargets = 2, PerTargetTimeoutSeconds = 1 }),
            NullLoggerFactory.Instance,
            NullLogger<FleetCollector>.Instance,
            target => target.Name == "tenant-slow"
                ? new StubQueryStoreCollector(async ct =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    return new CollectResult([], [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "READ_WRITE", DateTimeOffset.UtcNow);
                })
                : new StubQueryStoreCollector(_ => Task.FromResult(new CollectResult([], [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "READ_WRITE", DateTimeOffset.UtcNow))));

        var result = await collector.CollectAsync(new CollectOptions { Top = 5 }, null, null, CancellationToken.None);

        Assert.Contains(result.Results, row => row.Name == "tenant-fast" && row.Status == TargetResultStatus.Ok);
        Assert.Contains(result.Results, row => row.Name == "tenant-slow" && row.Status == TargetResultStatus.Failed && row.ErrorMessage!.Contains("Timed out", StringComparison.Ordinal));
    }

    private sealed class StubTargetProvider : ITargetProvider
    {
        private readonly IReadOnlyList<MonitoringTarget> _targets;

        public StubTargetProvider(IReadOnlyList<MonitoringTarget> targets)
        {
            _targets = targets;
        }

        public Task<IReadOnlyList<MonitoringTarget>> GetTargetsAsync(CancellationToken ct) => Task.FromResult(_targets);
    }

    private sealed class StubQueryStoreCollector : IQueryStoreCollector
    {
        private readonly Func<CancellationToken, Task<CollectResult>> _collect;

        public StubQueryStoreCollector(Func<CancellationToken, Task<CollectResult>> collect)
        {
            _collect = collect;
        }

        public Task<CollectResult> CollectAsync(CollectOptions options, CancellationToken ct) => _collect(ct);

        public Task<IReadOnlyList<SlowQuerySnapshot>> CollectTopSlowQueriesAsync(int topN, TimeSpan lookback, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SlowQuerySnapshot>>([]);

        public Task<SlowQuerySnapshot?> GetByQueryIdAsync(long queryId, CancellationToken ct)
            => Task.FromResult<SlowQuerySnapshot?>(null);
    }
}
