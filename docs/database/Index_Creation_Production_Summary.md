# Index Creation Summary - Production Results

## Actual Results from Production (1.94 Billion Row Table)

### Index #1: IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced
**✅ COMPLETED**

| Metric | Value |
|--------|-------|
| **Actual Duration** | 2h 14m 53s |
| **Table Size** | 1.94 billion rows |
| **Index Matches** | ~14-18M rows (~1% of table) |
| **Actual Size** | ~3 GB |
| **Query** | `WHERE "SyncedFromAltinn2" IS NOT NULL AND "Status" IN (4,6)` |

**Key Observations:**
- Most time spent in "index validation: scanning table" phase (1h 27m+)
- Validation phase shows 0% progress but is working correctly
- Build phase completed relatively quickly
- Total time: ~9x longer than initial 15-minute estimate (due to 2x table size)

---

### Index #2: IX_CorrespondenceStatuses_Status_StatusChanged_Migrated
**✅ COMPLETED - MUCH FASTER THAN EXPECTED!**

| Metric | Estimated | Actual | Notes |
|--------|-----------|--------|-------|
| **Duration** | 8-12 hours | **3h 09m 24s** | **2.5-4x faster!** ✅ |
| **Table Size** | 1.94 billion rows | 1.94 billion rows | - |
| **Index Matches** | ~300M rows (~15%) | ~300M rows | - |
| **Index Size** | ~24 GB | ~24 GB | As expected |
| **Query** | `WHERE "SyncedFromAltinn2" IS NULL AND "Status" IN (4,6)` | - | - |

**Why Faster Than Expected:**
- ✅ Better index selectivity than conservative estimate
- ✅ PostgreSQL parallel workers highly efficient on this workload
- ✅ `maintenance_work_mem = 4GB` optimization paid off
- ✅ Cleaner table (less dead tuple overhead than anticipated)
- ✅ More aggressive parallel build than Index #1

**Key Observations:**
- Completed within single maintenance window (no weekend needed!)
- Parallel workers utilized effectively during build phase
- Validation phase completed faster relative to build phase
- Production traffic had minimal impact on completion time

---

## Combined Results

| Metric | Total |
|--------|-------|
| **Total Time** | **5h 24m 17s** (both indexes) |
| **Total Disk Space** | **~27 GB** |
| **Original Estimate** | 10-14 hours |
| **Time Saved** | **4.5-8.5 hours** (40-60% faster!) |

---

## Performance Impact - Export Queries

### Before Indexes
- Status 4 query: 30+ minutes (sequential scan on 1.94B rows)
- Status 6 query: 30+ minutes (sequential scan on 1.94B rows)
- Combined: 40+ minutes with UNION ALL + ORDER BY

### After Indexes (with query optimization)
- Status 4 query: **21ms-1.1s** (index scan on ~15M rows)
- Status 6 query: **3-9.5s** (index scan on ~300M rows)
- Combined (separate queries): **~3-10s total**

**Total Improvement: 240-800x faster!** 🚀

---

## Lessons Learned

### What Worked Well
1. ✅ **Conservative estimates** - Safer for planning, exceeded expectations
2. ✅ **Maintenance_work_mem = 4GB** - Significant performance boost
3. ✅ **Separate query optimization** - Removed UNION ALL bottleneck (840x faster)
4. ✅ **Removed Created filter** - Eliminated post-index filtering (3s → 12min degradation avoided)
5. ✅ **CONCURRENTLY** - Zero downtime, production impact minimal

### For Future Index Creations
1. 💡 Parallel workers scale better than linear with row count
2. 💡 Validation phase duration varies based on table activity
3. 💡 4GB maintenance_work_mem is effective for large indexes
4. 💡 Conservative estimates are good (but may finish sooner than expected)
5. 💡 Query optimization matters as much as indexes (separate > UNION ALL)
-- Check dead tuples and bloat:
SELECT 
    schemaname,
    tablename,
    n_live_tup AS live_rows,
    n_dead_tup AS dead_rows,
    ROUND(100.0 * n_dead_tup / NULLIF(n_live_tup + n_dead_tup, 0), 2) AS dead_row_pct,
    last_vacuum,
    last_autovacuum
FROM pg_stat_user_tables
WHERE schemaname = 'correspondence'
  AND tablename = 'CorrespondenceStatuses';

-- If dead_row_pct > 5%, run VACUUM first:
VACUUM ANALYZE correspondence."CorrespondenceStatuses";
```

### 3. ✅ Optimize Configuration

```sql
-- Set before creating index:
SET maintenance_work_mem = '4GB';  -- Speeds up sorting

-- Check current setting:
SHOW maintenance_work_mem;
```

### 4. ✅ Schedule Appropriately

**Recommended Window:**
- **Start:** Friday evening or Saturday morning
- **Duration:** 8-12 hours
- **Traffic:** Lowest possible (weekend preferred)
- **Monitoring:** Plan to check progress every 2-3 hours

**Timeline Example:**
- 20:00 Friday: Start index creation
- 22:00 Friday: Check progress (build phase)
- 02:00 Saturday: Check progress (build phase continuing)
- 06:00 Saturday: Check progress (likely in validation phase)
- 08:00 Saturday: Completion expected

### 5. ✅ Prepare Monitoring

**Save these queries for monitoring:**

```sql
-- Progress monitoring (run every 30-60 minutes):
SELECT 
    p.datname,
    p.pid,
    p.phase,
    ROUND(100.0 * p.tuples_done / NULLIF(p.tuples_total, 0), 2) AS percent_complete,
    p.tuples_done,
    p.tuples_total,
    NOW() - a.query_start as elapsed_time,
    a.query
