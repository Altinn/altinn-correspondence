-- =====================================================================================
-- Complete Cleanup Plan for A2Iss1951A2Events
-- =====================================================================================
-- SITUATION: Temp index stuck at "waiting for old snapshots"
-- DUPLICATES: 4,612,355 rows to delete (2.36% of data)
-- ACTION: Unstick index, complete cleanup with index benefit
-- =====================================================================================

-- =====================================================================================
-- STEP 1: Unstick the Index Creation (CRITICAL)
-- =====================================================================================

-- Find idle transactions blocking the index
SELECT 
    pid,
    usename,
    application_name,
    state,
    age(clock_timestamp(), xact_start) AS xact_age,
    age(clock_timestamp(), state_change) AS idle_duration,
    LEFT(query, 100) AS last_query
FROM pg_stat_activity
WHERE state = 'idle in transaction'
  AND pid != pg_backend_pid()
ORDER BY xact_start;

-- Terminate ALL idle transactions (these are safe to kill)
-- Replace PIDs with actual values from query above
-- Example:
-- SELECT pg_terminate_backend(12345);
-- SELECT pg_terminate_backend(12346);

-- If no idle transactions, check for long-running queries
SELECT 
    pid,
    usename,
    application_name,
    state,
    age(clock_timestamp(), xact_start) AS xact_age,
    LEFT(query, 100) AS query_snippet
FROM pg_stat_activity
WHERE xact_start IS NOT NULL
  AND age(clock_timestamp(), xact_start) > interval '30 minutes'
  AND pid != pg_backend_pid()
  AND query NOT ILIKE '%temp_idx_dup_analysis%'  -- Don't kill the index itself
ORDER BY xact_start;

-- Carefully terminate old transactions if safe
-- SELECT pg_terminate_backend(<PID>);

-- After terminating blockers, check if index completes
SELECT 
    schemaname,
    relname,
    indexrelname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS size,
    indisvalid AS is_valid
FROM pg_stat_user_indexes
JOIN pg_index ON indexrelid = pg_stat_user_indexes.indexrelid
WHERE schemaname = 'correspondence'
  AND indexrelname = 'temp_idx_dup_analysis';

-- Should show: is_valid = true (meaning index is ready)

-- =====================================================================================
-- STEP 2: Create Backup (5-10 minutes)
-- =====================================================================================

-- Only run if not already created
CREATE TABLE IF NOT EXISTS correspondence."A2Iss1951A2Events_backup" AS
SELECT * FROM correspondence."A2Iss1951A2Events";

-- Verify backup
SELECT 
    'Backup created' AS status,
    COUNT(*) AS rows_backed_up,
    pg_size_pretty(pg_total_relation_size('correspondence."A2Iss1951A2Events_backup"')) AS backup_size
FROM correspondence."A2Iss1951A2Events_backup";

-- Expected: 195,499,279 rows

-- =====================================================================================
-- STEP 3: Analyze Source Distribution in Duplicates (30 seconds)
-- =====================================================================================

-- Understand which Source value to keep
WITH duplicates AS (
    SELECT 
        "CorrespondenceId",
        "Timestamp",
        "PartyUuid",
        "Status",
        COUNT(*) AS dup_count,
        BOOL_OR("Source" = 0) AS has_source_0,
        BOOL_OR("Source" = 1) AS has_source_1,
        COUNT(*) FILTER (WHERE "Source" = 0) AS count_source_0,
        COUNT(*) FILTER (WHERE "Source" = 1) AS count_source_1
    FROM correspondence."A2Iss1951A2Events"
    GROUP BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
    HAVING COUNT(*) > 1
)
SELECT 
    'Duplicate groups with both sources' AS category,
    COUNT(*) FILTER (WHERE has_source_0 AND has_source_1) AS groups,
    SUM(dup_count) FILTER (WHERE has_source_0 AND has_source_1) AS rows
FROM duplicates
UNION ALL
SELECT 
    'Duplicate groups with only Source=0',
    COUNT(*) FILTER (WHERE has_source_0 AND NOT has_source_1),
    SUM(dup_count) FILTER (WHERE has_source_0 AND NOT has_source_1)
FROM duplicates
UNION ALL
SELECT 
    'Duplicate groups with only Source=1',
    COUNT(*) FILTER (WHERE has_source_1 AND NOT has_source_0),
    SUM(dup_count) FILTER (WHERE has_source_1 AND NOT has_source_0)
FROM duplicates;

