-- Test queries to diagnose DISTINCT overhead

-- ============================================================
-- 1. Check if DISTINCT is actually needed
-- ============================================================
-- Count with and without DISTINCT for Status 4
SELECT COUNT(*) as count_with_distinct
FROM (
    SELECT DISTINCT
        a2Events."CorrespondenceId",
        stats."PartyUuid"
    FROM correspondence."A2Iss1716A2Events" a2Events
    INNER JOIN correspondence."CorrespondenceStatuses" stats 
        ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
        AND a2Events."Status" = stats."Status" 
        AND a2Events."PartyUuid" = stats."PartyUuid"
    WHERE a2Events."Status" = 4
    LIMIT 10000
) subq;

SELECT COUNT(*) as count_without_distinct
FROM correspondence."A2Iss1716A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
WHERE a2Events."Status" = 4
LIMIT 10000;

-- If counts are the same, DISTINCT is not needed!

-- ============================================================
-- 2. Test query without DISTINCT
-- ============================================================
EXPLAIN (ANALYZE, BUFFERS)
SELECT 
    er."ReferenceValue" AS DialogId,
    idcFetch."Id" AS DialogActivityId,
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    4 AS Status,
    'CorrespondenceOpened' AS ActivityType
FROM correspondence."A2Iss1716A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
INNER JOIN correspondence."ExternalReferences" er
    ON a2Events."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcFetch
    ON a2Events."CorrespondenceId" = idcFetch."CorrespondenceId"
    AND idcFetch."StatusAction" = '3'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE a2Events."Status" = 4
ORDER BY stats."CorrespondenceId"
LIMIT 5000;

-- Compare execution time with DISTINCT vs without DISTINCT

-- ============================================================
-- 3. Check for duplicate rows
-- ============================================================
-- See if the joins actually produce duplicates
WITH full_query AS (
    SELECT 
        stats."CorrespondenceId",
        stats."PartyUuid",
        COUNT(*) as row_count
    FROM correspondence."A2Iss1716A2Events" a2Events
    INNER JOIN correspondence."CorrespondenceStatuses" stats 
        ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
        AND a2Events."Status" = stats."Status" 
        AND a2Events."PartyUuid" = stats."PartyUuid"
    INNER JOIN correspondence."ExternalReferences" er
        ON a2Events."CorrespondenceId" = er."CorrespondenceId"
        AND er."ReferenceType" = 3
    INNER JOIN correspondence."IdempotencyKeys" idcFetch
        ON a2Events."CorrespondenceId" = idcFetch."CorrespondenceId"
        AND idcFetch."StatusAction" = '3'
    INNER JOIN correspondence."A2Parties" ap 
        ON stats."PartyUuid" = ap."PartyUuid"
    WHERE a2Events."Status" = 4
    GROUP BY stats."CorrespondenceId", stats."PartyUuid"
    LIMIT 1000
)
SELECT 
    row_count,
    COUNT(*) as occurrences
FROM full_query
GROUP BY row_count
ORDER BY row_count;

-- If all row_count = 1, then DISTINCT is unnecessary!
