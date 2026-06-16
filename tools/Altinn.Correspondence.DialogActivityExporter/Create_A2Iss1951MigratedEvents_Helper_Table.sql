-- =====================================================================================
-- Create Helper Table for Issue #1951: Migrated Events Export Optimization
-- =====================================================================================
-- 
-- PURPOSE:
--   Create a pre-filtered helper table containing all affected correspondence events
--   for Issue #1951 (Migrated Events, NOT synced from Altinn2). This table dramatically
--   improves export performance by reducing the query scope from 1.94B rows to ~150M.
--
-- ISSUE REFERENCE:
--   Issue #1951: Dialog Activity Export - Migrated Events
--   Estimated Records: ~150 million (Status 4 + Status 6)
--
-- APPROACH:
--   Similar to Issue #1716's A2Iss1716A2Events helper table, but for migrated events.
--   Pre-filters using all the complex JOIN conditions and stores only the relevant
--   CorrespondenceId, PartyUuid, Status, and StatusChanged columns.
--
-- PERFORMANCE:
--   - Without helper table: Query time 500-1500ms per batch, 100-200 hour export
--   - With helper table: Query time 100-200ms per batch, ~33 hour export (3-6x faster)
--
-- EXECUTION TIME:
--   - Table creation: 30-60 minutes (one-time setup)
--   - Index creation: 20-40 minutes (CONCURRENTLY, non-blocking)
--   - Total: ~50-100 minutes for complete setup
--
-- STORAGE:
--   - Table size: ~20-30 GB (estimated for 150M rows)
--   - Index size: ~10-15 GB (covering index)
--   - Total: ~30-45 GB (verify available space before running)
--
-- PREREQUISITES:
--   1. Sufficient disk space: ~50 GB free (table + indexes)
--   2. Azure AD authentication or connection string configured
--   3. PostgreSQL 12+ (for INCLUDE columns in indexes)
--   4. Maintenance window recommended (but CONCURRENTLY allows concurrent operations)
-
-- POST-CREATION:
--   After running this script, update DialogActivityExportService.cs to use the
--   helper table query for Issue #1951 (see ISSUE_1951_OPTIMIZATION_STRATEGY.md).
--
-- =====================================================================================

-- =====================================================================================
-- SECTION 1: PRE-FLIGHT CHECKS
-- =====================================================================================

-- Check available disk space (should have ~50 GB free)
SELECT 
    pg_size_pretty(pg_database_size(current_database())) AS current_db_size,
    'Verify at least 50 GB free space before proceeding' AS action_required;

-- Verify source table row counts
SELECT 
    'CorrespondenceStatuses' AS table_name,
    COUNT(*) AS total_rows,
    COUNT(*) FILTER (WHERE "SyncedFromAltinn2" IS NULL) AS null_synced_rows,
    COUNT(*) FILTER (WHERE "Status" IN (4, 6) AND "SyncedFromAltinn2" IS NULL) AS affected_rows
FROM correspondence."CorrespondenceStatuses";

-- Expected: ~1.94B total rows, ~150M affected rows

