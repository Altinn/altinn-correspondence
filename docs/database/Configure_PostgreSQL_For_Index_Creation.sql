-- =====================================================================================
-- PostgreSQL Configuration Optimization for Index #2 Creation
-- =====================================================================================
-- Current setting: maintenance_work_mem = 2097151kB (≈ 2 GB)
-- Recommended: Increase to 4 GB for Index #2 (300M rows)
-- =====================================================================================

-- =====================================================================================
-- Step 1: Check Current Configuration
-- =====================================================================================

-- View current maintenance_work_mem:
SHOW maintenance_work_mem;
-- Current result: 2097151kB (≈ 2 GB)

-- View all relevant memory settings:
SELECT 
    name,
    setting,
    unit,
    pg_size_pretty((setting::bigint * 
        CASE 
            -- Handle block-based units (e.g., '8kB' where blocks are 8192 bytes)
            WHEN unit ~ '^\d+kB$' THEN 
                (substring(unit from '^\d+')::bigint * 1024)
            WHEN unit = 'kB' THEN 1024
            WHEN unit = 'MB' THEN 1024*1024
            WHEN unit = 'GB' THEN 1024*1024*1024
            ELSE 1
        END)::bigint) as pretty_value,
    context,
    source
FROM pg_settings
WHERE name IN (
    'maintenance_work_mem',
    'work_mem',
    'shared_buffers',
    'effective_cache_size',
    'max_parallel_maintenance_workers'
);

-- =====================================================================================
-- Step 2: Recommended Settings for Index #2 Creation
-- =====================================================================================

-- RECOMMENDATION: Temporarily increase maintenance_work_mem for the session
-- This speeds up index building by allowing larger in-memory sorts

-- Set for current session only (reverts after disconnect):
SET maintenance_work_mem = '4GB';

-- Verify the change:
SHOW maintenance_work_mem;
-- Expected: 4194304kB (4 GB)

-- =====================================================================================
-- Step 3: Optional - Increase Parallelism (PostgreSQL 11+)
-- =====================================================================================

-- Check current parallel workers setting:
SHOW max_parallel_maintenance_workers;

-- If you have multiple CPU cores, consider increasing:
-- SET max_parallel_maintenance_workers = 4;  -- Adjust based on CPU cores

-- =====================================================================================
-- Step 4: Create Index #2 with Optimized Settings
-- =====================================================================================

-- NOW create the index with optimized configuration:
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CorrespondenceStatuses_Status_StatusChanged_Migrated"
ON correspondence."CorrespondenceStatuses" ("Status", "StatusChanged")
INCLUDE ("CorrespondenceId", "PartyUuid")
WHERE "SyncedFromAltinn2" IS NULL;

-- =====================================================================================
-- Why These Settings Matter
-- =====================================================================================
--
-- maintenance_work_mem:
-- • Controls memory for maintenance operations (CREATE INDEX, VACUUM, etc.)
-- • Larger values = faster index building (more in-memory sorting)
-- • Current 2 GB is good, but 4 GB is better for 300M row index
-- • Formula: Aim for (index_size / 6) as rough minimum
--   - Index #2: ~24 GB / 6 = ~4 GB
--
-- max_parallel_maintenance_workers:
-- • Enables parallel index building (if supported)
-- • Can significantly speed up index creation
-- • Diminishing returns beyond 4 workers
-- • Only helps if you have multiple CPU cores available
--
-- =====================================================================================
-- Expected Performance Impact
-- =====================================================================================
--
-- With current 2 GB maintenance_work_mem:
-- • Estimated time: 10-14 hours
-- • More disk I/O (external merge sorts)
--
-- With optimized 4 GB maintenance_work_mem:
-- • Estimated time: 8-12 hours (15-20% faster)
-- • Less disk I/O (larger in-memory sorts)
-- • Smoother progress through build phase
--
-- =====================================================================================
-- Step 5: Reset Configuration After Completion (Optional)
-- =====================================================================================

-- Session-based settings automatically reset on disconnect
-- If you want to reset manually in same session:
-- RESET maintenance_work_mem;
-- RESET max_parallel_maintenance_workers;

-- Verify reset:
-- SHOW maintenance_work_mem;  -- Should show default: 2097151kB

-- =====================================================================================
-- Alternative: Set for Entire Connection (DBeaver/pgAdmin)
-- =====================================================================================

-- In DBeaver, you can set these in the connection properties:
-- 1. Right-click connection → Edit Connection
-- 2. Go to "Connection settings" → "Initialization"
-- 3. Add to "SQL for execute after connect":
--    SET maintenance_work_mem = '4GB';

-- =====================================================================================
-- Complete Workflow for Index #2 Creation
-- =====================================================================================

/*
-- 1. Connect to database
psql -h altinn-corr-prod-dbserver.postgres.database.azure.com -U youruser -d correspondence

-- 2. Set optimized configuration
SET maintenance_work_mem = '4GB';
SET max_parallel_maintenance_workers = 4;  -- If multi-core available

-- 3. Verify settings
SHOW maintenance_work_mem;
SHOW max_parallel_maintenance_workers;

-- 4. Create the index
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CorrespondenceStatuses_Status_StatusChanged_Migrated"
ON correspondence."CorrespondenceStatuses" ("Status", "StatusChanged")
INCLUDE ("CorrespondenceId", "PartyUuid")
WHERE "SyncedFromAltinn2" IS NULL;

-- 5. Monitor progress (in separate session):
SELECT 
    p.phase,
    ROUND(100.0 * p.tuples_done / NULLIF(p.tuples_total, 0), 2) AS percent_complete,
    NOW() - a.query_start as elapsed_time
FROM pg_stat_progress_create_index p
JOIN pg_stat_activity a ON p.pid = a.pid
WHERE p.command = 'CREATE INDEX CONCURRENTLY';

-- 6. After completion, settings automatically reset on disconnect
*/

-- =====================================================================================
-- Important Notes
-- =====================================================================================
--
-- 1. SESSION vs GLOBAL settings:
--    - SET command = session-only (recommended for index creation)
--    - ALTER SYSTEM = global (requires superuser, not recommended for temp changes)
--
-- 2. Memory availability:
--    - Ensure server has 4+ GB free RAM before setting maintenance_work_mem = 4GB
--    - Check with: SELECT pg_size_pretty(pg_total_relation_size('pg_class'));
--
-- 3. Azure PostgreSQL specifics:
--    - Some settings may require server restart (unlikely for maintenance_work_mem)
--    - Check Azure Portal for any server-level restrictions
--
-- =====================================================================================
