-- =====================================================================================
-- Check Disk Space and Table Statistics
-- =====================================================================================
-- For use BEFORE creating large indexes on 1.94 billion row table
-- =====================================================================================

-- 1. Check available disk space
SELECT 
    pg_tablespace_name(oid) AS tablespace,
    pg_size_pretty(pg_tablespace_size(oid)) AS size
FROM pg_tablespace;

-- 2. Current CorrespondenceStatuses table size
SELECT 
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(relid)) AS total_size,
    pg_size_pretty(pg_relation_size(relid)) AS table_size,
    pg_size_pretty(pg_indexes_size(relid)) AS indexes_size,
    n_live_tup AS live_rows,
    n_dead_tup AS dead_rows,
    ROUND(100.0 * n_dead_tup / NULLIF(n_live_tup + n_dead_tup, 0), 2) AS dead_row_pct
FROM pg_stat_user_tables
WHERE schemaname = 'correspondence'
  AND tablename = 'CorrespondenceStatuses';

-- 3. Existing indexes on CorrespondenceStatuses
SELECT 
    schemaname,
    tablename,
    indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size,
    idx_scan AS times_used,
    idx_tup_read AS tuples_read,
    idx_tup_fetch AS tuples_fetched
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND tablename = 'CorrespondenceStatuses'
ORDER BY pg_relation_size(indexrelid) DESC;

-- 4. Estimate new index sizes (rough calculation)
-- Based on 1.94B total rows:
-- - Issue #1716 (SyncedFromAltinn2 IS NOT NULL): ~1% = ~19M rows → ~3 GB
-- - Issue #1951 (SyncedFromAltinn2 IS NULL): ~15% = ~300M rows → ~24 GB
SELECT 
    'Estimated disk space needed for new indexes:' AS info,
    '3 GB (Issue #1716) + 24 GB (Issue #1951) = ~27 GB' AS estimate,
    'Plus 50% temp space during creation = ~40 GB total' AS with_temp;

-- 5. Check if table needs VACUUM
SELECT 
    schemaname,
    tablename,
    last_vacuum,
    last_autovacuum,
    n_dead_tup,
    n_mod_since_analyze,
    CASE 
        WHEN n_dead_tup > 1000000 THEN 'VACUUM RECOMMENDED'
        ELSE 'OK'
    END AS vacuum_recommendation
FROM pg_stat_user_tables
WHERE schemaname = 'correspondence'
  AND tablename = 'CorrespondenceStatuses';

-- 6. Current database size
SELECT 
    pg_database.datname,
    pg_size_pretty(pg_database_size(pg_database.datname)) AS size
FROM pg_database
WHERE datname = current_database();

-- =====================================================================================
-- Recommendations based on 1.94 billion rows:
-- =====================================================================================
-- 
-- DISK SPACE:
-- - Ensure at least 50 GB free space (27 GB indexes + 20 GB temp + buffer)
-- - Monitor disk space during creation with: df -h (Linux) or Get-PSDrive (PowerShell)
--
-- TIMING:
-- - Index #1 (Issue #1716): 1-2 hours (currently running)
-- - Index #2 (Issue #1951): 6-10 hours (run overnight or maintenance window)
--
-- PERFORMANCE:
-- - Set maintenance_work_mem = '4GB' before creating Index #2
-- - Run VACUUM ANALYZE if dead_row_pct > 5%
-- - Schedule during lowest traffic period
-- - Consider pausing ETL/batch jobs during creation
--
-- =====================================================================================
