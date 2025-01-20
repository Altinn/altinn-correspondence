CREATE OR REPLACE PROCEDURE populate_test_database(total_records bigint)
LANGUAGE plpgsql
AS $$
DECLARE
    batches int;
    remaining_records int;
    i int;
BEGIN        
    -- SET session_replication_role = replica; // Must be set in Azure DbServer Server Parameters        
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
END;
$$;
