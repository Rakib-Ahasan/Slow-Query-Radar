//src/SlowQueryRadar.Profiler/ProfilerOptions.cs
namespace SlowQueryRadar.Profiler;

/// <summary>Configures Dapper profiler behavior.</summary>
public sealed class ProfilerOptions
{
    /// <summary>Gets or sets whether parameter values may be included in logs.</summary>
    public bool LogParameterValues { get; set; }

    /// <summary>Gets or sets the duration threshold in milliseconds for slow-query handling.</summary>
    public int SlowThresholdMs { get; set; } = 200;
}
