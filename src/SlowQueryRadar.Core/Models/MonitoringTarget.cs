//src/SlowQueryRadar.Core/Models/MonitoringTarget.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Defines one database target to monitor.</summary>
/// <param name="Name">Logical target name.</param>
/// <param name="Group">Optional target grouping label.</param>
/// <param name="ConnectionString">Target database connection string.</param>
/// <param name="Tags">Optional target tags.</param>
/// <param name="Enabled">Whether this target is enabled for collection.</param>
public sealed record MonitoringTarget(
    string Name,
    string? Group,
    string ConnectionString,
    IReadOnlyList<string> Tags,
    bool Enabled);
