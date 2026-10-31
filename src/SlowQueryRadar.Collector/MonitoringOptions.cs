//src/SlowQueryRadar.Collector/MonitoringOptions.cs
namespace SlowQueryRadar.Collector;

/// <summary>Defines monitoring options for multi-target collection.</summary>
public sealed class MonitoringOptions
{
    /// <summary>Gets or sets scheduler interval minutes.</summary>
    public int IntervalMinutes { get; set; } = 15;

    /// <summary>Gets or sets maximum parallel target collections.</summary>
    public int MaxParallelTargets { get; set; } = 4;

    /// <summary>Gets or sets per-target timeout in seconds.</summary>
    public int PerTargetTimeoutSeconds { get; set; } = 60;

    /// <summary>Gets or sets configured monitoring targets.</summary>
    public List<MonitoringTargetOptions> Targets { get; set; } = [];
}

/// <summary>Represents one configured monitoring target.</summary>
public sealed class MonitoringTargetOptions
{
    /// <summary>Gets or sets target name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets target group.</summary>
    public string? Group { get; set; }

    /// <summary>Gets or sets target connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets target tags.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Gets or sets whether this target is enabled.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>Defines discovery options for building targets from server metadata.</summary>
public sealed class DiscoveryOptions
{
    /// <summary>Gets or sets whether discovery is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the server-level connection string used for discovery.</summary>
    public string ServerConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets the database LIKE filter for discovery.</summary>
    public string DatabaseNameLike { get; set; } = "Tenant_%";

    /// <summary>Gets or sets excluded database names.</summary>
    public List<string> Exclude { get; set; } = ["master", "tempdb", "model", "msdb"];
}
