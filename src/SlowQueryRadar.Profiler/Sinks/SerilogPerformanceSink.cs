//src/SlowQueryRadar.Profiler/Sinks/SerilogPerformanceSink.cs
using Serilog;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Profiler.Sinks;

/// <summary>Writes profiler entries through Serilog structured properties.</summary>
public sealed class SerilogPerformanceSink : IPerformanceSink
{
    /// <inheritdoc />
    public void Write(DapperLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Log.Information(
            "Dapper query {SqlHash} took {DurationMs} ms. Success: {Success}. Caller: {CallerMemberName}",
            entry.SqlHash,
            entry.DurationMs,
            entry.Success,
            entry.CallerMemberName);
    }
}
