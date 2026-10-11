--src/SlowQueryRadar.Benchmark/Sql/BenchmarkHistory.sql
CREATE TABLE dbo.BenchmarkHistory (
    Id BIGINT IDENTITY PRIMARY KEY,
    SqlHash NVARCHAR(64) NOT NULL,
    SqlText NVARCHAR(MAX) NOT NULL,
    RunLabel NVARCHAR(50) NOT NULL,
    DurationP50Ms FLOAT NOT NULL,
    DurationP95Ms FLOAT NOT NULL,
    DurationMaxMs FLOAT NOT NULL,
    LogicalReads BIGINT NOT NULL,
    CpuTimeMs FLOAT NOT NULL,
    RowCount BIGINT NOT NULL,
    RanAtUtc DATETIME2 NOT NULL,
    RanBy NVARCHAR(100) NULL,
    FirstRowChecksum NVARCHAR(32) NULL
);

CREATE INDEX IX_BenchmarkHistory_SqlHash_RunLabel
    ON dbo.BenchmarkHistory (SqlHash, RunLabel);