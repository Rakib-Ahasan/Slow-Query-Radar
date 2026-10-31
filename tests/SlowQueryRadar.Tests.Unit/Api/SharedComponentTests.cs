// tests/SlowQueryRadar.Tests.Unit/Api/SharedComponentTests.cs
using Bunit;
using SlowQueryRadar.Api.Components.Shared;

namespace SlowQueryRadar.Tests.Unit.Api;

/// <summary>Verifies the mission-control shared Blazor components.</summary>
public sealed class SharedComponentTests : BunitContext
{

    [Fact]
    public void SeverityChip_RendersSeverityText()
    {
        var cut = Render<SeverityChip>(parameters => parameters.Add(x => x.Severity, "Critical"));
        Assert.Contains("Critical", cut.Markup);
    }

    [Fact]
    public void HeatmapGrid_RendersAccessibleCell()
    {
        var bucket = DateTimeOffset.UtcNow;
        var cut = Render<HeatmapGrid>(parameters => parameters
            .Add(x => x.Rows, ["Northstar"])
            .Add(x => x.Buckets, [bucket])
            .Add(x => x.Cells, [new HeatmapGrid.HeatmapCell("Northstar", bucket, 842)]));
        Assert.Contains("Northstar", cut.Markup);
        Assert.Contains("842 milliseconds", cut.Markup);
    }

    [Fact]
    public void RadarChart_MapsBlipDataToAccessibleSvgNode()
    {
        var cut = Render<RadarChart>(parameters => parameters.Add(x => x.Blips, [new RadarChart.RadarBlip("Bluebird", 12, -20, 8, "#FF5C6C", 42, "Critical")]));
        var blip = cut.Find("g[aria-label='Bluebird: 42 queries']");
        Assert.Equal("translate(222 130)", blip.GetAttribute("transform"));
        Assert.Contains("Bluebird", cut.Markup);
    }
}
