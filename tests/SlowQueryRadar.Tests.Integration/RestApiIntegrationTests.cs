//tests/SlowQueryRadar.Tests.Integration/RestApiIntegrationTests.cs
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SlowQueryRadar.Api.Contracts;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Tests.Integration;

public sealed class RestApiIntegrationTests : IClassFixture<RestApiFactory>
{
    private readonly HttpClient _client;
    public RestApiIntegrationTests(RestApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task TargetsEndpointReturnsConfiguredTarget()
    {
        var response = await _client.GetAsync("/api/v1/targets");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var targets = await response.Content.ReadFromJsonAsync<TargetDto>();
        Assert.NotNull(targets);
    }

    [Fact]
    public async Task QueryEndpointBoundsSqlText()
    {
        var response = await _client.GetAsync("/api/v1/targets/tenant-a/queries?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(new string('x', 20_000), json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveHealthEndpointReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/v1/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class RestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["QueryStore:ConnectionString"] = "Server=localhost;Database=RadarDb;Integrated Security=true",
            ["Benchmark:ConnectionString"] = "Server=localhost;Database=RadarDb;Integrated Security=true",
            ["SnapshotStore:ConnectionString"] = ":memory:"
        }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ISnapshotStore, FakeSnapshotStore>();
            services.AddSingleton<ITargetProvider, FakeTargetProvider>();
        });
    }
}

internal sealed class FakeTargetProvider : ITargetProvider
{
    public Task<IReadOnlyList<MonitoringTarget>> GetTargetsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<MonitoringTarget>>([new("tenant-a", "test", "Server=localhost", [], true)]);
}

internal sealed class FakeSnapshotStore : ISnapshotStore
{
    private readonly SlowQuerySnapshot _snapshot = new() { TargetName = "tenant-a", SqlText = "SELECT " + new string('x', 20_000), NormalizedSqlHash = "hash", QueryHash = "hash", TotalDurationMs = 10, CapturedAtUtc = DateTimeOffset.UtcNow };
    public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<long> SaveRunAsync(DateTimeOffset startedUtc, TimeSpan duration, FleetCollectResult result, CancellationToken ct) => Task.FromResult(1L);
    public Task<IReadOnlyList<QuerySample>> ReadSamplesAsync(string? target, DateTimeOffset sinceUtc, DateTimeOffset untilUtc, CancellationToken ct) => Task.FromResult<IReadOnlyList<QuerySample>>([new(1, "tenant-a", "hash", "hash", _snapshot.SqlText, 1, 1, 10, 1, 0, 1, 1, 1, DateTimeOffset.UtcNow)]);
    public Task<IReadOnlyList<QuerySample>> ReadLatestSamplesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuerySample>>([]);
    public Task<IReadOnlyList<TargetRunRecord>> ReadTargetRunsAsync(DateTimeOffset sinceUtc, DateTimeOffset untilUtc, CancellationToken ct) => Task.FromResult<IReadOnlyList<TargetRunRecord>>([new(1, "tenant-a", "Ok", null, DateTimeOffset.UtcNow, 1)]);
    public Task<int> CleanupAsync(TimeSpan retention, CancellationToken ct) => Task.FromResult(0);
}
