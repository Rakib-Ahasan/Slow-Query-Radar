//src/SlowQueryRadar.Analyzer/Rules/NPlus1Rule.cs
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Analyzer.Rules;

/// <summary>Represents a batch of recent Dapper log entries.</summary>
/// <param name="Entries">The recent log entries.</param>
public sealed record DapperLogBatch(IReadOnlyList<DapperLogEntry> Entries);

/// <summary>Reports repeated execution of one SQL hash in a short interval.</summary>
public sealed class NPlus1Rule : ISqlRule
{
    /// <inheritdoc />
    public string RuleId => "SQLR-007";

    /// <inheritdoc />
    public IEnumerable<Finding> Evaluate(SqlAnalysisContext context) => [];

    /// <summary>Evaluates recent logs for an N+1 execution pattern.</summary>
    /// <param name="batch">The recent Dapper logs.</param>
    /// <returns>Findings for hashes exceeding the execution threshold.</returns>
    public IEnumerable<Finding> Evaluate(DapperLogBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        foreach (var group in batch.Entries.GroupBy(entry => entry.SqlHash))
        {
            var ordered = group.OrderBy(entry => entry.TimestampUtc).ToArray();
            var start = 0;
            for (var end = 0; end < ordered.Length; end++)
            {
                while (ordered[end].TimestampUtc - ordered[start].TimestampUtc > TimeSpan.FromSeconds(60))
                {
                    start++;
                }

                if (end - start + 1 > 50)
                {
                    yield return new Finding(
                        RuleId,
                        Severity.High,
                        $"SQL hash {group.Key} executed more than 50 times within 60 seconds.",
                        $"Observed {end - start + 1} executions in a 60-second window.",
                        "Consider batching — the caller may be inside a loop. Options: Dapper QueryMultiple, WHERE IN (...), or a single join.",
                        0);
                    break;
                }
            }
        }
    }
}
