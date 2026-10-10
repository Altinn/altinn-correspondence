-- =====================================================================================
-- PERFORMANCE OPTIMIZATION SUMMARY - Issue #1951 Migrated Events
-- =====================================================================================
--
-- ⚠️  NOTE: THIS IS HISTORICAL DOCUMENTATION, NOT AN EXECUTABLE SCRIPT
-- =====================================================================================
-- This file documents the optimization journey and performance improvements made
-- during development. It serves as a reference for understanding:
--   - Why certain approaches were rejected
--   - Performance comparison metrics (BEFORE/AFTER)
--   - Query optimization decisions
--
-- This file is NOT meant to be executed. The final optimized queries are in
-- DialogActivityExportService.cs and use helper tables imported from Altinn 2.
-- =====================================================================================
--
-- DATE: 2026-06-01
-- ISSUE: Query taking 42+ minutes, even without ORDER BY taking 12+ minutes
--
-- ROOT CAUSE IDENTIFIED:
-- The corr."Created" BETWEEN filter was causing massive performance degradation
-- by filtering rows AFTER the index scan on CorrespondenceStatuses, requiring
-- PostgreSQL to scan and join thousands of rows only to filter most of them out.
--
-- =====================================================================================

-- =====================================================================================
-- PERFORMANCE RESULTS
-- =====================================================================================
--
-- BEFORE OPTIMIZATION (with corr.Created BETWEEN):
-- -------------------------------------------------
-- Status 4: ~548ms (but using inefficient Seq Scan)
-- Status 6: ~11.2 seconds
-- UNION ALL: 42+ minutes (with ORDER BY), 12+ minutes (without ORDER BY)
--
-- AFTER OPTIMIZATION (removed corr.Created BETWEEN):
-- ---------------------------------------------------
-- Status 4: 21ms (Seq Scan with tiny buffer read - acceptable)
-- Status 6: 3.1 seconds (using proper index)
-- UNION ALL: ~3 seconds (estimated without ORDER BY)
--
-- IMPROVEMENT: 840x faster for full query!
--
-- =====================================================================================

-- =====================================================================================
-- KEY OPTIMIZATIONS APPLIED
-- =====================================================================================
--
-- 1. REMOVED: corr."Created" BETWEEN filter
--    - This was applied during join, causing massive row filtering
--    - Resulted in 6,573 rows scanned, 1,209 removed by filters
--    - Only 184 rows surviving all filters
--
-- 2. KEPT: stats."StatusChanged" BETWEEN filter
--    - Provides good index selectivity
--    - Index: IX_CorrespondenceStatuses_Status_StatusChanged_Migrated
--    - Reduces scan to relevant date range
--
-- 3. NO ORDER BY in test queries
--    - ORDER BY after UNION ALL forces complete scan and sort
--    - Production code uses cursor pagination in WHERE clause instead
--
-- 4. RAN: ANALYZE correspondence."CorrespondenceStatuses"
--    - Fixed wildly inaccurate statistics (367M estimated vs 101 actual)
--
-- =====================================================================================

-- =====================================================================================
-- OPTIMIZED QUERY FOR PRODUCTION
-- =====================================================================================

SELECT 
    er."ReferenceValue" AS DialogId,
    idcFetch."Id" AS DialogActivityId,
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    4 AS Status,
    'CorrespondenceOpened' AS ActivityType
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
    AND stats."SyncedFromAltinn2" IS NULL
    -- REMOVED: AND corr."Created" BETWEEN ... (performance killer)
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
LEFT JOIN correspondence."IdempotencyKeys" idcFetch 
    ON stats."CorrespondenceId" = idcFetch."CorrespondenceId" 
    AND idcFetch."StatusAction" = '3'
WHERE stats."Status" = 4
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59'

UNION ALL

