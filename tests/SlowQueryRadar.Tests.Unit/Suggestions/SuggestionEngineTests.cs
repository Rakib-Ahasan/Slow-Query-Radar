//tests/SlowQueryRadar.Tests.Unit/Suggestions/SuggestionEngineTests.cs
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Core.Models;
using SlowQueryRadar.Suggestions;
using SlowQueryRadar.Suggestions.Ai;
using SlowQueryRadar.Suggestions.Deterministic;
using Xunit;

namespace SlowQueryRadar.Tests.Unit.Suggestions;

public sealed class SuggestionEngineTests
{
    [Fact]
    public async Task AiDisabledReturnsOnlyDeterministicFix()
    {
        var engine = CreateEngine(new AiOptions { Enabled = false });
        var result = await engine.BuildSuggestionAsync(CreateResult("fix this"), "SELECT * FROM T", CancellationToken.None);

        Assert.Equal("fix this", result.DeterministicFix);
        Assert.Null(result.AiExplanation);
    }

    [Fact]
    public async Task AiFailureStillReturnsDeterministicFix()
    {
        var engine = CreateEngine(
            new AiOptions { Enabled = true },
            new ThrowingExplainer());
        var result = await engine.BuildSuggestionAsync(CreateResult("keep this fix"), "SELECT * FROM T", CancellationToken.None);

        Assert.Equal("keep this fix", result.DeterministicFix);
        Assert.Null(result.AiExplanation);
    }

    private static SuggestionEngine CreateEngine(AiOptions options, IAiExplainer? explainer = null) =>
        new(
            new DapperSnippetGenerator(),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(options),
            explainer);

    private static AnalysisResult CreateResult(string fix) =>
        new(
            1,
            [new Finding("SQLR-001", Severity.High, "Finding", null, fix, 50)],
            DateTime.UtcNow);

    private sealed class ThrowingExplainer : IAiExplainer
    {
        public Task<string?> ExplainAsync(string sqlText, IReadOnlyList<Finding> findings, CancellationToken ct) =>
            throw new HttpRequestException("simulated HTTP failure");
    }
}
