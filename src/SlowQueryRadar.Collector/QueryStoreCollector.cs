// src/SlowQueryRadar.Collector/QueryStoreCollector.cs
using System.Data.Common;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Core;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Collector;

/// <summary>Configuration options for Query Store collection.</summary>
public sealed class QueryStoreOptions
{
    /// <summary>Gets or sets the SQL Server connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>Gets or sets the default number of queries.</summary>
    public int TopN { get; set; } = 20;
    /// <summary>Gets or sets the default lookback period in hours.</summary>
    public int LookbackHours { get; set; } = 24;
    /// <summary>Gets or sets the command timeout in seconds.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}

internal sealed class QueryStoreStateDto
{
    public string ActualStateDesc { get; set; } = string.Empty;
    public string? QueryCaptureModeDesc { get; set; }
    public string? WaitStatsCaptureModeDesc { get; set; }
}

internal sealed class CollectionDto
{
    public long QueryId { get; set; }
    public string QueryHash { get; set; } = string.Empty;
    public int? ObjectId { get; set; }
    public string? ObjectName { get; set; }
    public string SqlText { get; set; } = string.Empty;
    public int PlanCount { get; set; }
    public long TotalExecutions { get; set; }
    public double AvgDurationMs { get; set; }
    public double TotalDurationMs { get; set; }
    public double MinDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
    public double StandardDeviationMs { get; set; }
    public double TotalCpuMs { get; set; }
    public double AvgCpuMs { get; set; }
    public long TotalLogicalReads { get; set; }
    public double AvgLogicalReads { get; set; }
    public long TotalPhysicalReads { get; set; }
    public long TotalLogicalWrites { get; set; }
    public long TotalRows { get; set; }
    public DateTimeOffset? LastExecutionTime { get; set; }
    public DateTimeOffset? LastCompileStartTime { get; set; }
}

internal sealed class PlanDto
{
    public long QueryId { get; set; }
    public long PlanId { get; set; }
    public bool IsForcedPlan { get; set; }
    public DateTimeOffset? LastExecutionTime { get; set; }
    public long ExecutionCount { get; set; }
    public double AvgDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
    public double StandardDeviationMs { get; set; }
}

/// <summary>Collects read-only metrics from SQL Server Query Store.</summary>
public sealed class QueryStoreCollector : IQueryStoreCollector
{
    private const string StateSql = "SELECT /* SlowQueryRadar */ actual_state_desc AS ActualStateDesc, query_capture_mode_desc AS QueryCaptureModeDesc, wait_stats_capture_mode_desc AS WaitStatsCaptureModeDesc FROM sys.database_query_store_options;";
    private const string PlanXmlSql = "SELECT /* SlowQueryRadar */ TRY_CONVERT(xml, p.query_plan) FROM sys.query_store_plan AS p WHERE p.query_id = @queryId AND p.plan_id = @planId;";
    private const string QueryIdsFilterPlaceholder = "/*QUERY_IDS_FILTER*/";

    private readonly QueryStoreOptions _options;
    private readonly Func<DbConnection> _connectionFactory;
    private readonly ILogger<QueryStoreCollector> _logger;
    private static readonly ConcurrentDictionary<string, string> ResourceCache = new(StringComparer.Ordinal);

    /// <summary>Initializes a collector with configured SQL connections.</summary>
    public QueryStoreCollector(IOptions<QueryStoreOptions> options) : this(options, null, NullLogger<QueryStoreCollector>.Instance) { }
    /// <summary>Initializes a collector with a connection factory.</summary>
    public QueryStoreCollector(IOptions<QueryStoreOptions> options, Func<DbConnection>? connectionFactory) : this(options, connectionFactory, NullLogger<QueryStoreCollector>.Instance) { }
    /// <summary>Initializes a collector with a connection factory and logger.</summary>
    public QueryStoreCollector(IOptions<QueryStoreOptions> options, Func<DbConnection>? connectionFactory, ILogger<QueryStoreCollector> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value ?? throw new ArgumentException("Query Store options are required.", nameof(options));
        _connectionFactory = connectionFactory ?? (() => new SqlConnection(_options.ConnectionString));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CollectResult> CollectAsync(CollectOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var end = DateTimeOffset.UtcNow;
        var start = end.Subtract(options.Lookback);
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var commandTimeout = _options.CommandTimeoutSeconds is > 0 and <= 30 ? _options.CommandTimeoutSeconds : 30;
        var state = await connection.QueryFirstOrDefaultAsync<QueryStoreStateDto>(new CommandDefinition(StateSql, commandTimeout: commandTimeout, cancellationToken: ct)).ConfigureAwait(false)
            ?? throw new QueryStoreDisabledException("Query Store state could not be read. Ask a DBA to verify Query Store permissions.");
        if (string.Equals(state.ActualStateDesc, "OFF", StringComparison.OrdinalIgnoreCase))
            throw new QueryStoreDisabledException($"Query Store is not enabled. Ask a DBA to run: ALTER DATABASE {connection.Database} SET QUERY_STORE = ON;");

        var warnings = new List<string>();
        if (string.Equals(state.ActualStateDesc, "READ_ONLY", StringComparison.OrdinalIgnoreCase)) warnings.Add("Query Store is READ_ONLY; new runtime data may not be captured.");
        if (string.Equals(state.QueryCaptureModeDesc, "NONE", StringComparison.OrdinalIgnoreCase)) warnings.Add("Query Store query capture mode is NONE.");
        var sql = LoadResource("TopSlowQueries.sql").Replace("/*ORDER_BY*/", BuildOrderBy(options.RankBy));
        var rows = (await connection.QueryAsync<CollectionDto>(new CommandDefinition(sql, new
        {
            since = start.UtcDateTime, windowEnd = end.UtcDateTime, topN = options.Top,
            minExecutions = options.MinExecutions, minAvgDurationMs = options.MinAvgDurationMs,
            excludeNoise = options.ExcludeNoise ? 1 : 0
        }, commandTimeout: commandTimeout, cancellationToken: ct)).ConfigureAwait(false)).AsList();
        var plans = await LoadPlansAsync(connection, rows.Select(x => x.QueryId), start, end, ct).ConfigureAwait(false);
        var captured = DateTimeOffset.UtcNow;
        var snapshots = rows.Select(row => Map(row, plans.Where(plan => plan.QueryId == row.QueryId).ToArray(), captured)).ToArray();
        _logger.LogInformation("Collected {Count} weighted queries with rank {RankBy} and lookback {Lookback}", snapshots.Length, options.RankBy, options.Lookback);
        return new CollectResult(snapshots, warnings, start, end, state.ActualStateDesc, captured);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SlowQuerySnapshot>> CollectTopSlowQueriesAsync(int topN, TimeSpan lookback, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topN);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lookback.TotalSeconds);
        var result = await CollectAsync(new CollectOptions
        {
            Top = topN,
            Lookback = lookback,
            MinExecutions = 1
        }, ct).ConfigureAwait(false);
        return result.Queries;
    }

