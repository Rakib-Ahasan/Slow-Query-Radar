//src/SlowQueryRadar.Profiler/ConnectionExtensions.cs
using System.Data.Common;
using SlowQueryRadar.Profiler.Sinks;

namespace SlowQueryRadar.Profiler;

/// <summary>Provides extension methods for enabling Dapper profiling.</summary>
public static class ConnectionExtensions
{
    /// <summary>Gets the default sink used by the parameterless profiling extension.</summary>
    public static IPerformanceSink DefaultSink { get; } = new SerilogPerformanceSink();

    /// <summary>Wraps a connection with the default Serilog profiler sink.</summary>
    /// <param name="connection">The connection to wrap.</param>
    /// <returns>A profiled database connection.</returns>
    public static DbConnection Profile(this DbConnection connection) =>
        Profile(connection, DefaultSink);

    /// <summary>Wraps a connection with the supplied profiler sink.</summary>
    /// <param name="connection">The connection to wrap.</param>
    /// <param name="sink">The performance sink.</param>
    /// <returns>A profiled database connection.</returns>
    public static DbConnection Profile(this DbConnection connection, IPerformanceSink sink)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sink);
        return new ProfiledDbConnection(connection, sink);
    }
}
