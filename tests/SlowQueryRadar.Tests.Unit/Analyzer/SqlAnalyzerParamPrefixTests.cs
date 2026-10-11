//tests/SlowQueryRadar.Tests.Unit/Analyzer/SqlAnalyzerParamPrefixTests.cs
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SlowQueryRadar.Analyzer;

namespace SlowQueryRadar.Tests.Unit.Analyzer;

/// <summary>Tests parsing of Query Store parameter declaration prefixes.</summary>
public sealed class SqlAnalyzerParamPrefixTests
{
    [Fact]
    public void ParsesSingleParameterPrefix()
    {
        var statements = SqlAnalyzer.ParseSql("(@1 smallint)SELECT * FROM [Orders] WHERE [CustomerId]=@1");

        Assert.IsType<DeclareVariableStatement>(statements[0]);
        Assert.IsType<SelectStatement>(statements[1]);
    }

    [Fact]
    public void ParsesMultipleParametersWithParenthesizedType()
    {
        var statements = SqlAnalyzer.ParseSql("(@_msparam_0 nvarchar(4000),@_msparam_1 nvarchar(4000))SELECT @_msparam_0");

        Assert.Equal(3, statements.Count);
        Assert.IsType<DeclareVariableStatement>(statements[0]);
        Assert.IsType<DeclareVariableStatement>(statements[1]);
        Assert.IsType<SelectStatement>(statements[2]);
    }

    [Fact]
    public void ParsesSqlWithoutPrefixAsIs()
    {
        var statements = SqlAnalyzer.ParseSql("SELECT 1");

        Assert.IsType<SelectStatement>(Assert.Single(statements));
    }

    [Fact]
    public void DoesNotDoubleDeclareExistingDeclareBlock()
    {
        var statements = SqlAnalyzer.ParseSql("DECLARE @x int; SELECT @x");

        Assert.Equal(2, statements.Count);
        Assert.IsType<DeclareVariableStatement>(statements[0]);
        Assert.IsType<SelectStatement>(statements[1]);
    }
}
