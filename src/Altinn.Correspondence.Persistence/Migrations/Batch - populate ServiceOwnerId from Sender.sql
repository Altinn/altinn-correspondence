-- =============================================================================
-- MIGRATION SCRIPT SUMMARY
-- =============================================================================
-- This script successfully:
-- 1. Adds ServiceOwnerId columns to both Correspondences and Attachments tables
-- 2. Creates performance indexes for optimal query performance
-- 3. Migrates data from Sender field to ServiceOwnerId using batch processing
-- 4. Provides progress monitoring
-- 5. Includes comprehensive cleanup instructions
--
-- Total execution time: Varies based on table size (typically 1-8 hours for large tables)
-- Batch size: 20,000 records per batch for optimal performance
-- =============================================================================


-- =============================================================================
-- STEP 0: RESET TABLES (FOR TESTING)
-- =============================================================================
-- WARNING: This step will remove all migration data and reset tables to initial state
-- Only use this for testing or when you need to start over completely
-- 
-- Remove temporary migration tracking columns
ALTER TABLE correspondence."Correspondences" 
DROP COLUMN IF EXISTS "ServiceOwnerId";

ALTER TABLE correspondence."Correspondences" 
DROP COLUMN IF EXISTS "ServiceOwnerMigrationStatus";

ALTER TABLE correspondence."Attachments" 
DROP COLUMN IF EXISTS "ServiceOwnerId";

ALTER TABLE correspondence."Attachments" 
DROP COLUMN IF EXISTS "ServiceOwnerMigrationStatus";

-- Remove temporary functions
DROP FUNCTION IF EXISTS get_migration_progress(TEXT, TEXT[]);
DROP FUNCTION IF EXISTS update_service_owner_ids_batch(TEXT, TEXT[], INTEGER);

-- Remove temporary indexes
DROP INDEX IF EXISTS "IX_Correspondences_ServiceOwnerId";
DROP INDEX IF EXISTS "IX_Attachments_ServiceOwnerId";
DROP INDEX IF EXISTS "IX_Correspondences_Sender_OrgNo";
DROP INDEX IF EXISTS "IX_Attachments_Sender_OrgNo";

DO $$
BEGIN
    RAISE NOTICE '=== STEP 0 COMPLETED: All migration artifacts removed for fresh start ===';
END $$;


-- =============================================================================
-- STEP 1: ADD NEW COLUMNS
-- =============================================================================
-- Add ServiceOwnerId column to store the organization number from Sender field
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_schema = 'correspondence' 
                   AND table_name = 'Correspondences' 
                   AND column_name = 'ServiceOwnerId') THEN
        ALTER TABLE "correspondence"."Correspondences" 
        ADD COLUMN "ServiceOwnerId" TEXT NULL;
        RAISE NOTICE 'Added ServiceOwnerId column to Correspondences table';
    ELSE
        RAISE NOTICE 'ServiceOwnerId column already exists in Correspondences table';
    END IF;
    
END $$;

-- Add ServiceOwnerId column to Attachments table
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_schema = 'correspondence' 
                   AND table_name = 'Attachments' 
                   AND column_name = 'ServiceOwnerId') THEN
        ALTER TABLE "correspondence"."Attachments" 
        ADD COLUMN "ServiceOwnerId" TEXT NULL;
        RAISE NOTICE 'Added ServiceOwnerId column to Attachments table';
    ELSE
        RAISE NOTICE 'ServiceOwnerId column already exists in Attachments table';
    END IF;
    RAISE NOTICE '=== STEP 1 COMPLETED: ServiceOwnerId columns added/verified ===';
END $$;

-- =============================================================================
-- STEP 2: CREATE PERFORMANCE INDEXES
-- =============================================================================
-- Create indexes on ServiceOwnerId columns for better query performance
DO $$
BEGIN
    RAISE NOTICE '=== STEP 2 STARTING: Creating performance indexes ===';
    RAISE NOTICE 'Creating index on Correspondences.ServiceOwnerId...';
END $$;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Correspondences_ServiceOwnerId" 
ON "correspondence"."Correspondences" ("ServiceOwnerId");

DO $$
BEGIN
    RAISE NOTICE 'Index on Correspondences.ServiceOwnerId created/verified successfully';
    RAISE NOTICE 'Creating index on Attachments.ServiceOwnerId...';
    RAISE NOTICE 'WARNING: CONCURRENTLY indexes may take several hours on large tables';
END $$;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Attachments_ServiceOwnerId" 
ON "correspondence"."Attachments" ("ServiceOwnerId");

DO $$
BEGIN
    RAISE NOTICE 'Index on Attachments.ServiceOwnerId created/verified successfully';
    RAISE NOTICE '=== STEP 2 COMPLETED: Performance indexes created ===';
END $$;

