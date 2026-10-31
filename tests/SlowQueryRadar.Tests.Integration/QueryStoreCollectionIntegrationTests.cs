// tests/SlowQueryRadar.Tests.Integration/QueryStoreCollectionIntegrationTests.cs
using Microsoft.Extensions.Options;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Tests.Integration;

/// <summary>Runs optional read-only ranking checks against a prepared Query Store database.</summary>
public sealed class QueryStoreCollectionIntegrationTests
{
    private readonly IntegrationTestFixture _fixture = new();

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Collector_AllRankModes_ReturnOrderedResults()
    {
        var connectionString = _fixture.Configuration["QueryStore:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }
        var options = new QueryStoreOptions
        {
            ConnectionString = connectionString!,
            CommandTimeoutSeconds = int.TryParse(_fixture.Configuration["QueryStore:CommandTimeoutSeconds"], out var timeout) ? timeout : 30
        };
        var collector = new QueryStoreCollector(Options.Create(options));
        foreach (var rankBy in Enum.GetValues<RankBy>())
        {
            var result = await collector.CollectAsync(new CollectOptions { Top = 10, Lookback = TimeSpan.FromHours(24), RankBy = rankBy }, CancellationToken.None);
            var values = result.Queries.Select(query => rankBy switch
            {
                RankBy.TotalDuration => query.TotalDurationMs,
                RankBy.AvgDuration => query.AvgDurationMs,
                RankBy.TotalCpu => query.TotalCpuMs,
                RankBy.TotalLogicalReads => (double)query.TotalLogicalReads,
                RankBy.ExecutionCount => query.TotalExecutions,
                _ => 0
            }).ToArray();
            Assert.True(values.Zip(values.Skip(1), (current, next) => current >= next).All(x => x));
        }
    }
}
