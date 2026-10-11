//src/SlowQueryRadar.Benchmark/QueryBenchmarker.cs
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Benchmark;

/// <summary>Runs safe before/after benchmarks against the configured benchmark database.</summary>
public sealed class QueryBenchmarker
{
    private const string SessionMetricsSql = "SELECT reads AS LogicalReads, cpu_time AS CpuTimeMs FROM sys.dm_exec_sessions WHERE session_id = @@SPID;";
    private readonly BenchmarkOptions _options;
    private readonly Func<SqlConnection> _connectionFactory;

    /// <summary>Initializes the query benchmarker and validates benchmark safety settings.</summary>
    /// <param name="options">Benchmark options.</param>
    public QueryBenchmarker(IOptions<BenchmarkOptions> options)
        : this(options, null)
    {
    }

    /// <summary>Initializes the query benchmarker with a connection factory.</summary>
    /// <param name="options">Benchmark options.</param>
    /// <param name="connectionFactory">Optional connection factory for controlled tests.</param>
    public QueryBenchmarker(IOptions<BenchmarkOptions> options, Func<SqlConnection>? connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new InvalidOperationException("Benchmark options are required.");
        _options.Validate();
        _connectionFactory = connectionFactory ?? (() => new SqlConnection(_options.ConnectionString));
    }

    /// <summary>Validates SQL without opening a database connection.</summary>
    /// <param name="sql">The SQL batch to validate.</param>
    public static void ValidateSql(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var parser = new TSql170Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);
        if (errors.Count > 0)
        {
            throw new UnsafeQueryException(errors[0].Message);
        }

        var statements = fragment is TSqlScript script
            ? script.Batches.SelectMany(batch => batch.Statements).Cast<TSqlFragment>().ToArray()
            : new[] { fragment };
        if (statements.Any(statement => statement is InsertStatement or UpdateStatement or DeleteStatement or MergeStatement or ExecuteStatement))
        {
            throw new UnsafeQueryException("Only a single SELECT statement is allowed.");
        }

        if (statements.Length != 1)
        {
            throw new UnsafeQueryException("Benchmark SQL must contain exactly one SELECT statement.");
        }

        if (statements[0] is not SelectStatement select)
        {
            throw new UnsafeQueryException("Benchmark SQL must contain exactly one SELECT statement.");
        }

        if (select.Into is not null)
        {
            throw new UnsafeQueryException("SELECT INTO is not allowed in benchmark SQL.");
        }
    }

    /// <summary>Runs warmup and measured executions for a single safe SELECT statement.</summary>
    /// <param name="sql">The SQL text.</param>
    /// <param name="parameters">The Dapper parameters.</param>
    /// <param name="warmupRuns">The number of unmeasured warmup executions.</param>
    /// <param name="measuredRuns">The number of measured executions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregated benchmark metrics.</returns>
    public async Task<BenchmarkResult> RunAsync(
        string sql,
        object? parameters,
        int warmupRuns,
        int measuredRuns,
        CancellationToken ct)
    {
        ValidateSql(sql);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupRuns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(measuredRuns);

        var durations = new List<double>(measuredRuns);
        var reads = new List<long>(measuredRuns);
        var cpuTimes = new List<double>(measuredRuns);
        var rowCount = 0L;
        string? firstRowChecksum = null;

        for (var run = 0; run < warmupRuns + measuredRuns; run++)
        {
            ct.ThrowIfCancellationRequested();
            await using var connection = _connectionFactory();
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition(
                "SET STATISTICS IO ON; SET STATISTICS TIME ON;",
                cancellationToken: ct)).ConfigureAwait(false);

            var before = await ReadSessionMetricsAsync(connection, ct).ConfigureAwait(false);
            var stopwatch = Stopwatch.StartNew();
            var rows = (await connection.QueryAsync(new CommandDefinition(
                sql,
                parameters,
                cancellationToken: ct)).ConfigureAwait(false)).AsList();
            stopwatch.Stop();
            var after = await ReadSessionMetricsAsync(connection, ct).ConfigureAwait(false);

            if (run >= warmupRuns)
            {
                durations.Add(stopwatch.Elapsed.TotalMilliseconds);
                reads.Add(Math.Max(0, after.LogicalReads - before.LogicalReads));
                cpuTimes.Add(Math.Max(0, after.CpuTimeMs - before.CpuTimeMs));
                rowCount = rows.Count;
                firstRowChecksum ??= ComputeFirstRowsChecksum(rows);
            }
        }

        durations.Sort();
        return new BenchmarkResult(
            ComputeSqlHash(sql),
            Percentile(durations, 0.50),
            Percentile(durations, 0.95),
            durations[^1],
            (long)Math.Round(reads.Average()),
            cpuTimes.Average(),
            rowCount,
            DateTime.UtcNow)
        {
            FirstRowChecksum = firstRowChecksum
        };
    }

    private static async Task<SessionMetrics> ReadSessionMetricsAsync(SqlConnection connection, CancellationToken ct)
    {
        return await connection.QuerySingleAsync<SessionMetrics>(new CommandDefinition(
            SessionMetricsSql,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(values.Count * percentile) - 1, 0, values.Count - 1);
        return values[index];
    }

    private static string ComputeSqlHash(string sql)
    {
        var withoutComments = Regex.Replace(sql, @"(--[^\r\n]*|/\*.*?\*/)", " ", RegexOptions.Singleline);
        var withoutLiterals = Regex.Replace(withoutComments, @"(?i)N?'(?:''|[^'])*'", "?");
        var withoutNumbers = Regex.Replace(withoutLiterals, @"(?<![\w?])[-+]?\d+(?:\.\d+)?(?![\w])", "?");
        var normalized = Regex.Replace(withoutNumbers, @"\s+", " ").Trim().ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static string ComputeFirstRowsChecksum(IEnumerable<object> rows)
    {
        var canonicalRows = rows.Take(100).Select(row =>
        {
            if (row is IDictionary<string, object> values)
            {
                return values
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ToDictionary(pair => pair.Key, pair => NormalizeValue(pair.Value), StringComparer.Ordinal);
            }

            return new Dictionary<string, object?>
            {
                ["value"] = NormalizeValue(row)
            };
        });
        var json = JsonSerializer.Serialize(canonicalRows);
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static object? NormalizeValue(object? value) =>
        value is DBNull ? null : value;

    private sealed class SessionMetrics
    {
        public long LogicalReads { get; set; }
        public double CpuTimeMs { get; set; }
    }
}
