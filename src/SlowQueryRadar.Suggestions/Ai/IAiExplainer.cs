//src/SlowQueryRadar.Suggestions/Ai/IAiExplainer.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Suggestions.Ai;

/// <summary>Generates an optional natural-language explanation for findings.</summary>
public interface IAiExplainer
{
    /// <summary>Explains findings without sending literal values or row data.</summary>
    /// <param name="sqlText">SQL text to sanitize before transmission.</param>
    /// <param name="findings">The findings to explain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An explanation, or null when AI is disabled or unavailable.</returns>
    Task<string?> ExplainAsync(
        string sqlText,
        IReadOnlyList<Finding> findings,
        CancellationToken ct);
}
