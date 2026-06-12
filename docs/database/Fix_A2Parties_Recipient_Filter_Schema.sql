-- =====================================================================================
-- Fix A2Parties Recipient Filter Issue - Part 1: Schema Changes
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
-- SOLUTION (Part 1 - Schema Changes):
--   - Rename IdentifierUrn → OutputActorId (clear semantics: what gets exported)
--   - Add RecipientUrn column (for matching against Correspondences.Recipient)
--     - Self-identified: Use UUID format (urn:altinn:party:uuid:{PartyUuid})
--     - Others: Use OutputActorId format (already matches Recipient)
--   - Add NOT NULL constraint after populating RecipientUrn
--
-- DEPLOYMENT:
--   - This script contains ONLY transactional DDL operations
--   - Safe to run through migration tools that wrap scripts in transactions
--   - Estimated time: < 1 minute
--
--   LOCKING BEHAVIOR:
--   - ALTER TABLE operations acquire ACCESS EXCLUSIVE locks (brief but blocking)
--   - SET NOT NULL validates all rows (scans table to ensure no NULLs)
--   - Short lock impact (queries may briefly wait)
--   - Recommended: Run during low-traffic window
--
--   NEXT STEP:
--   ⚠️  After this script succeeds, run Fix_A2Parties_Recipient_Filter_Index.sql
--      to create the covering index (requires non-transactional execution)
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
            -- Mask the identifier part after the last colon
            RAISE NOTICE '  Self-identified: % (identifier masked)', regexp_replace(v_selfidentified_sample, ':[^:]+$', ':***');
        END IF;
        IF v_person_sample IS NOT NULL THEN
            -- Mask the identifier part after the last colon
            RAISE NOTICE '  Person: % (identifier masked)', regexp_replace(v_person_sample, ':[^:]+$', ':***');
        END IF;
        IF v_org_sample IS NOT NULL THEN
            -- Mask the identifier part after the last colon
            RAISE NOTICE '  Organization: % (identifier masked)', regexp_replace(v_org_sample, ':[^:]+$', ':***');
        END IF;
    END IF;
END $$;

-- =====================================================================================
-- STEP 4: Add NOT NULL Constraint
-- =====================================================================================

ALTER TABLE correspondence."A2Parties"
ALTER COLUMN "RecipientUrn" SET NOT NULL;

DO $$
BEGIN
    RAISE NOTICE 'Added NOT NULL constraint to RecipientUrn';
END $$;

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
    RAISE NOTICE 'A2Parties Schema Changes - COMPLETE';
    RAISE NOTICE '==========================================';
    RAISE NOTICE 'Total rows: %', v_total_rows;
    RAISE NOTICE 'RecipientUrn populated: %', v_recipient_populated;
    RAISE NOTICE '';
    RAISE NOTICE 'Schema changes:';
    RAISE NOTICE '  - Renamed: IdentifierUrn → OutputActorId';
    RAISE NOTICE '  - Added: RecipientUrn (conditional format)';
    RAISE NOTICE '  - Added: NOT NULL constraint on RecipientUrn';
    RAISE NOTICE '';
    RAISE NOTICE 'Next Steps:';
    RAISE NOTICE '⚠️  Run Fix_A2Parties_Recipient_Filter_Index.sql';
    RAISE NOTICE '   (Creates covering index via CREATE INDEX CONCURRENTLY)';
    RAISE NOTICE '==========================================';
END $$;

-- =====================================================================================
-- ROLLBACK (if needed)
-- =====================================================================================

-- Uncomment to rollback changes:
-- 
-- -- Drop RecipientUrn column
-- ALTER TABLE correspondence."A2Parties"
-- DROP COLUMN IF EXISTS "RecipientUrn";
-- 
-- -- Rename column back
-- ALTER TABLE correspondence."A2Parties"
-- RENAME COLUMN "OutputActorId" TO "IdentifierUrn";
