-- Add ServiceOwnerId and ServiceOwnerMigrationStatus columns to Correspondences table
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

-- Add ServiceOwnerId and ServiceOwnerMigrationStatus columns to Attachments table
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
END $$;

-- Create index on Correspondences.ServiceOwnerId
CREATE INDEX IF NOT EXISTS "IX_Correspondences_ServiceOwnerId" 
ON "correspondence"."Correspondences" ("ServiceOwnerId");

-- Create index on Attachments.ServiceOwnerId
CREATE INDEX IF NOT EXISTS "IX_Attachments_ServiceOwnerId" 
ON "correspondence"."Attachments" ("ServiceOwnerId");


---- Prepare for batch update ----

-- Add ServiceOwnerMigrationStatus column to Correspondences and Attachments tables

-- ServiceOwnerMigrationStatus values:
-- 0 = PENDING (not yet processed)
-- 1 = COMPLETED (successfully processed with ServiceOwner)
-- 2 = NO_SERVICE_OWNER_FOUND (optional status - processed but no matching ServiceOwner)
-- When the batch is finished, only 1 is set and the rest remain 0. We can explicitly set 2 for those that are not updated by the update query.

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
END $$;

-- Create index to strengthen the query in the update function
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Correspondences_Sender_OrgNo"
ON "correspondence"."Correspondences" (RIGHT("Sender", 9))
WHERE "ServiceOwnerMigrationStatus" = 0

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Attachments_Sender_OrgNo"
ON "correspondence"."Attachments" (RIGHT("Sender", 9))
WHERE "ServiceOwnerMigrationStatus" = 0


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
    -- Start batch
    -----------------------------------------------------------------------
    RAISE NOTICE 'Batch size: %', batch_size;
    RAISE NOTICE 'Timestamp: %', start_time;

    -----------------------------------------------------------------------
    -- Perform update
    -----------------------------------------------------------------------

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
    "ServiceOwnerMigrationStatus" = 1  -- COMPLETED (since all records in batch have valid org numbers)
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


-- Process Correspondences table
DO $$
DECLARE
    batch_count INTEGER;
    batch_number INTEGER := 0;
    service_owner_ids TEXT[];
BEGIN
    -- Get all ServiceOwner IDs once before starting the loop
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

        COMMIT;  -- ✅ commit each batch
		
        EXIT WHEN batch_count = 0;
		
		PERFORM pg_sleep(0.02);
    END LOOP;

    RAISE NOTICE '=== CORRESPONDENCES MIGRATION COMPLETED ===';
    RAISE NOTICE 'Total batches processed: %', batch_number - 1;
END $$;

-- Process Attachments table
DO $$
DECLARE
    batch_count INTEGER;
    batch_number INTEGER := 0;
    service_owner_ids TEXT[];
BEGIN
    -- Get all ServiceOwner IDs once before starting the loop
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

        COMMIT;  -- ✅ commit each batch
		
        EXIT WHEN batch_count = 0;
		
		PERFORM pg_sleep(0.02);
    END LOOP;

    RAISE NOTICE '=== ATTACHMENTS MIGRATION COMPLETED ===';
    RAISE NOTICE 'Total batches processed: %', batch_number - 1;
END $$;

RAISE NOTICE '=== ALL MIGRATIONS COMPLETED ===';
