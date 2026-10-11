//tests/SlowQueryRadar.Tests.Unit/Analyzer/TableScanRuleTests.cs
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Analyzer.Plan;
using SlowQueryRadar.Analyzer.Rules;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;
using Xunit;

namespace SlowQueryRadar.Tests.Unit.Analyzer;

public sealed class TableScanRuleTests
{
    [Fact]
    public void LargeTableScanProducesHighFinding()
    {
        var finding = Assert.Single(new TableScanRule(new ExecutionPlanParser()).Evaluate(CreateContext("table-scan.sqlplan")));

        Assert.Equal("SQLR-002", finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
    }

    private static SqlAnalysisContext CreateContext(string fixture)
    {
        var xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Plans", fixture));
        return new SqlAnalysisContext("SELECT * FROM dbo.Users", SqlAnalyzer.ParseSql("SELECT * FROM dbo.Users")[0], xml);
    }
}
