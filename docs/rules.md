# Analysis rules

All findings include concrete evidence and are advisory. Slow Query Radar never applies SQL changes automatically. Missing-index output is a copy-paste suggestion only and must be reviewed before applying.

## Execution-plan rules

| ID | Rule | Rationale | False-positive notes |
|---|---|---|---|
| PLAN-001 | Large filtered table or clustered-index scan | A scan over more than 10,000 estimated rows with a predicate may consume substantial IO and CPU. | A scan can be optimal for a large portion of a table, especially when the predicate is not selective. |
| PLAN-002 | High-cost key lookup | Repeated lookups can create random IO and high CPU. | A lookup may be appropriate for genuinely selective access with low execution count. |
| PLAN-003 | Plan-affecting implicit conversion | Converting an indexed expression can prevent an efficient seek. | Some conversions are harmless or occur on a small input; verify the operator and data types. |
| PLAN-004 | Missing index | SQL Server identified a possible access path from the MissingIndexes plan element. | Missing-index recommendations do not account for write cost, existing indexes, or workload overlap. Review before applying. |
| PLAN-005 | Spill or excessive memory grant | Spills add tempdb IO; oversized grants reduce concurrency. | Runtime memory information may be absent from estimated plans, and grants can be intentional for concurrency-sensitive workloads. |
| PLAN-006 | Cardinality mismatch | A tenfold or greater estimate/actual difference can lead to unsuitable joins, grants, or access paths. | Runtime row counts depend on parameter values and execution timing. |
| PLAN-007 | Parallelism skew or large sort | Uneven parallel work and sorting large rowsets can limit scalability. | Ordering may be required, and skew can be data-dependent. |

## SQL-text rules

| ID | Rule | Rationale | False-positive notes |
|---|---|---|---|
| SQL-001 | SELECT * | Unneeded columns increase network and IO costs and make covering indexes harder. | Some diagnostic or ad-hoc queries intentionally return all columns. |
| SQL-002 | Non-SARGable function or leading wildcard | Functions on filtered columns and leading wildcard LIKE patterns commonly prevent seeks. | Computed or specialized indexes and full-text strategies may make the pattern acceptable. |
| SQL-003 | Long OR chain | Long disjunctions can produce unstable estimates and repeated predicate work. | OR predicates may be clearer or faster than an alternative for a small number of branches. |
| SQL-004 | Possible scalar UDF | Scalar UDFs can run once per row and inhibit optimizer transformations. | The detection is conservative text-based evidence and may include user-defined table or CLR functions. |
| SQL-005 | Cursor or WHILE loop | Row-by-row control flow often scales worse than set-based operations. | Procedural control flow can be appropriate for external side effects or complex state transitions. |
| SQL-006 | NOLOCK | NOLOCK permits dirty and otherwise inconsistent reads. | It may be deliberately selected for non-critical approximate reporting, but that choice requires review. |

## Query Store rules

| ID | Rule | Rationale | False-positive notes |
|---|---|---|---|
| QS-001 | Parameter-sniffing suspect | Standard deviation at least twice the average with more than one plan indicates duration variance. | Variance can result from data size, blocking, cache state, or other environmental factors. |
| QS-002 | Death by a thousand cuts | At least 10,000 executions with at least 5 ms average duration can consume significant aggregate capacity. | High frequency may be expected for healthy polling or telemetry workloads. |
| QS-003 | Physical-read pressure | At least 1,000,000 physical reads can increase storage latency and cache eviction. | The threshold is workload-dependent; inspect the time window and storage baseline. |

## Malformed plans

Plan rules return an `AnalysisFailed` low-severity informational finding when showplan XML is malformed or cannot be parsed. The original XML is not executed or modified.
