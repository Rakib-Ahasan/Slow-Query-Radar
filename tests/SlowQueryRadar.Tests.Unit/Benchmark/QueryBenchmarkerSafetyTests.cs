//tests/SlowQueryRadar.Tests.Unit/Benchmark/QueryBenchmarkerSafetyTests.cs
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Benchmark;
using Xunit;

namespace SlowQueryRadar.Tests.Unit.Benchmark;

public sealed class QueryBenchmarkerSafetyTests
{
    [Theory]
    [InlineData("INSERT INTO T VALUES(1)")]
    [InlineData("SELECT * INTO #t FROM T")]
    [InlineData("EXEC sp_help")]
    [InlineData("SELECT * FROM T; SELECT * FROM U")]
    public void UnsafeSqlThrows(string sql)
    {
        Assert.Throws<UnsafeQueryException>(() => QueryBenchmarker.ValidateSql(sql));
    }

    [Fact]
    public void SingleSelectPassesSafetyCheck()
    {
        var exception = Record.Exception(() => QueryBenchmarker.ValidateSql("SELECT 1"));

        Assert.Null(exception);
    }
}
