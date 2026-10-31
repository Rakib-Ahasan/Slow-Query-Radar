//src/SlowQueryRadar.Suggestions/Ai/AiOptions.cs
namespace SlowQueryRadar.Suggestions.Ai;

/// <summary>Configures the optional AI suggestion explainer.</summary>
public sealed class AiOptions
{
    /// <summary>Gets or sets whether AI explanations are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the Claude endpoint.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Gets or sets the Claude API key.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the Claude model.</summary>
    public string Model { get; set; } = "claude-sonnet-4-5";

    /// <summary>Gets or sets the request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Gets the normalized timeout used for AI requests.</summary>
    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds > 0 ? TimeoutSeconds : 10);
}