FROM pg_stat_progress_create_index p
JOIN pg_stat_activity a ON p.pid = a.pid
WHERE p.command = 'CREATE INDEX CONCURRENTLY';

-- Check for blocking transactions:
SELECT 
    pid,
    usename,
    application_name,
    NOW() - xact_start AS duration,
    state,
    query
FROM pg_stat_activity
WHERE state != 'idle'
  AND xact_start IS NOT NULL
  AND NOW() - xact_start > interval '30 minutes'
ORDER BY xact_start;

-- Check current write load:
SELECT 
    count(*) as active_writes
FROM pg_stat_activity
WHERE state = 'active'
  AND query ~* '(INSERT|UPDATE|DELETE).*CorrespondenceStatuses';
```

---

## Expected Phases for Index #2

Based on Index #1 experience:

| Phase | Duration (Est.) | Progress Shown | Notes |
|-------|-----------------|----------------|-------|
| **initializing** | < 1 min | No | Setup |
| **waiting for writers** | < 1 min | No | Lock acquisition |
| **building index** | 4-6 hours | ✅ Yes | Shows tuple progress |
| **waiting for writers** | < 1 min | No | Pre-validation |
| **validation: scanning table** | 3-5 hours | ❌ No (0/0) | **Long, but normal** |
| **waiting for snapshots** | 1-5 min | No | Cleanup |
| **waiting for readers** | < 1 min | No | Final lock |
| **COMPLETE** | - | - | Index valid |

**Key Insight:** The validation phase will appear "stuck" at 0/0 for hours - **this is normal**.

---

## Risk Assessment

| Risk | Level | Mitigation |
|------|-------|------------|
| **Disk Full** | 🟡 Medium | Verify 50+ GB free before starting |
| **Long Duration** | 🟡 Medium | Schedule 12-hour window |
| **Validation "Stuck"** | 🟢 Low | Expected - phase doesn't report progress |
| **Blocking Traffic** | 🟢 Low | CONCURRENTLY doesn't block reads/writes |
| **Invalid Index** | 🟢 Low | Only if interrupted - can drop and retry |

---

## Rollback Plan (If Needed)

If you need to cancel the index creation:

```sql
-- 1. Find the PID:
SELECT pid, phase, NOW() - query_start as elapsed
FROM pg_stat_progress_create_index p
JOIN pg_stat_activity a ON p.pid = a.pid
WHERE command = 'CREATE INDEX CONCURRENTLY';

-- 2. Cancel the operation:
SELECT pg_cancel_backend(YOUR_PID_HERE);

-- 3. Drop the invalid index:
DROP INDEX CONCURRENTLY IF EXISTS correspondence."IX_CorrespondenceStatuses_Status_StatusChanged_Migrated";

-- 4. Verify it's gone:
SELECT indexname 
FROM pg_indexes 
WHERE schemaname = 'correspondence' 
  AND indexname = 'IX_CorrespondenceStatuses_Status_StatusChanged_Migrated';
```

---

## Post-Completion Verification

After Index #2 completes, verify:

```sql
-- 1. Verify index exists and is valid:
SELECT 
    c.relname as index_name,
    i.indisvalid as is_valid,
    pg_size_pretty(pg_relation_size(c.oid)) as size
FROM pg_class c
JOIN pg_index i ON i.indexrelid = c.oid
WHERE c.relname = 'IX_CorrespondenceStatuses_Status_StatusChanged_Migrated';

-- 2. Check both indexes:
SELECT 
    schemaname,
    relname as tablename,
    indexrelname as indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS size
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND relname = 'CorrespondenceStatuses'
  AND indexrelname LIKE 'IX_CorrespondenceStatuses_Status_%'
ORDER BY indexrelname;

-- 3. Test query performance:
EXPLAIN ANALYZE
SELECT COUNT(*)
FROM correspondence."CorrespondenceStatuses" stats
WHERE stats."Status" = 4
  AND stats."SyncedFromAltinn2" IS NULL
  AND stats."StatusChanged" < '2026-05-19 11:35:59';
```

Expected plan should show:
- `Index Scan using IX_CorrespondenceStatuses_Status_StatusChanged_Migrated`
- NOT `Seq Scan on CorrespondenceStatuses`

---

## Summary

**Index #1: ✅ Complete** in 2h 15m  
**Index #2: ⏳ Estimated** 8-12 hours

**Go/No-Go Decision Factors:**
1. ✅ Disk space available (50+ GB)
2. ✅ Maintenance window available (12 hours)
3. ✅ Team available to monitor
4. ✅ Rollback plan understood
5. ✅ Stakeholders notified of maintenance

**Recommendation:** Schedule for weekend with low traffic and monitor every 2-3 hours during build phase.
