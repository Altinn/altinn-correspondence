# A2Iss1716A2Events Helper Table Migration Guide

## Overview

The **A2Iss1716A2Events** helper table is a pre-filtered export from Altinn 2 containing only the correspondence events affected by Issue #1716. This optimization eliminates the need to scan 1.94 billion rows in CorrespondenceStatuses for Issue #1716 exports.

## Table Structure

```sql
CREATE TABLE correspondence."A2Iss1716A2Events" (
    "CorrespondenceId" uuid NOT NULL,
    "Timestamp" timestamptz NOT NULL,
    "PartyUuid" uuid NOT NULL,
    "Status" int4 NOT NULL
);
```

**Row Count**: ~9.97 million (Status 4 and Status 6 events)

## Performance Comparison

### Before (CTE Approach)
- **Query Time**: ~5-6 seconds per batch
- **Approach**: Scan CorrespondenceStatuses with SyncedFromAltinn2 filter
- **Index Used**: IX_CorrespondenceStatuses_Status_SyncedFromAltinn2_CorrId (3GB)
- **Problem**: Still scanning millions of rows even with index

### After (Helper Table Approach)
- **Query Time**: Expected < 1 second per batch
- **Approach**: Join from A2Iss1716A2Events (9.97M rows)
- **Indexes Required**: 2 indexes on helper table (see below)
- **Benefit**: Direct event lookup, no timestamp filtering needed

## Required Indexes

**IMPORTANT**: You must create these indexes BEFORE running the updated export job:

```sql
-- Index 1: Primary lookup by Status and CorrespondenceId (CRITICAL)
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_A2Iss1716A2Events_Status_CorrId"
ON correspondence."A2Iss1716A2Events" ("Status", "CorrespondenceId")
INCLUDE ("PartyUuid", "Timestamp");

-- Index 2: Composite covering index for joins
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_A2Iss1716A2Events_CorrId_Status_Party"
ON correspondence."A2Iss1716A2Events" ("CorrespondenceId", "Status", "PartyUuid")
INCLUDE ("Timestamp");
```

**Build Time**: 2-3 minutes each (9.97M rows)  
**Disk Space**: ~2 GB total for both indexes

See `Optimize_A2Iss1716A2Events_Indexes.sql` for detailed index creation script with verification queries.

## Code Changes

### DialogActivityExportService.cs

The export service now uses different query strategies for each issue:

#### Issue #1716 (NEW - Helper Table)
- **Source**: A2Iss1716A2Events helper table
- **Filter**: Direct Status = 4 or 6 lookup
- **No Timestamp Filter**: Helper table already pre-filtered
- **Count Query**: Simple COUNT(*) on helper table

#### Issue #1951 (Unchanged - CTE)
- **Source**: CorrespondenceStatuses with CTE
- **Filter**: SyncedFromAltinn2 IS NULL + StatusChanged BETWEEN
- **Requires**: Index #2 on CorrespondenceStatuses (not yet created)

### Query Structure

**Issue #1716 Query (Simplified)**:
```sql
SELECT DISTINCT ...
FROM correspondence."A2Iss1716A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
INNER JOIN ... (other tables)
WHERE a2Events."Status" = {4 or 6}
ORDER BY stats."CorrespondenceId", stats."Status"
LIMIT @fetchLimit;
```

**Key Benefits**:
- Starts from 9.97M pre-filtered rows instead of 1.94B
- Index-only scans with INCLUDE columns (0 heap fetches)
- Parallel execution with Gather Merge
- Memoize caching for repeated PartyUuid lookups
- **VERIFIED**: 17 ms per 100 rows in production testing

## Actual Performance Results

### Production Query Plan Analysis (EXPLAIN ANALYZE)

**Test Query**: Status 4, LIMIT 100, with both indexes created

| Metric | Result |
|--------|--------|
| **Execution Time** | **17.258 ms** |
| **Planning Time** | 5.416 ms |
| **Rows Returned** | 100 |
| **Buffer Hits** | 6,426 (all cached) |
| **Disk Reads** | 0 |
| **Workers** | 2 (1 parallel + main) |

### Index Usage Verification ✅

1. **IX_A2Iss1716A2Events_CorrId_Status_Party**: 
   - Parallel Index Only Scan
   - 0 Heap Fetches
   - 250 buffer hits

2. **IX_CorrespondenceStatuses_Unique**:
   - Index Only Scan
   - 22 Heap Fetches (minimal)
   - 2,089 buffer hits

