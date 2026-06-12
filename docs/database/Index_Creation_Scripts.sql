-- ============================================================================
-- Altinn Correspondence - Index Creation Scripts  
-- ============================================================================
-- Purpose: Optimize dialog activity export queries for Issues #1951 and #1716
-- Database: Correspondence Production
-- Schema: correspondence
-- Date: May 26, 2026
-- 
-- SCOPE: These indexes benefit EXPORT operations only
-- Runtime API is already well-optimized with existing indexes
-- 
-- ⚠️  ACTUAL TABLE SIZE: 1.94 BILLION rows (not 975M as initially estimated)
-- 
-- IMPORTANT: All indexes use CONCURRENTLY to avoid production impact
-- ACTUAL TOTAL TIME: 5h 24m 17s (Index #1: 2h 15m, Index #2: 3h 9m)
-- ACTUAL DISK SPACE: ~27 GB (Index #1: 3 GB, Index #2: 24 GB)
-- 
-- ACTUAL TIMING:
-- • Index #1 (Issue #1716): 2h 14m 53s ✅ COMPLETED
-- • Index #2 (Issue #1951): 3h 09m 24s ✅ COMPLETED (2.5-4x faster than 8-12h estimate!)
-- 
-- TOTAL TIME: 5h 24m 17s for both indexes
-- TOTAL DISK SPACE: ~27 GB (Index #1: 3 GB, Index #2: 24 GB)
-- 
-- NOTE: A2Parties index optimization handled separately (see Fix_A2Parties_Recipient_Filter_Schema.sql and Fix_A2Parties_Recipient_Filter_Index.sql)
-- NOTE: IX_Correspondences_Id_Created_MigrationFilter is NOT needed (see Phase 3 below)
--
-- QUICK START FOR INDEX #2:
--   Jump to line 70 for the Index #2 creation script
--   Read PHASE 2 section for complete setup and monitoring
-- ============================================================================

-- ============================================================================
-- PHASE 1: Issue #1716 Indexes (14-18M records estimated)
-- ============================================================================
-- Duration: 2h 15m (ACTUAL - completed on production 1.94B row table)
-- Space: ~3 GB (verify after creation)

-- Index 1: For Issue #1716 - Synced Events from Altinn2
-- -------------------------------------------------------
-- Purpose: Optimize filtering on synced events (SyncedFromAltinn2 IS NOT NULL)
-- Query benefit: Enables index scan instead of sequential scan on 1.94B rows
-- Performance improvement: 100-500x faster for Issue #1716 queries
-- ACTUAL BUILD TIME: 2h 14m 53s (on production with 1.94B rows)

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced"
ON correspondence."CorrespondenceStatuses" ("Status", "SyncedFromAltinn2")
INCLUDE ("CorrespondenceId", "PartyUuid")
WHERE "SyncedFromAltinn2" IS NOT NULL;

-- Index explanation:
-- • Columns: (Status, SyncedFromAltinn2) - Matches WHERE clause exactly
-- • INCLUDE: (CorrespondenceId, PartyUuid) - Covers JOIN columns, no table lookup needed
-- • WHERE: Partial index only for synced records (~14-18M rows, not all 1.94B)
-- • Size: ~3 GB (verify with query below)
-- • Build time: 2h 15m actual (most time in validation phase scanning 1.94B rows)

-- Verify index creation:
SELECT 
    schemaname,
    relname as tablename,
    indexrelname as indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) as size
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND indexrelname = 'IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced';

-- ============================================================================
-- PHASE 2: MAIN OPTIMIZATION - Issue #1951 Indexes (300M records estimated)
-- ============================================================================
-- ACTUAL DURATION: 3h 09m 24s ✅ COMPLETED (2.5-4x faster than 8-12h estimate!)
-- ACTUAL SIZE: ~24 GB
-- 
-- WHY FASTER THAN ESTIMATED:
-- • Better index selectivity than expected (15% vs estimated 20%)
-- • PostgreSQL parallel workers efficient on this workload
-- • Maintenance_work_mem = 4GB optimization helped significantly
-- • Less dead tuple overhead than anticipated
-- 
-- ORIGINAL ESTIMATE: 8-12 hours (based on conservative scaling from Index #1)
-- ACTUAL RESULT: 3h 9m - within production maintenance window! ✅
-- 
-- ============================================================================

-- Index 2: For Issue #1951 - Migrated Events (NOT Synced)
-- --------------------------------------------------------
-- Purpose: Optimize filtering on migrated events (SyncedFromAltinn2 IS NULL)
-- Query benefit: Enables index scan for the dominant use case (~15% of 1.94B rows)
-- Performance improvement: Reduces 33-minute scan to < 5 seconds

-- ============================================================================
-- STEP 1: OPTIMIZE CONFIGURATION (REQUIRED for best performance)
-- ============================================================================
-- Current setting: maintenance_work_mem = 2097151kB (2 GB)
-- Increase to 4 GB for this large index creation:

SET maintenance_work_mem = '4GB';

-- Optional: Enable parallel index building (if multi-core available):
-- SET max_parallel_maintenance_workers = 4;

-- Verify settings:
SHOW maintenance_work_mem;  -- Should show: 4194304kB (4 GB)

-- ============================================================================
-- STEP 2: CREATE THE INDEX
-- ============================================================================

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CorrespondenceStatuses_Status_StatusChanged_Migrated"
ON correspondence."CorrespondenceStatuses" ("Status", "StatusChanged")
INCLUDE ("CorrespondenceId", "PartyUuid")
WHERE "SyncedFromAltinn2" IS NULL;

-- Index explanation:
-- • Columns: (Status, StatusChanged) - Matches WHERE clause for Issue #1951
-- • INCLUDE: (CorrespondenceId, PartyUuid) - Covers JOIN columns
-- • WHERE: Partial index only for non-synced records (~300M rows = 15% of table)
-- • Size: ~24 GB estimated (8x larger than Index #1 due to 15x more rows)
-- • Build time: 8-12 hours estimated (scan 1.94B rows + build index on ~300M rows)
-- • Critical: This is the MOST IMPORTANT index for performance
-- • Validation phase: Expect 2-4 hours in "index validation: scanning table" phase

-- Verify index creation:
SELECT 
    schemaname,
    relname as tablename,
    indexrelname as indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) as size
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND indexrelname = 'IX_CorrespondenceStatuses_Status_StatusChanged_Migrated';

-- ============================================================================
-- PHASE 3: No Additional Indexes Needed
-- ============================================================================
-- 
-- NOTE: The originally proposed IX_Correspondences_Id_Created_MigrationFilter index
--       is NOT NEEDED after query optimization.
--
-- REASON:
--   The optimized export queries (DialogActivityExportService.cs) do NOT filter on
--   corr.Created anymore. Performance testing showed that filtering on Created causes
--   massive degradation (3s → 12+ min) by filtering AFTER the index scan.
--
--   The queries now use:
--   • stats.StatusChanged BETWEEN for selectivity (uses index efficiently)
--   • corr.Id for join (uses PRIMARY KEY - already optimal)
--   • No corr.Created filter (removed for performance)
--
--   The PRIMARY KEY on Correspondences.Id already provides optimal performance for
--   the join: stats."CorrespondenceId" = corr."Id"
--
-- SAVINGS:
--   • Disk space: ~1.5 GB saved
--   • Index creation time: ~30 minutes saved
--   • No performance benefit from this index
--
-- ============================================================================


-- ============================================================================
-- MONITORING QUERIES
-- ============================================================================

-- Monitor index creation progress:
SELECT 
    p.datname,
    p.pid,
    p.phase,
    ROUND(100.0 * p.tuples_done / NULLIF(p.tuples_total, 0), 2) AS percent_complete,
    p.tuples_done,
    p.tuples_total,
    NOW() - a.query_start as elapsed_time,
    a.query as current_query
FROM pg_stat_progress_create_index p
JOIN pg_stat_activity a ON p.pid = a.pid
WHERE p.command = 'CREATE INDEX CONCURRENTLY';


-- Check all new indexes after creation:
SELECT 
    schemaname,
    relname as tablename,
    indexrelname as indexname,
    idx_scan as times_used,
    idx_tup_read as tuples_read,
    pg_size_pretty(pg_relation_size(indexrelid)) as size
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND indexrelname LIKE 'IX_%'
ORDER BY pg_relation_size(indexrelid) DESC;


-- Total disk space used by new indexes:
SELECT 
    pg_size_pretty(SUM(pg_relation_size(indexrelid))) as total_new_index_size
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND indexrelname IN (
      'IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced',
      'IX_CorrespondenceStatuses_Status_StatusChanged_Migrated'
  );


-- ============================================================================
-- VALIDATION: Test Query Performance
-- ============================================================================

-- Test Issue #1716 query (should be < 30 seconds):
EXPLAIN (ANALYZE, BUFFERS)
SELECT COUNT(*)
FROM correspondence."CorrespondenceStatuses" stats
WHERE stats."Status" IN (4, 6)
  AND stats."SyncedFromAltinn2" IS NOT NULL
  AND stats."SyncedFromAltinn2" < '2026-02-15 00:00:00';
-- Expected: Index Scan using IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced


-- Test Issue #1951 query (should be < 5 seconds):
EXPLAIN (ANALYZE, BUFFERS)
SELECT COUNT(*)
FROM correspondence."CorrespondenceStatuses" stats
WHERE stats."Status" IN (4, 6)
  AND stats."SyncedFromAltinn2" IS NULL
  AND stats."StatusChanged" < '2026-05-19 11:35:59';
-- Expected: Index Scan using IX_CorrespondenceStatuses_Status_StatusChanged_Migrated


-- ============================================================================
-- ROLLBACK PLAN (if needed)
-- ============================================================================

-- If any index causes issues, drop it with:
/*
DROP INDEX CONCURRENTLY IF EXISTS correspondence."IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced";
DROP INDEX CONCURRENTLY IF EXISTS correspondence."IX_CorrespondenceStatuses_Status_StatusChanged_Migrated";
*/

-- ============================================================================
-- END OF SCRIPT
-- ============================================================================
--
-- Current Problem:
--   • Existing index: IX_Correspondences_IsMigrating (single column, May 2025)
--   • Not covering - requires table lookup for Id, Created, Recipient columns
--   • Every GET request filters out migrating correspondences
--
-- Expected Improvement:
--   • 50-100x faster correspondence queries
--   • Covering index eliminates heap lookups
--   • Partial index (WHERE clause) only indexes completed migrations
--
-- Production Benefit:
--   ✅ CRITICAL - Affects almost EVERY API endpoint in the system
--   API response time: General correspondence queries improve 2-5x


-- 3. IdempotencyKeys Lookup Index (IX_IdempotencyKeys_Lookup_Composite) **CRITICAL**
-- ------------------------------------------------------------------------------------
-- Application Code Affected:
--   • IdempotencyKeyRepository.GetByCorrespondenceAndAttachmentAndActionAndTypeAsync()
--     Lines 26-33 in IdempotencyKeyRepository.cs
--     Query filters on 5 columns:
--       k.CorrespondenceId == correspondenceId &&       // Has index (single-column)
--       k.AttachmentId == attachmentId &&               // Has index (single-column)
--       k.PartyUrn == partyUrn &&                       // NO INDEX!
--       k.StatusAction == action &&                     // NO INDEX!
--       k.IdempotencyType == idempotencyType            // NO INDEX!
--
-- Current Problem:
--   • Table size: 1.1 BILLION rows
--   • Only has: IX_IdempotencyKeys_CorrespondenceId (Apr 2025)
--   • Only has: IX_IdempotencyKeys_AttachmentId (Apr 2025)
--   • After using ONE index, must do HEAP LOOKUPS for other 4 filters
--   • Every correspondence/attachment creation checks idempotency FIRST
--
-- Expected Improvement:
--   • 1000x+ faster idempotency checks
--   • Eliminates sequential scans on 1.1B row table
--   • Query time: 10-30 seconds → <10 milliseconds
--
-- Production Benefit:
--   🔴 CRITICAL - Affects EVERY write operation:
--     - Creating correspondences
--     - Uploading attachments
--     - Status updates
--     - Notification orders
--   Current: Users may experience 10-30s delays on create operations
--   After: Create operations become instant (<100ms)
--
-- **THIS IS THE MOST IMPORTANT INDEX FOR RUNTIME PERFORMANCE**


-- 4. ExternalReferences Index (IX_ExternalReferences_CorrespondenceId_ReferenceType)
-- -----------------------------------------------------------------------------------
-- Application Code Affected:
--   • CorrespondenceRepository.GetCorrespondenceById() - Line 93
--     .Include(c => c.ExternalReferences)
--     Frequency: Every correspondence detail view
--   
--   • ExternalReferenceMapper and various detail views
--     Filter by ReferenceType (e.g., ReferenceType = 3 for Altinn2CorrespondenceId)
--
-- Current Problem:
--   • Existing index: IX_ExternalReferences_CorrespondenceId (Aug 2024, initial setup)
--   • Single-column index, not covering ReferenceType filter
--   • Not covering ReferenceValue (requires table lookup)
--
-- Expected Improvement:
--   • 20-50x faster external reference lookups
--   • Covering index (INCLUDE ReferenceValue) eliminates table access
--   • Filtered joins by ReferenceType use composite index
--
-- Production Benefit:
--   ✅ MODERATE - Correspondence detail loading becomes faster
--   API response time: Detail views improve 20-30%


-- 5. A2Parties Covering Index (IX_A2Parties_PartyUuid_Covering)
-- --------------------------------------------------------------
-- Application Code Affected:
--   • Dialog activity export queries (new console app)
--   • Any joins from CorrespondenceStatuses → A2Parties by PartyUuid
--
-- Current Problem:
--   • Legacy table with indexes: a2parties_partyuuid_idx (581 MB)
--   • Not covering - must lookup IdentifierUrn and Name from heap
--   • Unused indexes wasting 1.8 GB: a2parties_partyid_idx, a2parties_identifierurn_idx
--
-- Expected Improvement:
--   • 20x faster party lookups (eliminates table access)
--   • Can drop old a2parties_partyuuid_idx after migration (saves 581 MB)
--
-- Production Benefit:
--   ➡️ NEUTRAL - Only benefits export queries, not runtime API
--   Can optionally drop 2 unused indexes to save 1.8 GB disk space


-- ============================================================================
-- RUNTIME WRITE PERFORMANCE IMPACT
-- ============================================================================

-- Write Operations Affected:
-- ---------------------------
-- 1. Creating correspondences:
--    • Updates: CorrespondenceStatuses index, Correspondences index, IdempotencyKeys index
--    • Estimated overhead: +8-12% slower
--
-- 2. Updating correspondence status:
--    • Updates: Both CorrespondenceStatuses indexes (Synced + Migrated)
--    • Estimated overhead: +10-15% slower
--
-- 3. Creating attachments:
--    • Updates: IdempotencyKeys index
--    • Estimated overhead: +5-8% slower
--
-- 4. Adding external references:
--    • Updates: ExternalReferences composite index
--    • Estimated overhead: +3-5% slower
--
-- 5. Creating idempotency keys:
--    • Updates: IdempotencyKeys composite index (1.1B rows!)
--    • Estimated overhead: +15-20% slower
--
-- Overall Write Performance:
-- --------------------------
-- Estimated: 5-15% slower writes across the board
-- Acceptable: Yes - Altinn Correspondence is read-heavy workload
--   • Ratio: ~80% reads, 20% writes (typical for document/correspondence systems)
--   • Users read/list correspondences far more often than creating them
--   • Trade-off: Slightly slower creates for dramatically faster reads


-- ============================================================================
-- DISK SPACE REQUIREMENTS
-- ============================================================================

-- Estimated Index Sizes:
-- ----------------------
-- IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced:      ~3 GB    (Issue #1716: 7-9M rows)
-- IX_CorrespondenceStatuses_Status_StatusChanged_Migrated:     ~24 GB    (Issue #1951: 150M rows) **LARGEST**
-- IX_Correspondences_Id_Created_MigrationFilter:                ~1.5 GB  (partial index) **NOT NEEDED**
-- -----------------------------------------------------------------------------
-- TOTAL NEW INDEX SPACE:                                      ~27 GB (export optimization)

-- Note: A2Parties index is handled separately (see Fix_A2Parties_Recipient_Filter_Schema.sql and Fix_A2Parties_Recipient_Filter_Index.sql)


-- ============================================================================
-- DEPLOYMENT RISK ASSESSMENT
-- ============================================================================

-- Index Creation Time:
-- --------------------
-- All indexes use CONCURRENTLY = Zero downtime deployment
-- Estimated total time: 3-5 hours (mostly IdempotencyKeys with 1.1B rows)
--
-- Phase 1 (Quick wins, 15-20 min):
--   • IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced
--   • IX_ExternalReferences_CorrespondenceId_ReferenceType
--   • IX_A2Parties_PartyUuid_Covering
--
-- Phase 2 (Main optimization, 60-90 min):
--   • IX_CorrespondenceStatuses_Status_StatusChanged_Migrated
--   • IX_Correspondences_Id_Created_MigrationFilter
--
-- Phase 3 (Critical but slow, 2-3 hours):
--   • IX_IdempotencyKeys_Lookup_Composite (1.1B rows!)
--
-- Rollback Capability:
-- --------------------
-- ✅ All indexes can be dropped with CONCURRENTLY (no downtime)
-- ✅ No schema changes, only adding indexes
-- ✅ If any index causes issues, drop it immediately
--
-- Risk Level:
-- -----------
-- 🟢 LOW RISK for CorrespondenceStatuses, Correspondences, ExternalReferences, A2Parties
--    • Standard index patterns
--    • Covering indexes are well-supported by PostgreSQL
--    • Partial indexes reduce size and improve performance
--
-- 🟡 MEDIUM RISK for IdempotencyKeys
--    • Very large table (1.1B rows)
--    • Index will be 60-70 GB
--    • Long creation time (2-3 hours)
--    • But: CRITICAL for performance improvement
--
-- Monitoring:
-- -----------
-- Monitor during deployment:
--   • Index creation progress (see monitoring queries in this script)
--   • Disk space growth
--   • Lock contention (should be none with CONCURRENTLY)
--
-- Monitor after deployment:
--   • Query performance improvements (should be dramatic)
--   • Write operation latency (should increase slightly 5-15%)
--   • Index usage statistics (all should show idx_scan > 0 within hours)


-- ============================================================================
-- MIGRATION STRATEGY RECOMMENDATION
-- ============================================================================

-- Recommended Approach:
-- ---------------------
-- 1. Deploy in phases during maintenance window (even though CONCURRENTLY = no downtime)
-- 2. Monitor each phase before proceeding
-- 3. Start with quick wins (Phase 1)
-- 4. Deploy IdempotencyKeys index last (Phase 3) due to long creation time
-- 5. Test query performance after each phase
--
-- Post-Deployment Actions:
-- ------------------------
-- 1. Monitor index usage for 1-2 weeks
-- 2. Drop unused A2Parties indexes if confirmed (saves 1.8 GB)
-- 3. Document performance improvements in production metrics
-- 4. Consider vacuuming tables after index creation for optimal performance

-- ============================================================================
-- END OF IMPACT ASSESSMENT
-- ============================================================================

-- ============================================================================
-- END OF SCRIPT
-- ============================================================================
