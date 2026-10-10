-- Find which table is causing the duplicates

-- ============================================================
-- 1. Check IdempotencyKeys for duplicates
-- ============================================================
-- This is the most likely culprit
SELECT 
    "CorrespondenceId",
    COUNT(*) as count
FROM correspondence."IdempotencyKeys"
WHERE "StatusAction" = '3'  -- Status 4 (Read) maps to StatusAction 3 (Fetched)
GROUP BY "CorrespondenceId"
HAVING COUNT(*) > 1
ORDER BY count DESC
LIMIT 20;

-- If this returns rows, IdempotencyKeys has duplicates!

-- ============================================================
-- 2. Check how many duplicates exist per CorrespondenceId
-- ============================================================
WITH duplicate_counts AS (
    SELECT 
        "CorrespondenceId",
        COUNT(*) as duplicate_count
    FROM correspondence."IdempotencyKeys"
    WHERE "StatusAction" = '3'
    GROUP BY "CorrespondenceId"
)
SELECT 
    duplicate_count,
    COUNT(*) as num_correspondences,
    duplicate_count * COUNT(*) as total_duplicate_rows
FROM duplicate_counts
GROUP BY duplicate_count
ORDER BY duplicate_count;

-- This shows the distribution of duplicates

-- ============================================================
-- 3. Sample some actual duplicate records
-- ============================================================
SELECT 
    "CorrespondenceId",
    "Id",
    "StatusAction",
    "Created"
FROM correspondence."IdempotencyKeys"
WHERE "CorrespondenceId" IN (
    SELECT "CorrespondenceId"
    FROM correspondence."IdempotencyKeys"
    WHERE "StatusAction" = '3'
    GROUP BY "CorrespondenceId"
    HAVING COUNT(*) > 1
    LIMIT 5
)
AND "StatusAction" = '3'
ORDER BY "CorrespondenceId", "Created";

-- This shows what the duplicate records look like

-- ============================================================
-- 4. Check A2Parties for duplicates (less likely but possible)
-- ============================================================
SELECT 
    "PartyUuid",
    COUNT(*) as count
FROM correspondence."A2Parties"
GROUP BY "PartyUuid"
HAVING COUNT(*) > 1
LIMIT 10;

-- If this returns rows, A2Parties has duplicates

-- ============================================================
-- 5. Check CorrespondenceStatuses for duplicates
-- ============================================================
SELECT 
    "CorrespondenceId",
    "Status",
    "PartyUuid",
    COUNT(*) as count
FROM correspondence."CorrespondenceStatuses"
WHERE "Status" = 4
GROUP BY "CorrespondenceId", "Status", "PartyUuid"
HAVING COUNT(*) > 1
LIMIT 10;

-- This checks if CorrespondenceStatuses has duplicate entries
