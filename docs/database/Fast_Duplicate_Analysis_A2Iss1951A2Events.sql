-- =====================================================================================
-- FAST Duplicate Analysis for A2Iss1951A2Events
-- =====================================================================================
-- 
-- PROBLEM: COUNT(DISTINCT ...) on 195M rows is very slow (20+ minutes)
-- SOLUTION: Use approximate methods and sampling for fast initial analysis
--
-- =====================================================================================

-- =====================================================================================
-- METHOD 1: Quick Approximate Row Count Comparison (INSTANT)
-- =====================================================================================

-- Get total rows (from system statistics - instant)
SELECT 
    reltuples::bigint AS approximate_total_rows
FROM pg_class
WHERE relname = 'A2Iss1951A2Events'
  AND relnamespace = 'correspondence'::regnamespace;

-- Get approximate unique count using HyperLogLog (requires pg_stats)
-- This is much faster but approximate
SELECT 
    'Approximate unique combinations' AS description,
    n_distinct AS estimated_unique_ratio,
    CASE 
        WHEN n_distinct < 0 THEN 
            ROUND(ABS(n_distinct) * (SELECT reltuples FROM pg_class WHERE relname = 'A2Iss1951A2Events'))::bigint
        ELSE n_distinct::bigint
    END AS estimated_unique_count
FROM pg_stats
WHERE schemaname = 'correspondence'
  AND tablename = 'A2Iss1951A2Events'
  AND attname = 'CorrespondenceId';

-- =====================================================================================
-- METHOD 2: Sample-Based Analysis (30 seconds - 2 minutes)
-- =====================================================================================

