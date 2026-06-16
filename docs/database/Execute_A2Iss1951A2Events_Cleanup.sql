-- =====================================================================================
-- FINAL EXECUTION PLAN - A2Iss1951A2Events Cleanup
-- =====================================================================================
-- 
-- CURRENT SITUATION:
-- - Total rows: 195,499,279
-- - Duplicate rows to delete: 4,612,355 (2.36%)
-- - After cleanup: 190,886,924 rows
-- - Source distribution: 64.56% Source=0, 35.44% Source=1
-- - Temp index: Ready (after unsticking)
--
-- STRATEGY: Keep Source = 1 when both exist, otherwise keep whatever exists
-- ESTIMATED TIME: 25-40 minutes total
-- =====================================================================================

-- =====================================================================================
-- STEP 1: Create Backup (5-10 minutes) - CRITICAL SAFETY STEP
-- =====================================================================================

-- Check if backup already exists
SELECT 
    COUNT(*) AS backup_exists,
    CASE WHEN COUNT(*) > 0 THEN 'Backup already exists - SKIP creation' 
         ELSE 'No backup found - CREATE now' 
    END AS action
FROM pg_tables
WHERE schemaname = 'correspondence'
  AND tablename = 'A2Iss1951A2Events_backup';

-- If backup doesn't exist, create it:
CREATE TABLE IF NOT EXISTS correspondence."A2Iss1951A2Events_backup" AS
SELECT * FROM correspondence."A2Iss1951A2Events";

-- Verify backup
SELECT 
    'Backup verification' AS step,
    COUNT(*) AS rows_backed_up,
    pg_size_pretty(pg_total_relation_size('correspondence."A2Iss1951A2Events_backup"')) AS backup_size
FROM correspondence."A2Iss1951A2Events_backup";

-- Expected: 195,499,279 rows

-- =====================================================================================
-- STEP 2: Remove Duplicates (5-10 minutes with temp index)
-- =====================================================================================

-- Start transaction for safety
BEGIN;

-- Delete duplicates using temp index for fast execution
-- Strategy: Keep Source = 1 over Source = 0 (35.44% will be kept preferentially)
WITH ranked_records AS (
    SELECT 
        ctid,
        ROW_NUMBER() OVER (
            PARTITION BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
            ORDER BY "Source" DESC  -- Source 1 > Source 0
        ) AS rn
    FROM correspondence."A2Iss1951A2Events"
)
DELETE FROM correspondence."A2Iss1951A2Events"
WHERE ctid IN (
    SELECT ctid 
    FROM ranked_records 
    WHERE rn > 1
);

-- This will take 5-10 minutes
-- Expected to delete: 4,612,355 rows

-- =====================================================================================
-- VERIFICATION (CRITICAL - Review before COMMIT)
-- =====================================================================================

-- Check 1: How many rows were deleted?
SELECT 
    'Rows deleted' AS check_name,
    195499279 - COUNT(*) AS deleted_count,
    COUNT(*) AS remaining_rows,
    ROUND(100.0 * (195499279 - COUNT(*)) / 195499279, 2) AS pct_deleted
FROM correspondence."A2Iss1951A2Events";

-- Expected results:
-- deleted_count: 4,612,355
-- remaining_rows: 190,886,924
-- pct_deleted: 2.36%

-- Check 2: Are all duplicates removed?
SELECT 
    'Remaining duplicates' AS check_name,
    COUNT(*) AS duplicate_count,
    CASE 
        WHEN COUNT(*) = 0 THEN 'PASS ✓ - No duplicates remain'
        ELSE 'FAIL ✗ - Still have duplicates!'
    END AS result
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

-- Expected: 0 duplicates

-- Check 3: Source distribution after cleanup
SELECT 
    'Source distribution after cleanup' AS check_name,
    "Source",
    COUNT(*) AS rows,
    ROUND(100.0 * COUNT(*) / SUM(COUNT(*)) OVER (), 2) AS percentage
FROM correspondence."A2Iss1951A2Events"
GROUP BY "Source"
ORDER BY "Source";

-- Expected: Higher percentage of Source=1 (since we kept it preferentially)

