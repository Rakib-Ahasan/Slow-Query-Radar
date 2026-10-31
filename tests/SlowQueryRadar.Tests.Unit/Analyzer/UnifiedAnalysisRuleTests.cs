//tests/SlowQueryRadar.Tests.Unit/Analyzer/UnifiedAnalysisRuleTests.cs
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Analyzer.Plan;
using SlowQueryRadar.Analyzer.Rules;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Tests.Unit.Analyzer;

public sealed class UnifiedAnalysisRuleTests
{
    [Fact]
    public void LargeScanRuleReportsNodeAndEstimatedRows()
    {
        var finding = Assert.Single(new LargeScanAnalysisRule(new ExecutionPlanParser()).Analyze(CreateContext("SELECT * FROM dbo.Users WHERE TenantId = 1", "table-scan.sqlplan")));

        Assert.Equal("PLAN-001", finding.RuleId);
        Assert.Contains("Node 0", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("15,000", finding.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyLookupRuleReportsExecutionsAndRows()
    {
        var finding = Assert.Single(new KeyLookupAnalysisRule(new ExecutionPlanParser()).Analyze(CreateContext("SELECT Name FROM dbo.Users WHERE Id = 5", "key-lookup.sqlplan")));

        Assert.Equal("PLAN-002", finding.RuleId);
        Assert.Contains("executions 1,000", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("total rows 25,000", finding.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void ImplicitConversionRuleReportsPlanWarning()
    {
        var finding = Assert.Single(new ImplicitConversionAnalysisRule(new ExecutionPlanParser()).Analyze(CreateContext("SELECT * FROM dbo.Users WHERE Id = @Id", "implicit-conversion.sqlplan")));

        Assert.Equal("PLAN-003", finding.RuleId);
        Assert.Contains("CONVERT_IMPLICIT", finding.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedPlanProducesAnalysisFailedFinding()
    {
        var finding = Assert.Single(new LargeScanAnalysisRule(new ExecutionPlanParser()).Analyze(CreateContextWithPlan("SELECT 1", "<not-a-plan>")));

        Assert.Equal("AnalysisFailed", finding.RuleId);
        Assert.Equal(Severity.Low, finding.Severity);
    }

    [Fact]
    public void SqlRulesReportConcreteSyntaxEvidence()
    {
        var context = CreateContextWithPlan("SELECT * FROM dbo.Users WHERE YEAR(CreatedAt) = 2025 AND Name LIKE '%bob'", null);

        var selectStar = Assert.Single(new SelectStarAnalysisRule().Analyze(context));
        var nonSargable = new NonSargableAnalysisRule().Analyze(context).ToArray();

        Assert.Equal("SQL-001", selectStar.RuleId);
        Assert.Contains("SelectStarExpression", selectStar.Evidence, StringComparison.Ordinal);
        Assert.Equal(2, nonSargable.Length);
        Assert.All(nonSargable, finding => Assert.Equal("SQL-002", finding.RuleId));
        Assert.Contains(nonSargable, finding => finding.Evidence?.Contains("YEAR", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void QueryStoreRulesReportMetricEvidence()
    {
        var snapshot = new SlowQuerySnapshot
        {
            SqlText = "SELECT 1",
            AvgDurationMs = 20,
            StandardDeviationMs = 50,
            PlanCount = 2,
            TotalExecutions = 10_000,
            TotalPhysicalReads = 1_000_000
        };
        var context = new AnalysisContext(snapshot, SqlAnalyzer.ParseSql(snapshot.SqlText)[0]);

        var sniffing = Assert.Single(new ParameterSniffingAnalysisRule().Analyze(context));
        var cuts = Assert.Single(new DeathByThousandCutsAnalysisRule().Analyze(context));
        var reads = Assert.Single(new PhysicalReadPressureAnalysisRule().Analyze(context));

        Assert.Contains("plan count 2", sniffing.Evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("10,000", cuts.Evidence, StringComparison.Ordinal);
        Assert.Contains("1,000,000", reads.Evidence, StringComparison.Ordinal);
    }

    private static AnalysisContext CreateContext(string sql, string fixture)
    {
        var xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Plans", fixture));
        return CreateContextWithPlan(sql, xml);
    }

    private static AnalysisContext CreateContextWithPlan(string sql, string? plan)
    {
        var snapshot = new SlowQuerySnapshot { SqlText = sql, ExecutionPlanXml = plan };
        var statement = SqlAnalyzer.ParseSql(sql).First(statement => statement is not DeclareVariableStatement);
        return new AnalysisContext(snapshot, statement);
    }
}
