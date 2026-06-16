-- =====================================================================================
-- Monitor and Manage Long-Running Index Creation
-- =====================================================================================

-- =====================================================================================
-- CHECK 1: Monitor Index Creation Progress
-- =====================================================================================

-- Check if CREATE INDEX CONCURRENTLY is still running
SELECT 
    pid,
    usename,
    application_name,
    state,
    query,
    age(clock_timestamp(), query_start) AS query_duration,
    wait_event_type,
    wait_event
FROM pg_stat_activity
WHERE query ILIKE '%temp_idx_dup_analysis%'
  AND state != 'idle'
ORDER BY query_start;

-- Check system-wide index creation progress (PostgreSQL 12+)
SELECT 
    p.phase,
    p.blocks_total,
    p.blocks_done,
    ROUND(100.0 * p.blocks_done / NULLIF(p.blocks_total, 0), 2) AS pct_complete,
    p.tuples_total,
    p.tuples_done,
    ROUND(100.0 * p.tuples_done / NULLIF(p.tuples_total, 0), 2) AS tuples_pct,
    a.query,
    age(clock_timestamp(), a.query_start) AS running_time
FROM pg_stat_progress_create_index p
JOIN pg_stat_activity a ON p.pid = a.pid
WHERE a.query ILIKE '%temp_idx_dup_analysis%';

-- Check if index exists and is valid
SELECT 
    schemaname,
    relname AS tablename,
    indexrelname AS indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS current_size,
    idx_scan AS times_used,
    indisvalid AS is_valid,
    indisready AS is_ready
FROM pg_stat_user_indexes
JOIN pg_index ON indexrelid = pg_stat_user_indexes.indexrelid
WHERE schemaname = 'correspondence'
  AND indexrelname = 'temp_idx_dup_analysis';

-- =====================================================================================
-- CHECK 2: System Resource Usage
-- =====================================================================================

-- Check for locks that might be blocking
SELECT 
    blocked_locks.pid AS blocked_pid,
    blocked_activity.usename AS blocked_user,
    blocking_locks.pid AS blocking_pid,
    blocking_activity.usename AS blocking_user,
    blocked_activity.query AS blocked_statement,
    blocking_activity.query AS blocking_statement,
    age(clock_timestamp(), blocked_activity.query_start) AS blocked_duration
FROM pg_catalog.pg_locks blocked_locks
JOIN pg_catalog.pg_stat_activity blocked_activity ON blocked_activity.pid = blocked_locks.pid
JOIN pg_catalog.pg_locks blocking_locks 
    ON blocking_locks.locktype = blocked_locks.locktype
    AND blocking_locks.relation = blocked_locks.relation
    AND blocking_locks.pid != blocked_locks.pid
JOIN pg_catalog.pg_stat_activity blocking_activity ON blocking_activity.pid = blocking_locks.pid
WHERE NOT blocked_locks.granted
  AND blocked_activity.query ILIKE '%temp_idx_dup_analysis%';

-- Check table bloat (might slow down index creation)
SELECT 
    schemaname,
    relname,
    n_live_tup,
    n_dead_tup,
    ROUND(100.0 * n_dead_tup / NULLIF(n_live_tup + n_dead_tup, 0), 2) AS dead_pct,
    last_vacuum,
    last_autovacuum,
    last_analyze,
    last_autoanalyze
FROM pg_stat_user_tables
WHERE schemaname = 'correspondence'
  AND relname = 'A2Iss1951A2Events';

-- =====================================================================================
-- DECISION MATRIX
-- =====================================================================================

/*
SCENARIO A: Index is actively progressing (blocks_done increasing)
✅ ACTION: Let it continue
   - If 50%+ complete: Wait for completion
   - If <50% complete: Estimate remaining time based on progress rate

SCENARIO B: Index creation is stuck (no progress for 10+ minutes)
⚠️ ACTION: Consider cancelling and using alternative approach
   - Cancel the index creation
   - Use the optimized cleanup without temp index (only 44k duplicates)

SCENARIO C: High dead tuple percentage (>5%)
⚠️ ACTION: May need VACUUM before index creation
   - Cancel index creation
   - Run VACUUM ANALYZE
   - Retry index creation or skip temp index

SCENARIO D: Blocking locks detected
⚠️ ACTION: Cancel index creation
   - Investigate blocking queries
   - Retry during off-peak hours
*/

-- =====================================================================================
-- OPTION 1: Cancel Index Creation and Skip Temp Index
-- =====================================================================================

-- If you decide to cancel (recommended if >1 hour with no progress):

-- Find the PID of the CREATE INDEX process
SELECT 
    pid,
    usename,
    query_start,
    age(clock_timestamp(), query_start) AS running_time,
    state,
    query
FROM pg_stat_activity
WHERE query ILIKE '%CREATE INDEX%temp_idx_dup_analysis%'
  AND state != 'idle';

-- Cancel the query (replace <PID> with actual PID from above)
-- SELECT pg_cancel_backend(<PID>);

-- Example: SELECT pg_cancel_backend(12345);

-- After cancellation, verify index was dropped
SELECT 
    schemaname,
    relname,
    indexrelname
