-- =====================================================================================
-- Fix A2Parties Recipient Filter Issue - Part 2: Index Creation
-- =====================================================================================
-- 
-- PURPOSE:
--   Create covering index on A2Parties table for efficient export query joins.
--
-- PREREQUISITES:
--   ⚠️  Must run Fix_A2Parties_Recipient_Filter_Schema.sql FIRST!
--   This script assumes:
--   - Column "OutputActorId" exists (renamed from IdentifierUrn)
--   - Column "RecipientUrn" exists and is populated
--
-- DEPLOYMENT:
--   ⚠️  CRITICAL: This script MUST run outside a transaction!
--
--   TRANSACTION REQUIREMENTS:
--   - CREATE INDEX CONCURRENTLY cannot run inside BEGIN/COMMIT blocks
--   - Migration tools that wrap scripts in transactions will FAIL with:
--     ERROR: CREATE INDEX CONCURRENTLY cannot run inside a transaction block
--
--   HOW TO RUN:
--   ✅ CORRECT: Direct psql execution (autocommit ON by default)
--      psql -h server -U user -d correspondence -f Fix_A2Parties_Recipient_Filter_Index.sql
--
--   ✅ CORRECT: pgAdmin query tool (no transaction)
--      Paste script and execute
--
--   ❌ WRONG: Migration tool with transaction wrapper (Flyway, Liquibase, etc.)
--      Will fail with transaction block error
--
--   ❌ WRONG: psql with explicit transaction
--      BEGIN;
--      \i Fix_A2Parties_Recipient_Filter_Index.sql  -- FAILS!
--      COMMIT;
--
--   LOCKING BEHAVIOR:
--   - Acquires brief SHARE UPDATE EXCLUSIVE lock (allows reads/writes)
--   - Does NOT acquire ACCESS EXCLUSIVE lock during index build
--   - Minimal blocking impact on production queries
--   - Index build time: 5-20 minutes (depends on A2Parties table size)
--
--   SIZE ESTIMATE:
--   - Index size: ~2 GB
--   - Covering index includes: PartyUuid, OutputActorId, RecipientUrn, Name
--
-- =====================================================================================

-- Guard: Verify autocommit is enabled
-- This query will help diagnose transaction issues
DO $$
BEGIN
    IF current_setting('transaction_isolation', true) IS NOT NULL THEN
        RAISE WARNING 'You may be inside a transaction block!';
        RAISE WARNING 'CREATE INDEX CONCURRENTLY requires autocommit mode.';
        RAISE WARNING 'If the next command fails, exit transaction and run again.';
    END IF;
END $$;

-- =====================================================================================
-- Create Covering Index for Export Queries
-- =====================================================================================

-- Create covering index for efficient export query joins
-- This index includes all columns needed by export queries, eliminating heap lookups

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_A2Parties_PartyUuid_Covering"
ON correspondence."A2Parties" ("PartyUuid")
INCLUDE ("OutputActorId", "RecipientUrn", "Name");

-- =====================================================================================
-- Verification
-- =====================================================================================

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
        RAISE NOTICE '==========================================';
        RAISE NOTICE 'Index Creation - SUCCESS';
        RAISE NOTICE '==========================================';
        RAISE NOTICE 'Index: IX_A2Parties_PartyUuid_Covering';
        RAISE NOTICE 'Status: Created successfully';
        RAISE NOTICE '';
        RAISE NOTICE 'Index Details:';
        RAISE NOTICE '  - Key column: PartyUuid';
        RAISE NOTICE '  - Included columns: OutputActorId, RecipientUrn, Name';
        RAISE NOTICE '  - Type: Covering index (no table lookups needed)';
        RAISE NOTICE '';
        RAISE NOTICE 'Next Steps:';
        RAISE NOTICE '1. DialogActivityExportService.cs already updated';
        RAISE NOTICE '2. Test export query with recipient filter';
        RAISE NOTICE '3. Export indexes ready (see Index_Creation_Scripts.sql)';
        RAISE NOTICE '==========================================';
    ELSE
        RAISE WARNING '==========================================';
        RAISE WARNING 'Index Creation - FAILED';
        RAISE WARNING '==========================================';
        RAISE WARNING 'Index IX_A2Parties_PartyUuid_Covering was not created';
        RAISE WARNING 'Check for errors above or run with increased verbosity';
        RAISE WARNING '==========================================';
    END IF;
END $$;

-- Show index size
SELECT 
    schemaname,
    tablename,
    indexname,
    pg_size_pretty(pg_relation_size('correspondence."IX_A2Parties_PartyUuid_Covering"'::regclass)) as index_size
FROM pg_indexes
WHERE schemaname = 'correspondence'
  AND tablename = 'A2Parties'
  AND indexname = 'IX_A2Parties_PartyUuid_Covering';

-- =====================================================================================
-- ROLLBACK (if needed)
-- =====================================================================================

-- Uncomment to drop the index:
-- 
-- -- Note: Also requires CONCURRENTLY for safe removal
-- DROP INDEX CONCURRENTLY IF EXISTS correspondence."IX_A2Parties_PartyUuid_Covering";
