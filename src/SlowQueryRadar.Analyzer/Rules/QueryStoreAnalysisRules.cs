//src/SlowQueryRadar.Analyzer/Rules/QueryStoreAnalysisRules.cs
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Analyzer.Rules;

/// <summary>Reports Query Store variance consistent with parameter sensitivity.</summary>
public sealed class ParameterSniffingAnalysisRule : IAnalysisRule
{
    /// <inheritdoc />
    public string Id => "QS-001";
    /// <inheritdoc />
    public Severity Severity => Severity.High;
    /// <inheritdoc />
    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var snapshot = context.Snapshot;
        if (snapshot.PlanCount > 1 && snapshot.AvgDurationMs > 0 && snapshot.StandardDeviationMs >= snapshot.AvgDurationMs * 2)
            yield return new Finding(Id, Severity, "High duration variance across multiple plans", $"Average {snapshot.AvgDurationMs:F2} ms; standard deviation {snapshot.StandardDeviationMs:F2} ms; plan count {snapshot.PlanCount}", "Compare plans by parameter shape and evaluate Query Store plan forcing only through the normal review process.", 0)
            {
                WhyItMatters = "A high standard deviation across multiple plans can indicate parameter sensitivity or unstable plans.",
                Confidence = Confidence.Medium,
                DocsLink = "docs/rules.md"
            };
    }
}

/// <summary>Reports very frequent moderate-duration executions.</summary>
public sealed class DeathByThousandCutsAnalysisRule : IAnalysisRule
{
    /// <inheritdoc />
    public string Id => "QS-002";
    /// <inheritdoc />
    public Severity Severity => Severity.High;
    /// <inheritdoc />
    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var snapshot = context.Snapshot;
        var totalMs = snapshot.TotalExecutions * snapshot.AvgDurationMs;
        if (snapshot.TotalExecutions >= 10_000 && snapshot.AvgDurationMs >= 5 && totalMs >= 100_000)
            yield return new Finding(Id, Severity, "Death by a thousand cuts execution pattern", $"Executions {snapshot.TotalExecutions:N0}; average duration {snapshot.AvgDurationMs:F2} ms; estimated cumulative duration {totalMs:N0} ms", "Look for chatty application or N+1 call patterns and batch work where semantics allow.", 0)
            {
                WhyItMatters = "Aggregate cost is driven by frequency as well as latency per execution.",
                Confidence = Confidence.Medium,
                DocsLink = "docs/rules.md"
            };
    }
}

/// <summary>Reports high physical reads that may create IO and cache pressure.</summary>
public sealed class PhysicalReadPressureAnalysisRule : IAnalysisRule
{
    /// <inheritdoc />
    public string Id => "QS-003";
    /// <inheritdoc />
    public Severity Severity => Severity.High;
    /// <inheritdoc />
    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var snapshot = context.Snapshot;
        if (snapshot.TotalPhysicalReads >= 1_000_000)
            yield return new Finding(Id, Severity, "High physical-read pressure", $"Total physical reads {snapshot.TotalPhysicalReads:N0}; executions {snapshot.TotalExecutions:N0}; average logical reads {snapshot.AvgLogicalReads:N0}", "Inspect access paths, selectivity, and memory pressure before changing indexes or configuration.", 0)
            {
                WhyItMatters = "Repeated physical IO can increase latency for this and unrelated workloads.",
                Confidence = Confidence.Medium,
                DocsLink = "docs/rules.md"
            };
    }
}
