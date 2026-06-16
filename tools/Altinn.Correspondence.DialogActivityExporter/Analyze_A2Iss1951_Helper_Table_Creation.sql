-- =====================================================================================
-- Pre-Flight Analysis for A2Iss1951MigratedEvents Helper Table Creation
-- =====================================================================================
-- 
-- PURPOSE:
--   Analyze the helper table creation query BEFORE committing to the full 30-60 minute
--   execution. This script tests performance on small samples and provides accurate
--   estimates for the full table creation.
--
-- STRATEGY:
--   1. Test with EXPLAIN (no execution) to see query plan
--   2. Test with small LIMIT to measure per-row timing
--   3. Extrapolate to full 150M row estimate
--   4. Verify index selectivity and join efficiency
--   5. Estimate storage requirements
--
-- TIME REQUIRED: 5-10 minutes (vs 30-60 min for full table creation)
--
-- =====================================================================================

-- =====================================================================================
-- STEP 1: Verify Source Data Counts
-- =====================================================================================
-- Quick check: How many rows match the Issue #1951 criteria?
-- This helps validate we're targeting the right data.
--
-- EXPECTED: ~150M rows (Status 4 + Status 6 combined)
-- =====================================================================================

SELECT 
    'CorrespondenceStatuses: Issue #1951 Filter Check' AS check_name,
    COUNT(*) AS total_rows,
    COUNT(*) FILTER (WHERE "Status" = 4) AS status_4_rows,
    COUNT(*) FILTER (WHERE "Status" = 6) AS status_6_rows,
    pg_size_pretty(SUM(pg_column_size("CorrespondenceId"))) AS estimated_storage
FROM correspondence."CorrespondenceStatuses"
WHERE "Status" IN (4, 6)
  AND "SyncedFromAltinn2" IS NULL
  AND "StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-12-31 23:59:59';

-- If this query takes > 30 seconds, Issue #1951 export will definitely be slow without optimization!

-- =====================================================================================
-- STEP 2: EXPLAIN ANALYZE the Full Query (NO EXECUTION)
-- =====================================================================================
-- See the query plan WITHOUT creating the table.
-- This shows what indexes PostgreSQL will use and estimated costs.
--
-- Look for:
-- ✅ Sequential Scans (expected - no helper table yet)
-- ✅ Hash Joins or Nested Loops
-- ✅ Estimated rows ~150M
-- ⚠️ Cost values (higher = slower)
-- =====================================================================================

EXPLAIN (VERBOSE, COSTS, SETTINGS)
SELECT DISTINCT
    stats."CorrespondenceId",
    stats."PartyUuid",
    stats."Status",
    stats."StatusChanged"
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
    AND stats."SyncedFromAltinn2" IS NULL
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
WHERE stats."Status" IN (4, 6)
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-12-31 23:59:59';

-- Review output:
-- - "cost=XXX..YYY" - Total estimated cost (YYY is final cost)
-- - "rows=NNN" - Estimated row count (should be ~150M)
-- - Scan types: Seq Scan (slow), Index Scan (fast), Index Only Scan (fastest)

-- =====================================================================================
-- STEP 3: Test Small Sample (FAST - measures per-row timing)
-- =====================================================================================
-- Execute the query with LIMIT 10000 to measure actual timing.
-- This completes in seconds and gives us per-row performance data.
--
-- EXECUTION TIME: 5-30 seconds for 10K rows
-- =====================================================================================

EXPLAIN (ANALYZE, BUFFERS, TIMING)
SELECT DISTINCT
    stats."CorrespondenceId",
    stats."PartyUuid",
    stats."Status",
    stats."StatusChanged"
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
    AND stats."SyncedFromAltinn2" IS NULL
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
WHERE stats."Status" IN (4, 6)
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-12-31 23:59:59'
LIMIT 10000;

-- Record these values from output:
-- - "Execution Time: XXX.YYY ms" (bottom of output)
-- - "Buffers: shared hit=XXX read=YYY" (I/O activity)
-- - Actual rows returned (should be close to 10,000)

