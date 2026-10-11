//src/SlowQueryRadar.Benchmark/BenchmarkOptions.cs
using Microsoft.Data.SqlClient;

namespace SlowQueryRadar.Benchmark;

/// <summary>Configures the explicitly approved benchmark connection.</summary>
public sealed class BenchmarkOptions
{
    /// <summary>Gets or sets the named BenchmarkConnection connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets the explicit environment name.</summary>
    public string Environment { get; set; } = "Dev";

    /// <summary>Gets or sets server names that may never be benchmarked.</summary>
    public IReadOnlyList<string> BlockedServerNames { get; set; } = Array.Empty<string>();

    /// <summary>Validates the explicit benchmark safety configuration.</summary>
    public void Validate()
    {
        if (!string.Equals(Environment, "Dev", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Environment, "Staging", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Environment, "Prod", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Benchmark Environment must be Dev or Staging.");
        }

        if (string.Equals(Environment, "Prod", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Benchmarking is disabled for the configured environment.");
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException("BenchmarkConnection is required.");
        }

        var serverName = new SqlConnectionStringBuilder(ConnectionString).DataSource.Trim();
        if (BlockedServerNames.Any(blocked =>
                string.Equals(NormalizeServerName(serverName), NormalizeServerName(blocked), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("The configured benchmark server is blocked.");
        }
    }

    private static string NormalizeServerName(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }

        var commaIndex = normalized.IndexOf(',', StringComparison.Ordinal);
        return commaIndex >= 0 ? normalized[..commaIndex] : normalized;
    }
}
