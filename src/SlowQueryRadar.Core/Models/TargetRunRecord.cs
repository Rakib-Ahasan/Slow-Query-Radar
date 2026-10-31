//src/SlowQueryRadar.Core/Models/TargetRunRecord.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Represents one target execution status for a persisted run.</summary>
public sealed record TargetRunRecord(
    long RunId,
    string Target,
    string Status,
    string? Error,
    DateTimeOffset StartedUtc,
    double DurationMs);
