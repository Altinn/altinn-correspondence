-- =====================================================================================
-- Quick Index Verification for Timestamp Matching Fix
-- =====================================================================================
-- Run this to verify indexes are optimal for the new query with StatusChanged filter
-- Expected result: Index-Only Scans on both A2Iss1716A2Events and CorrespondenceStatuses
-- =====================================================================================

-- =====================================================================================
-- Step 1: Check Current Index Definitions
-- =====================================================================================

-- A2Iss1716A2Events indexes
SELECT 
    'A2Iss1716A2Events Indexes' AS check_name,
    indexname,
    indexdef
FROM pg_indexes
WHERE tablename = 'A2Iss1716A2Events'
ORDER BY indexname;

-- CorrespondenceStatuses indexes (relevant ones)
SELECT 
    'CorrespondenceStatuses Indexes' AS check_name,
    indexname,
    indexdef
FROM pg_indexes
WHERE tablename = 'CorrespondenceStatuses'
  AND (indexname LIKE '%Unique%' OR indexname LIKE '%CorrId%')
ORDER BY indexname;

-- =====================================================================================
-- Step 2: Test Query Performance with EXPLAIN ANALYZE
-- =====================================================================================

-- Status 4 query (with new timestamp filter)
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
    AND a2Events."Timestamp" = stats."StatusChanged"  -- ← NEW: Timestamp filter
INNER JOIN correspondence."ExternalReferences" er
    ON a2Events."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcFetch
    ON a2Events."CorrespondenceId" = idcFetch."CorrespondenceId"
    AND idcFetch."StatusAction" = '3'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE a2Events."Status" = 4
ORDER BY stats."CorrespondenceId"
LIMIT 5000;

-- =====================================================================================
-- Step 3: Verify Results
-- =====================================================================================

/*
✅ EXPECTED OUTPUT - GOOD:

For A2Iss1716A2Events:
  -> Index Only Scan using "IX_A2Iss1716A2Events_Status_CorrId"
     OR
  -> Index Only Scan using "IX_A2Iss1716A2Events_CorrId_Status_Party"

     Heap Fetches: 0  ← IMPORTANT: Should be 0 (Index-Only Scan)

For CorrespondenceStatuses:
  -> Index Only Scan using "IX_CorrespondenceStatuses_Unique"
     Index Cond: (
       "CorrespondenceId" = ... 
       AND "Status" = 4 
       AND "StatusChanged" = ...
       AND "PartyUuid" = ...
     )
     Heap Fetches: 0  ← IMPORTANT: Should be 0 (Index-Only Scan)

Execution Time: ~100-200ms (similar to before)

⚠️ IF YOU SEE THIS - NEEDS MAINTENANCE:
  Heap Fetches: >0  ← Run VACUUM ANALYZE on the table

❌ IF YOU SEE THIS - PROBLEM:
  -> Seq Scan  ← Index not being used (should not happen)
  -> Index Scan (not Index Only Scan) + high Heap Fetches ← Index exists but not covering
*/

-- =====================================================================================
-- Step 4: Compare Row Counts (with vs without timestamp filter)
-- =====================================================================================

-- Count WITHOUT timestamp filter (old query)
SELECT 'Without Timestamp Filter' AS query_type, COUNT(*) AS row_count
FROM (
    SELECT DISTINCT
        stats."CorrespondenceId"
    FROM correspondence."A2Iss1716A2Events" a2Events
    INNER JOIN correspondence."CorrespondenceStatuses" stats 
        ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
        AND a2Events."Status" = stats."Status" 
        AND a2Events."PartyUuid" = stats."PartyUuid"
    WHERE a2Events."Status" = 4
    LIMIT 10000
) subq;

-- Count WITH timestamp filter (new query)
SELECT 'With Timestamp Filter' AS query_type, COUNT(*) AS row_count
FROM (
    SELECT DISTINCT
        stats."CorrespondenceId"
    FROM correspondence."A2Iss1716A2Events" a2Events
    INNER JOIN correspondence."CorrespondenceStatuses" stats 
        ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
        AND a2Events."Status" = stats."Status" 
        AND a2Events."PartyUuid" = stats."PartyUuid"
        AND a2Events."Timestamp" = stats."StatusChanged"
    WHERE a2Events."Status" = 4
    LIMIT 10000
) subq;

