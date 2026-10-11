//tests/SlowQueryRadar.Tests.Unit/Benchmark/QueryBenchmarkerParameterTests.cs
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Benchmark;
using Xunit;

namespace SlowQueryRadar.Tests.Unit.Benchmark;

public sealed class QueryBenchmarkerParameterTests
{
    [Fact]
    public void ValidateSql_QueryStoreParameterPrefix_IsAccepted()
    {
        var exception = Record.Exception(() =>
            QueryBenchmarker.ValidateSql("(@1 smallint)SELECT * FROM Orders WHERE CustomerId=@1"));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateSql_SelectInto_IsRejected()
    {
        Assert.Throws<UnsafeQueryException>(() =>
            QueryBenchmarker.ValidateSql("SELECT * INTO #t FROM Orders"));
    }

    [Fact]
    public void ValidateSql_Insert_IsRejected()
    {
        Assert.Throws<UnsafeQueryException>(() =>
            QueryBenchmarker.ValidateSql("INSERT INTO Orders VALUES (1)"));
    }

    [Fact]
    public void ValidateSql_MultipleStatements_AreRejected()
    {
        Assert.Throws<UnsafeQueryException>(() =>
            QueryBenchmarker.ValidateSql("SELECT 1; SELECT 2"));
    }
}
