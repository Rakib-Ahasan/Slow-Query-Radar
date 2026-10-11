//src/SlowQueryRadar.Suggestions/Deterministic/IndexScriptGenerator.cs
using SlowQueryRadar.Analyzer.Plan;

namespace SlowQueryRadar.Suggestions.Deterministic;

/// <summary>Generates guarded SQL Server index recommendation scripts.</summary>
public sealed class IndexScriptGenerator
{
    /// <summary>Generates a non-mutating copy-paste script for a missing index.</summary>
    /// <param name="suggestion">The missing-index suggestion from the analyzer.</param>
    /// <returns>A guarded CREATE INDEX script.</returns>
    public string Generate(MissingIndexSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        var tableName = NormalizeTableName(suggestion.TableName);
        var columns = suggestion.EqualityColumns
            .Concat(suggestion.InequalityColumns)
            .ToArray();
        var indexName = $"IX_{CleanIdentifier(tableName)}_{string.Join("_", columns.Select(CleanIdentifier))}";
        var keyColumns = string.Join(", ", columns);
        var include = suggestion.IncludeColumns.Count == 0
            ? string.Empty
            : $"\n    INCLUDE ({string.Join(", ", suggestion.IncludeColumns)} )";

        return $"IF NOT EXISTS (\n    SELECT 1\n    FROM sys.indexes\n    WHERE object_id = OBJECT_ID(N'{EscapeSqlLiteral(tableName)}')\n      AND name = N'{EscapeSqlLiteral(indexName)}'\n)\nBEGIN\n    CREATE NONCLUSTERED INDEX [{indexName}]\n        ON {tableName} ({keyColumns}){include};\nEND\nELSE\nBEGIN\n    PRINT N'-- Index [{indexName}] already exists on {EscapeSqlLiteral(tableName)}; no duplicate DDL generated.';\nEND;";
    }

    private static string NormalizeTableName(string tableName)
    {
        var cleaned = tableName.Trim();
        return cleaned.Contains('.', StringComparison.Ordinal) ? cleaned : $"[dbo].[{cleaned.Trim('[', ']')}]";
    }

    private static string CleanIdentifier(string value) =>
        value.Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal)
            .Replace(".", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);

    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
