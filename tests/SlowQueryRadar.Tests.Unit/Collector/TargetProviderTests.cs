// tests/SlowQueryRadar.Tests.Unit/Collector/TargetProviderTests.cs
using Microsoft.Extensions.Options;
using SlowQueryRadar.Collector;

namespace SlowQueryRadar.Tests.Unit.Collector;

/// <summary>Tests monitoring target resolution behavior.</summary>
public sealed class TargetProviderTests
{
    [Fact]
    public async Task GetTargetsAsync_WhenMonitoringTargetsEmpty_UsesLegacyQueryStoreConnectionString()
    {
        var provider = new TargetProvider(
            Options.Create(new MonitoringOptions()),
            Options.Create(new DiscoveryOptions { Enabled = false }),
            Options.Create(new QueryStoreOptions { ConnectionString = "Server=.;Database=LegacyDb;Integrated Security=true;" }));

        var targets = await provider.GetTargetsAsync(CancellationToken.None);

        var target = Assert.Single(targets);
        Assert.Equal("default", target.Name);
        Assert.Equal("Server=.;Database=LegacyDb;Integrated Security=true;", target.ConnectionString);
    }
}
