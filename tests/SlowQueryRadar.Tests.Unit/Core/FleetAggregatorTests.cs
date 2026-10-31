// tests/SlowQueryRadar.Tests.Unit/Core/FleetAggregatorTests.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Tests.Unit.Core;

/// <summary>Tests fleet query aggregation across tenants.</summary>
public sealed class FleetAggregatorTests
{
    [Fact]
    public void AggregateByNormalizedHash_GroupsAcrossTargets()
    {
        var fleetResult = new FleetCollectResult(
            [
                new TargetResult(
                    "tenant-a",
                    "Enterprise",
                    TargetResultStatus.Ok,
                    null,
                    [new SlowQuerySnapshot { TargetName = "tenant-a", SqlText = "SELECT * FROM T WHERE Id = 1", NormalizedSqlHash = "HASH-1", TotalDurationMs = 30 }],
                    TimeSpan.FromSeconds(1)),
                new TargetResult(
                    "tenant-b",
                    "Enterprise",
                    TargetResultStatus.Ok,
                    null,
                    [new SlowQuerySnapshot { TargetName = "tenant-b", SqlText = "SELECT * FROM T WHERE Id = 2", NormalizedSqlHash = "HASH-1", TotalDurationMs = 10 }],
                    TimeSpan.FromSeconds(1))
            ],
            DateTimeOffset.UtcNow);

        var aggregates = FleetAggregator.AggregateByNormalizedHash(fleetResult);

        var aggregate = Assert.Single(aggregates);
        Assert.Equal("HASH-1", aggregate.NormalizedSqlHash);
        Assert.Equal(2, aggregate.AffectedTenants);
        Assert.Equal(40, aggregate.TotalDurationMs, 5);
        Assert.Equal("tenant-a", aggregate.WorstTenant);
        Assert.Equal("tenant-b", aggregate.BestTenant);
    }
}
