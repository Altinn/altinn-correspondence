-- =====================================================================================
-- EMERGENCY DIAGNOSIS - Slow Query on A2Iss1951A2Events
-- =====================================================================================
-- Query taking 15+ minutes instead of expected 30-100ms
-- Need to diagnose why indexes aren't working
-- =====================================================================================

-- =====================================================================================
-- STEP 1: Check if Indexes Actually Exist and Are Valid
-- =====================================================================================

SELECT 
    schemaname,
    relname AS tablename,
    indexrelname AS indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size,
    idx_scan AS times_used,
    indisvalid AS is_valid,
    indisready AS is_ready,
    indislive AS is_live
FROM pg_stat_user_indexes
JOIN pg_index ON indexrelid = pg_stat_user_indexes.indexrelid
WHERE schemaname = 'correspondence'
  AND relname = 'A2Iss1951A2Events'
ORDER BY indexrelname;

-- Expected: 2 indexes both with is_valid = true

-- =====================================================================================
-- STEP 2: Check Table Statistics (CRITICAL)
-- =====================================================================================

SELECT 
    schemaname,
    relname,
    n_live_tup AS live_rows,
    n_dead_tup AS dead_rows,
    ROUND(100.0 * n_dead_tup / NULLIF(n_live_tup, 0), 2) AS dead_pct,
    last_vacuum,
    last_autovacuum,
    last_analyze,
    last_autoanalyze
FROM pg_stat_user_tables
WHERE schemaname = 'correspondence'
  AND relname = 'A2Iss1951A2Events';

-- If last_analyze is NULL or old, RUN THIS:
ANALYZE correspondence."A2Iss1951A2Events";

-- =====================================================================================
-- STEP 3: Test Simple Query on Helper Table (Should be FAST)
-- =====================================================================================

-- Test 1: Simple count on helper table (should be instant from stats)
EXPLAIN (ANALYZE, BUFFERS)
SELECT COUNT(*) 
FROM correspondence."A2Iss1951A2Events"
WHERE "Status" = 4;

-- Expected: < 1 second, uses index

-- Test 2: Simple limit query on helper table
EXPLAIN (ANALYZE, BUFFERS)
SELECT "CorrespondenceId", "Status", "PartyUuid", "Timestamp"
FROM correspondence."A2Iss1951A2Events"
WHERE "Status" = 4
LIMIT 5000;

-- Expected: < 100ms, uses index scan

-- =====================================================================================
-- STEP 4: Check Join Target Tables
-- =====================================================================================

-- The slow query joins to 5 other tables. Check their sizes and indexes:

-- Check CorrespondenceStatuses (1.94 BILLION rows!)
SELECT 
    'CorrespondenceStatuses' AS table_name,
    pg_size_pretty(pg_total_relation_size('correspondence."CorrespondenceStatuses"')) AS total_size,
    n_live_tup AS approx_rows,
    last_analyze
FROM pg_stat_user_tables
WHERE schemaname = 'correspondence'
  AND relname = 'CorrespondenceStatuses';

-- Check indexes on CorrespondenceStatuses
SELECT 
    indexrelname AS indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS size,
    idx_scan AS times_used
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND relname = 'CorrespondenceStatuses'
ORDER BY pg_relation_size(indexrelid) DESC;

-- =====================================================================================
-- STEP 5: Simplified Test Query (Diagnose Join Performance)
-- =====================================================================================

-- Test just helper table + CorrespondenceStatuses join
EXPLAIN (ANALYZE, BUFFERS)
SELECT COUNT(*)
FROM correspondence."A2Iss1951A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
    AND a2Events."Timestamp" = stats."StatusChanged"
WHERE a2Events."Status" = 4
LIMIT 5000;

-- This should reveal if the problem is in the first join

-- =====================================================================================
-- LIKELY PROBLEMS AND SOLUTIONS
-- =====================================================================================

