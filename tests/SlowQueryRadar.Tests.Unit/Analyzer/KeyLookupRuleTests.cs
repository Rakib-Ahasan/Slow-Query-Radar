//tests/SlowQueryRadar.Tests.Unit/Analyzer/KeyLookupRuleTests.cs
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Analyzer.Plan;
using SlowQueryRadar.Analyzer.Rules;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;
using Xunit;

namespace SlowQueryRadar.Tests.Unit.Analyzer;

public sealed class KeyLookupRuleTests
{
    [Fact]
    public void KeyLookupProducesMediumFinding()
    {
        var finding = Assert.Single(new KeyLookupRule(new ExecutionPlanParser()).Evaluate(CreateContext("key-lookup.sqlplan")));

        Assert.Equal("SQLR-003", finding.RuleId);
        Assert.Equal(Severity.Medium, finding.Severity);
    }

    private static SqlAnalysisContext CreateContext(string fixture)
    {
        var xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Plans", fixture));
        return new SqlAnalysisContext("SELECT Name FROM dbo.Users WHERE Id = 5", SqlAnalyzer.ParseSql("SELECT Name FROM dbo.Users WHERE Id = 5")[0], xml);
    }
}
