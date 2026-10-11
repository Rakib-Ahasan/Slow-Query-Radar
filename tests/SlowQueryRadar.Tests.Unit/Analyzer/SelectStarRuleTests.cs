//tests/SlowQueryRadar.Tests.Unit/Analyzer/SelectStarRuleTests.cs
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Analyzer.Rules;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;
using Xunit;

namespace SlowQueryRadar.Tests.Unit.Analyzer;

public sealed class SelectStarRuleTests
{
    [Fact]
    public void SelectStarProducesMediumFinding()
    {
        const string sql = "SELECT * FROM dbo.Users";
        var finding = Assert.Single(new SelectStarRule().Evaluate(
            new SqlAnalysisContext(sql, SqlAnalyzer.ParseSql(sql)[0], null)));

        Assert.Equal("SQLR-006", finding.RuleId);
        Assert.Equal(Severity.Medium, finding.Severity);
    }
}
