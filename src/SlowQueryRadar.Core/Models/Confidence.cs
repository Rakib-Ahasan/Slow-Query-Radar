//src/SlowQueryRadar.Core/Models/Confidence.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Indicates how strongly the available evidence supports a finding.</summary>
public enum Confidence
{
    /// <summary>The finding is directly supported by explicit plan or syntax evidence.</summary>
    High,
    /// <summary>The finding is supported by strong but incomplete evidence.</summary>
    Medium,
    /// <summary>The finding is a heuristic requiring additional review.</summary>
    Low
}