-- =====================================================================================
-- STEP 4: Calculate Extrapolated Timing
-- =====================================================================================
-- Use the 10K sample to estimate full table creation time.
--
-- FORMULA:
--   time_per_10k_rows = (Execution Time from STEP 3) milliseconds
--   expected_total_rows = 150,000,000
--   batches = expected_total_rows / 10,000 = 15,000 batches
--   total_time_ms = time_per_10k_rows * batches
--   total_time_minutes = total_time_ms / 60,000
--
-- EXAMPLE:
--   If STEP 3 took 15,000ms (15 seconds) for 10K rows:
--   total_time = 15,000ms × 15,000 = 225,000,000ms = 3,750 minutes = 62.5 hours
--   ⚠️ This would be TOO SLOW - need to optimize source query first!
--
--   If STEP 3 took 500ms (0.5 seconds) for 10K rows:
--   total_time = 500ms × 15,000 = 7,500,000ms = 125 minutes = 2 hours
--   ✅ This is acceptable for one-time setup
-- =====================================================================================

-- Run this query to calculate the estimate automatically:
DO $$
DECLARE
    sample_time_ms NUMERIC; -- Replace with actual "Execution Time" from STEP 3
    expected_rows BIGINT := 150000000;
    rows_per_sample INTEGER := 10000;
    estimated_total_time_minutes NUMERIC;
BEGIN
    -- ⚠️ MANUAL INPUT REQUIRED: Set this to your actual execution time from STEP 3
    sample_time_ms := 500; -- Example: 500ms for 10K rows

    -- Calculate extrapolation
    estimated_total_time_minutes := (sample_time_ms * (expected_rows / rows_per_sample)) / 60000.0;

    RAISE NOTICE '=================================================================';
    RAISE NOTICE 'TIMING ESTIMATE for Helper Table Creation';
    RAISE NOTICE '=================================================================';
    RAISE NOTICE 'Sample time (10K rows): % ms', sample_time_ms;
    RAISE NOTICE 'Expected total rows: %', expected_rows;
    RAISE NOTICE 'Estimated batches: %', (expected_rows / rows_per_sample);
    RAISE NOTICE '-----------------------------------------------------------------';
    RAISE NOTICE 'ESTIMATED TOTAL TIME: % minutes (%.2f hours)', 
        estimated_total_time_minutes, 
        estimated_total_time_minutes / 60.0;
    RAISE NOTICE '=================================================================';

    IF estimated_total_time_minutes > 120 THEN
        RAISE WARNING 'SLOW: Table creation may take over 2 hours. Consider optimizing source query.';
    ELSIF estimated_total_time_minutes > 60 THEN
        RAISE NOTICE 'ACCEPTABLE: Table creation will take 1-2 hours (within expected range).';
    ELSE
        RAISE NOTICE 'FAST: Table creation will complete in under 1 hour.';
    END IF;
END $$;

-- =====================================================================================
-- STEP 5: Test Different Sample Sizes (Optional)
-- =====================================================================================
-- Test with multiple sample sizes to verify linear scaling.
-- If timing is NOT linear, the extrapolation may be inaccurate.
-- =====================================================================================

-- Test 1: 1,000 rows
SELECT 
    '1K rows' AS sample_size,
    COUNT(*) AS actual_rows,
    clock_timestamp() AS start_time
FROM (
    SELECT DISTINCT
        stats."CorrespondenceId",
        stats."PartyUuid",
        stats."Status",
        stats."StatusChanged"
    FROM correspondence."CorrespondenceStatuses" stats
    INNER JOIN correspondence."Correspondences" corr 
        ON stats."CorrespondenceId" = corr."Id" 
        AND corr."Altinn2CorrespondenceId" IS NOT NULL 
        AND corr."IsMigrating" = FALSE
        AND stats."SyncedFromAltinn2" IS NULL
    INNER JOIN correspondence."A2Parties" ap 
        ON stats."PartyUuid" = ap."PartyUuid"
        AND corr."Recipient" <> ap."RecipientUrn"
    INNER JOIN correspondence."ExternalReferences" er
        ON stats."CorrespondenceId" = er."CorrespondenceId" 
        AND er."ReferenceType" = 3
    WHERE stats."Status" IN (4, 6)
      AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-12-31 23:59:59'
    LIMIT 1000
) sample;

-- Test 2: 10,000 rows (repeat from STEP 3 with timing)
-- Test 3: 100,000 rows (if time permits)

