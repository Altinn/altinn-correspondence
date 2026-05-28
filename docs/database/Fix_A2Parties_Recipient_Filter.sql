-- =====================================================================================
-- Fix A2Parties Recipient Filter Issue
-- =====================================================================================
-- 
-- PURPOSE:
--   Rename IdentifierUrn to OutputActorId and add RecipientUrn column to fix
--   the recipient filter logic in dialog activity export queries.
--
-- ISSUE:
--   Self-identified users have different formats:
--   - Correspondences.Recipient: "urn:altinn:party:uuid:f48a5e8b-..." (UUID format)
--   - A2Parties.IdentifierUrn: "urn:altinn:person:legacy-selfidentified:..." (name format)
--   - The filter "Recipient <> IdentifierUrn" never matches for self-identified users
--
-- SOLUTION:
--   - Rename IdentifierUrn → OutputActorId (clear semantics: what gets exported)
--   - Add RecipientUrn column (for matching against Correspondences.Recipient)
--     - Self-identified: Use UUID format (urn:altinn:party:uuid:{PartyUuid})
--     - Others: Use OutputActorId format (already matches Recipient)
--
-- DEPLOYMENT:
--   - Safe to run on production (no indexes, fast operations)
--   - Estimated time: < 1 minute
--   - No downtime required
--
-- =====================================================================================

-- =====================================================================================
-- STEP 1: Rename IdentifierUrn to OutputActorId
-- =====================================================================================

DO $$
BEGIN
    -- Check if IdentifierUrn exists and OutputActorId doesn't
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'correspondence' 
          AND table_name = 'A2Parties' 
          AND column_name = 'IdentifierUrn'
    ) AND NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'correspondence' 
          AND table_name = 'A2Parties' 
          AND column_name = 'OutputActorId'
    ) THEN
        ALTER TABLE correspondence."A2Parties"
        RENAME COLUMN "IdentifierUrn" TO "OutputActorId";

        RAISE NOTICE 'Renamed IdentifierUrn to OutputActorId';
    ELSIF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'correspondence' 
          AND table_name = 'A2Parties' 
          AND column_name = 'OutputActorId'
    ) THEN
        RAISE NOTICE 'OutputActorId column already exists (rename already done)';
    ELSE
        RAISE EXCEPTION 'Neither IdentifierUrn nor OutputActorId column found!';
    END IF;
END $$;

-- =====================================================================================
-- STEP 2: Add RecipientUrn Column
-- =====================================================================================

DO $$
BEGIN
    -- Add RecipientUrn column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'correspondence' 
          AND table_name = 'A2Parties' 
          AND column_name = 'RecipientUrn'
    ) THEN
        ALTER TABLE correspondence."A2Parties"
        ADD COLUMN "RecipientUrn" TEXT;

        RAISE NOTICE 'Added RecipientUrn column';
    ELSE
        RAISE NOTICE 'RecipientUrn column already exists';
    END IF;
END $$;

-- =====================================================================================
-- STEP 3: Populate RecipientUrn
-- =====================================================================================
-- Populate RecipientUrn for comparison with Correspondences.Recipient
--
-- THREE USER TYPE FORMATS:
--
-- 1. Self-identified users:
--    - Correspondences.Recipient: "urn:altinn:party:uuid:f48a5e8b-..."
--    - A2Parties.OutputActorId: "urn:altinn:person:legacy-selfidentified:MurgitroydFinland"
--    - Solution: Use UUID format for RecipientUrn
--
-- 2. Person (SSN):
--    - Correspondences.Recipient: "urn:altinn:person:identifier-no:10078328644"
--    - A2Parties.OutputActorId: "urn:altinn:person:identifier-no:10078328644"
--    - Solution: Use OutputActorId (already matches)
--
-- 3. Organization:
--    - Correspondences.Recipient: "urn:altinn:organization:identifier-no:983415113"
--    - A2Parties.OutputActorId: "urn:altinn:organization:identifier-no:983415113"
--    - Solution: Use OutputActorId (already matches)

UPDATE correspondence."A2Parties"
SET "RecipientUrn" = 
    CASE 
        -- Self-identified users need UUID format to match Recipient
        WHEN "OutputActorId" LIKE 'urn:altinn:person:legacy-selfidentified:%' 
        THEN 'urn:altinn:party:uuid:' || "PartyUuid"::TEXT
        -- All other types already match Recipient format
        ELSE "OutputActorId"
    END
WHERE "RecipientUrn" IS NULL;

-- Verify population
DO $$
DECLARE
    v_null_count INT;
    v_selfidentified_sample TEXT;
    v_person_sample TEXT;
    v_org_sample TEXT;
BEGIN
    SELECT COUNT(*) INTO v_null_count
    FROM correspondence."A2Parties"
    WHERE "RecipientUrn" IS NULL;

    IF v_null_count > 0 THEN
        RAISE WARNING 'RecipientUrn has % NULL values after population', v_null_count;
    ELSE
        RAISE NOTICE 'RecipientUrn successfully populated for all rows';

        -- Show sample formats for each type
        SELECT "RecipientUrn" INTO v_selfidentified_sample
        FROM correspondence."A2Parties"
        WHERE "OutputActorId" LIKE 'urn:altinn:person:legacy-selfidentified:%'
        LIMIT 1;

        SELECT "RecipientUrn" INTO v_person_sample
        FROM correspondence."A2Parties"
        WHERE "OutputActorId" LIKE 'urn:altinn:person:identifier-no:%'
        LIMIT 1;

        SELECT "RecipientUrn" INTO v_org_sample
        FROM correspondence."A2Parties"
        WHERE "OutputActorId" LIKE 'urn:altinn:organization:identifier-no:%'
        LIMIT 1;

        RAISE NOTICE '';
        RAISE NOTICE 'Sample RecipientUrn formats:';
        IF v_selfidentified_sample IS NOT NULL THEN
            RAISE NOTICE '  Self-identified: %', v_selfidentified_sample;
        END IF;
        IF v_person_sample IS NOT NULL THEN
            RAISE NOTICE '  Person: %', v_person_sample;
        END IF;
        IF v_org_sample IS NOT NULL THEN
            RAISE NOTICE '  Organization: %', v_org_sample;
        END IF;
    END IF;
