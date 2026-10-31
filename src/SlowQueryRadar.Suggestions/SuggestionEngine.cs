//src/SlowQueryRadar.Suggestions/SuggestionEngine.cs
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;
using SlowQueryRadar.Suggestions.Ai;
using SlowQueryRadar.Suggestions.Deterministic;

namespace SlowQueryRadar.Suggestions;

/// <summary>Builds deterministic suggestions and optional AI explanations.</summary>
public sealed class SuggestionEngine : ISuggestionEngine
{
    private const string RuleSetVersion = "phase4-v1";
    private readonly DapperSnippetGenerator _dapperSnippetGenerator;
    private readonly IMemoryCache _cache;
    private readonly AiOptions _aiOptions;
    private readonly IAiExplainer? _aiExplainer;
    private readonly DeterministicSuggester _deterministicSuggester;

    /// <summary>Initializes the suggestion engine.</summary>
    /// <param name="dapperSnippetGenerator">The Dapper snippet generator.</param>
    /// <param name="cache">The memory cache.</param>
    /// <param name="aiOptions">AI configuration.</param>
    /// <param name="aiExplainer">Optional AI explainer.</param>
    /// <param name="deterministicSuggester">Optional deterministic action suggester.</param>
    public SuggestionEngine(
        DapperSnippetGenerator dapperSnippetGenerator,
        IMemoryCache cache,
        IOptions<AiOptions> aiOptions,
        IAiExplainer? aiExplainer = null,
        DeterministicSuggester? deterministicSuggester = null)
    {
        _dapperSnippetGenerator = dapperSnippetGenerator;
        _cache = cache;
        _aiOptions = aiOptions.Value;
        _aiExplainer = aiExplainer;
        _deterministicSuggester = deterministicSuggester ?? new DeterministicSuggester();
    }

    /// <inheritdoc />
    public async Task<Suggestion> BuildSuggestionAsync(
        AnalysisResult result,
        string sqlText,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(sqlText);
        ct.ThrowIfCancellationRequested();

        var deterministicFix = string.Join(
            Environment.NewLine + Environment.NewLine,
            result.Findings.Select(finding =>
                finding.SuggestedFix ?? _dapperSnippetGenerator.Generate(sqlText, finding)));
        if (string.IsNullOrWhiteSpace(deterministicFix))
        {
            deterministicFix = "No deterministic fix was generated.";
        }

        if (!_aiOptions.Enabled || _aiExplainer is null || result.Findings.Count == 0)
        {
            return new Suggestion(deterministicFix, null);
        }

        var cacheKey = $"suggestion-ai:{ComputeSqlHash(sqlText)}:{RuleSetVersion}";
        if (_cache.TryGetValue(cacheKey, out string? cachedExplanation))
        {
            return new Suggestion(deterministicFix, cachedExplanation);
        }

        string? explanation = null;
        try
        {
            explanation = await _aiExplainer.ExplainAsync(sqlText, result.Findings, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            explanation = null;
        }
        catch (HttpRequestException)
        {
            explanation = null;
        }
        catch (TimeoutException)
        {
            explanation = null;
        }

        if (!string.IsNullOrWhiteSpace(explanation))
        {
            _cache.Set(cacheKey, explanation, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });
        }

        return new Suggestion(deterministicFix, explanation);
    }

    private static string ComputeSqlHash(string sqlText) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sqlText)));
}