-- Compare timing:
-- 1K rows: X ms
-- 10K rows: ~10X ms (should scale linearly)
-- 100K rows: ~100X ms (confirms linear scaling)

-- =====================================================================================
-- STEP 6: Verify DISTINCT Impact
-- =====================================================================================
-- Measure how much overhead DISTINCT adds.
-- IdempotencyKeys join can create duplicates, so DISTINCT is necessary.
-- =====================================================================================

-- Without DISTINCT (faster, but may include duplicates)
EXPLAIN (ANALYZE, BUFFERS)
SELECT
    stats."CorrespondenceId",
    stats."PartyUuid",
    stats."Status",
    stats."StatusChanged"
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
    AND stats."SyncedFromAltinn2" IS NULL
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
WHERE stats."Status" IN (4, 6)
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-12-31 23:59:59'
LIMIT 10000;

-- With DISTINCT (required, slower)
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT
    stats."CorrespondenceId",
    stats."PartyUuid",
    stats."Status",
    stats."StatusChanged"
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
    AND stats."SyncedFromAltinn2" IS NULL
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
WHERE stats."Status" IN (4, 6)
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-12-31 23:59:59'
LIMIT 10000;

-- Compare "Execution Time" between the two queries
-- DISTINCT overhead = (time_with_distinct - time_without_distinct)
-- Typical overhead: 10-30% (acceptable for data correctness)

-- =====================================================================================
-- STEP 7: Estimate Storage Requirements
-- =====================================================================================
-- Calculate how much disk space the helper table will consume.
-- =====================================================================================

-- Estimate row size
SELECT 
    'Column Size Estimate' AS analysis,
    pg_column_size(uuid_generate_v4()) AS correspondenceid_size, -- ~16 bytes
    pg_column_size(uuid_generate_v4()) AS partyuuid_size,        -- ~16 bytes
    pg_column_size(4::INTEGER) AS status_size,                   -- ~4 bytes
    pg_column_size(NOW()::TIMESTAMP) AS statuschanged_size,      -- ~8 bytes
    pg_column_size(uuid_generate_v4()) + 
    pg_column_size(uuid_generate_v4()) + 
    pg_column_size(4::INTEGER) + 
    pg_column_size(NOW()::TIMESTAMP) AS total_row_size;          -- ~44 bytes base

-- Extrapolate to 150M rows
DO $$
DECLARE
    row_size_bytes INTEGER := 44; -- Base row size
    overhead_factor NUMERIC := 1.5; -- PostgreSQL overhead (toast, alignment, etc.)
    expected_rows BIGINT := 150000000;
    table_size_gb NUMERIC;
    index_size_gb NUMERIC;
    total_size_gb NUMERIC;
BEGIN
    table_size_gb := (row_size_bytes * overhead_factor * expected_rows) / (1024.0^3);
    index_size_gb := table_size_gb * 0.8; -- Indexes ~80% of table size
    total_size_gb := table_size_gb + index_size_gb;

    RAISE NOTICE '=================================================================';
    RAISE NOTICE 'STORAGE ESTIMATE for Helper Table';
    RAISE NOTICE '=================================================================';
    RAISE NOTICE 'Expected rows: %', expected_rows;
    RAISE NOTICE 'Row size (with overhead): % bytes', (row_size_bytes * overhead_factor)::INTEGER;
    RAISE NOTICE '-----------------------------------------------------------------';
    RAISE NOTICE 'Table size: % GB', ROUND(table_size_gb, 1);
    RAISE NOTICE 'Index size (2 indexes): % GB', ROUND(index_size_gb, 1);
    RAISE NOTICE 'TOTAL STORAGE: % GB', ROUND(total_size_gb, 1);
    RAISE NOTICE '=================================================================';

    IF total_size_gb > 50 THEN
        RAISE WARNING 'LARGE: Requires over 50 GB. Verify sufficient disk space!';
    ELSE
        RAISE NOTICE 'ACCEPTABLE: Storage requirement is within reasonable limits.';
    END IF;
END $$;

-- Check current available space
SELECT 
    pg_size_pretty(pg_database_size(current_database())) AS current_db_size,
    'Verify at least 50 GB free space' AS recommendation;