FROM pg_stat_user_indexes
WHERE indexrelname = 'temp_idx_dup_analysis';

-- If index still exists in invalid state, drop it
DROP INDEX IF EXISTS correspondence."temp_idx_dup_analysis";

-- =====================================================================================
-- OPTION 2: Alternative - Fast Cleanup Without Temp Index
-- =====================================================================================

-- Given you only have ~44k duplicates (0.02%), the temp index may not be worth it.
-- You can clean up duplicates efficiently without it:

-- Create backup first (if not already done)
CREATE TABLE IF NOT EXISTS correspondence."A2Iss1951A2Events_backup" AS
SELECT * FROM correspondence."A2Iss1951A2Events";

-- Fast duplicate removal (works fine for 44k duplicates even without index)
BEGIN;

WITH ranked_records AS (
    SELECT 
        ctid,
        ROW_NUMBER() OVER (
            PARTITION BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
            ORDER BY "Source" DESC  -- Keep Source = 1
        ) AS rn
    FROM correspondence."A2Iss1951A2Events"
)
DELETE FROM correspondence."A2Iss1951A2Events"
WHERE ctid IN (
    SELECT ctid 
    FROM ranked_records 
    WHERE rn > 1
);

-- Verify results
SELECT 
    'After cleanup' AS status,
    195499279 - COUNT(*) AS deleted_count,
    COUNT(*) AS remaining_rows
FROM correspondence."A2Iss1951A2Events";

-- If looks good (deleted ~44k):
-- COMMIT;

-- If something wrong:
-- ROLLBACK;

-- =====================================================================================
-- OPTION 3: Wait for Completion (if showing progress)
-- =====================================================================================

-- Monitor progress every 5 minutes
-- Run this query repeatedly to track progress:

SELECT 
    indexrelname AS index_name,
    pg_size_pretty(pg_relation_size(indexrelid)) AS current_size,
    phase,
    ROUND(100.0 * blocks_done / NULLIF(blocks_total, 0), 2) AS blocks_pct_complete,
    ROUND(100.0 * tuples_done / NULLIF(tuples_total, 0), 2) AS tuples_pct_complete,
    lockers_total,
    lockers_done
FROM pg_stat_progress_create_index p
JOIN pg_stat_user_indexes i ON p.relid = i.relid
WHERE indexrelname = 'temp_idx_dup_analysis';

-- Estimate completion time:
-- If at 50% after 1 hour → expect 2 hours total
-- If at 75% after 1 hour → expect 1.33 hours total

-- =====================================================================================
-- RECOMMENDATION: Given Your Situation
-- =====================================================================================

/*
RECOMMENDED ACTION (considering 1 hour elapsed):

1. Check progress using the monitoring queries above

2. IF progress < 50% after 1 hour:
   ✅ CANCEL the index creation
   ✅ Skip temp index entirely (only 44k duplicates don't need it)
   ✅ Run direct cleanup (OPTION 2 above)
   ✅ Proceed to production indexes

   WHY: 
   - Temp index would take another 1+ hour
   - You'll delete it afterward anyway
   - With only 44k duplicates, cleanup works fine without it
   - Saves 1-2 hours of waiting

3. IF progress > 75% after 1 hour:
   ✅ Let it finish (only 15-20 more minutes)
   ✅ Then run fast duplicate analysis
   ✅ Then cleanup with the index

   WHY:
   - Almost done, worth finishing
   - Will make cleanup slightly faster

TOTAL TIME SAVINGS by skipping temp index:
- Current approach: 1hr (index) + 5min (cleanup) = 65 min
- Skip temp index: 5-10min (cleanup without index) = 5-10 min
- Savings: 55-60 minutes
*/

-- =====================================================================================
-- EXECUTION: Cancel and Skip Temp Index (Recommended)
-- =====================================================================================

-- Step 1: Find and cancel the CREATE INDEX process
SELECT 
    pid,
    query_start,
    age(clock_timestamp(), query_start) AS running_time,
    state
FROM pg_stat_activity
WHERE query ILIKE '%CREATE INDEX%temp_idx_dup_analysis%'
  AND state != 'idle';

-- Step 2: Cancel it (replace <PID>)
-- SELECT pg_cancel_backend(<PID>);

-- Step 3: Drop the invalid index if exists
DROP INDEX IF EXISTS correspondence."temp_idx_dup_analysis";

-- Step 4: Proceed with cleanup (no temp index needed for 44k duplicates)
-- Use OPTION 2 above

-- Step 5: Create production indexes (10-15 min)
-- These are the indexes you actually need for queries

CREATE INDEX CONCURRENTLY "IX_A2Iss1951A2Events_CorrId_Status_Party"
ON correspondence."A2Iss1951A2Events" (
    "CorrespondenceId",
    "Status"
)
INCLUDE ("PartyUuid", "Timestamp");

CREATE INDEX CONCURRENTLY "IX_A2Iss1951A2Events_Status_Timestamp"
ON correspondence."A2Iss1951A2Events" (
    "Status",
    "Timestamp"
)
INCLUDE ("CorrespondenceId", "PartyUuid");

ANALYZE correspondence."A2Iss1951A2Events";