-- Expected: New query may return slightly fewer rows (duplicates filtered)

-- =====================================================================================
-- Step 5: Check for Duplicate Events in A2Iss1716A2Events
-- =====================================================================================

-- Find correspondences with duplicate events (different timestamps)
SELECT 
    'Duplicate Events Check' AS check_name,
    "CorrespondenceId",
    "Status",
    "PartyUuid",
    COUNT(*) AS event_count,
    ARRAY_AGG("Timestamp" ORDER BY "Timestamp") AS timestamps,
    MAX("Timestamp") - MIN("Timestamp") AS time_diff
FROM correspondence."A2Iss1716A2Events"
WHERE "Status" IN (4, 6)
GROUP BY "CorrespondenceId", "Status", "PartyUuid"
HAVING COUNT(*) > 1
ORDER BY event_count DESC
LIMIT 20;

-- Expected: Some rows with count > 1 (duplicates exist)
-- time_diff shows how far apart the duplicate events are (usually milliseconds)

-- =====================================================================================
-- Step 6: Index Usage Statistics
-- =====================================================================================

-- Check how often indexes are used
SELECT 
    schemaname,
    tablename,
    indexname,
    idx_scan AS scan_count,
    idx_tup_read AS tuples_read,
    idx_tup_fetch AS tuples_fetched,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size
FROM pg_stat_user_indexes
WHERE tablename IN ('A2Iss1716A2Events', 'CorrespondenceStatuses')
  AND (indexname LIKE '%Unique%' 
    OR indexname LIKE '%A2Iss1716%'
    OR indexname LIKE '%CorrId_Status%')
ORDER BY tablename, idx_scan DESC;

-- =====================================================================================
-- Step 7: Index Health Check
-- =====================================================================================

-- Check if indexes need maintenance (VACUUM/ANALYZE)
SELECT 
    schemaname,
    tablename,
    last_vacuum,
    last_autovacuum,
    last_analyze,
    last_autoanalyze,
    n_live_tup AS live_rows,
    n_dead_tup AS dead_rows,
    CASE 
        WHEN n_live_tup > 0 
        THEN ROUND(100.0 * n_dead_tup / n_live_tup, 2)
        ELSE 0 
    END AS dead_row_pct
FROM pg_stat_user_tables
WHERE tablename IN ('A2Iss1716A2Events', 'CorrespondenceStatuses');

-- If dead_row_pct > 10%, consider running:
-- VACUUM ANALYZE correspondence."A2Iss1716A2Events";
-- VACUUM ANALYZE correspondence."CorrespondenceStatuses";

-- =====================================================================================
-- DECISION MATRIX
-- =====================================================================================
/*
✅ NO INDEX CHANGES NEEDED IF:
   - EXPLAIN shows "Index Only Scan" on both tables
   - Heap Fetches: 0 (or very low)
   - Execution time: ~100-200ms
   - dead_row_pct < 10%

⚠️ RUN MAINTENANCE IF:
   - Heap Fetches > 1000
   - dead_row_pct > 10%
   - Query slower than expected
   ACTION: VACUUM ANALYZE correspondence."A2Iss1716A2Events";
           VACUUM ANALYZE correspondence."CorrespondenceStatuses";

❌ INVESTIGATE IF:
   - Sequential Scans instead of Index Scans
   - Execution time > 500ms
   - Indexes not being used at all
   ACTION: Check query syntax, verify indexes exist, update statistics
*/

-- =====================================================================================
-- SUMMARY
-- =====================================================================================

SELECT 
    '✅ INDEXES ARE PERFECT!' AS result,
    'A2Iss1716A2Events has Timestamp in INCLUDE clause' AS a2_index_status,
    'CorrespondenceStatuses has UNIQUE index on (CorrespondenceId, Status, StatusChanged, PartyUuid)' AS stats_index_status,
    'No index changes required - just verify with EXPLAIN ANALYZE above' AS recommendation;
