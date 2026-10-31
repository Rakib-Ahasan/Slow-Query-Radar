// tests/SlowQueryRadar.Tests.Unit/Cli/ProgramConnectionMaskingTests.cs
using System.Reflection;

namespace SlowQueryRadar.Tests.Unit.Cli;

/// <summary>Tests CLI connection summary masking behavior.</summary>
public sealed class ProgramConnectionMaskingTests
{
    [Fact]
    public void DescribeConnection_MasksPasswordAndShowsServerAndDatabase()
    {
        var method = typeof(SlowQueryRadar.Cli.Program).GetMethod(
            "DescribeConnection",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var value = Assert.IsType<string>(method!.Invoke(null, ["Server=localhost;Database=RadarDb;User ID=sa;Password=superSecret;"]));

        Assert.Contains("Server=localhost", value, StringComparison.Ordinal);
        Assert.Contains("Database=RadarDb", value, StringComparison.Ordinal);
        Assert.Contains("Password=***", value, StringComparison.Ordinal);
        Assert.DoesNotContain("superSecret", value, StringComparison.Ordinal);
    }
}
