//src/SlowQueryRadar.Core/Models/SnapshotStoreOptions.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Configures snapshot storage and retention.</summary>
public sealed class SnapshotStoreOptions
{
    /// <summary>Gets or sets SQLite connection string for radar storage.</summary>
    public string ConnectionString { get; set; } = "Data Source=radar.db";

    /// <summary>Gets or sets retention days for cleanup.</summary>
    public int RetentionDays { get; set; } = 30;
}
