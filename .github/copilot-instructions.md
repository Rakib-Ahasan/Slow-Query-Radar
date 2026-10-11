# Slow Query Radar — Copilot Instructions

You are assisting on **SlowQueryRadar**, an internal .NET tool that detects slow
SQL queries, analyzes execution plans, and suggests optimizations.

## ABSOLUTE RULES (never violate)

1. **Never auto-apply SQL changes.** No CREATE INDEX, DROP, ALTER, UPDATE, DELETE
   on production. All suggestions are copy-paste snippets for a developer.
2. **Production DB is read-only.** Enforced via SQL login permissions
   (`db_datareader` + `db_denydatawriter`), NOT via `ApplicationIntent=ReadOnly`.
3. **Dapper only.** No Entity Framework anywhere in this solution.
4. **ProfiledDbConnection MUST inherit `System.Data.Common.DbConnection`.**
   ProfiledDbCommand MUST inherit `System.Data.Common.DbCommand`.
   Do NOT implement raw `IDbConnection`/`IDbCommand` — Dapper's true async path
   requires the Db* abstract classes.
5. **Async rule:** all I/O-bound public methods must be `async` + accept
   `CancellationToken`. CPU-only methods (rules, parsers, generators) MUST be
   synchronous. No fake async with `Task.FromResult`.

## Coding conventions

- .NET 10, `nullable enable`, file-scoped namespaces, `ImplicitUsings` on.
- XML doc comments on every public API.
- Serilog structured logging only: `Log.Information("Query {SqlHash} took {DurationMs}", hash, ms)`. Never string concat.
- xUnit for tests. Use real anonymized `.sqlplan` fixtures from `tests/Fixtures/Plans/`.
- No `Task.Run` around DB calls.
- Every DB call uses `CancellationToken`.

## SQL / Query Store specifics

- Query Store times are **microseconds** — convert to ms before reporting.
- Correct impact formula: `impact = SUM(avg_cpu_time * count_executions)`.
  Do NOT multiply by count again.
- Join key between Dapper logs and Query Store = normalized SQL text hash,
  NOT `query_hash` (algorithms differ). Normalizer must work on both sides:
  strip literals → lowercase keywords → collapse whitespace.
- Plan XML can be huge (128+ nesting). Always `TRY_CAST(plan AS xml)` in SQL
  and handle NULL. Never plain `CAST`.
- Prefer `<MissingIndexes>` element from the plan — easiest win.

## Security & privacy

- Parameter values in logs: only when `LogParameterValues = true` (default false).
- Before sending SQL to any AI: strip string literals, comments, and PII.
  Table/column names are still sensitive — AI is off by default
  (`Ai:Enabled = false`). Only enable after security team approval.
- AI calls: 10s timeout, fail gracefully, cache by (SQL hash + rule set version).

## Project layout (respect boundaries)

- `Core` — domain models + interfaces, ZERO external dependencies.
- `Collector` — Query Store + DMVs, uses Dapper.
- `Profiler` — Dapper wrapper, no dependency on Collector/Analyzer.
- `Analyzer` — plan parser + rule engine, no HTTP, no DB.
- `Suggestions` — deterministic + optional AI. Only project allowed to make HTTP calls.
- `Benchmark` — before/after runner, only against `BenchmarkConnection`.
- `Api` — Blazor Server + controllers, uses all above.
- `Cli` — Spectre.Console commands.

## Do NOT

- Add EF Core, AutoMapper, MediatR, or any CQRS framework.
- Add `Prism.js`, `Monaco`, or `Chart.js` — use MudBlazor + `MudChart` + `<pre>`.
- Add comments explaining what code does; only why when non-obvious.
- Wrap `IDbCommand.ExecuteReader` result — reader stays untouched.
- Multi-statement batches: analyze the FIRST statement only, log the rest.

## When generating code

- Provide full file (usings + namespace + class), not fragments.
- Show the exact file path as a comment on the first line.
- Include a matching xUnit test in the same response.
- If a rule has severity, use enum `Severity { Low, Medium, High, Critical }`.