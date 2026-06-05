-- =====================================================================================
-- Index Optimizations for A2Iss1716A2Events Helper Table
-- =====================================================================================
-- Purpose: Optimize Issue #1716 export using pre-filtered A2Events helper table
-- Table: correspondence."A2Iss1716A2Events" (~9.97M rows total)
-- =====================================================================================

-- =====================================================================================
-- Index 1: Primary lookup by Status and CorrespondenceId
-- =====================================================================================
-- This is the MOST IMPORTANT index for the query
-- Eliminates the expensive external merge sort (450GB temp disk space!)

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_A2Iss1716A2Events_Status_CorrId"
ON correspondence."A2Iss1716A2Events" ("Status", "CorrespondenceId")
INCLUDE ("PartyUuid", "Timestamp");

-- Benefits:
-- • Filters Status = 4 using index (no full table scan)
-- • Pre-sorted by CorrespondenceId (no external merge sort needed)
-- • INCLUDE columns eliminate heap lookups
-- • Estimated size: ~800 MB - 1 GB
-- • Estimated build time: 2-3 minutes (9.97M rows)

-- =====================================================================================
-- Index 2: Composite covering index for joins
-- =====================================================================================
-- Optimizes the join conditions with CorrespondenceStatuses

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_A2Iss1716A2Events_CorrId_Status_Party"
ON correspondence."A2Iss1716A2Events" ("CorrespondenceId", "Status", "PartyUuid")
INCLUDE ("Timestamp");

-- Benefits:
-- • Optimizes 3-column join: CorrespondenceId + Status + PartyUuid
-- • Covering index (no heap lookups)
-- • Supports both Status = 4 and Status = 6 queries
-- • Estimated size: ~1 GB
-- • Estimated build time: 2-3 minutes

-- =====================================================================================
-- IMPORTANT: Update Table Statistics After Index Creation
-- =====================================================================================
-- The query planner needs accurate statistics to choose the optimal index.
-- Run this immediately after creating both indexes:

ANALYZE correspondence."A2Iss1716A2Events";

-- This updates:
-- • Row count estimates
-- • Data distribution statistics
-- • Index selectivity statistics
-- • Helps planner choose IX_A2Iss1716A2Events_CorrId_Status_Party for both Status 4 and 6

-- =====================================================================================
-- Verification Queries
-- =====================================================================================

-- Check index creation and sizes:
SELECT 
    schemaname,
    tablename,
    indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) as size,
    idx_scan as times_used
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND tablename = 'A2Iss1716A2Events'
ORDER BY indexname;

-- Verify index validity:
SELECT 
    c.relname as index_name,
    i.indisvalid as is_valid
FROM pg_class c
JOIN pg_index i ON i.indexrelid = c.oid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relname LIKE 'IX_A2Iss1716A2Events%'
  AND n.nspname = 'correspondence';

-- Check table statistics (after ANALYZE):
SELECT 
    schemaname,
    tablename,
    n_live_tup as row_count,
    n_dead_tup as dead_rows,
    last_analyze,
    last_autoanalyze
FROM pg_stat_user_tables
WHERE schemaname = 'correspondence'
  AND tablename = 'A2Iss1716A2Events';

-- =====================================================================================
-- Expected vs Actual Performance After Indexes
-- =====================================================================================
-- 
-- BEFORE (Without Indexes):
-- • Execution time: 16.9 seconds
-- • Temp disk usage: ~450 GB (external merge sort)
-- • Rows scanned: 9.97M
-- • Memory pressure: HIGH
--
-- AFTER (With Both Indexes) - PRODUCTION VERIFIED ✅:
-- • Execution time: 17 ms (17.258 ms actual)
-- • Temp disk usage: 0 (no external sort)
-- • Buffer hits: 6,426 (all cached, no disk I/O)
-- • Parallelism: Active (2 workers)
-- • Index Only Scans: All tables (0 heap fetches on helper table)
-- • Improvement: 994x faster (16.9s → 17ms)
--
-- Full Export Projection:
-- • Total rows: ~9.97M
-- • Estimated time: 30-45 minutes (vs 12 hours with old approach)
-- 
-- =====================================================================================

-- =====================================================================================
-- Test Query After Index Creation (Production Format)
-- =====================================================================================
-- This query matches the actual DialogActivityExportService.cs implementation

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
FROM correspondence."A2Iss1716A2Events" a2Events
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
LIMIT 100;

