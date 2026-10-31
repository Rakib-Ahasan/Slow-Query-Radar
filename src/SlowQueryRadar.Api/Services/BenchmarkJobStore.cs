//src/SlowQueryRadar.Api/Services/BenchmarkJobStore.cs
using System.Collections.Concurrent;
using SlowQueryRadar.Api.Contracts;
using SlowQueryRadar.Benchmark;

namespace SlowQueryRadar.Api.Services;

/// <summary>Stores API benchmark jobs in memory for the process lifetime.</summary>
public sealed class BenchmarkJobStore
{
    private readonly ConcurrentDictionary<Guid, BenchmarkJobDto> _jobs = new();

    /// <summary>Creates a pending job.</summary>
    public Guid Create() { var id = Guid.NewGuid(); _jobs[id] = new(id, "pending", null, null, null, null); return id; }
    /// <summary>Gets a job.</summary>
    public bool TryGet(Guid id, out BenchmarkJobDto job) => _jobs.TryGetValue(id, out job!);
    /// <summary>Updates a job.</summary>
    public void Set(BenchmarkJobDto job) => _jobs[job.Id] = job;
}
