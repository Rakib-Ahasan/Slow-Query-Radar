# Slow Query Radar

Slow Query Radar collects, analyzes, and benchmarks slow SQL queries.

## Local Development Setup

1. Copy `appsettings.Shared.template.json` to `appsettings.Shared.json` at the
   repository root.
   The template is valid JSON; the copied `appsettings.Shared.json` is
   gitignored and must never be committed.
2. Fill in your local connection strings:
   - `QueryStore:ConnectionString`
   - `Benchmark:ConnectionString`
3. Run any project:
   ```powershell
   dotnet run --project src/SlowQueryRadar.Api
   dotnet run --project src/SlowQueryRadar.Cli -- collect --top 10
	  dotnet run --project src/SlowQueryRadar.Cli -- doctor
   ```

4. Validate local configuration and SQL access with the CLI doctor:
   ```powershell
   dotnet run --project src/SlowQueryRadar.Cli -- doctor
   ```
   The doctor command checks shared config discovery, SQL connectivity,
   Query Store state, and `VIEW DATABASE STATE` permission. If TLS errors occur,
   for dev use `TrustServerCertificate=True`; for prod install a trusted certificate.

All three projects (Api, Cli, and Tests.Integration) read from this single
file. Do not commit `appsettings.Shared.json`.

For production, use environment variables or Azure Key Vault:

- `QueryStore__ConnectionString`
- `Benchmark__ConnectionString`

## CLI configuration

The project-local appsettings files remain placeholders. The shared file is
loaded first, followed by user-secrets and environment variables, so local
overrides retain their existing precedence. Connection strings and other
secrets must never be committed to the repository.

For example, use environment variables such as `QueryStore__ConnectionString` and `Benchmark__ConnectionString`.
