//src/SlowQueryRadar.Api/Services/WebhookNotifier.cs
using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Api.Services;

/// <summary>Sends regression notifications to Slack/Teams webhooks.</summary>
public sealed class WebhookNotifier : INotifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotificationOptions _options;
    private readonly ILogger<WebhookNotifier> _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSent = new(StringComparer.Ordinal);

    /// <summary>Initializes a notifier.</summary>
    public WebhookNotifier(
        IHttpClientFactory httpClientFactory,
        IOptions<NotificationOptions> options,
        ILogger<WebhookNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _options = options.Value ?? new NotificationOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task NotifyAsync(IReadOnlyList<Regression> regressions, CancellationToken ct)
    {
        if (!_options.Enabled || regressions.Count == 0)
        {
            return;
        }

        var cooldown = TimeSpan.FromMinutes(_options.CooldownMinutes > 0 ? _options.CooldownMinutes : 60);
        var now = DateTimeOffset.UtcNow;
        var toNotify = regressions.Where(regression => ShouldSend(regression, now, cooldown)).ToArray();
        if (toNotify.Length == 0)
        {
            return;
        }

        var message = BuildMessage(toNotify);
        if (!string.IsNullOrWhiteSpace(_options.SlackWebhookUrl))
        {
            await PostAsync(_options.SlackWebhookUrl, new { text = message }, ct).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(_options.TeamsWebhookUrl))
        {
            await PostAsync(_options.TeamsWebhookUrl, new { text = message }, ct).ConfigureAwait(false);
        }
    }

    private async Task PostAsync(string url, object payload, CancellationToken ct)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(WebhookNotifier));
            using var response = await client.PostAsJsonAsync(url, payload, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Webhook notification failed with status {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Webhook notification failed.");
        }
    }

    private bool ShouldSend(Regression regression, DateTimeOffset now, TimeSpan cooldown)
    {
        var key = $"{regression.Target}|{regression.NormalizedHash}|{regression.Severity}";
        if (_lastSent.TryGetValue(key, out var previous) && now - previous < cooldown)
        {
            return false;
        }

        _lastSent[key] = now;
        return true;
    }

    private static string BuildMessage(IReadOnlyList<Regression> regressions)
    {
        var lines = new List<string> { $"SlowQueryRadar regressions detected: {regressions.Count}" };
        foreach (var regression in regressions.Take(10))
        {
            lines.Add($"- [{regression.Severity}] {regression.Target} {regression.NormalizedHash} avg {regression.LatestAvgMs:F1}ms baseline {regression.BaselineMedianMs:F1}ms");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
