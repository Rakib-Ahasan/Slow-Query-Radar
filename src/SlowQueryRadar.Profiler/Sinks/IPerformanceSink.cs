//src/SlowQueryRadar.Profiler/Sinks/IPerformanceSink.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Profiler.Sinks;

/// <summary>Receives profiler performance entries.</summary>
public interface IPerformanceSink
{
    /// <summary>Writes a profiler performance entry.</summary>
    /// <param name="entry">The performance entry.</param>
    void Write(DapperLogEntry entry);
}
