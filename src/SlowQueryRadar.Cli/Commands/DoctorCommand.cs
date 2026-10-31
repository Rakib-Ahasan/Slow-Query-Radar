// src/SlowQueryRadar.Cli/Commands/DoctorCommand.cs
using System.CommandLine;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SlowQueryRadar.Core.Configuration;

namespace SlowQueryRadar.Cli.Commands;

/// <summary>Creates diagnostic checks for local configuration and SQL access.</summary>
public static class DoctorCommand
{
    /// <summary>Creates the <c>doctor</c> command.</summary>
    /// <param name="services">The application service provider.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("doctor", "Run configuration and SQL connectivity diagnostics");
        command.SetAction(async (_, ct) =>
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            var connectionString = configuration["QueryStore:ConnectionString"];
            var rows = new List<DoctorCheckResult>();

            if (SharedConfiguration.TryFindSharedSettingsPath(out var sharedSettingsPath))
            {
                rows.Add(DoctorCheckResult.Success(
                    "Shared config file",
                    sharedSettingsPath!,
                    "None"));
            }
            else
            {
                rows.Add(DoctorCheckResult.Failure(
                    "Shared config file",
                    "appsettings.Shared.json was not found from current execution paths.",
                    "Copy appsettings.Shared.template.json to appsettings.Shared.json at repository root."));
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                rows.Add(DoctorCheckResult.Failure(
                    "Connection opens",
                    "QueryStore:ConnectionString is empty.",
                    "Set QueryStore:ConnectionString in shared settings, user-secrets, or environment variables."));
                rows.Add(DoctorCheckResult.Failure(
                    "Query Store state",
                    "Check skipped because the connection could not be opened.",
                    "Set a valid QueryStore:ConnectionString, then rerun radar doctor."));
                rows.Add(DoctorCheckResult.Failure(
                    "VIEW DATABASE STATE",
                    "Check skipped because the connection could not be opened.",
                    "Grant VIEW DATABASE STATE on the target database for the login used by QueryStore:ConnectionString."));

                Render(rows);
                return;
            }

            await using var connection = new SqlConnection(connectionString);
            try
            {
                await connection.OpenAsync(ct).ConfigureAwait(false);
                rows.Add(DoctorCheckResult.Success(
                    "Connection opens",
                    SlowQueryRadar.Cli.Program.DescribeConnection(connectionString),
                    "None"));
            }
            catch (Exception ex)
            {
                rows.Add(DoctorCheckResult.Failure(
                    "Connection opens",
                    ex.Message,
                    "For dev use TrustServerCertificate=True; for prod install a trusted certificate."));
                rows.Add(DoctorCheckResult.Failure(
                    "Query Store state",
                    "Check skipped because the connection could not be opened.",
                    "Set a valid QueryStore:ConnectionString, then rerun radar doctor."));
                rows.Add(DoctorCheckResult.Failure(
                    "VIEW DATABASE STATE",
                    "Check skipped because the connection could not be opened.",
                    "Grant VIEW DATABASE STATE on the target database for the login used by QueryStore:ConnectionString."));

                Render(rows);
                return;
            }

            var state = await QueryScalarAsync<string>(
                connection,
                "SELECT actual_state_desc FROM sys.database_query_store_options;",
                ct).ConfigureAwait(false);
            var stateOk = string.Equals(state, "READ_WRITE", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(state, "READ_ONLY", StringComparison.OrdinalIgnoreCase);
            rows.Add(stateOk
                ? DoctorCheckResult.Success("Query Store state", state ?? "UNKNOWN", "None")
                : DoctorCheckResult.Failure("Query Store state", state ?? "UNKNOWN", "Enable Query Store: ALTER DATABASE <db> SET QUERY_STORE = ON;"));

            var hasPermission = await QueryScalarAsync<int?>(
                connection,
                "SELECT HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'VIEW DATABASE STATE');",
                ct).ConfigureAwait(false);
            rows.Add(hasPermission == 1
                ? DoctorCheckResult.Success("VIEW DATABASE STATE", "Permission granted.", "None")
                : DoctorCheckResult.Failure("VIEW DATABASE STATE", "Permission is missing.", "Grant VIEW DATABASE STATE on the target database for the collector login."));

            Render(rows);
        });

        return command;
    }

    private static async Task<T?> QueryScalarAsync<T>(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (value is null || value is DBNull)
        {
            return default;
        }

        return (T?)Convert.ChangeType(value, typeof(T));
    }

    private static void Render(IReadOnlyList<DoctorCheckResult> rows)
    {
        var table = new Table()
            .AddColumn("Check")
            .AddColumn("Status")
            .AddColumn("Details")
            .AddColumn("Fix Hint");

        foreach (var row in rows)
        {
            table.AddRow(
                Markup.Escape(row.Name),
                row.IsPassed ? "[green]✔[/]" : "[red]✘[/]",
                Markup.Escape(row.Details),
                Markup.Escape(row.Hint));
        }

        AnsiConsole.Write(table);
    }

    private sealed record DoctorCheckResult(string Name, bool IsPassed, string Details, string Hint)
    {
        public static DoctorCheckResult Success(string name, string details, string hint) => new(name, true, details, hint);
        public static DoctorCheckResult Failure(string name, string details, string hint) => new(name, false, details, hint);
    }
}
