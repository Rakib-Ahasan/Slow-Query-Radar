# Security

## Secret handling

- Connection strings must come from configuration sources only:
  - `appsettings.Shared.json` (local, gitignored)
  - user-secrets
  - environment variables
  - Azure Key Vault (when configured)
- Never hardcode connection strings in source code.
- Never print full connection strings in logs or CLI output.

## SQL access model (read-only)

The collector should use read-only logins for monitored databases.

Minimum required permissions:

- `VIEW DATABASE STATE`
- `SELECT` on Query Store catalog views:
  - `sys.query_store_query`
  - `sys.query_store_query_text`
  - `sys.query_store_plan`
  - `sys.query_store_runtime_stats`
  - `sys.query_store_runtime_stats_interval`
  - `sys.database_query_store_options`

Recommended:

- Keep write permissions denied for monitored databases.
- For development TLS issues, use `TrustServerCertificate=True` only in dev.
- For production, install and trust proper server certificates.
