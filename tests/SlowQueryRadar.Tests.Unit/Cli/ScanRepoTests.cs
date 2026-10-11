//tests/SlowQueryRadar.Tests.Unit/Cli/ScanRepoTests.cs
using SlowQueryRadar.Cli.Analyzer;
using SlowQueryRadar.Cli.Analyzer.Symbols;

namespace SlowQueryRadar.Tests.Unit.Cli;

/// <summary>Tests repository SQL usage scanning.</summary>
public sealed class ScanRepoTests
{
    [Fact]
    public async Task DetectsSelectStarAndStringConcatenation()
    {
        var directory = CreateFixture("using System.Threading.Tasks; class Fixture { void Run(dynamic conn, string table) { conn.Query<Foo>(\"SELECT * FROM \" + table); } } class Foo { }");
        try
        {
            var findings = await new RepositoryScanner().ScanAsync(directory, CancellationToken.None);
            Assert.Contains(findings, finding => finding.RuleId == "SQL001" && finding.Severity == ScanSeverity.Error);
            Assert.Contains(findings, finding => finding.RuleId == "SQLR-006-ROSLYN" && finding.Severity == ScanSeverity.Warning);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AsyncParameterizedQueryHasNoFindings()
    {
        var directory = CreateFixture("using System.Threading.Tasks; class Fixture { async Task Run(dynamic conn) { await conn.QueryAsync<Foo>(\"SELECT Id FROM T\"); } } class Foo { }");
        try
        {
            var findings = await new RepositoryScanner().ScanAsync(directory, CancellationToken.None);
            Assert.Empty(findings);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateFixture(string source)
    {
        var directory = Path.Combine(Path.GetTempPath(), "slow-query-radar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Fixture.cs"), source);
        return directory;
    }
}
