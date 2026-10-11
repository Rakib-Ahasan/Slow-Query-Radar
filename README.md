# Slow Query Radar

Slow Query Radar collects, analyzes, and benchmarks slow SQL queries.

## CLI configuration

The CLI reads `src/SlowQueryRadar.Cli/appsettings.json` as a placeholder configuration. Set `QueryStore:ConnectionString` and `Benchmark:ConnectionString` through .NET user-secrets or environment variables before running against a database. Connection strings and other secrets must never be committed to the repository.

For example, use environment variables such as `QueryStore__ConnectionString` and `Benchmark__ConnectionString`.
