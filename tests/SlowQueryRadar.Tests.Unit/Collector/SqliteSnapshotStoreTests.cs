// tests/SlowQueryRadar.Tests.Unit/Collector/SqliteSnapshotStoreTests.cs
using Microsoft.Extensions.Options;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Tests.Unit.Collector;

/// <summary>Tests SQLite snapshot store behavior.</summary>
public sealed class SqliteSnapshotStoreTests
{
    [Fact]
    public async Task SaveRunAndReadLatest_RoundTripsData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radar-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteSnapshotStore(Options.Create(new SnapshotStoreOptions { ConnectionString = $"Data Source={path};Pooling=False" }));
            await store.InitializeAsync(CancellationToken.None);

            var now = DateTimeOffset.UtcNow;
            var result = new FleetCollectResult(
                [new TargetResult("tenant-a", "G", TargetResultStatus.Ok, null,
                    [new SlowQuerySnapshot
                    {
                        QueryHash = "Q1",
                        NormalizedSqlHash = "N1",
                        SqlText = "SELECT 1",
                        TotalExecutions = 25,
                        AvgDurationMs = 12,
                        TotalDurationMs = 300,
                        MaxDurationMs = 40,
                        StandardDeviationMs = 6,
                        TotalCpuMs = 40,
                        TotalLogicalReads = 200,
                        PlanCount = 2,
                        CapturedAtUtc = now
                    }],
                    TimeSpan.FromSeconds(1))],
                now);

            await store.SaveRunAsync(now, TimeSpan.FromSeconds(2), result, CancellationToken.None);
            var latest = await store.ReadLatestSamplesAsync(CancellationToken.None);

            var sample = Assert.Single(latest);
            Assert.Equal("tenant-a", sample.Target);
            Assert.Equal("N1", sample.NormalizedHash);
            Assert.Equal(300, sample.TotalMs, 5);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Cleanup_RemovesExpiredRowsByRetention()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radar-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteSnapshotStore(Options.Create(new SnapshotStoreOptions { ConnectionString = $"Data Source={path};Pooling=False" }));
            await store.InitializeAsync(CancellationToken.None);

            var oldRun = DateTimeOffset.UtcNow.AddDays(-40);
            var result = new FleetCollectResult(
                [new TargetResult("tenant-a", null, TargetResultStatus.Ok, null,
                    [new SlowQuerySnapshot { QueryHash = "Q1", NormalizedSqlHash = "N1", SqlText = "SELECT 1", TotalExecutions = 1, AvgDurationMs = 1, TotalDurationMs = 1, MaxDurationMs = 1, StandardDeviationMs = 0, TotalCpuMs = 1, TotalLogicalReads = 1, PlanCount = 1, CapturedAtUtc = oldRun }],
                    TimeSpan.FromSeconds(1))],
                oldRun);

            await store.SaveRunAsync(oldRun, TimeSpan.FromSeconds(1), result, CancellationToken.None);
            var deleted = await store.CleanupAsync(TimeSpan.FromDays(30), CancellationToken.None);
            var latest = await store.ReadLatestSamplesAsync(CancellationToken.None);

            Assert.True(deleted > 0);
            Assert.Empty(latest);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
