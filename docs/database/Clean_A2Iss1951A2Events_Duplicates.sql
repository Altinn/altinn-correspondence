-- =====================================================================================
-- Clean Duplicates from A2Iss1951A2Events Helper Table
-- =====================================================================================
-- 
-- PURPOSE:
--   Identify and remove duplicate records in A2Iss1951A2Events where all fields
--   are identical except "Source" (which has values 0 or 1).
--
-- STRATEGY:
--   Keep one record per unique (CorrespondenceId, Timestamp, PartyUuid, Status)
--   combination, preferring Source = 1 over Source = 0 (or whichever makes sense
--   for your business logic).
--
-- SAFETY:
--   This script uses a transaction with explicit COMMIT. Review results before
--   committing. Run in a test environment first if possible.
--
-- =====================================================================================

-- =====================================================================================
-- STEP 1: Analyze the Duplicate Situation
-- =====================================================================================

-- Check how many total rows exist
SELECT 
    'Total rows in A2Iss1951A2Events' AS description,
    COUNT(*) AS count
FROM correspondence."A2Iss1951A2Events";

-- Check how many unique combinations exist (ignoring Source)
SELECT 
    'Unique combinations (ignoring Source)' AS description,
    COUNT(DISTINCT ("CorrespondenceId", "Timestamp", "PartyUuid", "Status")) AS count
FROM correspondence."A2Iss1951A2Events";

-- Find duplicate groups and their Source distribution
SELECT 
    COUNT(*) AS duplicate_groups,
    SUM(dup_count) AS total_duplicate_rows,
    SUM(CASE WHEN has_source_0 AND has_source_1 THEN dup_count ELSE 0 END) AS rows_with_both_sources,
    SUM(CASE WHEN has_source_0 AND NOT has_source_1 THEN dup_count ELSE 0 END) AS rows_with_only_source_0,
    SUM(CASE WHEN has_source_1 AND NOT has_source_0 THEN dup_count ELSE 0 END) AS rows_with_only_source_1
FROM (
    SELECT 
        "CorrespondenceId",
        "Timestamp",
        "PartyUuid",
        "Status",
        COUNT(*) AS dup_count,
        BOOL_OR("Source" = 0) AS has_source_0,
        BOOL_OR("Source" = 1) AS has_source_1
    FROM correspondence."A2Iss1951A2Events"
    GROUP BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
    HAVING COUNT(*) > 1
) duplicates;

