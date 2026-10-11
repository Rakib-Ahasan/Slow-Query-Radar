//src/SlowQueryRadar.Core/Models/Suggestion.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Contains deterministic and optional AI-generated query guidance.</summary>
public sealed record Suggestion(string DeterministicFix, string? AiExplanation);
