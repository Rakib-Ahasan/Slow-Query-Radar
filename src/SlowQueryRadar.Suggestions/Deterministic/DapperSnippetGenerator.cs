//src/SlowQueryRadar.Suggestions/Deterministic/DapperSnippetGenerator.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Suggestions.Deterministic;

/// <summary>Generates Dapper snippets for applying a finding's recommendation.</summary>
public sealed class DapperSnippetGenerator
{
    /// <summary>Generates a cancellation-aware Dapper query snippet.</summary>
    /// <param name="sqlText">The SQL text.</param>
    /// <param name="finding">The finding being addressed.</param>
    /// <returns>A copy-paste C# Dapper snippet.</returns>
    public string Generate(string sqlText, Finding finding)
    {
        ArgumentNullException.ThrowIfNull(sqlText);
        ArgumentNullException.ThrowIfNull(finding);

        var escapedSql = sqlText.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"using Dapper;\nusing Microsoft.Data.SqlClient;\nusing SlowQueryRadar.Profiler;\n\nawait using var conn = new SqlConnection(cs).Profile();\nvar result = await conn.QueryAsync<T>(\n    new CommandDefinition(\n        sql: @\"{escapedSql}\",\n        parameters: new {{ }},\n        commandTimeout: 30,\n        cancellationToken: ct));";
    }
}