-- =============================================================================
-- STEP 3: ADD MIGRATION TRACKING COLUMNS
-- =============================================================================
-- Add ServiceOwnerMigrationStatus column to track migration progress
-- 
-- Migration Status Values:
-- 0 = PENDING (not yet processed)
-- 1 = COMPLETED (successfully processed with ServiceOwner)
-- 2 = NO_SERVICE_OWNER_FOUND (processed but no matching ServiceOwner found)
-- 
-- Note: These columns are temporary and will be removed after migration completion

-- Add ServiceOwnerMigrationStatus column to Correspondences table
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_schema = 'correspondence' 
                   AND table_name = 'Correspondences' 
                   AND column_name = 'ServiceOwnerMigrationStatus') THEN
        ALTER TABLE correspondence."Correspondences" 
        ADD COLUMN "ServiceOwnerMigrationStatus" INT DEFAULT 0;
        RAISE NOTICE 'Added ServiceOwnerMigrationStatus column to Correspondences table';
    ELSE
        RAISE NOTICE 'ServiceOwnerMigrationStatus column already exists in Correspondences table';
    END IF;
END $$;

-- Add ServiceOwnerMigrationStatus column to Attachments table
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_schema = 'correspondence' 
                   AND table_name = 'Attachments' 
                   AND column_name = 'ServiceOwnerMigrationStatus') THEN
        ALTER TABLE "correspondence"."Attachments" 
        ADD COLUMN "ServiceOwnerMigrationStatus" INT DEFAULT 0;
        RAISE NOTICE 'Added ServiceOwnerMigrationStatus column to Attachments table';
    ELSE
        RAISE NOTICE 'ServiceOwnerMigrationStatus column already exists in Attachments table';
    END IF;
    RAISE NOTICE '=== STEP 3 COMPLETED: Migration tracking columns added/verified ===';
END $$;

-- =============================================================================
-- STEP 4: CREATE MIGRATION PERFORMANCE INDEXES
-- =============================================================================
-- Create partial indexes to optimize the batch update queries
-- These indexes only include records that haven't been processed yet (status = 0)
-- NOTE: CONCURRENTLY indexes can take significant time on large tables
DO $$
BEGIN
    RAISE NOTICE '=== STEP 4 STARTING: Creating migration performance indexes ===';
    RAISE NOTICE 'WARNING: CONCURRENTLY indexes may take several minutes on large tables';
    RAISE NOTICE 'Creating CONCURRENTLY index on Correspondences.Sender (RIGHT 9 chars)...';
END $$;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Correspondences_Sender_OrgNo"
ON "correspondence"."Correspondences" (RIGHT("Sender", 9))
WHERE "ServiceOwnerMigrationStatus" = 0;

DO $$
BEGIN
    RAISE NOTICE 'CONCURRENTLY index on Correspondences.Sender created/verified successfully';
    RAISE NOTICE 'Creating CONCURRENTLY index on Attachments.Sender (RIGHT 9 chars)...';
END $$;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Attachments_Sender_OrgNo"
ON "correspondence"."Attachments" (RIGHT("Sender", 9))
WHERE "ServiceOwnerMigrationStatus" = 0;

DO $$
BEGIN
    RAISE NOTICE 'CONCURRENTLY index on Attachments.Sender created/verified successfully';
    RAISE NOTICE '=== STEP 4 COMPLETED: Migration performance indexes created ===';
END $$;


-- =============================================================================
-- STEP 5: CREATE BATCH UPDATE FUNCTION
-- =============================================================================
-- This function processes records in batches to avoid long-running transactions
-- and memory issues with large datasets
CREATE OR REPLACE FUNCTION update_service_owner_ids_batch(
    target_table TEXT,
    service_owner_ids TEXT[],
    batch_size INTEGER DEFAULT 5000
) RETURNS INTEGER AS $$
DECLARE
    rows_processed_this_batch INTEGER := 0;
    start_time TIMESTAMP := clock_timestamp();
