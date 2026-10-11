//src/SlowQueryRadar.Core/Abstractions/ISuggestionEngine.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Core.Abstractions;

/// <summary>Builds deterministic and optional AI query suggestions.</summary>
public interface ISuggestionEngine
{
    /// <summary>Builds a suggestion for an analysis result.</summary>
    /// <param name="result">The analysis result.</param>
    /// <param name="sqlText">The SQL text being analyzed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A deterministic suggestion and optional AI explanation.</returns>
    Task<Suggestion> BuildSuggestionAsync(
        AnalysisResult result,
        string sqlText,
        CancellationToken ct);
}
