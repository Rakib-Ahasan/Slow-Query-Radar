//src/SlowQueryRadar.Core/Models/CollectionSchedulerOptions.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Configures API background collection scheduling.</summary>
public sealed class CollectionSchedulerOptions
{
    /// <summary>Gets or sets interval minutes between collection runs.</summary>
    public int IntervalMinutes { get; set; } = 15;
}
