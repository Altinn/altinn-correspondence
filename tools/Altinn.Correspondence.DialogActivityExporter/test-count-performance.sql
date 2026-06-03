-- =====================================================================================
-- Calculate Total Counts - PERFORMANCE TEST
-- =====================================================================================
-- 
-- PURPOSE: Test whether including ExternalReferences and IdempotencyKeys JOINs
--          affects COUNT performance if records are guaranteed to exist.
--
-- CRITICAL QUESTION:
--   We use LEFT JOIN IdempotencyKeys because the export code handles IsDBNull(1).
--   But is that circular reasoning? Does the data actually have NULL DialogActivityIds,
--   or did we just assume it might and use LEFT JOIN defensively?
--
-- TEST INSTRUCTIONS:
--   1. Run Query 1: Check how many DialogActivityIds are actually NULL
--   2. Run Query 2: Count WITH all JOINs (current structure)
--   3. Run Query 3: Count WITHOUT ExternalReferences/IdempotencyKeys
--   4. Run Query 4: Count WITH INNER JOIN IdempotencyKeys (if NULLs exist, count will be lower)
--   5. Compare counts and timing
--
-- =====================================================================================


-- =====================================================================================
-- QUERY 1A: Quick Check - Does ANY record lack IdempotencyKeys? (Fast!)
-- =====================================================================================
-- This exits immediately after finding first NULL, or confirms all exist
-- Much faster than counting millions of rows

SELECT 
    CASE 
        WHEN EXISTS (
            SELECT 1
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
              AND idcFetch."Id" IS NULL  -- Looking for missing IdempotencyKeys
            LIMIT 1
        ) THEN 'LEFT_JOIN_NEEDED'
        ELSE 'USE_INNER_JOIN'
    END AS IdempotencyKeys_Status,
    CASE 
        WHEN EXISTS (
            SELECT 1
            FROM correspondence."CorrespondenceStatuses" stats
            INNER JOIN correspondence."Correspondences" corr 
                ON stats."CorrespondenceId" = corr."Id" 
                AND corr."Altinn2CorrespondenceId" IS NOT NULL 
                AND corr."IsMigrating" = FALSE
                AND stats."SyncedFromAltinn2" IS NULL
            INNER JOIN correspondence."A2Parties" ap 
                ON stats."PartyUuid" = ap."PartyUuid"
                AND corr."Recipient" <> ap."RecipientUrn"
            LEFT JOIN correspondence."ExternalReferences" er
                ON stats."CorrespondenceId" = er."CorrespondenceId" 
                AND er."ReferenceType" = 3
            WHERE stats."Status" = 4
              AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59'
              AND er."CorrespondenceId" IS NULL  -- Looking for missing ExternalReferences
            LIMIT 1
        ) THEN 'INNER_JOIN_FILTERS'
        ELSE 'CAN_REMOVE_JOIN'
    END AS ExternalReferences_Status;

-- INTERPRETATION (runs in <1 second):
-- IdempotencyKeys_Status = 'LEFT_JOIN_NEEDED':  Some records lack IdempotencyKeys
-- IdempotencyKeys_Status = 'USE_INNER_JOIN':    All records have IdempotencyKeys
-- ExternalReferences_Status = 'INNER_JOIN_FILTERS': Some records lack ExternalReferences (JOIN is filtering)
-- ExternalReferences_Status = 'CAN_REMOVE_JOIN': All records have ExternalReferences (JOIN not filtering)


-- =====================================================================================
-- QUERY 1B: Find First Missing Record (if any) - Detailed Investigation
-- =====================================================================================
-- Only run this if Query 1A shows LEFT_JOIN_NEEDED or INNER_JOIN_FILTERS
-- Shows actual example of missing data

SELECT 
    stats."CorrespondenceId",
    stats."Status",
    stats."StatusChanged",
    corr."Altinn2CorrespondenceId",
    er."ReferenceValue" AS DialogId,
    idcFetch."Id" AS DialogActivityId,
    CASE WHEN er."CorrespondenceId" IS NULL THEN 'Missing ExternalReferences' ELSE 'OK' END AS ExternalRef_Status,
    CASE WHEN idcFetch."Id" IS NULL THEN 'Missing IdempotencyKeys' ELSE 'OK' END AS IdempotencyKeys_Status
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
    AND stats."SyncedFromAltinn2" IS NULL
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
LEFT JOIN correspondence."ExternalReferences" er
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
LEFT JOIN correspondence."IdempotencyKeys" idcFetch
    ON stats."CorrespondenceId" = idcFetch."CorrespondenceId" 
    AND idcFetch."StatusAction" = '3'
WHERE stats."Status" = 4
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59'
  AND (er."CorrespondenceId" IS NULL OR idcFetch."Id" IS NULL)  -- Show only records with missing data
LIMIT 5;


-- =====================================================================================
-- QUERY 1: How many DialogActivityIds are actually NULL? (Detailed Count)
-- =====================================================================================
-- ONLY RUN THIS IF YOU NEED EXACT PERCENTAGES (slower, but comprehensive)
-- Query 1A above is much faster for the yes/no decision

