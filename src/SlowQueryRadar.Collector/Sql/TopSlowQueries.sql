/* SlowQueryRadar */
SET LOCK_TIMEOUT 5000;

WITH RuntimeRows AS
(
	SELECT
		q.query_id AS QueryId,
		p.plan_id AS PlanId,
		CAST(rs.count_executions AS bigint) AS ExecutionCount,
		CAST(rs.avg_duration AS float) AS AvgDurationUs,
		CAST(rs.stdev_duration AS float) AS StdevDurationUs,
		CAST(rs.min_duration AS float) AS MinDurationUs,
		CAST(rs.max_duration AS float) AS MaxDurationUs,
		CAST(rs.avg_cpu_time AS float) AS AvgCpuUs,
		CAST(rs.avg_logical_io_reads AS float) AS AvgLogicalReads,
		CAST(rs.avg_physical_io_reads AS float) AS AvgPhysicalReads,
		CAST(rs.avg_logical_io_writes AS float) AS AvgLogicalWrites,
		CAST(rs.avg_rowcount AS float) AS AvgRowcount,
		CAST(rs.avg_query_max_used_memory AS float) AS AvgQueryMaxUsedMemory,
		rs.last_execution_time AS LastExecutionTime
	FROM sys.query_store_query AS q
	INNER JOIN sys.query_store_plan AS p ON p.query_id = q.query_id
	INNER JOIN sys.query_store_runtime_stats AS rs ON rs.plan_id = p.plan_id
	INNER JOIN sys.query_store_runtime_stats_interval AS rsi
		ON rsi.runtime_stats_interval_id = rs.runtime_stats_interval_id
	WHERE rsi.end_time >= @since
	  AND rsi.start_time <= @windowEnd
	  AND rs.count_executions > 0
),
Totals AS
(
	SELECT
		QueryId,
		SUM(ExecutionCount) AS TotalExecutions,
		SUM(AvgDurationUs * ExecutionCount) AS TotalDurationUs,
		SUM(AvgCpuUs * ExecutionCount) AS TotalCpuUs,
		SUM(AvgLogicalReads * ExecutionCount) AS TotalLogicalReads,
		SUM(AvgPhysicalReads * ExecutionCount) AS TotalPhysicalReads,
		SUM(AvgLogicalWrites * ExecutionCount) AS TotalLogicalWrites,
		SUM(AvgRowcount * ExecutionCount) AS TotalRows,
		SUM(AvgQueryMaxUsedMemory * ExecutionCount) AS TotalQueryMaxUsedMemory,
		-- Pooled variance uses E[x^2] - mean^2, where E[x^2] is built from
		-- each interval's count * (stdev^2 + average^2).
		SUM(ExecutionCount * (POWER(COALESCE(StdevDurationUs, 0), 2) + POWER(COALESCE(AvgDurationUs, 0), 2))) AS SumSqDuration,
		MIN(MinDurationUs) AS MinDurationUs,
		MAX(MaxDurationUs) AS MaxDurationUs,
		COUNT(DISTINCT PlanId) AS PlanCount,
		MAX(LastExecutionTime) AS LastExecutionTime
	FROM RuntimeRows
	GROUP BY QueryId
	HAVING SUM(ExecutionCount) >= @minExecutions
),
Metrics AS
(
	SELECT
		QueryId,
		TotalExecutions,
		TotalDurationUs,
		TotalDurationUs / NULLIF(TotalExecutions, 0) / 1000.0 AS AvgDurationMs,
		TotalDurationUs / 1000.0 AS TotalDurationMs,
		MinDurationUs / 1000.0 AS MinDurationMs,
		MaxDurationUs / 1000.0 AS MaxDurationMs,
		TotalCpuUs / NULLIF(TotalExecutions, 0) / 1000.0 AS AvgCpuMs,
		TotalCpuUs / 1000.0 AS TotalCpuMs,
		TotalLogicalReads / NULLIF(TotalExecutions, 0) AS AvgLogicalReads,
		TotalLogicalReads,
		TotalPhysicalReads,
		TotalLogicalWrites,
		TotalRows,
		TotalQueryMaxUsedMemory,
		PlanCount,
		LastExecutionTime,
		CASE WHEN TotalExecutions > 1
			THEN SumSqDuration / TotalExecutions - POWER(TotalDurationUs / TotalExecutions, 2)
			ELSE 0 END AS VarianceUs2
	FROM Totals
),
FinalMetrics AS
(
	SELECT *, SQRT(CASE WHEN VarianceUs2 < 0 THEN 0 ELSE VarianceUs2 END) / 1000.0 AS StandardDeviationMs
	FROM Metrics
)
SELECT TOP (@topN)
	m.QueryId,
	CONVERT(varchar(18), q.query_hash, 1) AS QueryHash,
	q.object_id AS ObjectId,
	CASE WHEN q.object_id IS NULL THEN NULL ELSE OBJECT_SCHEMA_NAME(q.object_id) + N'.' + OBJECT_NAME(q.object_id) END AS ObjectName,
	qt.query_sql_text AS SqlText,
	m.PlanCount,
	m.TotalExecutions,
	m.AvgDurationMs,
	m.TotalDurationMs,
	m.MinDurationMs,
	m.MaxDurationMs,
	m.StandardDeviationMs,
	m.TotalCpuMs,
	m.AvgCpuMs,
	m.TotalLogicalReads,
	m.AvgLogicalReads,
	m.TotalPhysicalReads,
	m.TotalLogicalWrites,
	m.TotalRows,
	m.TotalQueryMaxUsedMemory,
	m.LastExecutionTime,
	p.last_compile_start_time AS LastCompileStartTime
FROM FinalMetrics AS m
INNER JOIN sys.query_store_query AS q ON q.query_id = m.QueryId
INNER JOIN sys.query_store_query_text AS qt ON qt.query_text_id = q.query_text_id
OUTER APPLY (SELECT MAX(last_compile_start_time) AS last_compile_start_time FROM sys.query_store_plan WHERE query_id = m.QueryId) AS p
WHERE (@excludeNoise = 0 OR (
	q.is_internal_query = 0
	AND qt.query_sql_text NOT LIKE '%sys.query_store_%'
	AND qt.query_sql_text NOT LIKE '%sys.dm_%'
	AND qt.query_sql_text NOT LIKE '%UPDATE STATISTICS%'
	AND qt.query_sql_text NOT LIKE '%ALTER INDEX%'
	AND qt.query_sql_text NOT LIKE '%DBCC %'
	AND qt.query_sql_text NOT LIKE '%BACKUP %'
	AND qt.query_sql_text NOT LIKE '%sp_whoisactive%'
	AND qt.query_sql_text NOT LIKE '%SlowQueryRadar%'
	AND qt.query_sql_text NOT LIKE '%sys.database_query_store_options%'
	AND qt.query_sql_text NOT LIKE '%@_msparam%'))
  AND (@minAvgDurationMs IS NULL OR m.AvgDurationMs >= @minAvgDurationMs)
/*ORDER_BY*/;