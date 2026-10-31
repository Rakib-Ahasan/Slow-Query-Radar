//src/SlowQueryRadar.Collector/RadarTagParser.cs
using System.Text.RegularExpressions;

namespace SlowQueryRadar.Collector;

/// <summary>Parses the optional leading Slow Query Radar caller tag.</summary>
public sealed record RadarTagInfo(string? FeatureName, string? TenantId, string? Caller);

/// <summary>Extracts feature, tenant, and caller metadata from leading SQL comments.</summary>
public static partial class RadarTagParser
{
    /// <summary>Parses a leading `/* feature:...; tenant:...; caller:... */` tag.</summary>
    /// <param name="sql">The SQL text.</param>
    /// <returns>Parsed tag metadata, or empty metadata when absent.</returns>
    public static RadarTagInfo Parse(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new RadarTagInfo(null, null, null);
        var match = LeadingTagRegex().Match(sql);
        if (!match.Success)
            return new RadarTagInfo(null, null, null);
        var values = match.Groups["values"].Value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
        return new RadarTagInfo(Get(values, "feature"), Get(values, "tenant"), Get(values, "caller"));
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out var value) && value.Length > 0 ? value : null;

    [GeneratedRegex(@"^\s*/\*\s*(?<values>feature\s*:[\s\S]*?)\*/", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingTagRegex();
}
