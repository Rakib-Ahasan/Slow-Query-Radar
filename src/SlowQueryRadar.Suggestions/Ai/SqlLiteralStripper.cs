//src/SlowQueryRadar.Suggestions/Ai/SqlLiteralStripper.cs
using System.Text.RegularExpressions;

namespace SlowQueryRadar.Suggestions.Ai;

/// <summary>Removes SQL literals, numbers, and comments before approved AI transmission.</summary>
/// <remarks>Table and column names are intentionally retained because they are required for useful advice.</remarks>
public sealed class SqlLiteralStripper
{
    private static readonly Regex CommentPattern = new(@"(--[^\r\n]*|/\*.*?\*/)", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex SingleQuotedPattern = new(@"N?'(?:''|[^'])*'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DoubleQuotedPattern = new("\\\"(?:\\\"\\\"|[^\\\"])*\\\"", RegexOptions.Compiled);
    private static readonly Regex NumberPattern = new(@"(?<![\w?])[-+]?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?(?![\w])", RegexOptions.Compiled);
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Strips sensitive literal values while retaining identifiers.</summary>
    /// <param name="sql">The SQL text to sanitize.</param>
    /// <returns>SQL with literals, comments, and numeric values replaced by question marks.</returns>
    public string Strip(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var result = CommentPattern.Replace(sql, " ");
        result = SingleQuotedPattern.Replace(result, "?");
        result = DoubleQuotedPattern.Replace(result, "?");
        result = NumberPattern.Replace(result, "?");
        return WhitespacePattern.Replace(result, " ").Trim();
    }
}