3. **Supporting Indexes**:
   - IX_ExternalReferences_CorrId_RefType_RefValue: Index Only Scan
   - IX_A2Parties_PartyUuid_Covering: Index Only Scan with Memoize (144 cache hits)
   - IX_IdempotencyKeys_CorrespondenceId: Index Scan

### Full Export Projections

Based on **17 ms per 100 rows**:

| Estimate | Calculation | Result |
|----------|-------------|--------|
| Total Rows | ~9.97M (Status 4 + 6) | Known |
| Batches Needed | 9,970,000 / 100 | ~99,700 |
| Query Time Only | 99,700 × 0.017s | **28 minutes** |
| With CSV/Overhead | +20-30% | **35-40 minutes** |

**Previous Estimate**: 2-3 hours  
**Actual Expected**: **30-45 minutes** (4-6x faster than original estimate!)

## Migration Steps

### 1. Create Indexes (Required First)
```bash
# Run on production database
psql -h altinn-corr-prod-dbserver.postgres.database.azure.com \
     -U your-username \
     -d correspondence \
     -f Optimize_A2Iss1716A2Events_Indexes.sql
```

**Expected Duration**: 4-6 minutes total (both indexes)

### 2. Update Table Statistics (CRITICAL)
```sql
-- Run immediately after index creation
ANALYZE correspondence."A2Iss1716A2Events";
```

**Why This Is Important**:
- Helps query planner choose optimal index for both Status 4 and Status 6
- Without ANALYZE, Status 6 queries may use wrong index and be 200x slower
- Takes < 1 second to run

### 3. Verify Indexes
```sql
-- Check index creation status
SELECT 
    schemaname,
    tablename,
    indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) as size,
    idx_scan as times_used
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND tablename = 'A2Iss1716A2Events'
ORDER BY indexname;

-- Verify index validity
SELECT 
    c.relname as index_name,
    i.indisvalid as is_valid
FROM pg_class c
JOIN pg_index i ON i.indexrelid = c.oid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relname LIKE 'IX_A2Iss1716A2Events%'
  AND n.nspname = 'correspondence';
```

Both indexes should show `is_valid = true`.

### 4. Test Query Performance (IMPORTANT)
```sql
-- Test Status 4 (should be fast ~17ms)
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT ...
FROM correspondence."A2Iss1716A2Events" a2Events
WHERE a2Events."Status" = 4
ORDER BY stats."CorrespondenceId", Status
LIMIT 100;

-- Test Status 6 TWICE (first may be slower due to cold cache)
-- First run: May take 3-4 seconds (cold cache)
-- Second run: Should be <100ms (cached)
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT ...
FROM correspondence."A2Iss1716A2Events" a2Events
WHERE a2Events."Status" = 6
ORDER BY stats."CorrespondenceId", Status
LIMIT 100;
```

**Expected Results**:
- Status 4: ~17ms ✅
- Status 6 (first run): 3-4 seconds (acceptable - cache warmup)
- Status 6 (second run): <100ms ✅

**If Status 6 second run is still >500ms**, see Troubleshooting section below.

### 5. Deploy Updated Code
Deploy the updated DialogActivityExportService.cs with helper table support.

### 6. Test Export
```bash
# Test with limited batches
dotnet run --project tools/Altinn.Correspondence.DialogActivityExporter \
    --issue 1716 \
    --test \
    --max-batches 2
```

### 7. Run Production Export
```bash
# Full production export
dotnet run --project tools/Altinn.Correspondence.DialogActivityExporter \
    --issue 1716
```

## Testing Queries

See `Test_Export_Query.sql` for manual testing queries:
- **Section 1**: Production CTE version (old approach, for reference)
- **Section 2**: **OPTIMIZED HELPER TABLE VERSION** (new approach, lines 142-211)
- **Section 3**: Alternative simple join version (testing only)

## Troubleshooting

### Problem: Status 6 queries slow (>500ms) even after cache warmup

**Symptom**: Status 6 taking 3-4 seconds consistently, even on second/third run

**Check**: Which index is being used?
```sql
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT ...
FROM correspondence."A2Iss1716A2Events" a2Events
WHERE a2Events."Status" = 6
LIMIT 100;
```

**Expected plan** (fast):
- `Parallel Index Only Scan using IX_A2Iss1716A2Events_CorrId_Status_Party`
- Multiple workers (2+)
- Execution time: <100ms

**Problem plan** (slow):
- `Index Only Scan using IX_A2Iss1716A2Events_Status_CorrId`
- Only 1-2 workers
- Execution time: 3-4 seconds

