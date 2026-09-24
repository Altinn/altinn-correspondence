-- =====================================================================================
-- Dialog Activity Export Queries - CTE Version (Production Implementation)
-- =====================================================================================
-- 
-- WHY SEPARATE QUERIES (No UNION ALL):
--   Running Status 4 and Status 6 as separate queries and merging in the application
--   is significantly faster than UNION ALL with ORDER BY:
--   - Separate: Status 4 ~21ms + Status 6 ~3s = Total ~3s
--   - UNION ALL + ORDER BY: 12-40+ minutes (840x slower!)
--   
--   With UNION ALL, PostgreSQL must scan millions of rows before applying LIMIT.
--   Separate queries allow early termination at LIMIT, and in-memory sorting is faster.
--
-- CTE APPROACH (CURRENT IMPLEMENTATION):
--   Production code uses CTE (Common Table Expression) to filter CorrespondenceStatuses
--   ONLY, keeping the CTE simple for optimal index usage. Key benefits:
--   - Index-optimized filtering on CorrespondenceStatuses (Status, SyncedFromAltinn2)
--   - LIMIT applied directly on base table with index support
--   - Subsequent JOINs filter the candidate set (Correspondences, A2Parties, etc.)
--   
--   DESIGN DECISION: Early attempts to include Correspondences join IN the CTE caused
--   timeouts (>5 min). The current approach (CTE with CorrespondenceStatuses only) 
--   completes successfully, making it the preferred production implementation.
--
-- PERFORMANCE CHARACTERISTICS:
--   - Test mode (2 batches, logging enabled): ~6 minutes for ~1000 rows
--   - Production (no test logging, larger batches): Expected to be significantly faster
--   - Query does not timeout (300 second limit)
--   - Results are correct and complete
--
-- PRODUCTION CODE:
--   DialogActivityExportService.cs runs these as separate queries with CTE,
--   merges results in C#, sorts in-memory, and uses cursor pagination for efficient batching.
--
-- USAGE:
--   Run Status 4 and Status 6 queries separately. Application merges and sorts results.
--   CTE version below matches production code exactly.
--
-- =====================================================================================


-- =====================================================================================
-- Issue #1716: Synced Events from Altinn2
-- =====================================================================================
-- Records: ~7-9 million
-- Filter: SyncedFromAltinn2 IS NOT NULL
-- Timestamp: SyncedFromAltinn2 column
-- Performance: Optimized CTE version (production), Simple JOIN version (testing)
-- =====================================================================================

-- =====================================================================================
-- PRODUCTION CTE VERSION (Currently Implemented)
-- =====================================================================================
-- This version filters ONLY CorrespondenceStatuses in the CTE for optimal index usage.
-- Subsequent JOINs filter the result set. This approach was chosen after testing showed
-- that including Correspondences in the CTE caused timeouts.
-- =====================================================================================

-- Status 4: CorrespondenceOpened (Production CTE)
WITH filtered AS (
    SELECT 
        stats."CorrespondenceId",
        stats."PartyUuid",
        stats."StatusChanged",
        stats."Status"
    FROM correspondence."CorrespondenceStatuses" stats
    WHERE stats."Status" = 4
      AND stats."SyncedFromAltinn2" < '2026-02-15 00:00:00'
      AND stats."SyncedFromAltinn2" IS NOT NULL
      -- Cursor pagination (uncomment for subsequent batches):
      -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid'::uuid, 4)
    ORDER BY stats."CorrespondenceId", stats."Status"
    LIMIT 100
)
SELECT 
    er."ReferenceValue" AS DialogId,
    idcFetch."Id" AS DialogActivityId,
    filtered."CorrespondenceId",
    filtered."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    4 AS Status,
    'CorrespondenceOpened' AS ActivityType
FROM filtered
INNER JOIN correspondence."Correspondences" corr 
    ON filtered."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
INNER JOIN correspondence."A2Parties" ap 
    ON filtered."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON filtered."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcFetch 
    ON filtered."CorrespondenceId" = idcFetch."CorrespondenceId" 
    AND idcFetch."StatusAction" = '3'
ORDER BY filtered."CorrespondenceId", filtered."Status";

-- Status 6: CorrespondenceConfirmed (Production CTE)
WITH filtered AS (
    SELECT 
        stats."CorrespondenceId",
        stats."PartyUuid",
        stats."StatusChanged",
        stats."Status"
    FROM correspondence."CorrespondenceStatuses" stats
    WHERE stats."Status" = 6
      AND stats."SyncedFromAltinn2" < '2026-02-15 00:00:00'
      AND stats."SyncedFromAltinn2" IS NOT NULL
      -- Cursor pagination (uncomment for subsequent batches):
      -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid'::uuid, 6)
    ORDER BY stats."CorrespondenceId", stats."Status"
    LIMIT 100
)
SELECT 
    er."ReferenceValue" AS DialogId,
    idcConfirm."Id" AS DialogActivityId,
    filtered."CorrespondenceId",
    filtered."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    6 AS Status,
    'CorrespondenceConfirmed' AS ActivityType
FROM filtered
INNER JOIN correspondence."Correspondences" corr 
    ON filtered."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
