//src/SlowQueryRadar.Core/Models/RegressionSeverity.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Severity classification for detected regressions.</summary>
public enum RegressionSeverity
{
    /// <summary>Informational signal.</summary>
    Info,
    /// <summary>Warning-level regression.</summary>
    Warning,
    /// <summary>Critical regression requiring immediate action.</summary>
    Critical
}