-- =====================================================================================
-- SECTION 2: CREATE HELPER TABLE
-- =====================================================================================
-- 
-- LOGIC:
--   This query replicates the exact filters from the export query to ensure
--   consistent results. It pre-filters by:
--   1. Altinn2CorrespondenceId IS NOT NULL (migrated correspondences)
--   2. IsMigrating = FALSE (migration completed)
--   3. SyncedFromAltinn2 IS NULL (NOT synced from Altinn2 - defines Issue #1951)
--   4. A2Parties match with Recipient <> RecipientUrn (party filter)
--   5. ExternalReferences ReferenceType = 3 (DialogId exists)
--   6. Status IN (4, 6) (only affected statuses)
--   7. StatusChanged BETWEEN '2019-03-23' and cutoff (time range)
--
-- IMPORTANT: DEDUPLICATION STRATEGY
--   DO NOT use DISTINCT to remove duplicate timestamps from Altinn 2 source data!
--   Lesson learned from Issue #1716: Deduplication at import time caused inconsistencies.
--   
--   BETTER APPROACH:
--   - Keep ALL events from Altinn 2, including duplicates with different timestamps
--   - Filter duplicates at QUERY time by matching stats.StatusChanged = migEvents.StatusChanged
--   - This ensures we export the exact event that exists in CorrespondenceStatuses
--   
--   See: TIMESTAMP_MATCHING_FIX.md for detailed explanation
--
-- EXECUTION TIME: 30-60 minutes
-- =====================================================================================

-- Drop table if exists (for re-runs, BE CAREFUL in production!)
-- DROP TABLE IF EXISTS correspondence."A2Iss1951MigratedEvents";

BEGIN;

-- Create table WITHOUT deduplicating on timestamp
-- We'll filter duplicates at query time using timestamp matching
-- Includes "Source" column for troubleshooting (0 = ServiceEngine, 1 = Archive)
CREATE TABLE correspondence."A2Iss1951MigratedEvents" AS
SELECT 
    stats."CorrespondenceId",
    stats."PartyUuid",
    stats."Status",
    0 AS "Source",  -- TODO: Set from Altinn 2 source (0=ServiceEngine, 1=Archive)
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
  -- UPDATE CUTOFF TIMESTAMP: Use same cutoff as your export run

COMMIT;

-- Verify row count
SELECT 
    'A2Iss1951MigratedEvents' AS table_name,
    COUNT(*) AS total_rows,
    COUNT(*) FILTER (WHERE "Status" = 4) AS status_4_rows,
    COUNT(*) FILTER (WHERE "Status" = 6) AS status_6_rows,
    pg_size_pretty(pg_total_relation_size('correspondence."A2Iss1951MigratedEvents"')) AS table_size
FROM correspondence."A2Iss1951MigratedEvents";

-- Expected: ~150M total rows (Status 4 + Status 6)
-- If row count is significantly different, investigate before proceeding

-- =====================================================================================
-- SECTION 3: CREATE OPTIMIZED INDEXES
-- =====================================================================================
--
-- INDEX 1: Primary cursor index (CorrespondenceId, Status)
--   - Enables efficient cursor pagination (WHERE CorrespondenceId > @lastId)
--   - Supports ORDER BY CorrespondenceId
--   - Used by both Status 4 and Status 6 queries
--
-- INDEX 2: Covering index (CorrespondenceId, Status) INCLUDE (PartyUuid, StatusChanged)
--   - Enables Index-Only Scans (no heap access needed)
--   - Includes all columns needed by the export query
--   - Dramatically reduces I/O
--
-- NOTE: Using CREATE INDEX CONCURRENTLY to avoid blocking concurrent operations
--       This takes longer but allows normal database operations to continue
--
-- EXECUTION TIME: 20-40 minutes (CONCURRENTLY adds overhead but avoids blocking)
-- =====================================================================================

-- Index 1: Primary cursor index for pagination
CREATE INDEX CONCURRENTLY "IX_A2Iss1951MigratedEvents_CorrespondenceId_Status"
ON correspondence."A2Iss1951MigratedEvents" ("CorrespondenceId", "Status");

-- Index 2: Covering index for Index-Only Scans
CREATE INDEX CONCURRENTLY "IX_A2Iss1951MigratedEvents_Covering"
ON correspondence."A2Iss1951MigratedEvents" ("CorrespondenceId", "Status")
INCLUDE ("PartyUuid", "StatusChanged");

-- Verify indexes were created
SELECT 
    schemaname,
    tablename,
    indexname,
    pg_size_pretty(pg_relation_size(indexname::regclass)) AS index_size
FROM pg_indexes
WHERE tablename = 'A2Iss1951MigratedEvents'
ORDER BY indexname;

-- Expected: 2 indexes, ~10-15 GB total size

-- =====================================================================================
-- SECTION 4: UPDATE TABLE STATISTICS
-- =====================================================================================
--
-- Run ANALYZE to update query planner statistics
-- This ensures PostgreSQL can make optimal query plans using the new indexes
--
-- EXECUTION TIME: 2-5 minutes
-- =====================================================================================

ANALYZE correspondence."A2Iss1951MigratedEvents";

-- Verify statistics were updated
SELECT 
    schemaname,
    tablename,
    last_analyze,
    last_autoanalyze,
    n_live_tup AS estimated_rows
FROM pg_stat_user_tables
WHERE tablename = 'A2Iss1951MigratedEvents';

-- =====================================================================================
-- SECTION 5: VERIFY PERFORMANCE
-- =====================================================================================
--
-- Run sample queries to verify index usage and query performance
-- These should complete in < 200ms per query
-- =====================================================================================

-- Test Status 4 query (should use covering index)
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
FROM correspondence."A2Iss1951MigratedEvents" migEvents
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON migEvents."CorrespondenceId" = stats."CorrespondenceId" 
    AND migEvents."Status" = stats."Status" 
    AND migEvents."PartyUuid" = stats."PartyUuid"
INNER JOIN correspondence."ExternalReferences" er
    ON migEvents."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcFetch
    ON migEvents."CorrespondenceId" = idcFetch."CorrespondenceId"
    AND idcFetch."StatusAction" = 'Fetch'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE migEvents."Status" = 4
ORDER BY migEvents."CorrespondenceId"
LIMIT 5000;

-- Check for:
-- ✅ "Index Only Scan using IX_A2Iss1951MigratedEvents_Covering"
-- ✅ Execution Time: < 200ms
-- ✅ Buffers: shared hit (no read from disk if cached)

-- Test Status 6 query (should use covering index)
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
FROM correspondence."A2Iss1951MigratedEvents" migEvents
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON migEvents."CorrespondenceId" = stats."CorrespondenceId" 
    AND migEvents."Status" = stats."Status" 
    AND migEvents."PartyUuid" = stats."PartyUuid"
INNER JOIN correspondence."ExternalReferences" er
    ON migEvents."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcConfirm
    ON migEvents."CorrespondenceId" = idcConfirm."CorrespondenceId"
    AND idcConfirm."StatusAction" = 'Confirm'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE migEvents."Status" = 6
ORDER BY migEvents."CorrespondenceId"
LIMIT 5000;

-- Check for same performance characteristics as Status 4 query

-- Test cursor pagination (should use primary index)
EXPLAIN (ANALYZE, BUFFERS)
SELECT "CorrespondenceId", "Status"
FROM correspondence."A2Iss1951MigratedEvents"
WHERE "Status" = 4
  AND "CorrespondenceId" > '00000000-0000-0000-0000-000000000000'::uuid
ORDER BY "CorrespondenceId"
LIMIT 5000;

-- Check for:
-- ✅ "Index Scan using IX_A2Iss1951MigratedEvents_CorrespondenceId_Status" or
-- ✅ "Index Only Scan using IX_A2Iss1951MigratedEvents_Covering"
-- ✅ Execution Time: < 50ms

-- =====================================================================================
-- SECTION 6: FINAL VERIFICATION
-- =====================================================================================

-- Verify table and indexes are ready for production use
SELECT 
    'Helper table A2Iss1951MigratedEvents created successfully' AS status,
    (SELECT COUNT(*) FROM correspondence."A2Iss1951MigratedEvents") AS total_rows,
    (SELECT COUNT(*) FROM pg_indexes WHERE tablename = 'A2Iss1951MigratedEvents') AS index_count,
    pg_size_pretty(pg_total_relation_size('correspondence."A2Iss1951MigratedEvents"')) AS total_size;

-- Expected output:
-- status: "Helper table A2Iss1951MigratedEvents created successfully"
-- total_rows: ~150,000,000
-- index_count: 2
-- total_size: ~30-45 GB

-- =====================================================================================
-- NEXT STEPS
-- =====================================================================================
--
-- 1. ✅ Helper table created with ~150M rows
-- 2. ✅ Indexes created and verified (Index-Only Scans enabled)
-- 3. ✅ Statistics updated (ANALYZE completed)
-- 4. ✅ Performance verified (< 200ms query time)
--
-- 5. ⏭️ Update DialogActivityExportService.cs to use helper table for Issue #1951
--    - Add issueNumber == 1951 branch in FetchStatusRecordsAsync
--    - Use query from ISSUE_1951_OPTIMIZATION_STRATEGY.md
--
-- 6. ⏭️ Update GetTotalCountAsync to query helper table for counts
--    - Add issueNumber == 1951 branch
--    - Query: SELECT COUNT(*) FROM correspondence."A2Iss1951MigratedEvents" WHERE "Status" IN (4, 6)
--
-- 7. ⏭️ Test with --max-batches 10 (50K rows)
--    - Verify query timing ~100-200ms per batch
--    - Check EXPLAIN ANALYZE shows index usage
--    - Validate CSV output correctness
--
-- 8. ⏭️ Run production export (~33 hours for 150M rows @ 125 rows/sec)
--
-- =====================================================================================
-- TROUBLESHOOTING
-- =====================================================================================
--
-- If table creation fails with "out of memory":
--   - Increase work_mem: SET work_mem = '2GB';
--   - Or create in smaller batches with INSERT INTO ... SELECT WHERE conditions
--
-- If index creation is too slow:
--   - Remove CONCURRENTLY (but this will block table access during creation)
--   - Or schedule during maintenance window
--
-- If query still slow after optimization:
--   - Verify indexes are being used: EXPLAIN (ANALYZE) your query
--   - Check for bloat: SELECT * FROM pg_stat_user_tables WHERE tablename = 'A2Iss1951MigratedEvents';
--   - Rebuild indexes if needed: REINDEX TABLE CONCURRENTLY correspondence."A2Iss1951MigratedEvents";
--
-- If row count is wrong:
--   - Verify cutoff timestamp matches your export parameters
--   - Re-check source table filters (Altinn2CorrespondenceId, IsMigrating, etc.)
--   - Run COUNT queries from calculate-counts.sql to cross-verify
--
-- =====================================================================================
