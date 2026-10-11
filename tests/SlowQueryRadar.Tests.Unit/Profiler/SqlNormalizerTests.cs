//tests/SlowQueryRadar.Tests.Unit/Profiler/SqlNormalizerTests.cs
using SlowQueryRadar.Profiler;
using Xunit;

namespace SlowQueryRadar.Tests.Unit.Profiler;

public sealed class SqlNormalizerTests
{
    [Fact]
    public void EquivalentQueriesWithDifferentCaseAndWhitespaceProduceTheSameHash()
    {
        var first = SqlNormalizer.ComputeHashFromSql("SELECT * FROM T WHERE Id = 5");
        var second = SqlNormalizer.ComputeHashFromSql("select * from t where id=5");

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void StringLiteralIsReplacedWithQuestionMark()
    {
        var normalized = SqlNormalizer.Normalize("SELECT * FROM T WHERE Name = 'abc'");

        Assert.Equal("select * from t where name = ?", normalized);
    }
}
