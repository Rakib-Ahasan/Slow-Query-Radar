//tests/SlowQueryRadar.Tests.Unit/Analyzer/ImplicitConversionRuleTests.cs
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Analyzer.Plan;
using SlowQueryRadar.Analyzer.Rules;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;
using Xunit;

namespace SlowQueryRadar.Tests.Unit.Analyzer;

public sealed class ImplicitConversionRuleTests
{
    [Fact]
    public void ImplicitConversionProducesHighFinding()
    {
        var finding = Assert.Single(new ImplicitConversionRule(new ExecutionPlanParser()).Evaluate(CreateContext()));

        Assert.Equal("SQLR-004", finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Contains("smallint", finding.SuggestedFix ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static SqlAnalysisContext CreateContext()
    {
        var xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Plans", "implicit-conversion.sqlplan"));
        const string sql = "(@Id smallint)SELECT * FROM dbo.Users WHERE Id = @Id";
        var statement = SqlAnalyzer.ParseSql(sql)
            .First(fragment => fragment is not Microsoft.SqlServer.TransactSql.ScriptDom.DeclareVariableStatement);
        return new SqlAnalysisContext(sql, statement, xml);
    }
}
