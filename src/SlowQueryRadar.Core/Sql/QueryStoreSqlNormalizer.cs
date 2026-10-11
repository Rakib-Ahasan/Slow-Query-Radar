//src/SlowQueryRadar.Core/Sql/QueryStoreSqlNormalizer.cs
using System.Text.RegularExpressions;

namespace SlowQueryRadar.Core.Sql;

/// <summary>Normalizes SQL text captured from SQL Server Query Store.</summary>
public static class QueryStoreSqlNormalizer
{
    private static readonly Regex ParameterRegex = new(
        @"^(?<name>@\w+)\s+(?<type>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    /// <summary>Strips the Query Store parameter prefix and extracts parameter declarations.</summary>
    /// <param name="sql">The SQL text to normalize.</param>
    /// <returns>The SQL without its prefix and any extracted declarations.</returns>
    public static NormalizedQuery Normalize(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var start = 0;
        while (start < sql.Length && char.IsWhiteSpace(sql[start]))
        {
            start++;
        }

        if (start == sql.Length || sql[start] != '(')
        {
            return new NormalizedQuery(sql, []);
        }

        var depth = 0;
        var closingIndex = -1;
        for (var index = start; index < sql.Length; index++)
        {
            if (sql[index] == '(')
            {
                depth++;
            }
            else if (sql[index] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    closingIndex = index;
                    break;
                }
            }
        }

        if (closingIndex < 0)
        {
            return new NormalizedQuery(sql, []);
        }

        var declarations = SplitDeclarations(sql[(start + 1)..closingIndex])
            .Select(ParseDeclaration)
            .ToArray();
        if (declarations.Any(parameter => parameter is null))
        {
            return new NormalizedQuery(sql, []);
        }

        return new NormalizedQuery(
            sql[(closingIndex + 1)..].TrimStart(),
            declarations.OfType<ParameterDecl>().ToArray());
    }

    private static IEnumerable<string> SplitDeclarations(string value)
    {
        var start = 0;
        var depth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return value[start..index].Trim();
                    start = index + 1;
                    break;
            }
        }

        yield return value[start..].Trim();
    }

    private static ParameterDecl? ParseDeclaration(string declaration)
    {
        var match = ParameterRegex.Match(declaration);
        return match.Success
            ? new ParameterDecl(match.Groups["name"].Value, match.Groups["type"].Value.Trim())
            : null;
    }

    /// <summary>Normalized Query Store SQL and extracted parameter declarations.</summary>
    /// <param name="Sql">SQL text without the Query Store parameter prefix.</param>
    /// <param name="Parameters">Extracted parameter declarations.</param>
    public sealed record NormalizedQuery(
        string Sql,
        IReadOnlyList<ParameterDecl> Parameters);

    /// <summary>Describes a parameter extracted from a Query Store prefix.</summary>
    /// <param name="Name">Parameter name, including the @ prefix.</param>
    /// <param name="Type">SQL parameter type declaration.</param>
    public sealed record ParameterDecl(string Name, string Type);
}
