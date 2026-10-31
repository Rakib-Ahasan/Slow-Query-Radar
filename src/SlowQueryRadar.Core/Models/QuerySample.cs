//src/SlowQueryRadar.Core/Models/QuerySample.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Represents one persisted query sample for a run and target.</summary>
public sealed record QuerySample(
    long RunId,
    string Target,
    string QueryHash,
    string NormalizedHash,
    string SqlText,
    long Executions,
    double AvgMs,
    double TotalMs,
    double MaxMs,
    double StdDevMs,
    double Cpu,
    long LogicalReads,
    int PlanCount,
    DateTimeOffset CapturedUtc);
