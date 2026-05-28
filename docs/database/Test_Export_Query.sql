-- =====================================================================================
-- Test Export Query - For Manual Testing in DBeaver/pgAdmin
-- =====================================================================================
-- 
-- PURPOSE:
--   Test the dialog activity export query with cursor pagination
--
-- USAGE:
--   1. Set the parameter values below
--   2. Execute in DBeaver/pgAdmin
--   3. Verify results and performance
--
-- =====================================================================================

-- Parameters (modify these as needed)
-- For testing without cursor, set both to NULL
-- For testing with cursor, set to actual values from previous batch
DO $$
DECLARE
    p_lastId uuid := NULL;              -- Set to last CorrespondenceId from previous batch
    p_lastStatus int := NULL;           -- Set to last Status from previous batch (4 or 6)
    p_cutoffTimestamp timestamp := '2026-02-15 00:00:00';
    p_batchSize int := 100;
BEGIN
    -- This block just declares variables for the query below
    -- The actual query must be run outside the DO block
END $$;

-- =====================================================================================
-- Actual Query - Copy and modify the parameters directly in the WHERE clause
-- =====================================================================================

-- For testing WITHOUT cursor (first batch):
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
  -- For first batch: Comment out the cursor line below
  -- AND (stats."CorrespondenceId", stats."Status") > ('your-last-uuid-here'::uuid, 4)
ORDER BY stats."CorrespondenceId", stats."Status"

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
  -- For first batch: Comment out the cursor line below
  -- AND (stats."CorrespondenceId", stats."Status") > ('your-last-uuid-here'::uuid, 6)
ORDER BY stats."CorrespondenceId", stats."Status"

ORDER BY "CorrespondenceId", "Status"
LIMIT 100;

-- =====================================================================================
-- Example: Testing WITH cursor pagination (second batch)
-- =====================================================================================
-- After running the query above, take the last CorrespondenceId and Status from results
-- Then uncomment and modify the cursor comparison lines:
--
-- For Status 4 branch:
--   AND (stats."CorrespondenceId", stats."Status") > ('a1b2c3d4-...'::uuid, 4)
--
-- For Status 6 branch:
--   AND (stats."CorrespondenceId", stats."Status") > ('a1b2c3d4-...'::uuid, 6)
--
-- =====================================================================================

-- =====================================================================================
-- Performance Analysis
-- =====================================================================================

-- Run EXPLAIN ANALYZE to check index usage:
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
ORDER BY stats."CorrespondenceId", stats."Status"

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
ORDER BY stats."CorrespondenceId", stats."Status"

ORDER BY "CorrespondenceId", "Status"
LIMIT 100;

-- Expected index usage:
-- ✅ IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced (for Status filter)
-- ✅ IX_A2Parties_PartyUuid_Covering (for A2Parties join)
-- ✅ PK_Correspondences (for Correspondences join)
-- ✅ Index on ExternalReferences (for DialogId lookup)
