/* SlowQueryRadar */
SET LOCK_TIMEOUT 5000;
WITH PlanRows AS
(
	SELECT
		p.query_id AS QueryId,
		p.plan_id AS PlanId,
		CAST(p.is_forced_plan AS bit) AS IsForcedPlan,
		CAST(rs.count_executions AS bigint) AS ExecutionCount,
		CAST(rs.avg_duration AS float) AS AvgDurationUs,
		CAST(rs.stdev_duration AS float) AS StdevDurationUs,
		CAST(rs.max_duration AS float) AS MaxDurationUs,
		rs.last_execution_time AS LastExecutionTime
	FROM sys.query_store_plan AS p
	INNER JOIN sys.query_store_runtime_stats AS rs ON rs.plan_id = p.plan_id
	INNER JOIN sys.query_store_runtime_stats_interval AS rsi
		ON rsi.runtime_stats_interval_id = rs.runtime_stats_interval_id
	WHERE 1 = 1 /*QUERY_IDS_FILTER*/
	  AND rsi.end_time >= @since
	  AND rsi.start_time <= @windowEnd
	  AND rs.count_executions > 0
),
PlanTotals AS
(
	SELECT
		QueryId,
		PlanId,
		MAX(IsForcedPlan) AS IsForcedPlan,
		SUM(ExecutionCount) AS TotalExecutions,
		SUM(AvgDurationUs * ExecutionCount) AS TotalDurationUs,
		SUM(ExecutionCount * (POWER(COALESCE(StdevDurationUs, 0), 2) + POWER(COALESCE(AvgDurationUs, 0), 2))) AS SumSqDuration,
		MAX(MaxDurationUs) AS MaxDurationUs,
		MAX(LastExecutionTime) AS LastExecutionTime
	FROM PlanRows
	GROUP BY QueryId, PlanId
)
SELECT /* SlowQueryRadar */
	QueryId,
	PlanId,
	IsForcedPlan,
	LastExecutionTime,
	CAST(TotalExecutions AS bigint) AS ExecutionCount,
	CAST(TotalDurationUs / NULLIF(TotalExecutions, 0) / 1000.0 AS float) AS AvgDurationMs,
	CAST(MaxDurationUs / 1000.0 AS float) AS MaxDurationMs,
	CAST(SQRT(CASE WHEN TotalExecutions > 1
		THEN CASE WHEN SumSqDuration / TotalExecutions - POWER(TotalDurationUs / TotalExecutions, 2) < 0 THEN 0
			ELSE SumSqDuration / TotalExecutions - POWER(TotalDurationUs / TotalExecutions, 2) END
		ELSE 0 END) / 1000.0 AS float) AS StandardDeviationMs
FROM PlanTotals
ORDER BY QueryId, PlanId;