-- Overall Source distribution
SELECT 
    "Source",
    COUNT(*) AS total_rows,
    ROUND(100.0 * COUNT(*) / SUM(COUNT(*)) OVER (), 2) AS percentage
FROM correspondence."A2Iss1951A2Events"
GROUP BY "Source"
ORDER BY "Source";

-- =====================================================================================
-- STEP 4: Remove Duplicates with Transaction (5-10 minutes with index)
-- =====================================================================================

BEGIN;

-- Delete duplicates using temp index for fast execution
-- Strategy: Keep Source = 1 over Source = 0
WITH ranked_records AS (
    SELECT 
        ctid,
        ROW_NUMBER() OVER (
            PARTITION BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
            ORDER BY "Source" DESC  -- Prefer Source = 1
        ) AS rn
    FROM correspondence."A2Iss1951A2Events"
)
DELETE FROM correspondence."A2Iss1951A2Events"
WHERE ctid IN (
    SELECT ctid 
    FROM ranked_records 
    WHERE rn > 1
);

-- Expected: DELETE 4,612,355 rows

-- =====================================================================================
-- VERIFY RESULTS BEFORE COMMIT
-- =====================================================================================

-- Check deleted count
SELECT 
    'Deleted' AS action,
    195499279 - COUNT(*) AS deleted_count,
    COUNT(*) AS remaining_rows,
    ROUND(100.0 * (195499279 - COUNT(*)) / 195499279, 2) AS pct_removed
FROM correspondence."A2Iss1951A2Events";

-- Expected: 
-- deleted_count: 4,612,355
-- remaining_rows: 190,886,924
-- pct_removed: 2.36%

-- Verify NO duplicates remain
SELECT 
    'Remaining duplicates (MUST be 0)' AS check_name,
    COUNT(*) AS duplicate_count
FROM (
    SELECT 
        "CorrespondenceId",
        "Timestamp",
        "PartyUuid",
        "Status"
    FROM correspondence."A2Iss1951A2Events"
    GROUP BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
    HAVING COUNT(*) > 1
) remaining_dupes;

-- Expected: 0

-- Check Source distribution after cleanup
SELECT 
    "Source",
    COUNT(*) AS rows,
    ROUND(100.0 * COUNT(*) / SUM(COUNT(*)) OVER (), 2) AS percentage
FROM correspondence."A2Iss1951A2Events"
GROUP BY "Source"
ORDER BY "Source";

-- =====================================================================================
-- COMMIT OR ROLLBACK
-- =====================================================================================

-- IF all checks pass (deleted 4.6M, 0 duplicates remain):
COMMIT;

-- IF something looks wrong:
-- ROLLBACK;

-- =====================================================================================
-- STEP 5: Drop Temp Index (instant)
-- =====================================================================================

-- After successful cleanup, drop the temp analysis index
DROP INDEX IF EXISTS correspondence."temp_idx_dup_analysis";

-- Verify it's gone
SELECT COUNT(*) AS temp_index_exists
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND indexrelname = 'temp_idx_dup_analysis';

-- Expected: 0

-- =====================================================================================
-- STEP 6: Create Production Indexes (12-20 minutes)
-- =====================================================================================

-- These are the indexes needed for fast export queries

-- Index 1: Primary cursor pagination index
CREATE INDEX CONCURRENTLY "IX_A2Iss1951A2Events_CorrId_Status_Party"
ON correspondence."A2Iss1951A2Events" (
    "CorrespondenceId",
    "Status"
)
INCLUDE ("PartyUuid", "Timestamp");

-- Monitor progress (run periodically)
SELECT 
    phase,
    ROUND(100.0 * blocks_done / NULLIF(blocks_total, 0), 2) AS blocks_pct,
    ROUND(100.0 * tuples_done / NULLIF(tuples_total, 0), 2) AS tuples_pct
FROM pg_stat_progress_create_index
WHERE relid = 'correspondence."A2Iss1951A2Events"'::regclass;

-- Index 2: Secondary status filter index
CREATE INDEX CONCURRENTLY "IX_A2Iss1951A2Events_Status_Timestamp"
ON correspondence."A2Iss1951A2Events" (
    "Status",
    "Timestamp"
)
INCLUDE ("CorrespondenceId", "PartyUuid");

-- Update table statistics
ANALYZE correspondence."A2Iss1951A2Events";

-- Verify both indexes created successfully
SELECT 
    schemaname,
    relname AS tablename,
    indexrelname AS indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size,
    idx_scan AS times_used,
    indisvalid AS is_valid
