//src/SlowQueryRadar.Suggestions/Ai/ClaudeExplainer.cs
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Suggestions.Ai;

/// <summary>Uses the configured Claude endpoint for optional SQL explanations.</summary>
public sealed class ClaudeExplainer : IAiExplainer
{
    private const string Prompt = "Explain in Bangla (বাংলা) why this query is slow and what the developer should do. Under 150 words. Do not invent column names.";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SqlLiteralStripper _literalStripper;
    private readonly AiOptions _options;

    /// <summary>Initializes the Claude explainer.</summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="literalStripper">The SQL literal stripper.</param>
    /// <param name="options">AI configuration.</param>
    public ClaudeExplainer(
        IHttpClientFactory httpClientFactory,
        SqlLiteralStripper literalStripper,
        IOptions<AiOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _literalStripper = literalStripper;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<string?> ExplainAsync(
        string sqlText,
        IReadOnlyList<Finding> findings,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sqlText);
        ArgumentNullException.ThrowIfNull(findings);
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Endpoint)
            || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(ClaudeExplainer));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds > 0 ? _options.TimeoutSeconds : 10));
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
            request.Headers.Add("x-api-key", _options.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                model = _options.Model,
                max_tokens = 300,
                system = Prompt,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new
                        {
                            sql = _literalStripper.Strip(sqlText),
                            findings = findings.Select(finding => new
                            {
                                ruleId = finding.RuleId,
                                message = finding.Message
                            })
                        }
                    }
                }
            };
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: timeout.Token).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.Array
                || content.GetArrayLength() == 0)
            {
                return null;
            }

            var first = content[0];
            var explanation = first.TryGetProperty("text", out var text) ? text.GetString() : null;
            return string.IsNullOrWhiteSpace(explanation) ? null : $"AI-generated, verify: {explanation}";
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