SELECT 
    COUNT(*) AS Total_Records,
    COUNT(idcFetch."Id") AS Records_With_DialogActivityId,
    COUNT(*) - COUNT(idcFetch."Id") AS Records_With_NULL_DialogActivityId,
    ROUND(100.0 * (COUNT(*) - COUNT(idcFetch."Id")) / COUNT(*), 2) AS Percent_NULL
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
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59';

-- INTERPRETATION:
-- If Percent_NULL = 0%:    All records have IdempotencyKeys → Change to INNER JOIN
-- If Percent_NULL = 1-10%: Most have IdempotencyKeys → Keep LEFT JOIN (some missing)
-- If Percent_NULL > 10%:   Many missing IdempotencyKeys → Keep LEFT JOIN (definitely needed)


-- =====================================================================================
-- QUERY 2: Count WITH all JOINs (current calculate-counts.sql structure)
-- =====================================================================================

SELECT COUNT(*) AS Count_With_All_Joins
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
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59';


-- =====================================================================================
-- QUERY 3: Count WITHOUT ExternalReferences and IdempotencyKeys
-- =====================================================================================

SELECT COUNT(*) AS Count_Without_Optional_Joins
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
    AND stats."SyncedFromAltinn2" IS NULL
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
WHERE stats."Status" = 4
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59';


-- =====================================================================================
-- QUERY 4: Count WITH INNER JOIN IdempotencyKeys (filters out NULLs)
-- =====================================================================================

SELECT COUNT(*) AS Count_With_INNER_JOIN_IdempotencyKeys
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
INNER JOIN correspondence."IdempotencyKeys" idcFetch  -- Changed to INNER JOIN
    ON stats."CorrespondenceId" = idcFetch."CorrespondenceId" 
    AND idcFetch."StatusAction" = '3'
WHERE stats."Status" = 4
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59';


-- =====================================================================================
-- EXPECTED RESULTS & DECISIONS
-- =====================================================================================
--
-- EXAMPLE RESULT SET:
-- Query 1: Total_Records = 58,000,000, Records_With_NULL_DialogActivityId = 0, Percent_NULL = 0.00%
-- Query 2: Count_With_All_Joins = 58,000,000
-- Query 3: Count_Without_Optional_Joins = 58,000,000 (same - ExternalReferences always exists)
-- Query 4: Count_With_INNER_JOIN_IdempotencyKeys = 58,000,000 (same - no NULLs)
--
-- DECISION MATRIX:
--
-- ┌─────────────────────────────────────────────────────────────────────────────────┐
-- │ If Query Results Show:                    │ Action for calculate-counts.sql    │
-- ├───────────────────────────────────────────┼────────────────────────────────────┤
-- │ Query 2 = Query 3 = Query 4               │ Remove BOTH ExternalReferences     │
-- │ (All counts identical)                    │ and IdempotencyKeys JOINs          │
-- │ Percent_NULL = 0%                         │ FASTEST option                     │
-- ├───────────────────────────────────────────┼────────────────────────────────────┤
-- │ Query 2 = Query 4 < Query 3               │ Keep ExternalReferences INNER JOIN │
-- │ (ExternalReferences filters)              │ Remove IdempotencyKeys LEFT JOIN   │
-- │ Percent_NULL = 0%                         │ Medium performance                 │
-- ├───────────────────────────────────────────┼────────────────────────────────────┤
-- │ Query 2 = Query 3 > Query 4               │ Keep ExternalReferences INNER JOIN │
-- │ (IdempotencyKeys sometimes missing)       │ Remove IdempotencyKeys LEFT JOIN   │
-- │ Percent_NULL > 0%                         │ (LEFT JOIN doesn't affect COUNT)   │
-- ├───────────────────────────────────────────┼────────────────────────────────────┤
-- │ Query 2 > Query 4                         │ Keep all JOINs                     │
-- │ (Both ExternalRef and IdempotencyKeys     │ Change IdempotencyKeys to INNER    │
-- │  filter results)                          │ Update export query too!           │
-- │ Percent_NULL > 0%                         │                                    │
-- └───────────────────────────────────────────┴────────────────────────────────────┘
--
-- =====================================================================================
-- ACTION ITEMS BASED ON RESULTS
-- =====================================================================================
--
-- IF Percent_NULL = 0% AND Query 2 = Query 4:
--   1. Update Test_Export_Query.sql: Change LEFT JOIN to INNER JOIN
--   2. Update DialogActivityExportService.cs query: Change to INNER JOIN
--   3. Update calculate-counts.sql: Use INNER JOIN or remove entirely
--   4. Remove IsDBNull(1) check in C# code (never NULL)
--   5. PERFORMANCE GAIN: ~10-20% faster queries
--
-- IF Percent_NULL = 0% AND Query 2 = Query 3 (IdempotencyKeys can be removed):
--   1. Keep export query as-is (still need DialogActivityId data)
--   2. Update calculate-counts.sql: Remove IdempotencyKeys LEFT JOIN
--   3. PERFORMANCE GAIN: ~5-10% faster COUNT queries
--
-- IF Percent_NULL > 0%:
--   1. Keep LEFT JOIN in export queries (needed to include all records)
--   2. Remove LEFT JOIN from calculate-counts.sql (doesn't affect count)
--   3. Keep IsDBNull(1) check in C# code
--   4. PERFORMANCE GAIN: ~5-10% faster COUNT queries
--
-- =====================================================================================