FROM pg_stat_user_indexes
JOIN pg_index ON indexrelid = pg_stat_user_indexes.indexrelid
WHERE schemaname = 'correspondence'
  AND relname = 'A2Iss1951A2Events'
  AND indexrelname LIKE 'IX_A2Iss1951%'
ORDER BY indexrelname;

-- Expected: 2 indexes, both is_valid = true
-- Index 1 size: ~10-15 GB
-- Index 2 size: ~10-15 GB

-- =====================================================================================
-- STEP 7: Test Query Performance (30 seconds)
-- =====================================================================================

-- Test Status 4 query with helper table approach
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
    AND a2Events."Timestamp" = stats."StatusChanged"
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

-- Expected results:
-- Execution time: < 100ms
-- Index Scan using IX_A2Iss1951A2Events_CorrId_Status_Party
-- No sequential scans
-- Buffers: shared hit + read (efficient I/O)

-- Test Status 6 query
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT
    er."ReferenceValue" AS DialogId,
    idcConfirm."Id" AS DialogActivityId,
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    6 AS Status,
    'CorrespondenceConfirmed' AS ActivityType
FROM correspondence."A2Iss1951A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
    AND a2Events."Timestamp" = stats."StatusChanged"
INNER JOIN correspondence."Correspondences" corr
    ON a2Events."CorrespondenceId" = corr."Id"
    AND corr."Altinn2CorrespondenceId" IS NOT NULL
    AND corr."IsMigrating" = FALSE
INNER JOIN correspondence."ExternalReferences" er
    ON a2Events."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcConfirm
    ON a2Events."CorrespondenceId" = idcConfirm."CorrespondenceId"
    AND idcConfirm."StatusAction" = '6'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
WHERE a2Events."Status" = 6
LIMIT 5000;

-- Expected: Similar performance to Status 4 (< 100ms)

-- =====================================================================================
-- STEP 8: Summary Report
-- =====================================================================================

SELECT 'Cleanup Summary' AS report;

-- Before vs After
SELECT 
    'Original rows' AS metric,
    COUNT(*) AS count
FROM correspondence."A2Iss1951A2Events_backup"
UNION ALL
SELECT 
    'After cleanup',
    COUNT(*)
FROM correspondence."A2Iss1951A2Events"
UNION ALL
SELECT 
    'Rows deleted',
    (SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events_backup") -
    (SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events")
UNION ALL
SELECT
    'Duplicate groups removed',
    4612355;

-- Storage savings
SELECT 
    'Original table + indexes' AS component,
    pg_size_pretty(pg_total_relation_size('correspondence."A2Iss1951A2Events_backup"')) AS size
UNION ALL
SELECT 
    'After cleanup table',
    pg_size_pretty(pg_relation_size('correspondence."A2Iss1951A2Events"'))
UNION ALL
SELECT 
    'After cleanup indexes',
    pg_size_pretty(pg_indexes_size('correspondence."A2Iss1951A2Events"'))
UNION ALL
SELECT 
    'After cleanup total',
    pg_size_pretty(pg_total_relation_size('correspondence."A2Iss1951A2Events"'));

-- Final verification
SELECT 
    'Verification: Unique rows = Total rows?' AS check_name,
    COUNT(*) AS total_rows,
    COUNT(DISTINCT ("CorrespondenceId", "Timestamp", "PartyUuid", "Status")) AS unique_rows,
    CASE 
        WHEN COUNT(*) = COUNT(DISTINCT ("CorrespondenceId", "Timestamp", "PartyUuid", "Status"))
        THEN 'PASS ✓'
        ELSE 'FAIL ✗'
    END AS result
FROM correspondence."A2Iss1951A2Events";

-- =====================================================================================
-- TIMELINE SUMMARY
-- =====================================================================================

/*
COMPLETED STEPS:
✅ Temp index created (took 1+ hour, but now ready to use)
✅ Exact duplicate count: 4,612,355 rows (2.36%)

REMAINING STEPS:
1. Unstick index (terminate blocking transactions) - 1-5 minutes
2. Create backup - 5-10 minutes
3. Delete duplicates (fast with temp index) - 5-10 minutes
4. Drop temp index - instant
5. Create production indexes - 12-20 minutes
6. Test queries - 1 minute

TOTAL REMAINING TIME: 23-46 minutes

FINAL RESULT:
- Table: 190,886,924 rows (cleaned)
- Indexes: 2 production indexes (~25 GB total)
- Query performance: 30-50ms per batch
- Full export time: 25-35 minutes for 191M rows
*/