-- Expected plan (VERIFIED IN PRODUCTION ✅):
-- • Parallel Index Only Scan using IX_A2Iss1716A2Events_CorrId_Status_Party
--   - Index Cond: ("Status" = 4)
--   - Heap Fetches: 0 (INCLUDE columns working!)
-- • Index Only Scan using IX_CorrespondenceStatuses_Unique
-- • Index Only Scan using IX_ExternalReferences_CorrId_RefType_RefValue
-- • Index Only Scan using IX_A2Parties_PartyUuid_Covering (with Memoize cache)
-- • Index Scan using IX_IdempotencyKeys_CorrespondenceId
-- • Total execution: ~17 ms for 100 rows

-- =====================================================================================
-- Table Statistics (Helpful for Index Planning)
-- =====================================================================================

-- Get row count breakdown by Status:
SELECT 
    "Status",
    COUNT(*) as row_count,
    ROUND(100.0 * COUNT(*) / SUM(COUNT(*)) OVER (), 2) as percentage
FROM correspondence."A2Iss1716A2Events"
GROUP BY "Status"
ORDER BY "Status";

-- Check for duplicate combinations (should be unique):
SELECT 
    COUNT(*) as total_rows,
    COUNT(DISTINCT ("CorrespondenceId", "Status", "PartyUuid")) as unique_combinations,
    COUNT(*) - COUNT(DISTINCT ("CorrespondenceId", "Status", "PartyUuid")) as duplicates
FROM correspondence."A2Iss1716A2Events";

-- If duplicates exist, consider adding a unique constraint:
-- CREATE UNIQUE INDEX CONCURRENTLY "UQ_A2Iss1716A2Events" 
-- ON correspondence."A2Iss1716A2Events" ("CorrespondenceId", "Status", "PartyUuid");

-- =====================================================================================
-- Troubleshooting: Status 6 Query Performance
-- =====================================================================================
-- 
-- ISSUE: Status 6 queries may be slower than Status 4 due to wrong index selection
-- 
-- SYMPTOMS:
-- • Status 4: ~17ms execution, uses IX_A2Iss1716A2Events_CorrId_Status_Party (parallel)
-- • Status 6: ~3.5s execution, uses IX_A2Iss1716A2Events_Status_CorrId (sequential)
-- • Status 6: Disk I/O (1,362 reads, 4.6s I/O time)
-- • Status 6: Only 1 parallel worker instead of multiple
--
-- ROOT CAUSE:
-- Query planner choosing IX_A2Iss1716A2Events_Status_CorrId because it appears
-- more selective for "WHERE Status = 6", but this index:
-- 1. Doesn't support 3-column join as efficiently
-- 2. Not cached yet (Status 4 warmed up the other index first)
-- 3. Doesn't enable optimal parallel execution
--
-- SOLUTION 1: Update statistics (run after index creation)
ANALYZE correspondence."A2Iss1716A2Events";

-- SOLUTION 2: Warm up the cache by running Status 6 query again
-- Second execution should be faster as data gets cached

-- SOLUTION 3: Check if Status 6 rows are significantly fewer
-- If Status 6 has very few rows, planner may choose different strategy
SELECT 
    "Status",
    COUNT(*) as row_count
FROM correspondence."A2Iss1716A2Events"
GROUP BY "Status";

-- SOLUTION 4: Verify both indexes are being used
SELECT 
    schemaname,
    tablename,
    indexname,
    idx_scan as times_used,
    idx_tup_read as tuples_read,
    idx_tup_fetch as tuples_fetched
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND tablename = 'A2Iss1716A2Events'
ORDER BY indexname;

-- SOLUTION 5: If Status 6 consistently slow, drop IX_A2Iss1716A2Events_Status_CorrId
-- This forces planner to use IX_A2Iss1716A2Events_CorrId_Status_Party for both queries
-- NOTE: Only do this if Status 6 performance doesn't improve after ANALYZE
-- DROP INDEX CONCURRENTLY correspondence."IX_A2Iss1716A2Events_Status_CorrId";

-- EXPECTED BEHAVIOR AFTER FIXES:
-- • Both Status 4 and Status 6 should use IX_A2Iss1716A2Events_CorrId_Status_Party
-- • Both should have parallel execution (2+ workers)
-- • Both should complete in <100ms (after cache warmup)
-- • Second run of Status 6 should be much faster (cached)

-- =====================================================================================