INNER JOIN correspondence."A2Parties" ap 
    ON filtered."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON filtered."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcConfirm
    ON filtered."CorrespondenceId" = idcConfirm."CorrespondenceId" 
    AND idcConfirm."StatusAction" = '6'
ORDER BY filtered."CorrespondenceId", filtered."Status";


-- =====================================================================================
-- OPTIMIZED HELPER TABLE VERSION (New - Currently Implemented in Production)
-- =====================================================================================
-- Uses A2Iss1716A2Events helper table pre-filtered from Altinn 2
-- This eliminates the need to scan 1.94B CorrespondenceStatuses rows
-- REQUIRES: Indexes from Optimize_A2Iss1716A2Events_Indexes.sql
-- Performance: Expected <1s per batch vs ~5-6s with CTE approach
-- =====================================================================================

-- Status 4: CorrespondenceOpened (Helper Table)
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
  -- Cursor pagination (uncomment for subsequent batches):
  -- CRITICAL: Cursor on a2Events (indexed table) but ORDER BY uses stats (SELECT list)
  -- This works because a2Events."CorrespondenceId" = stats."CorrespondenceId" (join condition)
  -- AND (a2Events."CorrespondenceId", a2Events."Status") > ('last-uuid'::uuid, 4)
ORDER BY stats."CorrespondenceId", Status
LIMIT 100;

-- Status 6: CorrespondenceConfirmed (Helper Table)
SELECT DISTINCT
    er."ReferenceValue" AS DialogId,
    idcConfirm."Id" AS DialogActivityId,
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    6 AS Status,
    'CorrespondenceConfirmed' AS ActivityType
FROM correspondence."A2Iss1716A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
INNER JOIN correspondence."ExternalReferences" er
    ON a2Events."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcConfirm
    ON a2Events."CorrespondenceId" = idcConfirm."CorrespondenceId"
    AND idcConfirm."StatusAction" = '6'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE a2Events."Status" = 6
  -- Cursor pagination (uncomment for subsequent batches):
  -- CRITICAL: Cursor on a2Events (indexed table) but ORDER BY uses stats (SELECT list)
  -- This works because a2Events."CorrespondenceId" = stats."CorrespondenceId" (join condition)
  -- AND (a2Events."CorrespondenceId", a2Events."Status") > ('last-uuid'::uuid, 6)
ORDER BY stats."CorrespondenceId", Status
LIMIT 100;


-- =====================================================================================
-- ALTERNATIVE: SIMPLE JOIN VERSION (For Testing/Verification)
-- =====================================================================================
-- This version without CTE is simpler to read but may have different performance.
-- Use for ad-hoc testing and query verification.
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
INNER JOIN correspondence."IdempotencyKeys" idcFetch 
    ON stats."CorrespondenceId" = idcFetch."CorrespondenceId" 
    AND idcFetch."StatusAction" = '3'
WHERE stats."Status" = 4
  AND stats."SyncedFromAltinn2" < '2026-02-15 00:00:00'
  -- Cursor pagination (uncomment for subsequent batches):
  -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid'::uuid, 4)
ORDER BY stats."CorrespondenceId", stats."Status"
LIMIT 100;

-- Status 6: CorrespondenceConfirmed
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
INNER JOIN correspondence."IdempotencyKeys" idcConfirm
    ON stats."CorrespondenceId" = idcConfirm."CorrespondenceId" 
    AND idcConfirm."StatusAction" = '6'
WHERE stats."Status" = 6
  AND stats."SyncedFromAltinn2" < '2026-02-15 00:00:00'
  -- Cursor pagination (uncomment for subsequent batches):
  -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid'::uuid, 6)
ORDER BY stats."CorrespondenceId", stats."Status"
LIMIT 100;


-- =====================================================================================
-- Issue #1951: Migrated Events (NOT Synced from Altinn2)
-- =====================================================================================
-- Records: ~150 million
-- Filter: SyncedFromAltinn2 IS NULL
-- Timestamp: StatusChanged with BETWEEN for index selectivity
-- Performance: Status 4 ~21ms + Status 6 ~3s = Total ~3s
--
-- IMPORTANT: corr.Created filter is NOT used.
--   Adding corr.Created BETWEEN causes massive degradation (3s → 12+ min) because it
--   filters AFTER the index scan. StatusChanged BETWEEN provides sufficient selectivity.
-- =====================================================================================

-- Status 4: CorrespondenceOpened
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
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcFetch 
    ON stats."CorrespondenceId" = idcFetch."CorrespondenceId" 
    AND idcFetch."StatusAction" = '3'
WHERE stats."Status" = 4
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59'
  -- Cursor pagination (uncomment for subsequent batches):
  -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid'::uuid, 4)
ORDER BY stats."CorrespondenceId", stats."Status"
LIMIT 100;

-- Status 6: CorrespondenceConfirmed
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
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er 
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcConfirm
    ON stats."CorrespondenceId" = idcConfirm."CorrespondenceId" 
    AND idcConfirm."StatusAction" = '6'
WHERE stats."Status" = 6
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59'
  -- Cursor pagination (uncomment for subsequent batches):
  -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid'::uuid, 6)
ORDER BY stats."CorrespondenceId", stats."Status"
LIMIT 100;