-- Take a 1% random sample and extrapolate
WITH sample AS (
    SELECT 
        "CorrespondenceId",
        "Timestamp",
        "PartyUuid",
        "Status",
        "Source"
    FROM correspondence."A2Iss1951A2Events"
    TABLESAMPLE SYSTEM (1)  -- 1% sample
),
sample_stats AS (
    SELECT 
        COUNT(*) AS sample_total,
        COUNT(DISTINCT ("CorrespondenceId", "Timestamp", "PartyUuid", "Status")) AS sample_unique
    FROM sample
)
SELECT 
    'Sample-based estimate (1% sample)' AS method,
    sample_total AS sample_size,
    sample_unique AS unique_in_sample,
    ROUND(100.0 * sample_unique / sample_total, 2) AS pct_unique,
    (SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events") AS total_rows,
    ROUND((SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events") * 
          (sample_unique::numeric / sample_total), 0)::bigint AS estimated_unique_total,
    (SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events") - 
    ROUND((SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events") * 
          (sample_unique::numeric / sample_total), 0)::bigint AS estimated_duplicates
FROM sample_stats;

-- =====================================================================================
-- METHOD 3: Create Temporary Indexes for Fast Analysis (5-10 minutes)
-- =====================================================================================

-- Create a temporary composite index specifically for duplicate analysis
-- This speeds up GROUP BY queries dramatically
CREATE INDEX CONCURRENTLY IF NOT EXISTS "temp_idx_dup_analysis"
ON correspondence."A2Iss1951A2Events" (
    "CorrespondenceId",
    "Timestamp",
    "PartyUuid",
    "Status"
);

-- Now this query will be MUCH faster (2-5 minutes instead of 20+)
-- Note: Still slow but 4-10x faster than without index
SELECT 
    'Unique combinations (with index)' AS description,
    COUNT(DISTINCT ("CorrespondenceId", "Timestamp", "PartyUuid", "Status")) AS count
FROM correspondence."A2Iss1951A2Events";

-- =====================================================================================
-- METHOD 4: Direct Duplicate Count (FASTEST with index - 2-5 minutes)
-- =====================================================================================

-- After creating the temp index above, this is the fastest way to find duplicates
SELECT 
    COUNT(*) FILTER (WHERE dup_count > 1) AS duplicate_groups,
    SUM(dup_count) FILTER (WHERE dup_count > 1) AS total_duplicate_rows,
    SUM(dup_count - 1) FILTER (WHERE dup_count > 1) AS rows_to_delete,
    195499279 - SUM(dup_count - 1) FILTER (WHERE dup_count > 1) AS rows_after_cleanup
FROM (
    SELECT COUNT(*) AS dup_count
    FROM correspondence."A2Iss1951A2Events"
    GROUP BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
) counts;

-- =====================================================================================
-- METHOD 5: Streaming Analysis with Progress (if you need exact count)
-- =====================================================================================

-- This uses a materialized view to cache results for future use
-- First run: 10-15 minutes
-- Subsequent queries: instant

CREATE MATERIALIZED VIEW IF NOT EXISTS correspondence."mv_A2Iss1951A2Events_unique_combos" AS
SELECT DISTINCT
    "CorrespondenceId",
    "Timestamp",
    "PartyUuid",
    "Status"
FROM correspondence."A2Iss1951A2Events";

-- Create index on materialized view for fast counting
CREATE INDEX IF NOT EXISTS "idx_mv_unique_combos_all"
ON correspondence."mv_A2Iss1951A2Events_unique_combos" (
    "CorrespondenceId",
    "Timestamp",
    "PartyUuid",
    "Status"
);

-- Now counting is instant
SELECT 
    'Exact unique combinations (materialized view)' AS description,
    COUNT(*) AS count
FROM correspondence."mv_A2Iss1951A2Events_unique_combos";

-- Compare with total
SELECT 
    'Total rows' AS metric,
    COUNT(*) AS count
FROM correspondence."A2Iss1951A2Events"
UNION ALL
SELECT 
    'Unique combinations',
    COUNT(*)
FROM correspondence."mv_A2Iss1951A2Events_unique_combos"
UNION ALL
SELECT 
    'Duplicate rows to remove',
    (SELECT COUNT(*) FROM correspondence."A2Iss1951A2Events") - 
    (SELECT COUNT(*) FROM correspondence."mv_A2Iss1951A2Events_unique_combos");

-- =====================================================================================
-- RECOMMENDED WORKFLOW (Fastest Overall)
-- =====================================================================================

-- Step 1: Quick sample-based estimate (30 seconds)
-- Run METHOD 2 above to get quick estimate

-- Step 2: If duplicates detected, create temp index (5-10 min one-time cost)
CREATE INDEX CONCURRENTLY IF NOT EXISTS "temp_idx_dup_analysis"
ON correspondence."A2Iss1951A2Events" (
    "CorrespondenceId",
    "Timestamp",
    "PartyUuid",
    "Status"
);

-- Step 3: Get exact duplicate count with index (2-5 minutes)
-- Run METHOD 4 above

-- Step 4: Analyze Source distribution in duplicates (1-2 minutes)
WITH duplicates AS (
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
)
SELECT 
    COUNT(*) AS duplicate_groups,
    SUM(dup_count) AS total_duplicate_rows,
    SUM(CASE WHEN has_source_0 AND has_source_1 THEN dup_count ELSE 0 END) AS rows_with_both_sources,
    SUM(CASE WHEN has_source_0 AND NOT has_source_1 THEN dup_count ELSE 0 END) AS rows_with_only_source_0,
    SUM(CASE WHEN has_source_1 AND NOT has_source_0 THEN dup_count ELSE 0 END) AS rows_with_only_source_1
FROM duplicates;

-- =====================================================================================
-- OPTIMIZED DUPLICATE REMOVAL (After Analysis)
-- =====================================================================================

-- If you decide to proceed with cleanup, this is MUCH faster with the temp index

BEGIN;

-- Delete duplicates using the temp index for fast lookups
WITH ranked_records AS (
    SELECT 
        ctid,
        ROW_NUMBER() OVER (
            PARTITION BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
            ORDER BY "Source" DESC  -- Keep Source = 1 over Source = 0
        ) AS rn
    FROM correspondence."A2Iss1951A2Events"
)
DELETE FROM correspondence."A2Iss1951A2Events"
WHERE ctid IN (
    SELECT ctid 
    FROM ranked_records 
    WHERE rn > 1
);

-- Verify results
SELECT 
    'After cleanup' AS status,
    COUNT(*) AS total_rows
FROM correspondence."A2Iss1951A2Events";

-- REVIEW CAREFULLY, then either:
-- COMMIT;
-- or
-- ROLLBACK;

-- =====================================================================================
-- CLEANUP (After you're done)
-- =====================================================================================

-- Drop temporary analysis index
DROP INDEX IF EXISTS correspondence."temp_idx_dup_analysis";

-- Drop materialized view if created
DROP MATERIALIZED VIEW IF EXISTS correspondence."mv_A2Iss1951A2Events_unique_combos";

-- =====================================================================================
-- SUMMARY: Recommended Approach
-- =====================================================================================

/*
FASTEST WORKFLOW (Total time: 10-20 minutes for 195M rows):

1. Sample-based estimate (30 sec) - METHOD 2
   → Quick check if duplicates exist

2. If duplicates > 1%:
   CREATE INDEX for analysis (5-10 min) - METHOD 3

3. Exact duplicate count (2-5 min) - METHOD 4
   → Know exactly what you're dealing with

4. Source distribution (1-2 min) - Step 4 above
   → Understand Source 0 vs 1 pattern

5. Backup + Delete (5-15 min depending on duplicate count)
   → Use temp index for fast cleanup

6. Drop temp index (instant)

TOTAL: 13-32 minutes (vs 60+ minutes without optimizations)
*/
