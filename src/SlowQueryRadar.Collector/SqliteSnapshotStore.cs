//src/SlowQueryRadar.Collector/SqliteSnapshotStore.cs
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;
using System.Globalization;

namespace SlowQueryRadar.Collector;

/// <summary>SQLite implementation of snapshot storage.</summary>
public sealed class SqliteSnapshotStore : ISnapshotStore
{
    private readonly SnapshotStoreOptions _options;
    private readonly Func<SqliteConnection> _connectionFactory;

    /// <summary>Initializes a SQLite snapshot store.</summary>
    public SqliteSnapshotStore(IOptions<SnapshotStoreOptions> options)
        : this(options, null)
    {
    }

    /// <summary>Initializes a SQLite snapshot store with optional connection factory.</summary>
    public SqliteSnapshotStore(IOptions<SnapshotStoreOptions> options, Func<SqliteConnection>? connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? new SnapshotStoreOptions();
        _connectionFactory = connectionFactory ?? (() => new SqliteConnection(_options.ConnectionString));
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition("PRAGMA foreign_keys = ON;", cancellationToken: ct)).ConfigureAwait(false);
        await EnsureMigrationsAsync(connection, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> SaveRunAsync(DateTimeOffset startedUtc, TimeSpan duration, FleetCollectResult result, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(result);

        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition("PRAGMA foreign_keys = ON;", cancellationToken: ct)).ConfigureAwait(false);
        await EnsureMigrationsAsync(connection, ct).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var runId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "INSERT INTO Runs(StartedUtc, DurationMs) VALUES(@startedUtc, @durationMs); SELECT last_insert_rowid();",
            new { startedUtc = startedUtc.UtcDateTime, durationMs = duration.TotalMilliseconds },
            transaction,
            cancellationToken: ct)).ConfigureAwait(false);

        foreach (var targetResult in result.Results)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO TargetRuns(RunId, Target, Status, Error) VALUES(@runId, @target, @status, @error);",
                new { runId, target = targetResult.Name, status = targetResult.Status.ToString(), error = targetResult.ErrorMessage },
                transaction,
                cancellationToken: ct)).ConfigureAwait(false);

            foreach (var query in targetResult.Queries)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO QuerySamples
                    (RunId, Target, QueryHash, NormalizedHash, SqlText, Executions, AvgMs, TotalMs, MaxMs, StdDevMs, Cpu, LogicalReads, PlanCount, CapturedUtc)
                    VALUES
                    (@runId, @target, @queryHash, @normalizedHash, @sqlText, @executions, @avgMs, @totalMs, @maxMs, @stdDevMs, @cpu, @logicalReads, @planCount, @capturedUtc);
                    """,
                    new
                    {
                        runId,
                        target = targetResult.Name,
                        queryHash = query.QueryHash,
                        normalizedHash = query.NormalizedSqlHash,
                        sqlText = query.SqlText,
                        executions = query.TotalExecutions,
                        avgMs = query.AvgDurationMs,
                        totalMs = query.TotalDurationMs,
                        maxMs = query.MaxDurationMs,
                        stdDevMs = query.StandardDeviationMs,
                        cpu = query.TotalCpuMs,
                        logicalReads = query.TotalLogicalReads,
                        planCount = query.PlanCount,
                        capturedUtc = query.CapturedAtUtc.UtcDateTime
                    },
                    transaction,
                    cancellationToken: ct)).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return runId;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuerySample>> ReadSamplesAsync(string? target, DateTimeOffset sinceUtc, DateTimeOffset untilUtc, CancellationToken ct)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var rows = await connection.QueryAsync<QuerySampleRow>(new CommandDefinition(
            """
            SELECT RunId, Target, QueryHash, NormalizedHash, SqlText, Executions, AvgMs, TotalMs, MaxMs, StdDevMs, Cpu, LogicalReads, PlanCount, CapturedUtc
            FROM QuerySamples
            WHERE CapturedUtc >= @sinceUtc
              AND CapturedUtc <= @untilUtc
              AND (@target IS NULL OR Target = @target)
            ORDER BY CapturedUtc DESC;
            """,
            new { target, sinceUtc = sinceUtc.UtcDateTime, untilUtc = untilUtc.UtcDateTime },
            cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuerySample>> ReadLatestSamplesAsync(CancellationToken ct)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);

        var latestRunId = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT MAX(RunId) FROM Runs;",
            cancellationToken: ct)).ConfigureAwait(false);
        if (latestRunId is null)
        {
            return Array.Empty<QuerySample>();
        }

        var rows = await connection.QueryAsync<QuerySampleRow>(new CommandDefinition(
            """
            SELECT RunId, Target, QueryHash, NormalizedHash, SqlText, Executions, AvgMs, TotalMs, MaxMs, StdDevMs, Cpu, LogicalReads, PlanCount, CapturedUtc
            FROM QuerySamples
            WHERE RunId = @runId;
            """,
            new { runId = latestRunId.Value },
            cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TargetRunRecord>> ReadTargetRunsAsync(DateTimeOffset sinceUtc, DateTimeOffset untilUtc, CancellationToken ct)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);

        var rows = await connection.QueryAsync<TargetRunRecord>(new CommandDefinition(
            """
            SELECT tr.RunId, tr.Target, tr.Status, tr.Error, r.StartedUtc, r.DurationMs
            FROM TargetRuns AS tr
            INNER JOIN Runs AS r ON r.RunId = tr.RunId
            WHERE r.StartedUtc >= @sinceUtc AND r.StartedUtc <= @untilUtc
            ORDER BY r.StartedUtc DESC, tr.Target;
            """,
            new { sinceUtc = sinceUtc.UtcDateTime, untilUtc = untilUtc.UtcDateTime },
            cancellationToken: ct)).ConfigureAwait(false);

        return rows.ToArray();
    }

    /// <inheritdoc />
    public async Task<int> CleanupAsync(TimeSpan retention, CancellationToken ct)
    {
        if (retention <= TimeSpan.Zero)
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.Subtract(retention).UtcDateTime;
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition("PRAGMA foreign_keys = ON;", cancellationToken: ct)).ConfigureAwait(false);

        var deletedSamples = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)
            FROM QuerySamples
            WHERE RunId IN (SELECT RunId FROM Runs WHERE StartedUtc < @cutoff);
            """,
            new { cutoff },
            cancellationToken: ct)).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Runs WHERE StartedUtc < @cutoff;",
            new { cutoff },
            cancellationToken: ct)).ConfigureAwait(false);

        return deletedSamples;
    }

    private static async Task EnsureMigrationsAsync(SqliteConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "CREATE TABLE IF NOT EXISTS SchemaVersions (Version INTEGER PRIMARY KEY);",
            cancellationToken: ct)).ConfigureAwait(false);

        var currentVersion = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT MAX(Version) FROM SchemaVersions;",
            cancellationToken: ct)).ConfigureAwait(false) ?? 0;

        if (currentVersion < 1)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                CREATE TABLE IF NOT EXISTS Runs(
                    RunId INTEGER PRIMARY KEY AUTOINCREMENT,
                    StartedUtc TEXT NOT NULL,
                    DurationMs REAL NOT NULL
                );

                CREATE TABLE IF NOT EXISTS TargetRuns(
                    RunId INTEGER NOT NULL,
                    Target TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    Error TEXT NULL,
                    PRIMARY KEY (RunId, Target),
                    FOREIGN KEY (RunId) REFERENCES Runs(RunId) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS QuerySamples(
                    RunId INTEGER NOT NULL,
                    Target TEXT NOT NULL,
                    QueryHash TEXT NOT NULL,
                    NormalizedHash TEXT NOT NULL,
                    SqlText TEXT NOT NULL,
                    Executions INTEGER NOT NULL,
                    AvgMs REAL NOT NULL,
                    TotalMs REAL NOT NULL,
                    MaxMs REAL NOT NULL,
                    StdDevMs REAL NOT NULL,
                    Cpu REAL NOT NULL,
                    LogicalReads INTEGER NOT NULL,
                    PlanCount INTEGER NOT NULL,
                    CapturedUtc TEXT NOT NULL,
                    FOREIGN KEY (RunId) REFERENCES Runs(RunId) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS IX_QuerySamples_Target_NormalizedHash_CapturedUtc
                ON QuerySamples(Target, NormalizedHash, CapturedUtc);

                INSERT INTO SchemaVersions(Version) VALUES (1);
                """,
                cancellationToken: ct)).ConfigureAwait(false);
        }
    }

    private static QuerySample Map(QuerySampleRow row)
    {
        return new QuerySample(
            row.RunId,
            row.Target,
            row.QueryHash,
            row.NormalizedHash,
            row.SqlText,
            row.Executions,
            row.AvgMs,
            row.TotalMs,
            row.MaxMs,
            row.StdDevMs,
            row.Cpu,
            row.LogicalReads,
            row.PlanCount,
            DateTimeOffset.Parse(row.CapturedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));
    }

    private sealed class QuerySampleRow
    {
        public long RunId { get; set; }
        public string Target { get; set; } = string.Empty;
        public string QueryHash { get; set; } = string.Empty;
        public string NormalizedHash { get; set; } = string.Empty;
        public string SqlText { get; set; } = string.Empty;
        public long Executions { get; set; }
        public double AvgMs { get; set; }
        public double TotalMs { get; set; }
        public double MaxMs { get; set; }
        public double StdDevMs { get; set; }
        public double Cpu { get; set; }
        public long LogicalReads { get; set; }
        public int PlanCount { get; set; }
        public string CapturedUtc { get; set; } = string.Empty;
    }
}