-- =====================================================================================
-- DECISION POINT: COMMIT or ROLLBACK
-- =====================================================================================

/*
✅ COMMIT if all checks pass:
   - Deleted count ≈ 4.6M rows
   - Remaining rows ≈ 190.9M
   - No duplicates remain (0)
   - Source distribution shows higher % of Source=1

❌ ROLLBACK if something looks wrong:
   - Unexpected deleted count
   - Duplicates still exist
   - Source distribution looks suspicious
*/

-- If everything looks good:
COMMIT;

-- If something is wrong:
-- ROLLBACK;

-- =====================================================================================
-- STEP 3: Drop Temporary Analysis Index (instant)
-- =====================================================================================

-- After successful cleanup, drop the temp index (no longer needed)
DROP INDEX IF EXISTS correspondence."temp_idx_dup_analysis";

-- Verify it's dropped
SELECT 
    COUNT(*) AS temp_index_count,
    CASE 
        WHEN COUNT(*) = 0 THEN 'Temp index dropped ✓'
        ELSE 'Temp index still exists ✗'
    END AS status
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND indexrelname = 'temp_idx_dup_analysis';

-- Expected: 0

-- =====================================================================================
-- STEP 4: Create Production Indexes (12-20 minutes)
-- =====================================================================================

-- These indexes are required for fast export queries
-- They will be created CONCURRENTLY to avoid blocking table access

-- Index 1: Primary cursor pagination index
-- This supports: WHERE (CorrespondenceId, Status) > (@lastId, @lastStatus)
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_A2Iss1951A2Events_CorrId_Status_Party"
ON correspondence."A2Iss1951A2Events" (
    "CorrespondenceId",
    "Status"
)
INCLUDE ("PartyUuid", "Timestamp");

-- Expected time: 6-10 minutes
-- Expected size: ~10-15 GB

-- Monitor progress (run this in another session while index builds):
SELECT 
    phase,
    ROUND(100.0 * blocks_done / NULLIF(blocks_total, 0), 2) AS blocks_pct,
    ROUND(100.0 * tuples_done / NULLIF(tuples_total, 0), 2) AS tuples_pct,
    lockers_done,
    lockers_total
FROM pg_stat_progress_create_index
WHERE relid = 'correspondence."A2Iss1951A2Events"'::regclass;

-- Index 2: Secondary status filter index
-- This supports: WHERE Status = X with timestamp range
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_A2Iss1951A2Events_Status_Timestamp"
ON correspondence."A2Iss1951A2Events" (
    "Status",
    "Timestamp"
)
INCLUDE ("CorrespondenceId", "PartyUuid");

-- Expected time: 6-10 minutes
-- Expected size: ~10-15 GB

-- Update table statistics (CRITICAL for query optimizer)
ANALYZE correspondence."A2Iss1951A2Events";

-- Verify both indexes created successfully
SELECT 
    schemaname,
    relname AS tablename,
    indexrelname AS indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size,
    idx_scan AS times_used,
    indisvalid AS is_valid,
    indisready AS is_ready
FROM pg_stat_user_indexes
JOIN pg_index ON indexrelid = pg_stat_user_indexes.indexrelid
WHERE schemaname = 'correspondence'
  AND relname = 'A2Iss1951A2Events'
ORDER BY indexrelname;

-- Expected: 2 indexes with is_valid = true, is_ready = true

-- =====================================================================================
-- STEP 5: Test Query Performance (30 seconds)
-- =====================================================================================

-- Test Status 4 query (Read events)
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
-- ✓ Execution time: 30-100ms
-- ✓ Index Scan using IX_A2Iss1951A2Events_CorrId_Status_Party
-- ✓ No Sequential Scans
-- ✓ Reasonable buffer hits/reads

-- Test Status 6 query (Confirmed events)
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

-- Expected: Similar performance to Status 4

-- =====================================================================================
-- STEP 6: Final Summary and Verification
-- =====================================================================================

SELECT '========== CLEANUP COMPLETE ==========' AS summary;

-- Before and After comparison
SELECT 
    'Original row count' AS metric,
    COUNT(*) AS value
FROM correspondence."A2Iss1951A2Events_backup"
UNION ALL
SELECT 
    'Final row count',
    COUNT(*)
