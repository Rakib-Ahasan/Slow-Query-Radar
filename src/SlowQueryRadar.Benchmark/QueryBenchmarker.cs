//src/SlowQueryRadar.Benchmark/QueryBenchmarker.cs
using System.Diagnostics;
using System.Data.Common;
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
using SlowQueryRadar.Core.Sql;

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
        var normalized = QueryStoreSqlNormalizer.Normalize(sql);
        var parser = new TSql170Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(normalized.Sql);
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
        return await RunCoreAsync(sql, parameters, null, warmupRuns, measuredRuns, ct).ConfigureAwait(false);
    }

    /// <summary>Benchmarks original and proposed SELECT statements and compares their metrics.</summary>
    /// <param name="originalSql">The original SELECT statement.</param>
    /// <param name="proposedSql">The proposed SELECT statement.</param>
    /// <param name="parameters">Shared Dapper parameters.</param>
    /// <param name="warmupRuns">Warm-up executions discarded from statistics.</param>
    /// <param name="measuredRuns">Measured executions per statement.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Before/after metrics and improvement percentages.</returns>
    public async Task<BenchmarkComparisonResult> CompareAsync(
        string originalSql,
        string proposedSql,
        object? parameters,
        int warmupRuns,
        int measuredRuns,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(originalSql);
        ArgumentNullException.ThrowIfNull(proposedSql);
        ValidateSql(originalSql);
        ValidateSql(proposedSql);
        var original = await RunAsync(originalSql, parameters, warmupRuns, measuredRuns, ct).ConfigureAwait(false);
        var proposed = await RunAsync(proposedSql, parameters, warmupRuns, measuredRuns, ct).ConfigureAwait(false);
        var confidence = original.RowCount == proposed.RowCount
            && string.Equals(original.FirstRowChecksum, proposed.FirstRowChecksum, StringComparison.OrdinalIgnoreCase)
            ? "High: result-set row count and checksum match."
            : "Low: result-set shape or checksum differs; review proposed SQL before use.";
        return new BenchmarkComparisonResult(
            original,
            proposed,
            Improvement(original.DurationP50Ms, proposed.DurationP50Ms),
            Improvement(original.LogicalReads, proposed.LogicalReads),
            confidence,
            original.RowCount == proposed.RowCount
                && string.Equals(original.FirstRowChecksum, proposed.FirstRowChecksum, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Runs a benchmark with optional named parameter overrides.</summary>
    /// <param name="sql">The SQL text.</param>
    /// <param name="parameterOverrides">Optional parameter values keyed by parameter name.</param>
    /// <param name="warmupRuns">The number of unmeasured warmup executions.</param>
    /// <param name="measuredRuns">The number of measured executions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregated benchmark metrics.</returns>
    public async Task<BenchmarkResult> RunAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameterOverrides,
        int warmupRuns,
        int measuredRuns,
        CancellationToken ct)
    {
        return await RunCoreAsync(sql, null, parameterOverrides, warmupRuns, measuredRuns, ct).ConfigureAwait(false);
    }

    private async Task<BenchmarkResult> RunCoreAsync(
        string sql,
        object? parameters,
        IReadOnlyDictionary<string, object?>? parameterOverrides,
        int warmupRuns,
        int measuredRuns,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var normalized = QueryStoreSqlNormalizer.Normalize(sql);
        ValidateSql(normalized.Sql);
        var boundParameters = CreateParameters(normalized.Parameters, parameters, parameterOverrides);
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
            await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            long logicalReads = 0;
            connection.InfoMessage += (_, eventArgs) =>
            {
                foreach (SqlError error in eventArgs.Errors)
                {
                    var match = Regex.Match(
                        error.Message,
                        @"logical reads (\d+)",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (match.Success && long.TryParse(match.Groups[1].Value, out var readsForObject))
                    {
                        logicalReads += readsForObject;
                    }
                }
            };

            await connection.ExecuteAsync(new CommandDefinition(
                "SET STATISTICS IO ON; SET STATISTICS TIME ON;",
                transaction: transaction,
                cancellationToken: ct)).ConfigureAwait(false);

            var before = await ReadSessionMetricsAsync(connection, transaction, ct).ConfigureAwait(false);
            var stopwatch = Stopwatch.StartNew();
            var rows = (await connection.QueryAsync(new CommandDefinition(
                normalized.Sql,
                boundParameters,
                transaction: transaction,
                cancellationToken: ct)).ConfigureAwait(false)).AsList();
            stopwatch.Stop();
            await connection.ExecuteAsync(new CommandDefinition(
                "SET STATISTICS IO OFF; SET STATISTICS TIME OFF;",
                transaction: transaction,
                cancellationToken: ct)).ConfigureAwait(false);
            var after = await ReadSessionMetricsAsync(connection, transaction, ct).ConfigureAwait(false);

            if (run >= warmupRuns)
            {
                durations.Add(stopwatch.Elapsed.TotalMilliseconds);
                reads.Add(logicalReads);
                cpuTimes.Add(Math.Max(0, after.CpuTimeMs - before.CpuTimeMs));
                rowCount = rows.Count;
                firstRowChecksum ??= ComputeFirstRowsChecksum(rows);
            }

            await transaction.RollbackAsync(ct).ConfigureAwait(false);
        }

        durations.Sort();
        return new BenchmarkResult(
            ComputeSqlHash(normalized.Sql),
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

    private static object? CreateParameters(
        IReadOnlyList<QueryStoreSqlNormalizer.ParameterDecl> declarations,
        object? parameters,
        IReadOnlyDictionary<string, object?>? parameterOverrides)
    {
        if (declarations.Count == 0 && parameterOverrides is null)
        {
            return parameters;
        }

        var dynamicParameters = new DynamicParameters();
        if (parameters is not null)
        {
            dynamicParameters.AddDynamicParams(parameters);
        }

        var overrides = parameterOverrides is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : parameterOverrides.ToDictionary(
                pair => TrimParameterPrefix(pair.Key),
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);

        foreach (var declaration in declarations)
        {
            var key = TrimParameterPrefix(declaration.Name);
            if (overrides.TryGetValue(key, out var overrideValue))
            {
                dynamicParameters.Add(declaration.Name, overrideValue);
            }
            else if (!dynamicParameters.ParameterNames.Any(name =>
                         string.Equals(TrimParameterPrefix(name), key, StringComparison.OrdinalIgnoreCase)))
            {
                dynamicParameters.Add(declaration.Name, DefaultValue(declaration.Type));
            }
        }

        return dynamicParameters;
    }

    private static string TrimParameterPrefix(string name) => name.TrimStart('@');

    private static object DefaultValue(string type)
    {
        var normalizedType = type.Trim().ToLowerInvariant();
        if (normalizedType.StartsWith("smallint", StringComparison.Ordinal)
            || normalizedType.StartsWith("int", StringComparison.Ordinal)
            || normalizedType.StartsWith("bigint", StringComparison.Ordinal)
            || normalizedType.StartsWith("tinyint", StringComparison.Ordinal))
        {
            return 1;
        }

        if (normalizedType.StartsWith("decimal", StringComparison.Ordinal)
            || normalizedType.StartsWith("numeric", StringComparison.Ordinal)
            || normalizedType.StartsWith("float", StringComparison.Ordinal)
            || normalizedType.StartsWith("real", StringComparison.Ordinal)
            || normalizedType.StartsWith("money", StringComparison.Ordinal)
            || normalizedType.StartsWith("smallmoney", StringComparison.Ordinal))
        {
            return 1.0;
        }

        if (normalizedType.StartsWith("bit", StringComparison.Ordinal))
        {
            return true;
        }

        if (normalizedType.StartsWith("varchar", StringComparison.Ordinal)
            || normalizedType.StartsWith("nvarchar", StringComparison.Ordinal)
            || normalizedType.StartsWith("char", StringComparison.Ordinal)
            || normalizedType.StartsWith("nchar", StringComparison.Ordinal))
        {
            return "test";
        }

        if (normalizedType.StartsWith("date", StringComparison.Ordinal)
            || normalizedType.StartsWith("datetime", StringComparison.Ordinal))
        {
            return new DateTime(2024, 1, 1);
        }

        if (normalizedType.StartsWith("uniqueidentifier", StringComparison.Ordinal))
        {
            return Guid.Empty;
        }

        return DBNull.Value;
    }

    private static async Task<SessionMetrics> ReadSessionMetricsAsync(SqlConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        return await connection.QuerySingleAsync<SessionMetrics>(new CommandDefinition(
            SessionMetricsSql,
            transaction: transaction,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    private static double Improvement(double before, double after) => before <= 0 ? 0 : (before - after) / before * 100;

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
