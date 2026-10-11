//src/SlowQueryRadar.Benchmark/BenchmarkHistoryStore.cs
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Benchmark;

/// <summary>Persists benchmark results and computes before/after comparisons in RadarDb.</summary>
public sealed class BenchmarkHistoryStore
{
    private readonly BenchmarkOptions _options;
    private readonly Func<SqlConnection> _connectionFactory;

    /// <summary>Initializes a history store for the configured benchmark database.</summary>
    /// <param name="options">Benchmark connection options.</param>
    public BenchmarkHistoryStore(IOptions<BenchmarkOptions> options)
        : this(options, null)
    {
    }

    /// <summary>Initializes a history store with a controlled connection factory.</summary>
    /// <param name="options">Benchmark connection options.</param>
    /// <param name="connectionFactory">Optional connection factory for tests.</param>
    public BenchmarkHistoryStore(IOptions<BenchmarkOptions> options, Func<SqlConnection>? connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new InvalidOperationException("Benchmark options are required.");
        _options.Validate();
        _connectionFactory = connectionFactory ?? (() => new SqlConnection(_options.ConnectionString));
    }

    /// <summary>Saves a benchmark result to RadarDb.</summary>
    /// <param name="sqlHash">The normalized SQL hash.</param>
    /// <param name="sqlText">The SQL text.</param>
    /// <param name="runLabel">The before/after run label.</param>
    /// <param name="result">The benchmark result.</param>
    /// <param name="ranBy">The optional operator name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The inserted history identifier.</returns>
    public async Task<long> SaveAsync(
        string sqlHash,
        string sqlText,
        string runLabel,
        BenchmarkResult result,
        string? ranBy,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sqlHash);
        ArgumentNullException.ThrowIfNull(sqlText);
        ArgumentNullException.ThrowIfNull(runLabel);
        ArgumentNullException.ThrowIfNull(result);

        const string sql = """
            INSERT INTO dbo.BenchmarkHistory
                (SqlHash, SqlText, RunLabel, DurationP50Ms, DurationP95Ms, DurationMaxMs,
                 LogicalReads, CpuTimeMs, RowCount, RanAtUtc, RanBy, FirstRowChecksum)
            OUTPUT INSERTED.Id
            VALUES
                (@SqlHash, @SqlText, @RunLabel, @DurationP50Ms, @DurationP95Ms, @DurationMaxMs,
                 @LogicalReads, @CpuTimeMs, @RowCount, @RanAtUtc, @RanBy, @FirstRowChecksum);
            """;

        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        return await connection.QuerySingleAsync<long>(new CommandDefinition(
            sql,
            new
            {
                sqlHash,
                sqlText,
                runLabel,
                DurationP50Ms = result.DurationP50Ms,
                DurationP95Ms = result.DurationP95Ms,
                DurationMaxMs = result.DurationMaxMs,
                LogicalReads = result.LogicalReads,
                CpuTimeMs = result.CpuTimeMs,
                RowCount = result.RowCount,
                RanAtUtc = result.RanAtUtc,
                ranBy,
                FirstRowChecksum = result.FirstRowChecksum
            },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    /// <summary>Compares two persisted benchmark results.</summary>
    /// <param name="beforeId">The before history identifier.</param>
    /// <param name="afterId">The after history identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The computed comparison.</returns>
    public async Task<BenchmarkComparison> CompareAsync(long beforeId, long afterId, CancellationToken ct)
    {
        const string sql = """
            SELECT Id, SqlHash, DurationP50Ms, DurationP95Ms, DurationMaxMs, LogicalReads,
                   CpuTimeMs, RowCount, RanAtUtc, FirstRowChecksum
            FROM dbo.BenchmarkHistory
            WHERE Id IN (@BeforeId, @AfterId);
            """;

        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var rows = (await connection.QueryAsync<BenchmarkHistoryRow>(new CommandDefinition(
            sql,
            new { BeforeId = beforeId, AfterId = afterId },
            cancellationToken: ct)).ConfigureAwait(false)).ToArray();
        var before = rows.SingleOrDefault(row => row.Id == beforeId)
            ?? throw new KeyNotFoundException($"Benchmark history entry {beforeId} was not found.");
        var after = rows.SingleOrDefault(row => row.Id == afterId)
            ?? throw new KeyNotFoundException($"Benchmark history entry {afterId} was not found.");

        var beforeResult = before.ToResult();
        var afterResult = after.ToResult();
        var durationImprovement = Improvement(beforeResult.DurationP50Ms, afterResult.DurationP50Ms);
        var readsImprovement = Improvement(beforeResult.LogicalReads, afterResult.LogicalReads);
        var resultSetsMatch = beforeResult.RowCount == afterResult.RowCount
            && string.Equals(beforeResult.FirstRowChecksum, afterResult.FirstRowChecksum, StringComparison.OrdinalIgnoreCase);
        return new BenchmarkComparison(
            beforeResult,
            afterResult,
            durationImprovement,
            readsImprovement,
            resultSetsMatch,
            resultSetsMatch && durationImprovement > 10);
    }

    private static double Improvement(double before, double after) =>
        before == 0 ? 0 : (before - after) / before * 100;

    private sealed class BenchmarkHistoryRow
    {
        public long Id { get; set; }
        public string SqlHash { get; set; } = string.Empty;
        public double DurationP50Ms { get; set; }
        public double DurationP95Ms { get; set; }
        public double DurationMaxMs { get; set; }
        public long LogicalReads { get; set; }
        public double CpuTimeMs { get; set; }
        public long RowCount { get; set; }
        public DateTime RanAtUtc { get; set; }
        public string? FirstRowChecksum { get; set; }

        public BenchmarkResult ToResult() => new(
            SqlHash,
            DurationP50Ms,
            DurationP95Ms,
            DurationMaxMs,
            LogicalReads,
            CpuTimeMs,
            RowCount,
            RanAtUtc)
        {
            FirstRowChecksum = FirstRowChecksum
        };
    }
}
