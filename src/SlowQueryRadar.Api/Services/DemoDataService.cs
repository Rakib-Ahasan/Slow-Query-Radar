//src/SlowQueryRadar.Api/Services/DemoDataService.cs
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Api.Components.Shared;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Api.Services;

/// <summary>Provides deterministic demo data for screenshots and local development.</summary>
public sealed class DemoDataService
{
    /// <summary>Gets realistic demo tenant names.</summary>
    public IReadOnlyList<string> Tenants { get; } = ["Northstar", "Bluebird", "Atlas", "Cobalt", "Juniper", "Keystone"];
    /// <summary>Gets demo radar points.</summary>
    public IReadOnlyList<RadarChart.RadarBlip> Blips => Tenants.Select((tenant, index) => new RadarChart.RadarBlip(tenant, Math.Cos(index * 1.05) * (34 + index * 12), Math.Sin(index * 1.05) * (34 + index * 12), 5 + index % 4, index == 1 ? "#FF5C6C" : index == 4 ? "#F5B544" : "#2DD4BF", 8 + index * 3, index == 1 ? "critical" : "healthy")).ToArray();
    /// <summary>Gets demo heatmap data.</summary>
    public IReadOnlyList<HeatmapGrid.HeatmapCell> Heatmap => Tenants.SelectMany((tenant, row) => Enumerable.Range(0, 12).Select(bucket => new HeatmapGrid.HeatmapCell(tenant, DateTimeOffset.UtcNow.Date.AddHours(bucket * 2), 80 + row * 90 + (bucket % 4) * 55))).ToArray();
    /// <summary>Gets demo feature pain totals.</summary>
    public IReadOnlyList<(string Feature, double Value)> Features { get; } = [("Orders.Search", 92), ("Billing.Invoice", 74), ("Catalog.Lookup", 51), ("Identity.Resolve", 34)];
}
