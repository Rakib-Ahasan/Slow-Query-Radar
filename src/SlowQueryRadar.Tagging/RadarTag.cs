//src/SlowQueryRadar.Tagging/RadarTag.cs
namespace SlowQueryRadar.Tagging;

/// <summary>Creates leading comment tags for Dapper command text.</summary>
public static class RadarTag
{
    /// <summary>Adds a sanitized feature, tenant, and caller tag before SQL text.</summary>
    /// <param name="sql">The SQL command text.</param>
    /// <param name="feature">The application feature name.</param>
    /// <param name="tenant">The tenant identifier.</param>
    /// <param name="caller">The application caller.</param>
    /// <returns>Tagged command text.</returns>
    public static string Add(string sql, string feature, string? tenant, string caller)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller);
        return $"/* feature:{Clean(feature)}; tenant:{Clean(tenant)}; caller:{Clean(caller)} */{Environment.NewLine}{sql}";
    }

    /// <summary>Adds a tag through an extension method suitable for Dapper command construction.</summary>
    public static string WithRadarTag(this string sql, string feature, string? tenant, string caller) => Add(sql, feature, tenant, caller);

    private static string Clean(string? value) => (value ?? string.Empty).Replace("*/", string.Empty, StringComparison.Ordinal).Replace(";", string.Empty, StringComparison.Ordinal).Trim();
}
