-- =====================================================================================
-- Calculate Total Counts for DialogActivityExporter
-- =====================================================================================
-- 
-- PURPOSE:
--   Calculate the total number of records for each issue to populate the
--   PreCalculatedCounts section in appsettings.json
--
-- WHY PRE-CALCULATE:
--   COUNT(*) queries on 1.94B row CorrespondenceStatuses table take time.
--   By running these queries once and storing results in appsettings.json, we avoid
--   the delay on every export run. Counts are used for progress reporting only.
--
-- USAGE:
--   1. Update the cutoff timestamp in all queries below
--   2. Run ALL FOUR queries separately in pgAdmin or Azure Data Studio
--   3. Add the Status 4 + Status 6 counts for each issue
--   4. Copy the summed results to appsettings.json PreCalculatedCounts section
--   5. Set to 0 to force runtime calculation (useful for testing)
--
-- PERFORMANCE:
--   Running separate queries (no UNION ALL) allows PostgreSQL to optimize each:
--   - Issue #1716 Status 4: ~1-2 seconds
--   - Issue #1716 Status 6: ~3-5 seconds
--   - Issue #1951 Status 4: ~3-5 seconds (with Index #2)
--   - Issue #1951 Status 6: ~15-20 seconds (with Index #2)
--   - Total: ~20-30 seconds for all four queries (was 90s with old structure!)
--
-- IMPORTANT:
--   These queries match the exact structure of the export queries for consistent
--   performance. They run SEPARATELY (no UNION ALL) just like the export code.
--
-- =====================================================================================

-- =====================================================================================
-- Issue #1716: Synced Events from Altinn2
-- =====================================================================================
-- Records: ~7-9 million
-- Filter: SyncedFromAltinn2 IS NOT NULL
-- =====================================================================================

-- Status 4: CorrespondenceOpened
SELECT COUNT(*) AS Issue1716_Status4_Count
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
WHERE stats."Status" = 4
  AND stats."SyncedFromAltinn2" < '2026-05-19 11:35:59';  -- UPDATE THIS TIMESTAMP

-- Status 6: CorrespondenceConfirmed
SELECT COUNT(*) AS Issue1716_Status6_Count
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
WHERE stats."Status" = 6
  AND stats."SyncedFromAltinn2" < '2026-05-19 11:35:59';  -- UPDATE THIS TIMESTAMP

-- ISSUE #1716 TOTAL = Status4_Count + Status6_Count


-- =====================================================================================
-- Issue #1951: Migrated Events (NOT Synced from Altinn2)
-- =====================================================================================
-- Records: ~150 million
-- Filter: SyncedFromAltinn2 IS NULL
-- Note: Uses BETWEEN for better index selectivity on large dataset (Index #2)
-- =====================================================================================

-- Status 4: CorrespondenceOpened
SELECT COUNT(*) AS Issue1951_Status4_Count
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
WHERE stats."Status" = 4
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59';  -- UPDATE THIS TIMESTAMP

-- Status 6: CorrespondenceConfirmed
SELECT COUNT(*) AS Issue1951_Status6_Count
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
WHERE stats."Status" = 6
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-05-19 11:35:59';  -- UPDATE THIS TIMESTAMP

-- ISSUE #1951 TOTAL = Status4_Count + Status6_Count


-- =====================================================================================
-- WHY SEPARATE QUERIES (NO UNION ALL)?
-- =====================================================================================
-- From Test_Export_Query.sql and DialogActivityExportService.cs:
--
-- "Running Status 4 and Status 6 as separate queries and merging in the application
--  is significantly faster than UNION ALL with ORDER BY:
--  - Separate: Status 4 ~21ms + Status 6 ~3s = Total ~3s
--  - UNION ALL + ORDER BY: 12-40+ minutes (840x slower!)
--
--  With UNION ALL, PostgreSQL must scan millions of rows before applying aggregate.
--  Separate queries allow early termination and better index usage."
--
-- This same principle applies to COUNT(*):
-- - Separate queries: PostgreSQL can optimize each independently
-- - UNION ALL: Forced to combine result sets before counting (inefficient)
--
-- =====================================================================================
-- WHY THESE SPECIFIC JOINS?
-- =====================================================================================
-- The COUNT queries include the same joins as Test_Export_Query.sql for accuracy:
--
-- 1. Correspondences: Filters by Altinn2CorrespondenceId IS NOT NULL, IsMigrating = FALSE
-- 2. A2Parties: Filters by PartyUuid match AND Recipient <> RecipientUrn
-- 3. ExternalReferences: CRITICAL - Filters by ReferenceType = 3 (DialogId)
--    - Without this, counts will be HIGHER than actual exports
-- 4. IdempotencyKeys: REMOVED from COUNT queries (not needed for counting)
--    - Changed to INNER JOIN in export queries (30min+ test found no missing records)
--    - Performance testing confirmed: IdempotencyKeys always exist for these records
--    - For COUNT: Removed entirely (5-10% faster, same accuracy)
--
-- If we omit ExternalReferences or A2Parties joins, the COUNT will NOT match what
-- actually gets exported, making progress reporting inaccurate.
--
-- =====================================================================================
-- EXAMPLE RESULTS (2026-05-19):
-- =====================================================================================
-- Issue1716_Status4_Count: 3,234,567
-- Issue1716_Status6_Count: 5,222,222
-- Issue1716_Total:         8,456,789  <- Add these manually
--
-- Issue1951_Status4_Count: 58,123,456
-- Issue1951_Status6_Count: 94,225,456
-- Issue1951_Total:         152,348,912  <- Add these manually
--
-- Grand Total: 160,805,701
--
-- UPDATE appsettings.json:
-- {
--   "PreCalculatedCounts": {
--     "Issue1716": 8456789,
--     "Issue1951": 152348912,
--     "Comment": "Pre-calculated 2026-05-19 11:35:59. Run calculate-counts.sql (4 separate queries). Counts used for progress reporting only."
--   }
-- }
-- =====================================================================================
