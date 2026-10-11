//tests/SlowQueryRadar.Tests.Unit/Core/QueryStoreSqlNormalizerTests.cs
using SlowQueryRadar.Core.Sql;
using Xunit;

namespace SlowQueryRadar.Tests.Unit.Core;

public sealed class QueryStoreSqlNormalizerTests
{
    [Fact]
    public void Normalize_ExtractsSingleParameterPrefix()
    {
        var normalized = QueryStoreSqlNormalizer.Normalize("(@1 smallint)SELECT * FROM Orders WHERE CustomerId=@1");

        Assert.Equal("SELECT * FROM Orders WHERE CustomerId=@1", normalized.Sql);
        var parameter = Assert.Single(normalized.Parameters);
        Assert.Equal("@1", parameter.Name);
        Assert.Equal("smallint", parameter.Type);
    }

    [Fact]
    public void Normalize_WithoutPrefixReturnsEmptyParameters()
    {
        var normalized = QueryStoreSqlNormalizer.Normalize("SELECT 1");

        Assert.Equal("SELECT 1", normalized.Sql);
        Assert.Empty(normalized.Parameters);
    }

    [Fact]
    public void Normalize_ExtractsMultipleParenthesizedTypes()
    {
        var normalized = QueryStoreSqlNormalizer.Normalize("(@a int,@b nvarchar(100))SELECT @a, @b");

        Assert.Equal("SELECT @a, @b", normalized.Sql);
        Assert.Collection(
            normalized.Parameters,
            first =>
            {
                Assert.Equal("@a", first.Name);
                Assert.Equal("int", first.Type);
            },
            second =>
            {
                Assert.Equal("@b", second.Name);
                Assert.Equal("nvarchar(100)", second.Type);
            });
    }
}