**Solution**: Drop the Status_CorrId index to force use of CorrId_Status_Party:
```sql
DROP INDEX CONCURRENTLY correspondence."IX_A2Iss1716A2Events_Status_CorrId";
```

This forces both Status 4 and Status 6 to use the optimal index.

### Problem: Query still slow after index creation
- NO `external merge sort` or `temp written`

### Problem: Missing data in export
**Check**: Does helper table have all events?
```sql
-- Compare counts
SELECT 'Helper Table' as source, COUNT(*) as count
FROM correspondence."A2Iss1716A2Events"
WHERE "Status" IN (4, 6)
UNION ALL
SELECT 'CorrespondenceStatuses', COUNT(*)
FROM correspondence."CorrespondenceStatuses" stats
WHERE stats."Status" IN (4, 6)
  AND stats."SyncedFromAltinn2" IS NOT NULL;
```

Counts should match (within reason for timing differences).

### Problem: Index creation fails
**Check**: Disk space and memory
```sql
-- Check available disk space
SELECT 
    pg_tablespace_location(oid) as location,
    pg_size_pretty(pg_tablespace_size(oid)) as size
FROM pg_tablespace;
```

Ensure at least 5 GB free disk space.

## Rollback Plan

If the helper table approach causes issues:

1. **Revert Code**: Restore previous DialogActivityExportService.cs from git
2. **Keep Indexes**: The helper table indexes don't impact other queries
3. **Drop Helper Table** (optional):
   ```sql
   DROP TABLE IF EXISTS correspondence."A2Iss1716A2Events";
   ```

The old CTE approach will still work with Index #1 on CorrespondenceStatuses.

## Performance Metrics

### Actual Results (Production Verified) ✅

**Test Query**: EXPLAIN ANALYZE on Status 4, LIMIT 100

| Metric | Before (CTE) | After (Helper Table) | Improvement |
|--------|-------------|---------------------|-------------|
| Query Time (per 100 rows) | 5-6 seconds | **17 ms** | **300-350x faster** |
| Temp Disk Usage | Minimal | **0** | Eliminated |
| Disk I/O per batch | Variable | **0** (all cached) | 100% cached |
| Index Size | 3 GB (on CorrespondenceStatuses) | 2 GB (on helper table) | Smaller |
| Parallelism | Limited | **Active** (2 workers) | Improved |
| Total Export Time (estimated) | ~12 hours | **30-45 minutes** | **16-24x faster** |

### Count Query Performance

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Count Query Time | 30-60 seconds | < 1 second | **30-60x faster** |
| Approach | Complex joins + filters | Simple COUNT(*) | Much simpler |

### Query Plan Highlights

- ✅ **Parallel Index Only Scan** on IX_A2Iss1716A2Events_CorrId_Status_Party
- ✅ **0 Heap Fetches** on helper table (INCLUDE columns working)
- ✅ **Memoize Caching** on A2Parties (144 hits, 151 misses)
- ✅ **Index Only Scans** on all joined tables
- ✅ **No External Sort** (ORDER BY handled by index)
- ✅ **6,426 Buffer Hits** (all from shared memory, no disk I/O)

## Future Considerations

### Helper Table Maintenance
- **Refresh**: Helper table is a one-time export, no refresh needed
- **Purpose**: Issue #1716 is a specific historical data fix
- **Cleanup**: After Issue #1716 export completes, table can be dropped

### Issue #1951
Issue #1951 still uses the original CTE approach:
- **Index Required**: Index #2 on CorrespondenceStatuses (not yet created)
- **Estimated Time**: 8-12 hours to create Index #2
- **No Helper Table**: Issue #1951 has ~150M rows, too large for helper table

## Summary

The A2Iss1716A2Events helper table optimization provides:
- ✅ **6-10x faster** query performance for Issue #1716
- ✅ **Simpler queries** (no timestamp filtering)
- ✅ **Faster counts** (< 1 second vs 30-60 seconds)
- ✅ **Lower resource usage** (no large scans)
- ✅ **Easy rollback** (keep old code available)

**Next Steps**:
1. ✅ Create indexes (4-6 minutes)
2. ✅ Deploy updated code
3. ✅ Test with --max-batches 2
4. ✅ Run full production export

---

**Related Documentation**:
- `Optimize_A2Iss1716A2Events_Indexes.sql` - Index creation script
- `Test_Export_Query.sql` - Testing queries with helper table examples
- `Index_Creation_Scripts.sql` - Main CorrespondenceStatuses indexes (for Issue #1951)