FROM correspondence."A2Iss1951A2Events"
UNION ALL
SELECT 
    'Rows removed',
    (SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events_backup") - 
    (SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events")
UNION ALL
SELECT
    'Removal percentage',
    ROUND(100.0 * ((SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events_backup") - 
                   (SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events")) /
                   (SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events_backup"), 2);

-- Storage summary
SELECT 
    'Table size (current)' AS component,
    pg_size_pretty(pg_relation_size('correspondence."A2Iss1951A2Events"')) AS size
UNION ALL
SELECT 
    'Index size (current)',
    pg_size_pretty(pg_indexes_size('correspondence."A2Iss1951A2Events"'))
UNION ALL
SELECT 
    'Total size (current)',
    pg_size_pretty(pg_total_relation_size('correspondence."A2Iss1951A2Events"'))
UNION ALL
SELECT 
    'Backup size',
    pg_size_pretty(pg_total_relation_size('correspondence."A2Iss1951A2Events_backup"'));

-- Final data quality check
SELECT 
    'FINAL VERIFICATION' AS check_type,
    COUNT(*) AS total_rows,
    COUNT(DISTINCT ("CorrespondenceId", "Timestamp", "PartyUuid", "Status")) AS unique_combinations,
    CASE 
        WHEN COUNT(*) = COUNT(DISTINCT ("CorrespondenceId", "Timestamp", "PartyUuid", "Status"))
        THEN '✓ PASS - All rows are unique'
        ELSE '✗ FAIL - Duplicates still exist!'
    END AS result
FROM correspondence."A2Iss1951A2Events";

-- Source distribution final state
SELECT 
    'Source=' || "Source"::text AS source_value,
    COUNT(*) AS row_count,
    ROUND(100.0 * COUNT(*) / SUM(COUNT(*)) OVER (), 2) AS percentage,
    CASE "Source"
        WHEN 0 THEN 'Source 0'
        WHEN 1 THEN 'Source 1 (preferred)'
    END AS description
FROM correspondence."A2Iss1951A2Events"
GROUP BY "Source"
ORDER BY "Source";

-- =====================================================================================
-- SUCCESS CRITERIA CHECKLIST
-- =====================================================================================

/*
✅ Cleanup successful if ALL of these are true:

1. Rows removed: ~4.6M (2.36%)
2. Final row count: ~190.9M
3. No duplicates remain (verification query returns 0)
4. Two production indexes exist and are valid
5. Test queries execute in < 100ms
6. Index scans used (no sequential scans)
7. Backup table exists with original 195.5M rows

NEXT STEPS:

1. Update DialogActivityExportService.cs to use helper table for Issue #1951
   (similar to Issue #1716 implementation)

2. Update calculate-counts.sql with new helper table queries

3. Test export with: --issue 1951 --max-batches 2

4. Expected export performance:
   - Batch time: 30-50ms
   - Full export: 25-35 minutes for 191M rows
   - Improvement: 200-300x faster than old CTE approach

5. After 1 week of successful exports, drop backup:
   DROP TABLE correspondence."A2Iss1951A2Events_backup";
*/

-- =====================================================================================
-- TROUBLESHOOTING
-- =====================================================================================

/*
IF something goes wrong:

SCENARIO: Deleted wrong number of rows
ACTION: ROLLBACK transaction (if still in transaction)
        Restore from backup: DROP TABLE ... ; ALTER TABLE backup RENAME TO ...

SCENARIO: Duplicates still exist after cleanup
ACTION: Check if transaction was committed
        Re-run cleanup with different ORDER BY strategy

SCENARIO: Index creation stuck at "waiting for old snapshots"
ACTION: Find and kill idle transactions:
        SELECT pg_terminate_backend(pid) FROM pg_stat_activity 
        WHERE state = 'idle in transaction';

SCENARIO: Query performance still slow after indexes
ACTION: Run ANALYZE again
        Check EXPLAIN plan for sequential scans
        May need to drop and recreate indexes

SCENARIO: Backup table takes too much space
ACTION: Wait 1 week, verify exports work, then DROP backup
*/

SELECT 'Script complete. Review results above and proceed if all checks pass.' AS final_note;
