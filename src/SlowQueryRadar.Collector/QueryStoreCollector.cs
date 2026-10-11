//src/SlowQueryRadar.Collector/QueryStoreCollector.cs
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Serilog;
using SlowQueryRadar.Core;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Collector;

/// <summary>Configuration options for Query Store collection.</summary>
public sealed class QueryStoreOptions
{
    /// <summary>Gets or sets the SQL Server connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets the default number of queries to collect.</summary>
    public int TopN { get; set; } = 20;

    /// <summary>Gets or sets the default lookback period in hours.</summary>
    public int LookbackHours { get; set; } = 24;
}

internal sealed class SlowQuerySnapshotDto
{
    public long QueryId { get; set; }
    public string SqlText { get; set; } = string.Empty;
    public long ExecCount { get; set; }
    public double AvgDurationUs { get; set; }
    public double TotalCpuUs { get; set; }
    public long TotalLogicalReads { get; set; }
    public double ImpactScore { get; set; }
    public string? ExecutionPlanXml { get; set; }
    public DateTime LastExecutionUtc { get; set; }
}

/// <summary>Collects slow queries from SQL Server Query Store.</summary>
public sealed class QueryStoreCollector : IQueryStoreCollector
{
    private const string QueryStoreStateSql = "SELECT actual_state_desc FROM sys.database_query_store_options WHERE database_id = DB_ID();";

    private const string CollectionSql = """
        SELECT TOP (@topN)
            q.query_id AS QueryId,
            qt.text AS SqlText,
            SUM(rs.count_executions) AS ExecCount,
            SUM(rs.avg_duration * rs.count_executions) / NULLIF(SUM(rs.count_executions), 0) AS AvgDurationUs,
            SUM(rs.avg_cpu_time * rs.count_executions) AS TotalCpuUs,
            SUM(rs.total_logical_reads) AS TotalLogicalReads,
            SUM(rs.avg_cpu_time * rs.count_executions) AS ImpactScore,
            plan.ExecutionPlanXml,
            MAX(rsi.start_time) AS LastExecutionUtc
        FROM sys.query_store_query AS q
        INNER JOIN sys.query_store_query_text AS qt ON q.query_text_id = qt.query_text_id
        INNER JOIN sys.query_store_plan AS p ON q.query_id = p.query_id
        INNER JOIN sys.query_store_runtime_stats AS rs ON p.plan_id = rs.plan_id
        INNER JOIN sys.query_store_runtime_stats_interval AS rsi
            ON rs.runtime_stats_interval_id = rsi.runtime_stats_interval_id
        OUTER APPLY
        (
            SELECT TOP (1) TRY_CAST(p2.query_plan AS xml) AS ExecutionPlanXml
            FROM sys.query_store_plan AS p2
            WHERE p2.query_id = q.query_id
            ORDER BY p2.plan_id DESC
        ) AS plan
        WHERE rsi.start_time > @since
        GROUP BY q.query_id, qt.text, plan.ExecutionPlanXml
        ORDER BY ImpactScore DESC;
        """;

    private readonly QueryStoreOptions _options;
    private readonly Func<DbConnection> _connectionFactory;
    private readonly ILogger _logger = Log.ForContext<QueryStoreCollector>();

    /// <summary>Initializes a collector that creates SQL Server connections.</summary>
    /// <param name="options">Query Store configuration options.</param>
    public QueryStoreCollector(IOptions<QueryStoreOptions> options)
        : this(options, null)
    {
    }

    /// <summary>Initializes a collector with a connection factory.</summary>
    /// <param name="options">Query Store configuration options.</param>
    /// <param name="connectionFactory">Factory used to create an openable database connection.</param>
    public QueryStoreCollector(IOptions<QueryStoreOptions> options, Func<DbConnection>? connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentException("Query Store options are required.", nameof(options));
        _connectionFactory = connectionFactory ?? (() => new SqlConnection(_options.ConnectionString));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SlowQuerySnapshot>> CollectTopSlowQueriesAsync(
        int topN, TimeSpan lookback, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topN);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lookback.TotalSeconds);

        var since = DateTime.UtcNow.Subtract(lookback);
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);

        var state = await connection.QuerySingleAsync<string>(new CommandDefinition(
            QueryStoreStateSql, cancellationToken: ct)).ConfigureAwait(false);
        if (!string.Equals(state, "READ_WRITE", StringComparison.OrdinalIgnoreCase))
        {
            throw new QueryStoreDisabledException(
                $"Query Store is not enabled. Run: ALTER DATABASE {connection.Database} SET QUERY_STORE = ON;");
        }

        var rows = (await connection.QueryAsync<SlowQuerySnapshotDto>(new CommandDefinition(
            CollectionSql,
            new { since, topN },
            cancellationToken: ct)).ConfigureAwait(false)).AsList();

        var capturedAt = DateTime.UtcNow;
        var snapshots = rows.Select(dto => new SlowQuerySnapshot
        {
            QueryId = dto.QueryId,
            SqlText = dto.SqlText,
            NormalizedSqlHash = ComputeNormalizedSqlHash(dto.SqlText),
            ExecCount = dto.ExecCount,
            AvgDurationMs = dto.AvgDurationUs / 1000d,
            TotalCpuMs = dto.TotalCpuUs / 1000d,
            TotalLogicalReads = dto.TotalLogicalReads,
            ImpactScore = dto.ImpactScore,
            ExecutionPlanXml = dto.ExecutionPlanXml,
            LastExecutionUtc = dto.LastExecutionUtc,
            CapturedAtUtc = capturedAt
        }).ToList();

        _logger.Information("Collected {Count} slow queries with topN {TopN} and lookback {Lookback}",
            snapshots.Count, topN, lookback);
        return snapshots;
    }

    private static string ComputeNormalizedSqlHash(string sqlText)
    {
        var withoutComments = Regex.Replace(sqlText, @"(--[^\r\n]*|/\*.*?\*/)", " ", RegexOptions.Singleline);
        var withoutLiterals = Regex.Replace(withoutComments, @"'(?:''|[^'])*'", " ");
        var normalized = Regex.Replace(withoutLiterals.ToLowerInvariant(), @"\s+", " ").Trim();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}