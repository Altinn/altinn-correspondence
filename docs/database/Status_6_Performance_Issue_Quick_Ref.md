# Status 6 Query Performance Issue - Quick Reference

## Problem Summary

**Status 4 Query**: ✅ 17ms execution time  
**Status 6 Query**: ⚠️ 3.5 seconds execution time (206x slower)

## Root Cause

Query planner choosing **wrong index** for Status 6:
- ❌ Using: `IX_A2Iss1716A2Events_Status_CorrId` (slower, sequential)
- ✅ Should use: `IX_A2Iss1716A2Events_CorrId_Status_Party` (faster, parallel)

## Quick Diagnosis

Run this query:
```sql
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT
    er."ReferenceValue" AS DialogId,
    idcConfirm."Id" AS DialogActivityId,
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    6 AS Status,
    'CorrespondenceConfirmed' AS ActivityType
FROM correspondence."A2Iss1716A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
INNER JOIN correspondence."ExternalReferences" er
    ON a2Events."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcConfirm
    ON a2Events."CorrespondenceId" = idcConfirm."CorrespondenceId"
    AND idcConfirm."StatusAction" = '6'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE a2Events."Status" = 6
ORDER BY stats."CorrespondenceId", Status
LIMIT 100;
```

### Signs of Problem
Look for these in query plan:
- ❌ `Index Only Scan using IX_A2Iss1716A2Events_Status_CorrId`
- ❌ Execution Time: 3,000-4,000 ms
- ❌ Shared read: 1,000+ blocks
- ❌ I/O Timings: 4,000+ ms
- ❌ Only 1-2 workers

### Signs of Success
Look for these:
- ✅ `Parallel Index Only Scan using IX_A2Iss1716A2Events_CorrId_Status_Party`
- ✅ Execution Time: <100 ms
- ✅ Shared read: 0 blocks (all cached)
- ✅ I/O Timings: minimal or 0
- ✅ 2+ workers

## Solutions (In Order)

### Solution 1: Update Statistics (TRY THIS FIRST)
```sql
ANALYZE correspondence."A2Iss1716A2Events";
```
**Duration**: < 1 second  
**Effect**: Helps query planner make better index choice

### Solution 2: Warm Up Cache
Run the Status 6 query 2-3 times:
- **First run**: 3-4 seconds (cold cache) - EXPECTED
- **Second run**: Should drop to <100ms - LOOK FOR THIS
- **Third run**: Should stay <100ms

If second/third run still slow → Try Solution 3

### Solution 3: Drop Competing Index
```sql
DROP INDEX CONCURRENTLY correspondence."IX_A2Iss1716A2Events_Status_CorrId";
```
**Duration**: 2-3 minutes  
**Effect**: Forces planner to use optimal index for both Status 4 and 6  
**Safety**: Can be recreated later if needed

## Verification After Fix

Run Status 6 query again and check:
```sql
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT ...
WHERE a2Events."Status" = 6
LIMIT 100;
```

**Expected Results**:
| Metric | Target | Your Result |
|--------|--------|-------------|
| Execution Time | <100 ms | __________ |
| Index Used | CorrId_Status_Party | __________ |
| Workers | 2+ | __________ |
| Disk Reads | 0 (after warmup) | __________ |

## Impact on Full Export

### Current Status (With Issue)
- Status 4 batches: ~17 ms each
- Status 6 batches: ~3,500 ms each
- Mixed average: ~1,750 ms per batch
- **Total export time**: ~3 hours (assuming 50/50 split)

### After Fix (Cache Warmed)
- Status 4 batches: ~17 ms each
- Status 6 batches: ~50 ms each (after warmup)
- Mixed average: ~35 ms per batch
- **Total export time**: ~35-60 minutes ✅

### After Fix (Index Dropped)
- Both Status 4 and 6: ~17-50 ms each
- Mixed average: ~30 ms per batch
- **Total export time**: ~30-45 minutes ✅✅

## When to Apply Fix

### Before Full Export (Recommended)
1. Create both indexes
2. Run `ANALYZE`
3. Test Status 6 query 2-3 times
4. If still slow, drop `Status_CorrId` index
5. Start full export

### During Export (If Needed)
If export is running and Status 6 batches are slow:
1. Let current batch finish
2. Run `ANALYZE` in separate session
3. Monitor next few batches for improvement
4. If no improvement, schedule index drop during next maintenance window

## Monitoring Commands

### Check Current Index Usage
```sql
SELECT 
    indexname,
    idx_scan as times_used,
    idx_tup_read as tuples_read,
    idx_tup_fetch as tuples_fetched
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND tablename = 'A2Iss1716A2Events'
ORDER BY indexname;
```

### Check Cache Hit Rate
```sql
SELECT 
    schemaname,
    tablename,
    heap_blks_read as disk_reads,
    heap_blks_hit as cache_hits,
    ROUND(100.0 * heap_blks_hit / NULLIF(heap_blks_read + heap_blks_hit, 0), 2) as cache_hit_rate
FROM pg_statio_user_tables
WHERE schemaname = 'correspondence'
  AND tablename = 'A2Iss1716A2Events';
```

Target cache hit rate: >99%

### Check Last ANALYZE Time
```sql
SELECT 
    schemaname,
    tablename,
    last_analyze,
    last_autoanalyze
FROM pg_stat_user_tables
WHERE schemaname = 'correspondence'
  AND tablename = 'A2Iss1716A2Events';
```

## Decision Tree

```
Is Status 6 query slow (>500ms)?
│
├─ NO → ✅ You're good! Proceed with export
│
└─ YES → Run ANALYZE
    │
    ├─ Still slow after ANALYZE?
    │  │
    │  ├─ NO → ✅ Fixed! Cache will warm up during export
    │  │
    │  └─ YES → Run query 2-3 times (cache warmup)
    │      │
    │      ├─ Faster on 2nd/3rd run?
    │      │  │
    │      │  ├─ YES → ✅ Fixed! Cache effect working
    │      │  │
    │      │  └─ NO → Drop IX_A2Iss1716A2Events_Status_CorrId
    │           │
    │           └─ ✅ Fixed! Both queries now using optimal index
```

## FAQ

**Q: Why is Status 4 fast but Status 6 slow?**  
A: Status 4 ran first and warmed up the cache + PostgreSQL chose better index. Status 6 is hitting cold cache + suboptimal index.

**Q: Will this fix itself during export?**  
A: Partially. Cache will warm up (3.5s → ~500ms), but without ANALYZE or index drop, it won't reach optimal <100ms.

**Q: Can I skip the fix and run export anyway?**  
A: Yes, but export will take ~3 hours instead of 30-45 minutes. The fix takes < 5 minutes.

**Q: Is it safe to drop the Status_CorrId index?**  
A: Yes. The CorrId_Status_Party index handles all queries efficiently. You can recreate Status_CorrId later if needed.

**Q: What if I drop the wrong index?**  
A: Indexes can be recreated with the scripts in `Optimize_A2Iss1716A2Events_Indexes.sql`. Takes 2-3 minutes.

## Summary

✅ **Status 4**: Working perfectly (17ms)  
⚠️ **Status 6**: Needs attention (3.5s → should be <100ms)  
🔧 **Fix**: Run ANALYZE + test + optionally drop competing index  
⏱️ **Time to fix**: < 5 minutes  
📊 **Impact**: 3 hours → 30-45 minutes export time

---

**Related Files**:
- Full analysis: `A2Iss1716A2Events_Production_Verification.md`
- Migration guide: `A2Iss1716A2Events_Helper_Table_Migration.md`
- Index scripts: `Optimize_A2Iss1716A2Events_Indexes.sql`