-- =====================================================================================
-- STEP 8: Test Actual Table Creation with Small Subset
-- =====================================================================================
-- Create a TEST table with only 100K rows to verify the full process works.
-- This tests the entire CREATE TABLE AS SELECT pipeline.
--
-- EXECUTION TIME: 1-5 minutes (vs 30-60 min for full table)
-- =====================================================================================

-- Drop test table if exists
DROP TABLE IF EXISTS correspondence."A2Iss1951MigratedEvents_TEST";

-- Create test table with LIMIT
CREATE TABLE correspondence."A2Iss1951MigratedEvents_TEST" AS
SELECT DISTINCT
    stats."CorrespondenceId",
    stats."PartyUuid",
    stats."Status",
    stats."StatusChanged"
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
    AND stats."SyncedFromAltinn2" IS NULL
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
WHERE stats."Status" IN (4, 6)
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-12-31 23:59:59'
LIMIT 100000; -- Only 100K rows for testing

-- Verify test table was created
SELECT 
    'A2Iss1951MigratedEvents_TEST' AS table_name,
    COUNT(*) AS row_count,
    COUNT(*) FILTER (WHERE "Status" = 4) AS status_4_count,
    COUNT(*) FILTER (WHERE "Status" = 6) AS status_6_count,
    pg_size_pretty(pg_total_relation_size('correspondence."A2Iss1951MigratedEvents_TEST"')) AS table_size
FROM correspondence."A2Iss1951MigratedEvents_TEST";

-- Test creating index on test table
CREATE INDEX "IX_TEST_CorrespondenceId_Status"
ON correspondence."A2Iss1951MigratedEvents_TEST" ("CorrespondenceId", "Status");

-- Measure index creation time (extrapolate to full table)
-- Time to index 100K rows × 1500 = Time to index 150M rows

-- Test query performance on test table
EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM correspondence."A2Iss1951MigratedEvents_TEST"
WHERE "Status" = 4
  AND "CorrespondenceId" > '00000000-0000-0000-0000-000000000000'::uuid
ORDER BY "CorrespondenceId"
LIMIT 5000;

-- Should show "Index Scan using IX_TEST_CorrespondenceId_Status"
-- Execution time should be < 50ms

-- Clean up test table when done
-- DROP TABLE correspondence."A2Iss1951MigratedEvents_TEST";

-- =====================================================================================
-- DECISION MATRIX: Should You Proceed?
-- =====================================================================================
--
-- ✅ PROCEED if:
--    - STEP 3 execution time < 1 second per 10K rows (< 30 min total)
--    - STEP 4 estimate < 90 minutes
--    - STEP 7 storage estimate < 50 GB and space available
--    - STEP 8 test table created successfully
--
-- ⚠️ OPTIMIZE FIRST if:
--    - STEP 3 execution time > 3 seconds per 10K rows (> 90 min total)
--    - Sequential Scans dominating EXPLAIN ANALYZE
--    - Excessive I/O (Buffers: read >> hit)
--    - Consider adding indexes to source tables first
--
-- ❌ ABORT if:
--    - STEP 3 execution time > 10 seconds per 10K rows (> 5 hours total)
--    - Not enough disk space (< 50 GB available)
--    - Query plan shows inefficient join strategies
--    - Need to rethink optimization approach
--
-- =====================================================================================
-- NEXT STEPS AFTER ANALYSIS
-- =====================================================================================
--
-- If analysis looks good (✅ PROCEED):
--   1. Note the estimated time from STEP 4
--   2. Schedule helper table creation during off-peak hours (if possible)
--   3. Run Create_A2Iss1951MigratedEvents_Helper_Table.sql
--   4. Monitor progress: SELECT COUNT(*) FROM correspondence."A2Iss1951MigratedEvents";
--   5. Create indexes after table is populated
--
-- If analysis is concerning (⚠️ OPTIMIZE FIRST):
--   1. Review EXPLAIN ANALYZE output from STEP 2 and 3
--   2. Check if CorrespondenceStatuses needs better indexes for SyncedFromAltinn2
--   3. Consider VACUUM ANALYZE on source tables
--   4. Re-run analysis after optimizations
--
-- If analysis is terrible (❌ ABORT):
--   1. Discuss alternative approaches (e.g., incremental population)
--   2. Consider using materialized view with incremental refresh
--   3. May need to partition CorrespondenceStatuses first
--
-- =====================================================================================
