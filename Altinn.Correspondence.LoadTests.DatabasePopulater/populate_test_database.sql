
CREATE OR REPLACE FUNCTION populate_test_database(total_records bigint)
RETURNS void AS $$
DECLARE
    batches int;
    remaining_records int;
    i int;
BEGIN
    -- Disable triggers and indexes temporarily for better performance
    -- ALTER TABLE "correspondence"."Correspondences" DISABLE TRIGGER ALL;
    -- ALTER TABLE "correspondence"."CorrespondenceStatuses" DISABLE TRIGGER ALL;
    -- ALTER TABLE "correspondence"."CorrespondenceContents" DISABLE TRIGGER ALL;
    -- ALTER TABLE "correspondence"."CorrespondenceReplyOptions" DISABLE TRIGGER ALL;
    -- ALTER TABLE "correspondence"."CorrespondenceNotifications" DISABLE TRIGGER ALL;
                
    -- SET session_replication_role = replica; // Must be set in Azure Resource call
    batches := total_records / 1000;
    remaining_records := total_records % 1000;

    FOR i IN 1..batches LOOP
        RAISE NOTICE 'Processing batch % of %', i, batches;
        PERFORM generate_test_data(1000);
    END LOOP;

    IF remaining_records > 0 THEN
        PERFORM generate_test_data(remaining_records);
    END IF;

    -- SET session_replication_role = DEFAULT;
    -- Re-enable triggers and rebuild indexes
    -- ALTER TABLE "correspondence"."Correspondences" ENABLE TRIGGER ALL;
    -- ALTER TABLE "correspondence"."CorrespondenceStatuses" ENABLE TRIGGER ALL;
    -- ALTER TABLE "correspondence"."CorrespondenceContents" ENABLE TRIGGER ALL;
    -- ALTER TABLE "correspondence"."CorrespondenceReplyOptions" ENABLE TRIGGER ALL;
    -- ALTER TABLE "correspondence"."CorrespondenceNotifications" ENABLE TRIGGER ALL;
END;
$$ LANGUAGE plpgsql;