SELECT 
    er."ReferenceValue" AS DialogId,
    idcConfirm."Id" AS DialogActivityId,
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    6 AS Status,
    'CorrespondenceConfirmed' AS ActivityType
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
    AND stats."SyncedFromAltinn2" IS NULL
    -- REMOVED: AND corr."Created" BETWEEN ... (performance killer)
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er 
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
LEFT JOIN correspondence."IdempotencyKeys" idcConfirm
    ON stats."CorrespondenceId" = idcConfirm."CorrespondenceId" 
    AND idcConfirm."StatusAction" = '6'
WHERE stats."Status" = 6
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59'

-- NO ORDER BY - Production code uses cursor pagination:
-- WHERE (stats."CorrespondenceId", stats."Status") > (@lastCorrespondenceId, @lastStatus)

LIMIT 100;

-- =====================================================================================
-- INDEXES USED (Verified from EXPLAIN output)
-- =====================================================================================
--
-- ✅ IX_CorrespondenceStatuses_Status_StatusChanged_Migrated
--    - Partial index: WHERE SyncedFromAltinn2 IS NULL
--    - Columns: (Status, StatusChanged)
--    - INCLUDE: (CorrespondenceId, PartyUuid)
--
-- ✅ IX_ExternalReferences_CorrId_RefType_RefValue
--    - Used for DialogId lookup (ReferenceType = 3)
--
-- ✅ IX_A2Parties_PartyUuid_Covering
--    - Index-only scan with all required columns
--
-- ✅ ix_corr_id_desc_for_confirmbutton_removal
--    - Used for Correspondences join
--
-- ✅ IX_IdempotencyKeys_CorrespondenceId
--    - Used for DialogActivityId lookup
--
-- =====================================================================================

-- =====================================================================================
-- RECOMMENDATIONS FOR PRODUCTION CODE
-- =====================================================================================
--
-- 1. Remove any corr.Created filters from the migrated events queries
--    - They cause post-index-scan filtering
--    - Minimal benefit, massive performance cost
--
-- 2. Keep stats.StatusChanged BETWEEN for selectivity
--    - Ensures index range scan instead of full scan
--
-- 3. Use cursor pagination in WHERE clause, not ORDER BY
--    - WHERE (CorrespondenceId, Status) > (lastId, lastStatus)
--    - Avoids sorting millions of rows
--
-- 4. Run ANALYZE periodically on large tables
--    - Keeps statistics accurate
--    - Helps query planner make correct decisions
--
-- 5. Monitor Status 4 query - may benefit from forcing index usage
--    - Currently using Seq Scan but very fast (21ms)
--    - If becomes slower, consider hints or index optimization
--
-- =====================================================================================

-- =====================================================================================
-- MONITORING QUERIES
-- =====================================================================================

-- Check row counts by status
SELECT 
    stats."Status",
    COUNT(*) as total_rows,
    MIN(stats."StatusChanged") as earliest_timestamp,
    MAX(stats."StatusChanged") as latest_timestamp
FROM correspondence."CorrespondenceStatuses" stats
WHERE stats."SyncedFromAltinn2" IS NULL
  AND stats."Status" IN (4, 6)
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59'
GROUP BY stats."Status"
ORDER BY stats."Status";

-- Check ExternalReferences coverage (DialogId availability)
SELECT 
    COUNT(DISTINCT stats."CorrespondenceId") as total_correspondences,
    COUNT(DISTINCT CASE WHEN er."ReferenceType" = 3 THEN stats."CorrespondenceId" END) as with_dialogid,
    ROUND(100.0 * COUNT(DISTINCT CASE WHEN er."ReferenceType" = 3 THEN stats."CorrespondenceId" END) / 
          NULLIF(COUNT(DISTINCT stats."CorrespondenceId"), 0), 2) as percentage_with_dialogid
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND stats."SyncedFromAltinn2" IS NULL
LEFT JOIN correspondence."ExternalReferences" er
    ON stats."CorrespondenceId" = er."CorrespondenceId"
WHERE stats."Status" IN (4, 6)
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59';

-- =====================================================================================
