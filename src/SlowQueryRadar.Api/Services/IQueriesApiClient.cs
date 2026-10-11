//src/SlowQueryRadar.Api/Services/IQueriesApiClient.cs
using SlowQueryRadar.Api.Contracts;

namespace SlowQueryRadar.Api.Services;

/// <summary>Provides typed access to the Slow Query Radar API endpoints.</summary>
public interface IQueriesApiClient
{
    /// <summary>Gets the top slow queries.</summary>
    Task<IReadOnlyList<SlowQueryDto>> GetTopAsync(int top, int lookbackHours, CancellationToken ct);

    /// <summary>Gets a cached query snapshot.</summary>
    Task<SlowQueryDto?> GetAsync(long queryId, CancellationToken ct);

    /// <summary>Analyzes a query.</summary>
    Task<AnalyzeResponseDto?> AnalyzeAsync(AnalyzeRequestDto request, CancellationToken ct);

    /// <summary>Runs a benchmark for a cached query.</summary>
    Task<BenchmarkResultDto?> BenchmarkAsync(long queryId, BenchmarkRequestDto request, CancellationToken ct);
}

/// <summary>HTTP implementation of IQueriesApiClient.</summary>
public sealed class QueriesApiClient : IQueriesApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>Initializes the typed API client.</summary>
    public QueriesApiClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SlowQueryDto>> GetTopAsync(int top, int lookbackHours, CancellationToken ct) =>
        await _httpClient.GetFromJsonAsync<IReadOnlyList<SlowQueryDto>>(
            $"api/queries/top?top={top}&lookbackHours={lookbackHours}", ct).ConfigureAwait(false)
        ?? Array.Empty<SlowQueryDto>();

    /// <inheritdoc />
    public async Task<SlowQueryDto?> GetAsync(long queryId, CancellationToken ct) =>
        await GetOrDefaultAsync<SlowQueryDto>($"api/queries/{queryId}", ct).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<AnalyzeResponseDto?> AnalyzeAsync(AnalyzeRequestDto request, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/analyze", request, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AnalyzeResponseDto>(cancellationToken: ct).ConfigureAwait(false)
            : null;
    }

    /// <inheritdoc />
    public async Task<BenchmarkResultDto?> BenchmarkAsync(long queryId, BenchmarkRequestDto request, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync($"api/analyze/{queryId}/benchmark", request, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<BenchmarkResultDto>(cancellationToken: ct).ConfigureAwait(false)
            : null;
    }

    private async Task<T?> GetOrDefaultAsync<T>(string uri, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(uri, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false)
            : default;
    }
}