/*
PROBLEM 1: Index not being used (planner choosing seq scan)
CAUSE: Missing ANALYZE after cleanup
SOLUTION:
    ANALYZE correspondence."A2Iss1951A2Events";
    Re-run query

PROBLEM 2: Join condition too restrictive
CAUSE: Timestamp equality (a2Events."Timestamp" = stats."StatusChanged") 
       may not match due to precision differences
SOLUTION:
    Remove timestamp from join condition:

    INNER JOIN correspondence."CorrespondenceStatuses" stats 
        ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
        AND a2Events."Status" = stats."Status" 
        AND a2Events."PartyUuid" = stats."PartyUuid"
        -- Remove: AND a2Events."Timestamp" = stats."StatusChanged"

PROBLEM 3: CorrespondenceStatuses table needs index
CAUSE: 1.94B row table without proper index for this join
SOLUTION:
    Need index on CorrespondenceStatuses (CorrespondenceId, Status, PartyUuid)
    This is a SEPARATE issue from helper table optimization

PROBLEM 4: A2Parties Recipient filter is slow
CAUSE: corr."Recipient" <> ap."RecipientUrn" inequality join
SOLUTION:
    Check if A2Parties.RecipientUrn is indexed
    This filter was identified as problematic in Issue #1716 too

PROBLEM 5: DISTINCT is forcing slow merge
CAUSE: DISTINCT on large result set before LIMIT
SOLUTION:
    Use subquery pattern from Issue #1716:
    - Get distinct correspondence IDs first
    - Then join to other tables
*/

-- =====================================================================================
-- RECOMMENDED IMMEDIATE ACTION
-- =====================================================================================

-- 1. Run ANALYZE (CRITICAL if not done after cleanup)
ANALYZE correspondence."A2Iss1951A2Events";

-- 2. Test simplified query without timestamp join condition
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT
    er."ReferenceValue" AS DialogId,
    idcFetch."Id" AS DialogActivityId,
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    4 AS Status,
    'CorrespondenceOpened' AS ActivityType
FROM correspondence."A2Iss1951A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
    -- REMOVED: AND a2Events."Timestamp" = stats."StatusChanged"
INNER JOIN correspondence."Correspondences" corr
    ON a2Events."CorrespondenceId" = corr."Id"
    AND corr."Altinn2CorrespondenceId" IS NOT NULL
    AND corr."IsMigrating" = FALSE
INNER JOIN correspondence."ExternalReferences" er
    ON a2Events."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcFetch
    ON a2Events."CorrespondenceId" = idcFetch."CorrespondenceId"
    AND idcFetch."StatusAction" = '3'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
WHERE a2Events."Status" = 4
LIMIT 5000;

-- Expected: Much faster (seconds, not minutes)

-- =====================================================================================
-- ALTERNATIVE: Use Simpler Query Pattern (Like Issue #1716)
-- =====================================================================================

-- If above still slow, use this proven pattern from Issue #1716:

EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT
    stats."CorrespondenceId",
    stats."StatusChanged",
    stats."PartyUuid",
    4 AS Status
FROM correspondence."A2Iss1951A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
WHERE a2Events."Status" = 4
ORDER BY stats."CorrespondenceId"
LIMIT 5000;

-- This should be FAST (< 100ms)
-- Then you can join to other tables separately if needed

-- =====================================================================================
-- CHECK: Is This the Same Pattern as Issue #1716?
-- =====================================================================================

-- Let's verify Issue #1716 query still works fast:
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT
    er."ReferenceValue" AS DialogId,
    idcFetch."Id" AS DialogActivityId,
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    4 AS Status,
    'CorrespondenceOpened' AS ActivityType
FROM correspondence."A2Iss1716A2Events" a2Events  -- Note: Different helper table
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
INNER JOIN correspondence."ExternalReferences" er
    ON a2Events."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcFetch
    ON a2Events."CorrespondenceId" = idcFetch."CorrespondenceId"
    AND idcFetch."StatusAction" = '3'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE a2Events."Status" = 4
ORDER BY stats."CorrespondenceId", Status
LIMIT 5000;

-- If #1716 is fast but #1951 is slow, the difference is either:
-- 1. Index missing/invalid on A2Iss1951A2Events
-- 2. ANALYZE not run on A2Iss1951A2Events
-- 3. A2Iss1951A2Events helper table has data quality issue
-- 4. Scale difference (191M vs 10M rows)

-- =====================================================================================
-- DIAGNOSIS CHECKLIST
-- =====================================================================================

/*
Run these checks in order:

□ Check indexes exist and are valid (STEP 1)
□ Check last_analyze timestamp (STEP 2)
□ Run ANALYZE if needed
□ Test simple query on helper table alone (STEP 3)
□ Test join to CorrespondenceStatuses only (STEP 5)
□ Try query without timestamp join condition
□ Compare with Issue #1716 query performance
□ Check EXPLAIN plan for sequential scans
□ Check if Correspondences filter is problematic
□ Check if A2Parties Recipient filter is slow

Report findings and we'll determine the fix!
*/
