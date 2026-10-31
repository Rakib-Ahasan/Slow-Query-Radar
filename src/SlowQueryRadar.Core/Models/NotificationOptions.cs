//src/SlowQueryRadar.Core/Models/NotificationOptions.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Configures outbound notifications.</summary>
public sealed class NotificationOptions
{
    /// <summary>Gets or sets whether notifications are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets Slack webhook URL.</summary>
    public string? SlackWebhookUrl { get; set; }

    /// <summary>Gets or sets Teams webhook URL.</summary>
    public string? TeamsWebhookUrl { get; set; }

    /// <summary>Gets or sets duplicate notification cooldown minutes.</summary>
    public int CooldownMinutes { get; set; } = 60;
}
