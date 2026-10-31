//tests/SlowQueryRadar.Tests.Unit/Collector/QueryStoreCollectorTests.cs
using System.Reflection;
using System.Text.RegularExpressions;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Tests.Unit.Collector;

public sealed class QueryStoreCollectorTests
{
    [Theory]
    [InlineData(RankBy.TotalDuration, "ORDER BY m.TotalDurationMs DESC")]
    [InlineData(RankBy.AvgDuration, "ORDER BY m.AvgDurationMs DESC")]
    [InlineData(RankBy.TotalCpu, "ORDER BY m.TotalCpuMs DESC")]
    [InlineData(RankBy.TotalLogicalReads, "ORDER BY m.TotalLogicalReads DESC")]
    [InlineData(RankBy.ExecutionCount, "ORDER BY m.TotalExecutions DESC")]
    public void BuildOrderBy_MapsEveryRankBy(RankBy rankBy, string expected)
    {
        var buildOrderBy = typeof(QueryStoreCollector).GetMethod("BuildOrderBy", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(buildOrderBy);
        var actual = Assert.IsType<string>(buildOrderBy!.Invoke(null, [rankBy]));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EmbeddedSqlResources_AreLoadable()
    {
        Assert.NotEmpty(ReadEmbeddedSql("TopSlowQueries.sql"));
        Assert.NotEmpty(ReadEmbeddedSql("PlanEvidence.sql"));
    }

    [Fact]
    public void TopSlowQueriesSql_ParametersMatchCollectorSuppliedNames()
    {
        var sql = ReadSourceSql("TopSlowQueries.sql");
        var parametersInSql = Regex.Matches(sql, @"@([A-Za-z_][A-Za-z0-9_]*)")
            .Select(match => match.Groups[1].Value)
            .Where(name => !string.Equals(name, "_msparam", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var required = new HashSet<string>(StringComparer.Ordinal)
        {
            "since",
            "windowEnd",
            "topN",
            "minExecutions",
            "minAvgDurationMs",
            "excludeNoise"
        };

        var suppliedByCollector = new HashSet<string>(StringComparer.Ordinal)
        {
            "since",
            "windowEnd",
            "topN",
            "minExecutions",
            "minAvgDurationMs",
            "excludeNoise"
        };

        Assert.True(required.All(parametersInSql.Contains),
            $"Missing SQL parameters: {string.Join(", ", required.Where(x => !parametersInSql.Contains(x)))}");
        Assert.Equal(required, suppliedByCollector);
    }

    private static string ReadEmbeddedSql(string fileName)
    {
        using var stream = typeof(QueryStoreCollector).Assembly.GetManifestResourceStream($"SlowQueryRadar.Collector.Sql.{fileName}");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    private static string ReadSourceSql(string fileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "SlowQueryRadar.slnx")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        var path = Path.Combine(current!.FullName, "src", "SlowQueryRadar.Collector", "Sql", fileName);
        Assert.True(File.Exists(path), $"SQL file was not found at '{path}'.");
        return File.ReadAllText(path);
    }
}
