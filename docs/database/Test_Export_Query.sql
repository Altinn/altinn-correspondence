-- =====================================================================================
-- Dialog Activity Export Queries - Optimized Version
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
-- PRODUCTION CODE:
--   DialogActivityExportService.cs runs these as separate queries, merges results in C#,
--   sorts in-memory, and uses cursor pagination for efficient batching.
--
-- USAGE:
--   Run Status 4 and Status 6 queries separately. Application merges and sorts results.
--
-- =====================================================================================


-- =====================================================================================
-- Issue #1716: Synced Events from Altinn2
-- =====================================================================================
-- Records: ~7-9 million
-- Filter: SyncedFromAltinn2 IS NOT NULL
-- Timestamp: SyncedFromAltinn2 column
-- Performance: ~60ms (cached)
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
  -- Cursor pagination (uncomment for subsequent batches):
  -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid'::uuid, 4)
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
LEFT JOIN correspondence."IdempotencyKeys" idcConfirm
    ON stats."CorrespondenceId" = idcConfirm."CorrespondenceId" 
    AND idcConfirm."StatusAction" = '6'
WHERE stats."Status" = 6
  AND stats."SyncedFromAltinn2" < '2026-02-15 00:00:00'
  -- Cursor pagination (uncomment for subsequent batches):
  -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid'::uuid, 6)
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
LEFT JOIN correspondence."IdempotencyKeys" idcFetch 
    ON stats."CorrespondenceId" = idcFetch."CorrespondenceId" 
    AND idcFetch."StatusAction" = '3'
WHERE stats."Status" = 4
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59'
  -- Cursor pagination (uncomment for subsequent batches):
  -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid'::uuid, 4)
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
LEFT JOIN correspondence."IdempotencyKeys" idcConfirm
    ON stats."CorrespondenceId" = idcConfirm."CorrespondenceId" 
    AND idcConfirm."StatusAction" = '6'
WHERE stats."Status" = 6
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59'
  -- Cursor pagination (uncomment for subsequent batches):
  -- AND (stats."CorrespondenceId", stats."Status") > ('last-uuid'::uuid, 6)
LIMIT 100;
