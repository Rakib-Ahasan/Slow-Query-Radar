//src/SlowQueryRadar.Profiler/Sinks/InMemoryPerformanceSink.cs
using System.Collections.Concurrent;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Profiler.Sinks;

/// <summary>Stores a bounded set of profiler entries for tests and diagnostics.</summary>
public sealed class InMemoryPerformanceSink : IPerformanceSink
{
    private const int MaximumEntries = 10_000;
    private readonly ConcurrentQueue<DapperLogEntry> _entries = new();

    /// <summary>Gets a snapshot of entries currently retained by the sink.</summary>
    public IReadOnlyList<DapperLogEntry> Entries => _entries.ToArray();

    /// <inheritdoc />
    public void Write(DapperLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Enqueue(entry);
        while (_entries.Count > MaximumEntries && _entries.TryDequeue(out _))
        {
        }
    }
}
