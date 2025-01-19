CREATE OR REPLACE FUNCTION generate_test_data(batch_size int)
RETURNS void AS $$
begin
    -- Drop temporary table if it exists
    DROP TABLE IF EXISTS temp_correspondencebase;
    -- Create temporary table for storing correspondence base data
    CREATE TEMP TABLE temp_correspondencebase (
        "Id" uuid,
        "PartyUuid" uuid
    ) ON COMMIT DROP;

    -- Generate all correspondence data
    WITH SenderPartyIds AS (
        SELECT "orgnumber_ak"
        FROM "correspondence"."altinn2party"
        WHERE "orgnumber_ak" != 'NULL'
        ORDER BY RANDOM()
        LIMIT batch_size
    ),
    RecipientPartyIds AS (
        -- Select either organizations or persons based on what's available in each row
        SELECT 
            CASE 
                WHEN "fnumber_ak" IS NOT NULL AND "fnumber_ak" != 'NULL' 
                THEN 'urn:altinn:person:identifier-no:' || "fnumber_ak"
                ELSE 'urn:altinn:organization:identifier-no:' || "orgnumber_ak"
            END as identifier
        FROM "correspondence"."altinn2party"
        WHERE (
            ("fnumber_ak" IS NOT NULL AND "fnumber_ak" != 'NULL') OR
            ("orgnumber_ak" IS NOT NULL AND "orgnumber_ak" != 'NULL')
        )
        ORDER BY RANDOM()
        LIMIT batch_size
    ),
    inserted_correspondences AS (
        INSERT INTO "correspondence"."Correspondences" (
            "Id",
            "ResourceId",
            "Recipient",
            "Sender",
            "SendersReference",
            "MessageSender",
            "RequestedPublishTime",
            "AllowSystemDeleteAfter",
            "DueDateTime",
            "PropertyList",
            "IgnoreReservation",
            "Created",
            "Altinn2CorrespondenceId",
            "Published",
            "IsConfirmationNeeded"
        )
        SELECT 
            gen_random_uuid(),
            'dagl-correspondence-' || (floor(random() * 10) + 1)::text,
            R.identifier,
            'urn:altinn:organization:identifier-no:' || Sender."orgnumber_ak",
            Series."Id"::text,
            NULL,
            NOW() + interval '1 hour',
            NOW() + interval '8 months',
            NOW() + interval '5 months',
            '',
            false,
            NOW(),
            NULL,
            NOW() + interval '1 hour',
            false
        FROM 
            generate_series(1, batch_size) Series("Id")
            CROSS JOIN LATERAL (
                SELECT "orgnumber_ak" 
                FROM SenderPartyIds 
                LIMIT 1
            ) Sender
            CROSS JOIN LATERAL (
                SELECT identifier
                FROM RecipientPartyIds
                WHERE identifier != 'urn:altinn:organization:identifier-no:' || Sender."orgnumber_ak"
                LIMIT 1
            ) R
        RETURNING "Id", gen_random_uuid()::uuid AS "PartyUuid"
    )
    INSERT INTO temp_correspondencebase ("Id", "PartyUuid")
    SELECT "Id", "PartyUuid" FROM inserted_correspondences;

    -- [Rest of the function remains unchanged]
    -- Generate correspondence statuses
    INSERT INTO "correspondence"."CorrespondenceStatuses" (
        "Id",
        "Status",
        "StatusText",
        "StatusChanged",
        "CorrespondenceId",
        "PartyUuid"
    )
    SELECT 
        gen_random_uuid(),
        CASE (S.StatusNum)
            WHEN 1 THEN 0  -- Initialized
            WHEN 2 THEN 2  -- ReadyForPublish
            WHEN 3 THEN 3  -- Published
        END,
        CASE (S.StatusNum)
            WHEN 1 THEN 'Initialized'
            WHEN 2 THEN 'ReadyForPublish'
            WHEN 3 THEN 'Published'
        END,
        CASE (S.StatusNum)
            WHEN 1 THEN NOW()
            WHEN 2 THEN NOW() + interval '10 seconds'
            WHEN 3 THEN NOW() + interval '1 hour'
        END,
        C."Id",
        C."PartyUuid"
    FROM 
        temp_correspondencebase C
        CROSS JOIN generate_series(1, 3) S(StatusNum);

    -- Generate correspondence content
    INSERT INTO "correspondence"."CorrespondenceContents" (
        "Id",
        "Language",
        "MessageTitle",
        "MessageSummary",
        "MessageBody",
        "CorrespondenceId"
    )
    SELECT 
        gen_random_uuid(),
        'nb',
        'Meldingstittel',
        'Ett sammendrag for meldingen',
        'Dette er tekst i meldingen',
        C."Id"
    FROM temp_correspondencebase C;

    -- Generate reply options
    INSERT INTO "correspondence"."CorrespondenceReplyOptions" (
        "Id",
        "LinkURL",
        "LinkText",
        "CorrespondenceId"
    )
    SELECT 
        gen_random_uuid(),
        CASE WHEN S.OptNum = 1 THEN 'test.no' ELSE 'www.test.no' END,
        'test',
        C."Id"
    FROM 
        temp_correspondencebase C
        CROSS JOIN generate_series(1, 2) S(OptNum);

    -- Generate notifications
    INSERT INTO "correspondence"."CorrespondenceNotifications" (
        "Id",
        "NotificationTemplate",
        "NotificationAddress",
        "RequestedSendTime",
        "CorrespondenceId",
        "Created",
        "NotificationChannel",
        "NotificationOrderId",
        "NotificationSent",
        "IsReminder",
        "Altinn2NotificationId"
    )
    SELECT 
        gen_random_uuid(),
        0,
        NULL,
        NOW() + interval '1 hour',
        C."Id",
        NOW(),
        3,
        gen_random_uuid(),
        NULL,
        N.NotifNum = 2,
        NULL
    FROM 
        temp_correspondencebase C
        CROSS JOIN generate_series(1, 2) N(NotifNum);
END;
$$ LANGUAGE plpgsql;
