//src/SlowQueryRadar.Core/Models/CollectOptions.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Provides validated inputs for Query Store collection.</summary>
public sealed class CollectOptions
{
    /// <summary>Gets or sets the maximum number of rows to return.</summary>
    public int Top { get; set; } = 20;

    /// <summary>Gets or sets the lookback window ending at current UTC time.</summary>
    public TimeSpan Lookback { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Gets or sets the ranking metric for result ordering.</summary>
    public RankBy RankBy { get; set; } = RankBy.TotalDuration;

    /// <summary>Gets or sets the minimum total executions threshold.</summary>
    public long MinExecutions { get; set; } = 2;

    /// <summary>Gets or sets the optional minimum average duration filter in milliseconds.</summary>
    public double? MinAvgDurationMs { get; set; }

    /// <summary>Gets or sets a value indicating whether known maintenance/noise queries are excluded.</summary>
    public bool ExcludeNoise { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether plan XML should be included when available.</summary>
    public bool IncludePlanXml { get; set; }

    /// <summary>Validates option ranges.</summary>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Top);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Lookback.TotalSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(MinExecutions);
        if (MinAvgDurationMs is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinAvgDurationMs));
        }
    }
}
