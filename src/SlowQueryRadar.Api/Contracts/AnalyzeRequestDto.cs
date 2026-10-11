//src/SlowQueryRadar.Api/Contracts/AnalyzeRequestDto.cs
namespace SlowQueryRadar.Api.Contracts;

/// <summary>Request to collect and analyze a slow query.</summary>
public sealed record AnalyzeRequestDto(string SqlText, int TopN, int LookbackHours);

/// <summary>Request options for benchmarking a cached query.</summary>
public sealed record BenchmarkRequestDto(int WarmupRuns = 1, int MeasuredRuns = 5);
