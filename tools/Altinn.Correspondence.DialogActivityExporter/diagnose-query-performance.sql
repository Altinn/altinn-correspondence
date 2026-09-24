-- Diagnose Query Performance for Issue #1716 Export
-- Run this on the production database to understand what's happening

-- ============================================================
-- 1. Check table statistics freshness
-- ============================================================
SELECT 
    schemaname,
    tablename,
    last_analyze,
    last_autoanalyze,
    n_live_tup as live_rows,
    n_dead_tup as dead_rows
FROM pg_stat_user_tables
WHERE schemaname = 'correspondence'
  AND tablename IN (
      'A2Iss1716A2Events',
      'CorrespondenceStatuses',
      'ExternalReferences',
      'IdempotencyKeys',
      'A2Parties'
  )
ORDER BY tablename;

-- ============================================================
-- 2. Check index usage on A2Iss1716A2Events
-- ============================================================
SELECT 
    indexrelname as index_name,
    idx_scan as times_used,
    idx_tup_read as tuples_read,
    idx_tup_fetch as tuples_fetched
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND tablename = 'A2Iss1716A2Events'
ORDER BY idx_scan DESC;

-- ============================================================
-- 3. Get EXPLAIN for Status 4 query (adjust cursor as needed)
-- ============================================================
-- This is the query being used by the exporter for Status 4
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT DISTINCT
    er."ReferenceValue" AS DialogId,
    idcFetch."Id" AS DialogActivityId,
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    4 AS Status,
    'CorrespondenceOpened' AS ActivityType
FROM correspondence."A2Iss1716A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
    AND a2Events."StatusChanged" = stats."StatusChanged"
INNER JOIN correspondence."ExternalReferences" er
    ON a2Events."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcFetch
    ON a2Events."CorrespondenceId" = idcFetch."CorrespondenceId"
    AND idcFetch."StatusAction" = '3'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE a2Events."Status" = 4
  -- AND a2Events."CorrespondenceId" > 'REPLACE-WITH-ACTUAL-CURSOR-GUID'
ORDER BY stats."CorrespondenceId"
LIMIT 5000;

-- ============================================================
-- 4. Get EXPLAIN for Status 6 query
-- ============================================================
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT DISTINCT
    er."ReferenceValue" AS DialogId,
    idcConfirm."Id" AS DialogActivityId,
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    6 AS Status,
    'CorrespondenceConfirmed' AS ActivityType
FROM correspondence."A2Iss1716A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
    AND a2Events."StatusChanged" = stats."StatusChanged"
INNER JOIN correspondence."ExternalReferences" er
    ON a2Events."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcConfirm
    ON a2Events."CorrespondenceId" = idcConfirm."CorrespondenceId"
    AND idcConfirm."StatusAction" = '6'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE a2Events."Status" = 6
  -- AND a2Events."CorrespondenceId" > 'REPLACE-WITH-ACTUAL-CURSOR-GUID'
ORDER BY stats."CorrespondenceId"
LIMIT 5000;

-- ============================================================
-- 5. Check for locks and blocking
-- ============================================================
SELECT 
    pid,
    usename,
    application_name,
    state,
    query_start,
    state_change,
    wait_event_type,
    wait_event,
    LEFT(query, 100) as query_preview
FROM pg_stat_activity
WHERE datname = 'correspondence'
  AND state != 'idle'
  AND query NOT LIKE '%pg_stat_activity%'
ORDER BY query_start;

-- ============================================================
-- 6. Check table bloat (dead tuples affecting performance)
-- ============================================================
SELECT 
    schemaname,
    tablename,
    n_dead_tup as dead_tuples,
    n_live_tup as live_tuples,
    ROUND(100 * n_dead_tup / NULLIF(n_live_tup + n_dead_tup, 0), 2) as dead_tuple_pct
FROM pg_stat_user_tables
WHERE schemaname = 'correspondence'
  AND tablename IN (
      'A2Iss1716A2Events',
      'CorrespondenceStatuses',
      'ExternalReferences',
      'IdempotencyKeys',
      'A2Parties'
  )
ORDER BY dead_tuple_pct DESC NULLS LAST;

-- ============================================================
-- 7. Recommended actions based on findings
-- ============================================================
/*
Based on the results:

1. If last_analyze is old (> 24 hours):
   ANALYZE correspondence."A2Iss1716A2Events";
   ANALYZE correspondence."CorrespondenceStatuses";
   ANALYZE correspondence."ExternalReferences";
   ANALYZE correspondence."IdempotencyKeys";
   ANALYZE correspondence."A2Parties";

2. If dead_tuple_pct > 10%:
   VACUUM ANALYZE correspondence."A2Iss1716A2Events";

3. If EXPLAIN shows sequential scans instead of index scans:
   - Check if indexes exist
   - Check if statistics are up to date
   - Consider index tuning

4. If wait_event shows locking:
   - Database may be under heavy concurrent load
   - Consider running export during off-peak hours
*/
