//src/SlowQueryRadar.Core/Models/RegressionDetectionOptions.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Configures regression detection windows and thresholds.</summary>
public sealed class RegressionDetectionOptions
{
    /// <summary>Gets or sets baseline lookback days.</summary>
    public int BaselineDays { get; set; } = 7;

    /// <summary>Gets or sets minimum relative increase for regression detection.</summary>
    public double RelativeIncreaseThreshold { get; set; } = 0.50;

    /// <summary>Gets or sets minimum absolute increase in ms for regression detection.</summary>
    public double AbsoluteIncreaseMsThreshold { get; set; } = 20;

    /// <summary>Gets or sets minimum executions for regression detection.</summary>
    public long MinimumExecutions { get; set; } = 20;
}
