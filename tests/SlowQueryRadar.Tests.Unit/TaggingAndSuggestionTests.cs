//tests/SlowQueryRadar.Tests.Unit/TaggingAndSuggestionTests.cs
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Core.Models;
using SlowQueryRadar.Suggestions.Deterministic;
using SlowQueryRadar.Tagging;

namespace SlowQueryRadar.Tests.Unit;

public sealed class TaggingAndSuggestionTests
{
    [Fact]
    public void RadarTagCreatesLeadingComment()
    {
        var tagged = RadarTag.Add("SELECT 1", "Orders.Search", "tenant-42", "OrdersRepository.Search");

        Assert.StartsWith("/* feature:Orders.Search; tenant:tenant-42; caller:OrdersRepository.Search */", tagged, StringComparison.Ordinal);
    }

    [Fact]
    public void DeterministicSuggesterRanksBySeverityAndConfidence()
    {
        var snapshot = new SlowQuerySnapshot
        {
            QueryHash = "query-1",
            NormalizedSqlHash = "normalized-1",
            TotalDurationMs = 100,
            TenantId = "tenant-1",
            FeatureName = "Orders.Search"
        };
        var result = new AnalysisResult(1, [
            new Finding("LOW", Severity.Low, "Low", null, "low fix", 0) { Confidence = Confidence.Low },
            new Finding("HIGH", Severity.High, "High", null, "high fix", 0) { Confidence = Confidence.High }
        ], DateTime.UtcNow);

        var actions = new DeterministicSuggester().BuildActions(snapshot, result);

        Assert.Equal("HIGH", actions[0].RuleId);
        Assert.True(actions[0].ImpactScore > actions[1].ImpactScore);
    }

    [Fact]
    public void BenchmarkEnvironmentProductionIsRejected()
    {
        var options = new SlowQueryRadar.Benchmark.BenchmarkOptions
        {
            Environment = "Production",
            ConnectionString = "Server=localhost;Database=RadarDb;Integrated Security=true"
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
