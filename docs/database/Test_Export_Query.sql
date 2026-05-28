-- =====================================================================================
-- Test Export Queries - For Manual Testing in DBeaver/pgAdmin
-- =====================================================================================
-- 
-- PURPOSE:
--   Test the dialog activity export queries with cursor pagination
--   These represent the actual queries from DialogActivityExportService.cs
--
-- CONTENTS:
--   1. Query for Issue #1716 - Synced Events (SyncedFromAltinn2 IS NOT NULL)
--   2. Query for Issue #1951 - Migrated Events (SyncedFromAltinn2 IS NULL)
--   3. EXPLAIN ANALYZE versions for performance testing
--
-- USAGE:
--   1. Choose the query for your issue (1716 or 1951)
--   2. Modify cutoff dates and cursor values as needed
--   3. Execute in DBeaver/pgAdmin
--   4. Verify results and performance
--
-- =====================================================================================


-- =====================================================================================
-- QUERY 1: Issue #1716 - Synced Events from Altinn2
-- =====================================================================================
-- Records: ~7-9 million
-- Filter: SyncedFromAltinn2 IS NOT NULL
-- Timestamp: SyncedFromAltinn2 column
-- Created Filter: None
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
    AND stats."SyncedFromAltinn2" IS NOT NULL    
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
  AND stats."SyncedFromAltinn2" < '2026-02-15 00:00:00'
  -- For cursor pagination: Uncomment and replace with values from last row
  -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid-here'::uuid, last-status-here)

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
    AND stats."SyncedFromAltinn2" IS NOT NULL
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
  AND stats."SyncedFromAltinn2" < '2026-02-15 00:00:00'
  -- For cursor pagination: Use SAME values as Status 4 branch above
  -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid-here'::uuid, last-status-here)

ORDER BY "CorrespondenceId", "Status"
LIMIT 100;

-- =====================================================================================
-- QUERY 2: Issue #1951 - Migrated Events (NOT Synced)
-- =====================================================================================
-- Records: ~150 million
-- Filter: SyncedFromAltinn2 IS NULL
-- Timestamp: StatusChanged column
-- Created Filter: corr."Created" > '2019-03-23' (optional)
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
    AND corr."Created" > '2019-03-23'  -- Optional: Remove if testing all dates
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
  AND stats."StatusChanged" < '2026-05-19 11:35:59'
  -- For cursor pagination: Uncomment and replace with values from last row
  -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid-here'::uuid, last-status-here)

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
    AND corr."Created" > '2019-03-23'  -- Optional: Remove if testing all dates
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
  AND stats."StatusChanged" < '2026-05-19 11:35:59'
  -- For cursor pagination: Use SAME values as Status 4 branch above
  -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid-here'::uuid, last-status-here)

ORDER BY "CorrespondenceId", "Status"
LIMIT 100;


-- =====================================================================================
-- Cursor Pagination Guide
-- =====================================================================================
-- 
-- HOW TO USE CURSOR PAGINATION:
-- 1. Run either Query 1 or Query 2 above (first batch)
-- 2. Note the LAST row in the result set
-- 3. Take the CorrespondenceId and Status from that last row
-- 4. Uncomment the cursor lines in BOTH UNION branches
-- 5. Replace 'last-uuid-here' and last-status-here with actual values
-- 6. Run the query again to get the next batch
-- 
-- IMPORTANT: Both UNION branches must use the SAME cursor values!
--
-- Example: If last row was:
--   CorrespondenceId = 'a1b2c3d4-5678-90ab-cdef-1234567890ab'
--   Status = 4
--
-- Then uncomment and update BOTH branches:
--   AND (stats."CorrespondenceId", stats."Status") > ('a1b2c3d4-5678-90ab-cdef-1234567890ab'::uuid, 4)
--
-- PostgreSQL tuple comparison (a,b) > (x,y) is equivalent to:
--   (a > x) OR (a = x AND b > y)
--
-- This ensures correct pagination across the UNION ALL:
-- - Each branch filters independently
-- - Global ORDER BY merges both branches
-- - No rows are skipped or duplicated
-- =====================================================================================


-- =====================================================================================
-- Performance Analysis - EXPLAIN ANALYZE
-- =====================================================================================

-- =====================================================================================
-- EXPLAIN for Issue #1716
-- =====================================================================================
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
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
    AND stats."SyncedFromAltinn2" IS NOT NULL    
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
  AND stats."SyncedFromAltinn2" < '2026-02-15 00:00:00'

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
    AND stats."SyncedFromAltinn2" IS NOT NULL
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
  AND stats."SyncedFromAltinn2" < '2026-02-15 00:00:00'

ORDER BY "CorrespondenceId", "Status"
LIMIT 100;

-- Expected index usage for Issue #1716:
-- ✅ IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced (Status 4 & 6 filters)
-- ✅ IX_A2Parties_PartyUuid_Covering (A2Parties join with INCLUDE columns)
-- ✅ PK_Correspondences (Correspondences join)
-- ✅ Index on ExternalReferences (DialogId lookup)

-- =====================================================================================
-- EXPLAIN for Issue #1951
-- =====================================================================================
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
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
    AND corr."Created" > '2019-03-23'
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
  AND stats."StatusChanged" < '2026-05-19 11:35:59'

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
    AND corr."Created" > '2019-03-23'
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
  AND stats."StatusChanged" < '2026-05-19 11:35:59'

ORDER BY "CorrespondenceId", "Status"
LIMIT 100;

-- Expected index usage for Issue #1951:
-- ✅ IX_CorrespondenceStatuses_Status_StatusChanged_Migrated (Status 4 & 6 filters)
-- ✅ IX_A2Parties_PartyUuid_Covering (A2Parties join with INCLUDE columns)
-- ✅ PK_Correspondences (Correspondences join)
-- ✅ IX_Correspondences_Id_Created_MigrationFilter (Created date filter - optional)
-- ✅ Index on ExternalReferences (DialogId lookup)