    /// <inheritdoc />
    public async Task<string?> GetPlanXmlAsync(long queryId, long planId, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(queryId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(planId);
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(PlanXmlSql, new { queryId, planId }, commandTimeout: 30, cancellationToken: ct)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SlowQuerySnapshot?> GetByQueryIdAsync(long queryId, CancellationToken ct)
    {
        var result = await CollectAsync(new CollectOptions { Top = 500, MinExecutions = 1 }, ct).ConfigureAwait(false);
        return result.Queries.FirstOrDefault(query => query.QueryId == queryId);
    }

    private async Task<IReadOnlyList<PlanDto>> LoadPlansAsync(DbConnection connection, IEnumerable<long> ids, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var queryIds = ids.Distinct().ToArray();
        if (queryIds.Length == 0) return Array.Empty<PlanDto>();
        var sql = LoadResource("PlanEvidence.sql").Replace("/*QUERY_IDS_FILTER*/", "OR p.query_id IN @queryIds", StringComparison.Ordinal);
        var rows = await connection.QueryAsync<PlanDto>(new CommandDefinition(sql, new { queryIds, since = start.UtcDateTime, windowEnd = end.UtcDateTime }, commandTimeout: 30, cancellationToken: ct)).ConfigureAwait(false);
        return rows.AsList();
    }

    private static SlowQuerySnapshot Map(CollectionDto row, IReadOnlyList<PlanDto> plans, DateTimeOffset captured) => new()
    {
        QueryId = row.QueryId, QueryHash = row.QueryHash, ObjectId = row.ObjectId, ObjectName = row.ObjectName, SqlText = row.SqlText,
        NormalizedSqlHash = ComputeNormalizedSqlHash(row.SqlText), TotalExecutions = row.TotalExecutions, AvgDurationMs = row.AvgDurationMs,
        TotalDurationMs = row.TotalDurationMs, MinDurationMs = row.MinDurationMs, MaxDurationMs = row.MaxDurationMs, StandardDeviationMs = row.StandardDeviationMs,
        TotalCpuMs = row.TotalCpuMs, AvgCpuMs = row.AvgCpuMs, TotalLogicalReads = row.TotalLogicalReads, AvgLogicalReads = row.AvgLogicalReads,
        TotalPhysicalReads = row.TotalPhysicalReads, TotalLogicalWrites = row.TotalLogicalWrites, TotalRows = row.TotalRows, PlanCount = row.PlanCount,
        LastExecutionTime = row.LastExecutionTime, LastCompileStartTime = row.LastCompileStartTime,
        Plans = plans.Select(plan => new PlanSummary(plan.PlanId, plan.IsForcedPlan, plan.LastExecutionTime, plan.ExecutionCount, plan.AvgDurationMs, plan.MaxDurationMs, plan.StandardDeviationMs, null)).ToArray(),
        CapturedAtUtc = captured,
        FeatureName = RadarTagParser.Parse(row.SqlText).FeatureName,
        TenantId = RadarTagParser.Parse(row.SqlText).TenantId,
        Caller = RadarTagParser.Parse(row.SqlText).Caller
    };

    private static string BuildOrderBy(RankBy rankBy) => rankBy switch
    {
        RankBy.TotalDuration => "ORDER BY m.TotalDurationMs DESC",
        RankBy.AvgDuration => "ORDER BY m.AvgDurationMs DESC",
        RankBy.TotalCpu => "ORDER BY m.TotalCpuMs DESC",
        RankBy.TotalLogicalReads => "ORDER BY m.TotalLogicalReads DESC",
        RankBy.ExecutionCount => "ORDER BY m.TotalExecutions DESC",
        _ => throw new ArgumentOutOfRangeException(nameof(rankBy))
    };

    private static string LoadResource(string name)
    {
        return ResourceCache.GetOrAdd(name, static resourceName =>
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"SlowQueryRadar.Collector.Sql.{resourceName}")
                ?? throw new InvalidOperationException($"Embedded SQL resource '{resourceName}' was not found.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }

    private static string ComputeNormalizedSqlHash(string sqlText)
    {
        var withoutComments = Regex.Replace(sqlText, @"(--[^\r\n]*|/\*.*?\*/)", " ", RegexOptions.Singleline);
        var withoutLiterals = Regex.Replace(withoutComments, @"'(?:''|[^'])*'", " ");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Regex.Replace(withoutLiterals.ToLowerInvariant(), @"\s+", " ").Trim())));
    }
}
