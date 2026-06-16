-- =====================================================================================
-- Issue #1951 Query Template with Timestamp Matching
-- =====================================================================================
--
-- This query should be used when A2Iss1951MigratedEvents helper table is created.
-- It includes the timestamp matching fix to handle duplicate events correctly.
--
-- HELPER TABLE SCHEMA:
--   - CorrespondenceId (uuid) - Primary identifier
--   - PartyUuid (uuid) - Party involved in the event
--   - Status (int4) - Status code (4 or 6)
--   - StatusChanged (timestamptz) - Event timestamp
--   - Source (int4) - Source system (0=ServiceEngine, 1=Archive) - For troubleshooting only
--
-- NOTE: Source column is NOT used in queries, only for human readability/debugging
--
-- USAGE:
--   Once A2Iss1951MigratedEvents table is populated from Altinn 2 export,
--   add this query logic to DialogActivityExportService.cs as a new branch:
--   if (issueNumber == 1951 && helperTableExists) { ... }
--
-- =====================================================================================

-- Status 4: CorrespondenceOpened
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
    AND migEvents."StatusChanged" = stats."StatusChanged"  -- ← CRITICAL: Timestamp matching
INNER JOIN correspondence."ExternalReferences" er
    ON migEvents."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcFetch
    ON migEvents."CorrespondenceId" = idcFetch."CorrespondenceId"
    AND idcFetch."StatusAction" = '3'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE migEvents."Status" = 4
  AND migEvents."CorrespondenceId" > '00000000-0000-0000-0000-000000000000'::uuid  -- Cursor
ORDER BY migEvents."CorrespondenceId"
LIMIT 5000;

-- Status 6: CorrespondenceConfirmed
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
    AND migEvents."StatusChanged" = stats."StatusChanged"  -- ← CRITICAL: Timestamp matching
INNER JOIN correspondence."ExternalReferences" er
    ON migEvents."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcConfirm
    ON migEvents."CorrespondenceId" = idcConfirm."CorrespondenceId"
    AND idcConfirm."StatusAction" = '6'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE migEvents."Status" = 6
  AND migEvents."CorrespondenceId" > '00000000-0000-0000-0000-000000000000'::uuid  -- Cursor
ORDER BY migEvents."CorrespondenceId"
LIMIT 5000;

-- =====================================================================================
-- C# Implementation Template for DialogActivityExportService.cs
-- =====================================================================================

/*
Add this to FetchStatusRecordsAsync method after Issue #1716 check:

else if (issueNumber == 1951)
{
    // Use A2Iss1951MigratedEvents helper table for Issue #1951
    // Same approach as Issue #1716, includes timestamp matching to handle duplicates

    var migEventsCursorPredicate = lastCorrespondenceId.HasValue 
        ? "AND migEvents.\"CorrespondenceId\" > @lastId"
        : "";

    query = $@"
        SELECT DISTINCT
            er.""ReferenceValue"" AS DialogId,
            {idcJoinAlias}.""Id"" AS DialogActivityId,
            stats.""CorrespondenceId"",
            stats.""StatusChanged"" AS Timestamp,
            ap.""OutputActorId"" AS ActorId,
            ap.""Name"" AS ActorName,
            {statusValue} AS Status,
            '{activityType}' AS ActivityType
        FROM correspondence.""A2Iss1951MigratedEvents"" migEvents
        INNER JOIN correspondence.""CorrespondenceStatuses"" stats 
            ON migEvents.""CorrespondenceId"" = stats.""CorrespondenceId"" 
            AND migEvents.""Status"" = stats.""Status"" 
            AND migEvents.""PartyUuid"" = stats.""PartyUuid""
            AND migEvents.""StatusChanged"" = stats.""StatusChanged""
        INNER JOIN correspondence.""ExternalReferences"" er
            ON migEvents.""CorrespondenceId"" = er.""CorrespondenceId""
            AND er.""ReferenceType"" = 3
        INNER JOIN correspondence.""IdempotencyKeys"" {idcJoinAlias}
            ON migEvents.""CorrespondenceId"" = {idcJoinAlias}.""CorrespondenceId""
            AND {idcJoinAlias}.""StatusAction"" = '{statusActionValue}'
        INNER JOIN correspondence.""A2Parties"" ap 
            ON stats.""PartyUuid"" = ap.""PartyUuid""
        WHERE migEvents.""Status"" = {statusValue}
          {migEventsCursorPredicate}
        ORDER BY stats.""CorrespondenceId""
        LIMIT @fetchLimit";
}

*/

