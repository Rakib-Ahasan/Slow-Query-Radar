//tests/SlowQueryRadar.Tests.Unit/Suggestions/SqlLiteralStripperTests.cs
using SlowQueryRadar.Suggestions.Ai;
using Xunit;

namespace SlowQueryRadar.Tests.Unit.Suggestions;

public sealed class SqlLiteralStripperTests
{
    private readonly SqlLiteralStripper _stripper = new();

    [Fact]
    public void StripsSingleQuotedLiteral()
    {
        var result = _stripper.Strip("SELECT * FROM T WHERE Name = 'John'");

        Assert.Equal("SELECT * FROM T WHERE Name = ?", result);
    }

    [Fact]
    public void StripsEscapedSingleAndDoubleQuotedLiterals()
    {
        var result = _stripper.Strip("'It''s' and \"double\" quotes");

        Assert.Equal("? and ? quotes", result);
    }

    [Fact]
    public void StripsNumericLiteral()
    {
        var result = _stripper.Strip("WHERE Id = 42");

        Assert.Equal("WHERE Id = ?", result);
    }
}