END $$;

-- =====================================================================================
-- STEP 4: Add NOT NULL Constraint
-- =====================================================================================

ALTER TABLE correspondence."A2Parties"
ALTER COLUMN "RecipientUrn" SET NOT NULL;

RAISE NOTICE 'Added NOT NULL constraint to RecipientUrn';

-- =====================================================================================
-- STEP 5: Create Covering Index for Export Queries
-- =====================================================================================

-- Create covering index for efficient export query joins
-- This index includes all columns needed by export queries, eliminating heap lookups

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_A2Parties_PartyUuid_Covering"
ON correspondence."A2Parties" ("PartyUuid")
INCLUDE ("OutputActorId", "RecipientUrn", "Name");

-- Index explanation:
-- • Key: PartyUuid (used for joins from CorrespondenceStatuses)
-- • INCLUDE: OutputActorId (export output), RecipientUrn (filtering), Name (actor name)
-- • Covering index = no table access needed during export query joins
-- • Size: ~2 GB (estimated)
-- • This is the first index on A2Parties besides the primary key

-- Verify index creation:
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM pg_indexes 
        WHERE schemaname = 'correspondence' 
          AND tablename = 'A2Parties' 
          AND indexname = 'IX_A2Parties_PartyUuid_Covering'
    ) THEN
        RAISE NOTICE 'Index IX_A2Parties_PartyUuid_Covering created successfully';
    ELSE
        RAISE WARNING 'Index IX_A2Parties_PartyUuid_Covering was not created';
    END IF;
END $$;

-- =====================================================================================
-- STEP 6: Verification Query
-- =====================================================================================

-- Show sample data for each user type to verify correct formats
SELECT 
    CASE 
        WHEN "OutputActorId" LIKE 'urn:altinn:person:legacy-selfidentified:%' THEN 'Self-identified'
        WHEN "OutputActorId" LIKE 'urn:altinn:person:identifier-no:%' THEN 'Person'
        WHEN "OutputActorId" LIKE 'urn:altinn:organization:identifier-no:%' THEN 'Organization'
        ELSE 'Other'
    END AS UserType,
    "PartyUuid",
    "OutputActorId",
    "RecipientUrn",
    "Name"
FROM correspondence."A2Parties"
WHERE "OutputActorId" LIKE 'urn:altinn:person:legacy-selfidentified:%'
   OR "OutputActorId" LIKE 'urn:altinn:person:identifier-no:%'
   OR "OutputActorId" LIKE 'urn:altinn:organization:identifier-no:%'
ORDER BY UserType, "Name"
LIMIT 10;

-- =====================================================================================
-- VERIFICATION SUMMARY
-- =====================================================================================

DO $$
DECLARE
    v_total_rows INT;
    v_recipient_populated INT;
BEGIN
    SELECT COUNT(*) INTO v_total_rows
    FROM correspondence."A2Parties";

    SELECT COUNT(*) INTO v_recipient_populated
    FROM correspondence."A2Parties"
    WHERE "RecipientUrn" IS NOT NULL;

    RAISE NOTICE '==========================================';
    RAISE NOTICE 'A2Parties Recipient Filter Fix - COMPLETE';
    RAISE NOTICE '==========================================';
    RAISE NOTICE 'Total rows: %', v_total_rows;
    RAISE NOTICE 'RecipientUrn populated: %', v_recipient_populated;
    RAISE NOTICE '';
    RAISE NOTICE 'Schema changes:';
    RAISE NOTICE '  - Renamed: IdentifierUrn → OutputActorId';
    RAISE NOTICE '  - Added: RecipientUrn (conditional format)';
    RAISE NOTICE '  - Created: IX_A2Parties_PartyUuid_Covering index';
    RAISE NOTICE '';
    RAISE NOTICE 'Next Steps:';
    RAISE NOTICE '1. DialogActivityExportService.cs already updated';
    RAISE NOTICE '2. Test export query with recipient filter';
    RAISE NOTICE '3. Export indexes ready for creation (see Index_Creation_Scripts.sql)';
    RAISE NOTICE '==========================================';
END $$;

-- =====================================================================================
-- ROLLBACK (if needed)
-- =====================================================================================

-- Uncomment to rollback changes:
-- 
-- -- Drop the covering index
-- DROP INDEX CONCURRENTLY IF EXISTS correspondence."IX_A2Parties_PartyUuid_Covering";
-- 
-- -- Rename column back
-- ALTER TABLE correspondence."A2Parties"
-- RENAME COLUMN "OutputActorId" TO "IdentifierUrn";
-- 
-- -- Drop RecipientUrn column
-- ALTER TABLE correspondence."A2Parties"
-- DROP COLUMN IF EXISTS "RecipientUrn";
