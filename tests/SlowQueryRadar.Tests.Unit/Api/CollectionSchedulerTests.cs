// tests/SlowQueryRadar.Tests.Unit/Api/CollectionSchedulerTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Api.Services;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Tests.Unit.Api;

/// <summary>Tests collection scheduler overlap protection.</summary>
public sealed class CollectionSchedulerTests
{
    [Fact]
    public async Task RunOnceGuardedAsync_PreventsOverlappingRuns()
    {
        var fleetCollector = new BlockingFleetCollector();
        var snapshotStore = new RecordingSnapshotStore();
        var scheduler = new CollectionScheduler(
            fleetCollector,
            snapshotStore,
            new NoopRegressionDetector(),
            new NoopNotifier(),
            Options.Create(new QueryStoreOptions { TopN = 5, LookbackHours = 1 }),
            Options.Create(new MonitoringOptions { IntervalMinutes = 15 }),
            Options.Create(new SnapshotStoreOptions { RetentionDays = 30 }),
            NullLogger<CollectionScheduler>.Instance);

        var first = scheduler.RunOnceGuardedAsync(CancellationToken.None);
        await fleetCollector.Started.Task;
        var second = scheduler.RunOnceGuardedAsync(CancellationToken.None);
        fleetCollector.Release.SetResult();

        await Task.WhenAll(first, second);

        Assert.Equal(1, fleetCollector.CallCount);
    }

    private sealed class BlockingFleetCollector : IFleetCollector
    {
        public int CallCount;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<FleetCollectResult> CollectAsync(CollectOptions options, string? group, string? tag, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            Started.TrySetResult();
            await Release.Task;
            return new FleetCollectResult([], DateTimeOffset.UtcNow);
        }
    }

    private sealed class RecordingSnapshotStore : ISnapshotStore
    {
        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<long> SaveRunAsync(DateTimeOffset startedUtc, TimeSpan duration, FleetCollectResult result, CancellationToken ct) => Task.FromResult(1L);
        public Task<IReadOnlyList<QuerySample>> ReadSamplesAsync(string? target, DateTimeOffset sinceUtc, DateTimeOffset untilUtc, CancellationToken ct) => Task.FromResult<IReadOnlyList<QuerySample>>([]);
        public Task<IReadOnlyList<QuerySample>> ReadLatestSamplesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuerySample>>([]);
        public Task<IReadOnlyList<TargetRunRecord>> ReadTargetRunsAsync(DateTimeOffset sinceUtc, DateTimeOffset untilUtc, CancellationToken ct) => Task.FromResult<IReadOnlyList<TargetRunRecord>>([]);
        public Task<int> CleanupAsync(TimeSpan retention, CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class NoopRegressionDetector : IRegressionDetector
    {
        public Task<IReadOnlyList<Regression>> DetectAsync(string? target, CancellationToken ct) => Task.FromResult<IReadOnlyList<Regression>>([]);
    }

    private sealed class NoopNotifier : INotifier
    {
        public Task NotifyAsync(IReadOnlyList<Regression> regressions, CancellationToken ct) => Task.CompletedTask;
    }
}
