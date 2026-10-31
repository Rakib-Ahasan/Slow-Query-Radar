//src/SlowQueryRadar.Collector/TargetProvider.cs
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Collector;

/// <summary>Resolves monitoring targets from configuration and optional discovery.</summary>
public sealed class TargetProvider : ITargetProvider
{
    private const string DiscoverySqlWithExclude = "SELECT name FROM sys.databases WHERE state_desc = 'ONLINE' AND is_query_store_on = 1 AND name LIKE @databaseNameLike AND name NOT IN @exclude ORDER BY name;";
    private const string DiscoverySqlNoExclude = "SELECT name FROM sys.databases WHERE state_desc = 'ONLINE' AND is_query_store_on = 1 AND name LIKE @databaseNameLike ORDER BY name;";

    private readonly MonitoringOptions _monitoring;
    private readonly DiscoveryOptions _discovery;
    private readonly QueryStoreOptions _legacyQueryStore;

    /// <summary>Initializes a target provider.</summary>
    public TargetProvider(
        IOptions<MonitoringOptions> monitoring,
        IOptions<DiscoveryOptions> discovery,
        IOptions<QueryStoreOptions> legacyQueryStore)
    {
        ArgumentNullException.ThrowIfNull(monitoring);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(legacyQueryStore);

        _monitoring = monitoring.Value;
        _discovery = discovery.Value;
        _legacyQueryStore = legacyQueryStore.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MonitoringTarget>> GetTargetsAsync(CancellationToken ct)
    {
        var targets = new List<MonitoringTarget>();

        foreach (var configured in _monitoring.Targets.Where(target => target.Enabled && !string.IsNullOrWhiteSpace(target.ConnectionString)))
        {
            targets.Add(new MonitoringTarget(
                string.IsNullOrWhiteSpace(configured.Name) ? "unnamed-target" : configured.Name,
                configured.Group,
                configured.ConnectionString,
                configured.Tags.ToArray(),
                configured.Enabled));
        }

        if (_discovery.Enabled && !string.IsNullOrWhiteSpace(_discovery.ServerConnectionString))
        {
            targets.AddRange(await DiscoverTargetsAsync(ct).ConfigureAwait(false));
        }

        if (targets.Count == 0 && !string.IsNullOrWhiteSpace(_legacyQueryStore.ConnectionString))
        {
            targets.Add(new MonitoringTarget(
                "default",
                null,
                _legacyQueryStore.ConnectionString,
                [],
                true));
        }

        return targets
            .Where(target => target.Enabled)
            .GroupBy(target => target.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private async Task<IReadOnlyList<MonitoringTarget>> DiscoverTargetsAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(_discovery.ServerConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        var exclude = (_discovery.Exclude ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var sql = exclude.Length > 0 ? DiscoverySqlWithExclude : DiscoverySqlNoExclude;
        var databaseNames = (await connection.QueryAsync<string>(
                new CommandDefinition(sql, new { databaseNameLike = _discovery.DatabaseNameLike, exclude }, cancellationToken: ct))
            .ConfigureAwait(false))
            .ToArray();

        return databaseNames.Select(databaseName =>
        {
            var builder = new SqlConnectionStringBuilder(_discovery.ServerConnectionString)
            {
                InitialCatalog = databaseName
            };

            return new MonitoringTarget(
                databaseName,
                "Discovered",
                builder.ConnectionString,
                [],
                true);
        }).ToArray();
    }
}