BEGIN
    -----------------------------------------------------------------------
    -- Initialize batch processing
    -----------------------------------------------------------------------
    RAISE NOTICE 'Batch size: %', batch_size;
    RAISE NOTICE 'Timestamp: %', start_time;

    -----------------------------------------------------------------------
    -- Process records in batches
    -----------------------------------------------------------------------
    -- Extract organization number from Sender field and update matching records
    EXECUTE format('
        WITH batch_to_process AS (
    SELECT sub."Id",
           sub.extracted_org_no
    FROM (
        SELECT c."Id",
               RIGHT(c."Sender", 9) AS extracted_org_no
        FROM correspondence.%I c
        WHERE c."ServiceOwnerMigrationStatus" = 0
    ) sub
    WHERE extracted_org_no = ANY($1)
    LIMIT $2
)
UPDATE correspondence.%I c
SET 
    "ServiceOwnerId" = b.extracted_org_no,
    "ServiceOwnerMigrationStatus" = 1  -- Mark as COMPLETED
FROM batch_to_process b
WHERE c."Id" = b."Id"', target_table, target_table)
    USING service_owner_ids, batch_size;

    GET DIAGNOSTICS rows_processed_this_batch = ROW_COUNT;

    -----------------------------------------------------------------------
    -- Report results of this batch
    -----------------------------------------------------------------------
    IF rows_processed_this_batch > 0 THEN
        RAISE NOTICE 'Batch completed: % rows updated', rows_processed_this_batch;
    ELSE
        RAISE NOTICE 'No rows updated in this batch. Probably finished!';
    END IF;

    -----------------------------------------------------------------------
    -- Timing
    -----------------------------------------------------------------------
    RAISE NOTICE 'Batch duration: % ms',
        EXTRACT(MILLISECOND FROM (clock_timestamp() - start_time));

    -----------------------------------------------------------------------
    -- Return number of rows processed in this batch
    -----------------------------------------------------------------------
    RETURN rows_processed_this_batch;
END;
$$ LANGUAGE plpgsql;

DO $$
BEGIN
    RAISE NOTICE '=== STEP 5 COMPLETED: Batch update function created ===';
END $$;

-- =============================================================================
-- STEP 6: CREATE PROGRESS MONITORING FUNCTION
-- =============================================================================
-- This function provides real-time progress tracking during migration
-- Returns counts and completion percentage for monitoring purposes
CREATE OR REPLACE FUNCTION get_migration_progress(
    target_table TEXT,
    service_owner_ids TEXT[]
) RETURNS TABLE(
    table_name TEXT,
    total_rows INTEGER,
    total_pending INTEGER,
    total_completed INTEGER,
    completion_percentage DECIMAL(5,2),
    total_without_service_owner_id INTEGER
) AS $$
DECLARE
    pending_count INTEGER;
    completed_count INTEGER;
    total_rows INTEGER;
BEGIN
    -- Count records awaiting processing (status 0) with valid Sender format
    EXECUTE format('
        SELECT COUNT(*) 
        FROM correspondence.%I 
        WHERE "ServiceOwnerMigrationStatus" = 0 
          AND RIGHT("Sender", 9) = ANY($1)', target_table)
    INTO pending_count
    USING service_owner_ids;
    
    -- Count records that have been successfully processed (status 1)
    EXECUTE format('
        SELECT COUNT(*) 
        FROM correspondence.%I 
        WHERE "ServiceOwnerMigrationStatus" = 1', target_table)
    INTO completed_count;
    
    -- Get total table count
    EXECUTE format('
        SELECT COUNT(*) 
        FROM correspondence.%I', target_table)
    INTO total_rows;

    -- Calculate and return completion percentage
    RETURN QUERY SELECT 
        target_table::TEXT,
        total_rows,
        pending_count,
        completed_count,
        CASE 
            WHEN (pending_count + completed_count) > 0 THEN 
                ROUND((completed_count::DECIMAL / (pending_count + completed_count) * 100), 2)
            ELSE 0 
        END,
        total_rows - pending_count - completed_count;
END;
$$ LANGUAGE plpgsql;

-- Verify function was created successfully
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'get_migration_progress') THEN
        RAISE NOTICE '=== STEP 6 COMPLETED: Progress monitoring function created successfully ===';
        RAISE NOTICE 'Function signature: get_migration_progress(TEXT, TEXT[])';
    ELSE
        RAISE NOTICE 'ERROR: Function get_migration_progress was not created!';
    END IF;
END $$;

-- =============================================================================
-- STEP 7: EXECUTE MIGRATION FOR CORRESPONDENCES TABLE
-- =============================================================================
-- Process all Correspondence records in batches of 20,000
-- Each batch is committed individually to avoid long-running transactions
DO $$
DECLARE
    batch_count INTEGER;
    batch_number INTEGER := 0;
    service_owner_ids TEXT[];
BEGIN
    -- Retrieve all ServiceOwner IDs once to avoid repeated database calls
    SELECT ARRAY_AGG("Id") INTO service_owner_ids
    FROM "correspondence"."ServiceOwners";
    
    IF array_length(service_owner_ids, 1) IS NULL THEN
        RAISE NOTICE 'No ServiceOwner IDs found in the ServiceOwners table';
        RETURN;
    END IF;
    
    RAISE NOTICE 'Found % ServiceOwner IDs: %', array_length(service_owner_ids, 1), array_to_string(service_owner_ids, ', ');
    RAISE NOTICE '=== STARTING CORRESPONDENCES MIGRATION ===';
    
    LOOP
        batch_number := batch_number + 1;

        RAISE NOTICE '--- Running batch #% for Correspondences ---', batch_number;

        SELECT update_service_owner_ids_batch('Correspondences', service_owner_ids, 20000)
        INTO batch_count;

        COMMIT;  -- Commit each batch to avoid long-running transactions
        
        EXIT WHEN batch_count = 0;
		
		PERFORM pg_sleep(0.02);  -- Small delay to reduce database load
    END LOOP;

    RAISE NOTICE '=== CORRESPONDENCES MIGRATION COMPLETED ===';
    RAISE NOTICE 'Total batches processed: %', batch_number - 1;
    RAISE NOTICE '=== STEP 7 COMPLETED: Correspondences table migration finished ===';
END $$;



-- =============================================================================
-- STEP 8: EXECUTE MIGRATION FOR ATTACHMENTS TABLE
-- =============================================================================
-- Process all Attachment records in batches of 20,000
-- This runs after Correspondences migration is complete
DO $$
DECLARE
    batch_count INTEGER;
    batch_number INTEGER := 0;
    service_owner_ids TEXT[];
BEGIN
    -- Retrieve all ServiceOwner IDs once to avoid repeated database calls
    SELECT ARRAY_AGG("Id") INTO service_owner_ids
    FROM "correspondence"."ServiceOwners";
    
    IF array_length(service_owner_ids, 1) IS NULL THEN
        RAISE NOTICE 'No ServiceOwner IDs found in the ServiceOwners table';
        RETURN;
    END IF;
    
    RAISE NOTICE '=== STARTING ATTACHMENTS MIGRATION ===';
    
    LOOP
        batch_number := batch_number + 1;

        RAISE NOTICE '--- Running batch #% for Attachments ---', batch_number;

        SELECT update_service_owner_ids_batch('Attachments', service_owner_ids, 20000)
        INTO batch_count;

        COMMIT;  -- Commit each batch to avoid long-running transactions
        
        EXIT WHEN batch_count = 0;
		
		PERFORM pg_sleep(0.02);  -- Small delay to reduce database load
    END LOOP;

    RAISE NOTICE '=== ATTACHMENTS MIGRATION COMPLETED ===';
    RAISE NOTICE 'Total batches processed: %', batch_number - 1;
    RAISE NOTICE '=== STEP 8 COMPLETED: Attachments table migration finished ===';
END $$;

-- =============================================================================
-- STEP 9: PROGRESS MONITORING
-- =============================================================================
-- Use these queries to monitor migration progress during execution
--
-- Check Correspondences table progress:
-- SELECT * FROM get_migration_progress('Correspondences'::TEXT, 
--     (SELECT ARRAY_AGG("Id") FROM "correspondence"."ServiceOwners"));
--
-- Check Attachments table progress:
-- SELECT * FROM get_migration_progress('Attachments'::TEXT,
--     (SELECT ARRAY_AGG("Id") FROM "correspondence"."ServiceOwners"));
--
-- Function returns:
-- - table_name: Name of the table being migrated
-- - total_rows: Total number of records in the table
-- - total_pending: Records awaiting processing (status 0 with valid Sender format)
-- - total_completed: Records successfully processed (status 1)
-- - completion_percentage: Progress as percentage (0.00 to 100.00)
-- - total_without_service_owner_id: Records that are not to be processed by batch processing

-- =============================================================================
-- STEP 10: POST-MIGRATION CLEANUP
-- =============================================================================
-- IMPORTANT: Only run these cleanup commands AFTER migration is complete
-- and you have verified all data is correct
--
-- Cleanup commands (uncomment and run as needed):
--
-- -- Remove temporary functions (no longer needed after migration)
-- DROP FUNCTION IF EXISTS get_migration_progress(TEXT, TEXT[]);
-- DROP FUNCTION IF EXISTS update_service_owner_ids_batch(TEXT, TEXT[], INTEGER);
--
-- -- Remove temporary migration tracking columns
-- -- WARNING: Only remove if ALL records have been successfully migrated
-- ALTER TABLE "correspondence"."Correspondences" DROP COLUMN IF EXISTS "ServiceOwnerMigrationStatus";
-- ALTER TABLE "correspondence"."Attachments" DROP COLUMN IF EXISTS "ServiceOwnerMigrationStatus";
--
-- -- Optional: Remove performance indexes (keep if you want better query performance)
-- -- DROP INDEX IF EXISTS "IX_Correspondences_Sender_OrgNo";
-- -- DROP INDEX IF EXISTS "IX_Attachments_Sender_OrgNo";
--
-- -- IMPORTANT: Keep these permanent columns as they are part of the final data model
-- -- - ServiceOwnerId (stores the organization number from Sender field)
-- -- - All existing indexes on ServiceOwnerId columns
-- =============================================================================