-- =====================================================================================
-- Index Requirements for A2Iss1951MigratedEvents
-- =====================================================================================

-- NOTE: Source column is NOT included in indexes (not used in queries)
-- It's only for troubleshooting/debugging purposes

-- Primary index for cursor pagination
CREATE INDEX CONCURRENTLY "IX_A2Iss1951MigratedEvents_CorrespondenceId_Status"
ON correspondence."A2Iss1951MigratedEvents" ("CorrespondenceId", "Status");

-- Covering index for Index-Only Scans (includes timestamp!)
-- Does NOT include Source - not needed for queries
CREATE INDEX CONCURRENTLY "IX_A2Iss1951MigratedEvents_Covering"
ON correspondence."A2Iss1951MigratedEvents" ("CorrespondenceId", "Status")
INCLUDE ("PartyUuid", "StatusChanged");

-- Alternative: Status-first index (if you filter by status first)
CREATE INDEX CONCURRENTLY "IX_A2Iss1951MigratedEvents_Status_CorrId"
ON correspondence."A2Iss1951MigratedEvents" ("Status", "CorrespondenceId")
INCLUDE ("PartyUuid", "StatusChanged");

-- Update statistics
ANALYZE correspondence."A2Iss1951MigratedEvents";

-- =====================================================================================
-- Key Differences from Issue #1716
-- =====================================================================================

/*
Issue #1716 (Synced Events):
  - Table: A2Iss1716A2Events
  - Column: a2Events."Timestamp" 
  - Filter: SyncedFromAltinn2 IS NOT NULL
  - Volume: ~10M rows

Issue #1951 (Migrated Events):
  - Table: A2Iss1951MigratedEvents
  - Column: migEvents."StatusChanged"
  - Filter: SyncedFromAltinn2 IS NULL
  - Volume: ~337.8M rows

SAME APPROACH:
  - Both use timestamp matching to handle duplicates
  - Both join on (CorrespondenceId, Status, PartyUuid, StatusChanged)
  - Both require covering indexes with INCLUDE (PartyUuid, StatusChanged)
  - Both use DISTINCT for IdempotencyKeys join duplicates
*/

-- =====================================================================================
-- Testing Query Performance
-- =====================================================================================

-- Test with EXPLAIN ANALYZE to verify indexes are used correctly
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
    AND migEvents."StatusChanged" = stats."StatusChanged"
INNER JOIN correspondence."ExternalReferences" er
    ON migEvents."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcFetch
    ON migEvents."CorrespondenceId" = idcFetch."CorrespondenceId"
    AND idcFetch."StatusAction" = '3'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE migEvents."Status" = 4
ORDER BY migEvents."CorrespondenceId"
LIMIT 5000;

-- Expected output:
-- -> Index Only Scan using "IX_A2Iss1951MigratedEvents_Covering" on migEvents
--    Heap Fetches: 0
-- -> Index Only Scan using "IX_CorrespondenceStatuses_Unique" on stats
--    Index Cond: (...StatusChanged = migEvents."StatusChanged"...)
--    Heap Fetches: 0
-- Execution Time: ~100-200ms

-- =====================================================================================
-- Summary
-- =====================================================================================

/*
✅ Issue #1951 uses the SAME timestamp matching fix as Issue #1716

Key Points:
1. Don't deduplicate timestamps when populating A2Iss1951MigratedEvents from Altinn 2
2. Add StatusChanged to JOIN condition: migEvents."StatusChanged" = stats."StatusChanged"
3. Use covering indexes that INCLUDE (PartyUuid, StatusChanged)
4. CorrespondenceStatuses UNIQUE index already covers all 4 columns
5. Expected performance: ~100-200ms per batch (same as Issue #1716)

See: TIMESTAMP_MATCHING_FIX.md for detailed document