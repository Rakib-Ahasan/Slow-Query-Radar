//src/SlowQueryRadar.Profiler/SqlNormalizer.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SlowQueryRadar.Profiler;

/// <summary>Normalizes SQL text and computes stable hashes for equivalent queries.</summary>
public static class SqlNormalizer
{
    /// <summary>Normalizes comments, literals, whitespace, and common operator spacing.</summary>
    /// <param name="sql">The SQL text to normalize.</param>
    /// <returns>The normalized SQL text.</returns>
    public static string Normalize(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var withoutComments = CommentPattern.Replace(sql, " ");
        var withoutLiterals = StringLiteralPattern.Replace(withoutComments, "?");
        var withoutNumbers = NumericLiteralPattern.Replace(withoutLiterals, "?");
        var normalized = WhitespacePattern.Replace(withoutNumbers, " ").Trim().ToLowerInvariant();
        return OperatorWhitespacePattern.Replace(normalized, " $1 ");
    }

    /// <summary>Computes a SHA256 hexadecimal hash of normalized SQL text.</summary>
    /// <param name="normalizedSql">The already normalized SQL text.</param>
    /// <returns>A 64-character uppercase hexadecimal SHA256 hash.</returns>
    public static string ComputeHash(string normalizedSql)
    {
        ArgumentNullException.ThrowIfNull(normalizedSql);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSql)));
    }

    /// <summary>Normalizes SQL text and computes its SHA256 hexadecimal hash.</summary>
    /// <param name="sql">The SQL text to normalize and hash.</param>
    /// <returns>A 64-character uppercase hexadecimal SHA256 hash.</returns>
    public static string ComputeHashFromSql(string sql) => ComputeHash(Normalize(sql));

    private static readonly Regex CommentPattern = new(@"(--[^\r\n]*|/\*.*?\*/)", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex StringLiteralPattern = new(@"(?i)N?'(?:''|[^'])*'", RegexOptions.Compiled);
    private static readonly Regex NumericLiteralPattern = new(@"(?<![\w?])[-+]?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?(?![\w])", RegexOptions.Compiled);
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex OperatorWhitespacePattern = new(@"\s*(=|<>|<=|>=|<|>|\+|-|\*|/|,)\s*", RegexOptions.Compiled);
}
