-- PostgreSQL Query for stratified sampling of recipients
WITH recipient_counts AS (
    SELECT 
        "Recipient",
        COUNT("Recipient") as correspondence_count
    FROM "correspondence"."Correspondences" c
    WHERE c."Altinn2CorrespondenceId" IS NOT NULL
    GROUP BY "Recipient"
),
categorized_recipients AS (
    SELECT 
        "Recipient",
        correspondence_count,
        CASE 
            WHEN correspondence_count BETWEEN 1 AND 10 THEN '1-10'
            WHEN correspondence_count BETWEEN 11 AND 50 THEN '11-50'
            WHEN correspondence_count BETWEEN 51 AND 100 THEN '51-100'
            WHEN correspondence_count BETWEEN 101 AND 499 THEN '101-499'
            WHEN correspondence_count BETWEEN 500 AND 999 THEN '500-999'
            WHEN correspondence_count >= 1000 THEN '1000+'
            ELSE 'Other'
        END AS range_category
    FROM recipient_counts
),
category_stats AS (
    SELECT 
        range_category,
        COUNT(*) as category_count,
        COUNT(*) * 1.0 / SUM(COUNT(*)) OVER () as category_proportion
    FROM categorized_recipients
    GROUP BY range_category
),
sample_sizes AS (
    SELECT 
        range_category,
        category_count,
        category_proportion,
        -- Set your desired total sample size here
        GREATEST(1, ROUND(category_proportion * 3000)) as target_sample_size
    FROM category_stats
),
sampled_recipients AS (
    SELECT 
        cr."Recipient",
        cr.correspondence_count,
        cr.range_category,
        ss.target_sample_size,
        ROW_NUMBER() OVER (PARTITION BY cr.range_category ORDER BY RANDOM()) as rn
    FROM categorized_recipients cr
    JOIN sample_sizes ss ON cr.range_category = ss.range_category
)
SELECT 
    COALESCE(p_person.partyid_pk, p_org.partyid_pk) as partyId,
    sr."Recipient",
    sr.correspondence_count,
    sr.range_category
FROM sampled_recipients sr
LEFT JOIN "correspondence"."altinn2party" p_person ON 
    sr."Recipient" LIKE 'urn:altinn:person:identifier-no:%' AND
    p_person.fnumber_ak = SUBSTRING(sr."Recipient" FROM 'urn:altinn:person:identifier-no:(.+)')
LEFT JOIN "correspondence"."altinn2party" p_org ON 
    sr."Recipient" LIKE 'urn:altinn:organization:identifier-no:%' AND
    p_org.orgnumber_ak = SUBSTRING(sr."Recipient" FROM 'urn:altinn:organization:identifier-no:(.+)')
WHERE sr.rn <= sr.target_sample_size
ORDER BY 
    CASE range_category
        WHEN '1-10' THEN 1
        WHEN '11-50' THEN 2
        WHEN '51-100' THEN 3
        WHEN '101-499' THEN 4
        WHEN '500-999' THEN 5
        WHEN '1000+' THEN 6
        ELSE 7
    END,
    "Recipient";