-- Sample duplicate records (first 20 groups)
SELECT 
    "CorrespondenceId",
    "Timestamp",
    "PartyUuid",
    "Status",
    "Source",
    COUNT(*) OVER (PARTITION BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status") AS dup_count
FROM correspondence."A2Iss1951A2Events"
WHERE ("CorrespondenceId", "Timestamp", "PartyUuid", "Status") IN (
    SELECT "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
    FROM correspondence."A2Iss1951A2Events"
    GROUP BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
    HAVING COUNT(*) > 1
    LIMIT 20
)
ORDER BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status", "Source";

-- =====================================================================================
-- STEP 2: Preview Which Records Will Be Kept vs Deleted
-- =====================================================================================

-- Preview: Records that will be KEPT
-- Strategy: Keep Source = 1 if it exists, otherwise keep Source = 0
-- If multiple records with same Source exist, keep first one by Timestamp
WITH ranked_records AS (
    SELECT 
        "CorrespondenceId",
        "Timestamp",
        "PartyUuid",
        "Status",
        "Source",
        ROW_NUMBER() OVER (
            PARTITION BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
            ORDER BY "Source" DESC  -- Prefer Source = 1 over Source = 0
        ) AS rn
    FROM correspondence."A2Iss1951A2Events"
)
SELECT 
    'Records to KEEP' AS action,
    COUNT(*) AS count
FROM ranked_records
WHERE rn = 1;

-- Preview: Records that will be DELETED
WITH ranked_records AS (
    SELECT 
        "CorrespondenceId",
        "Timestamp",
        "PartyUuid",
        "Status",
        "Source",
        ROW_NUMBER() OVER (
            PARTITION BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
            ORDER BY "Source" DESC
        ) AS rn
    FROM correspondence."A2Iss1951A2Events"
)
SELECT 
    'Records to DELETE' AS action,
    COUNT(*) AS count
FROM ranked_records
WHERE rn > 1;

-- =====================================================================================
-- STEP 3: Create Backup Table (RECOMMENDED)
-- =====================================================================================

-- Create backup of original table before deletion
DROP TABLE IF EXISTS correspondence."A2Iss1951A2Events_backup";

CREATE TABLE correspondence."A2Iss1951A2Events_backup" AS
SELECT * FROM correspondence."A2Iss1951A2Events";

SELECT 
    'Backup created' AS status,
    COUNT(*) AS rows_backed_up
FROM correspondence."A2Iss1951A2Events_backup";

-- =====================================================================================
-- STEP 4: Remove Duplicates (TRANSACTED)
-- =====================================================================================

-- Start transaction
BEGIN;

-- Method 1: Delete duplicates using CTE
-- Keeps Source = 1 over Source = 0, keeps first record if multiple with same Source
WITH ranked_records AS (
    SELECT 
        ctid,  -- Physical row identifier
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

-- Check results before committing
SELECT 
    'After cleanup' AS status,
    COUNT(*) AS total_rows,
    COUNT(DISTINCT ("CorrespondenceId", "Timestamp", "PartyUuid", "Status")) AS unique_combinations
FROM correspondence."A2Iss1951A2Events";

-- Verify no duplicates remain
SELECT 
    'Remaining duplicates (should be 0)' AS description,
    COUNT(*) AS count
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

-- If everything looks good, COMMIT. Otherwise ROLLBACK.
-- COMMIT;
-- ROLLBACK;

-- Uncomment one of the above after reviewing results

-- =====================================================================================
-- STEP 5: Alternative Approach - Create Clean Table and Swap
-- =====================================================================================
-- This approach is safer for large tables as it doesn't modify in-place

/*
-- Create new clean table
CREATE TABLE correspondence."A2Iss1951A2Events_clean" AS
WITH ranked_records AS (
    SELECT 
        "CorrespondenceId",
        "Timestamp",
        "PartyUuid",
        "Status",
        "Source",
        ROW_NUMBER() OVER (
            PARTITION BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
            ORDER BY "Source" DESC
        ) AS rn
    FROM correspondence."A2Iss1951A2Events"
)
SELECT 
    "CorrespondenceId",
    "Timestamp",
    "PartyUuid",
    "Status",
    "Source"
FROM ranked_records
WHERE rn = 1;

-- Verify clean table
SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events_clean";

-- After verification, swap tables (in transaction)
BEGIN;

-- Rename old table
ALTER TABLE correspondence."A2Iss1951A2Events" 
RENAME TO "A2Iss1951A2Events_old";

-- Rename clean table to original name
ALTER TABLE correspondence."A2Iss1951A2Events_clean" 
RENAME TO "A2Iss1951A2Events";

-- COMMIT;
-- ROLLBACK;

-- After successful swap, you can drop the old table
-- DROP TABLE correspondence."A2Iss1951A2Events_old";
*/

-- =====================================================================================
-- STEP 6: Recreate Indexes (After Cleanup)
-- =====================================================================================

-- Drop existing indexes if any
DROP INDEX IF EXISTS correspondence."IX_A2Iss1951A2Events_CorrId_Status_Party";
DROP INDEX IF EXISTS correspondence."IX_A2Iss1951A2Events_Status_Timestamp";

-- Create optimized indexes for the export query
-- Index 1: Covering index for cursor pagination (main query index)
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_A2Iss1951A2Events_CorrId_Status_Party"
ON correspondence."A2Iss1951A2Events" (
    "CorrespondenceId",
    "Status"
)
INCLUDE ("PartyUuid", "Timestamp");

-- Index 2: Filter index for Status + Timestamp range queries
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_A2Iss1951A2Events_Status_Timestamp"
ON correspondence."A2Iss1951A2Events" (
    "Status",
    "Timestamp"
)
INCLUDE ("CorrespondenceId", "PartyUuid");

-- Update table statistics
ANALYZE correspondence."A2Iss1951A2Events";

-- Verify indexes
SELECT 
    schemaname,
    relname AS tablename,
    indexrelname AS indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size,
    idx_scan AS times_used
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND relname = 'A2Iss1951A2Events'
ORDER BY indexrelname;

-- =====================================================================================
-- SUMMARY REPORT
-- =====================================================================================

SELECT 
    'Cleanup Summary' AS report;

SELECT 
    'Original rows (from backup)' AS description,
    COUNT(*) AS count
FROM correspondence."A2Iss1951A2Events_backup";

SELECT 
    'Current rows (after cleanup)' AS description,
    COUNT(*) AS count
FROM correspondence."A2Iss1951A2Events";

SELECT 
    'Rows removed' AS description,
    (SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events_backup") - 
    (SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events") AS count;

SELECT 
    'Unique combinations' AS description,
    COUNT(DISTINCT ("CorrespondenceId", "Timestamp", "PartyUuid", "Status")) AS count
FROM correspondence."A2Iss1951A2Events";

SELECT 
    'Source distribution after cleanup' AS description,
    "Source",
    COUNT(*) AS count,
    ROUND(100.0 * COUNT(*) / SUM(COUNT(*)) OVER (), 2) AS percentage
FROM correspondence."A2Iss1951A2Events"
GROUP BY "Source"
ORDER BY "Source";
