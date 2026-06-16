# Issue #1951 vs #1716: Optimization Comparison

## Overview

Both issues require Dialog Activity exports from Altinn2-migrated correspondences, but differ significantly in scale and data source:

| Metric | Issue #1716 | Issue #1951 |
|--------|-------------|-------------|
| **Definition** | Synced from Altinn2 | Migrated (NOT synced) |
| **Filter** | `SyncedFromAltinn2 IS NOT NULL` | `SyncedFromAltinn2 IS NULL` |
| **Estimated Rows** | ~10 million | ~150 million |
| **Scale Factor** | 1x (baseline) | **15x larger** |
| **Current Status** | ✅ Optimized (helper table) | ⚠️ Needs optimization |

---

## Current Implementation Status

### ✅ Issue #1716: OPTIMIZED

**Helper Table:** `correspondence.A2Iss1716A2Events`
- **Rows:** ~10M pre-filtered records
- **Indexes:** 2 optimized indexes (cursor + covering)
- **Query Time:** 91-100ms per batch
- **Export Time:** ~18 hours (155 rows/sec @ batch size 2500)
- **Status:** Production-ready, tested, documented

**Optimization Benefits:**
- Query scope reduced: 1.94B → 10M rows (99.5% reduction)
- Eliminates Correspondences JOIN (5 tables → 4 tables)
- Index-Only Scans (no heap access)
- Proven reliable in production

---

### ⚠️ Issue #1951: NEEDS OPTIMIZATION

**Current Approach:** CTE + 5-table JOINs on CorrespondenceStatuses
- **Rows:** Queries 1.94B row table on every batch
- **Indexes:** General-purpose indexes (not optimized for this query)
- **Query Time:** Estimated 500-1500ms per batch (5-15x slower)
- **Export Time:** Estimated 100-200 hours (4-8 days)
- **Status:** ⚠️ Unoptimized, will be extremely slow

**Current Bottlenecks:**
1. No helper table (queries full CorrespondenceStatuses table)
2. 5-table JOINs executed on every batch
3. Cursor pagination on 1.94B rows
4. No specialized indexes

---

## Recommended Solution: Mirror Issue #1716 Approach

### Create Helper Table: `A2Iss1951MigratedEvents`

**Same strategy as Issue #1716, just different filter:**

```sql
-- Issue #1716 filter: SyncedFromAltinn2 IS NOT NULL
-- Issue #1951 filter: SyncedFromAltinn2 IS NULL (opposite!)

CREATE TABLE correspondence."A2Iss1951MigratedEvents" AS
SELECT DISTINCT
    stats."CorrespondenceId",
    stats."PartyUuid",
    stats."Status",
    stats."StatusChanged"
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
    AND stats."SyncedFromAltinn2" IS NULL  -- ← Issue #1951 filter
-- ... (same JOINs as Issue #1716)
WHERE stats."Status" IN (4, 6);
```

---

## Performance Projection

### Without Helper Table (Current)
- **Query Time:** 500-1500ms per batch
- **Batches:** ~60,000 batches @ 2500 batch size
- **Total Time:** 100-200 hours (4-8 days)
- **Bottleneck:** Complex 5-table JOINs on 1.94B row table

### With Helper Table (Optimized)
- **Query Time:** 100-200ms per batch (same as Issue #1716)
- **Batches:** ~60,000 batches @ 2500 batch size
- **Total Time:** ~33 hours (same throughput as Issue #1716)
- **Improvement:** **3-6x faster**

**Note:** Network transfer is still the bottleneck (~8ms/row), but helper table makes query time negligible.

---

## Implementation Comparison

### Issue #1716 Implementation (Reference)

```csharp
if (issueNumber == 1716)
{
    // Use A2Iss1716A2Events helper table
    query = $@"
        SELECT DISTINCT ...
        FROM correspondence.""A2Iss1716A2Events"" a2Events
        INNER JOIN correspondence.""CorrespondenceStatuses"" stats 
            ON a2Events.""CorrespondenceId"" = stats.""CorrespondenceId"" ...
        -- 4 tables total (no Correspondences JOIN)
        WHERE a2Events.""Status"" = {statusValue}
          AND a2Events.""CorrespondenceId"" > @lastId
        ORDER BY a2Events.""CorrespondenceId""
        LIMIT @fetchLimit";
}
```

### Issue #1951 Implementation (Proposed)

```csharp
if (issueNumber == 1951)
{
    // Use A2Iss1951MigratedEvents helper table
    query = $@"
        SELECT DISTINCT ...
        FROM correspondence.""A2Iss1951MigratedEvents"" migEvents
        INNER JOIN correspondence.""CorrespondenceStatuses"" stats 
            ON migEvents.""CorrespondenceId"" = stats.""CorrespondenceId"" ...
        -- 4 tables total (no Correspondences JOIN)
        WHERE migEvents.""Status"" = {statusValue}
          AND migEvents.""CorrespondenceId"" > @lastId
        ORDER BY migEvents.""CorrespondenceId""
        LIMIT @fetchLimit";
}
```

**Difference:** Just the table name and alias! The query structure is identical.

---

## Storage Requirements

| Component | Issue #1716 | Issue #1951 | Notes |
|-----------|-------------|-------------|-------|
| **Helper Table** | ~1.5 GB | ~20-30 GB | 15x scale difference |
| **Primary Index** | ~500 MB | ~7-10 GB | CorrespondenceId + Status |
| **Covering Index** | ~1 GB | ~10-15 GB | Includes PartyUuid, StatusChanged |
| **Total Storage** | ~3 GB | ~30-45 GB | Verify available space |

**Prerequisite:** Verify at least **50 GB free space** in production database before creating helper table.

---

## Timeline Comparison

### Issue #1716 (Completed)
| Phase | Duration | Status |
|-------|----------|--------|
| Helper table creation | 5-10 min | ✅ Done |
| Index creation | 10-15 min | ✅ Done |
| Code changes | 30 min | ✅ Done |
| Testing | 1-2 hours | ✅ Done |
| Production export | 18 hours | ✅ Done |
| **TOTAL** | ~20 hours | ✅ Complete |

### Issue #1951 (Estimated)
| Phase | Duration | Status |
|-------|----------|--------|
| Helper table creation | 30-60 min | ⏭️ Pending |
| Index creation | 20-40 min | ⏭️ Pending |
| Code changes | 30 min | ⏭️ Pending |
| Testing | 1-2 hours | ⏭️ Pending |
| Production export | ~33 hours | ⏭️ Pending |
| **TOTAL** | ~35-36 hours | ⏭️ Not started |

**Key Difference:** Helper table creation takes longer (15x more rows), but export time is similar due to network bottleneck.

---

## Decision Matrix

### Option 1: Create Helper Table (RECOMMENDED)
**Pros:**
- ✅ Proven approach (Issue #1716 success)
- ✅ 3-6x faster export
- ✅ Reduces query complexity
- ✅ Index-Only Scans
- ✅ Reusable for future exports

**Cons:**
- ⚠️ Requires 50 GB storage
- ⚠️ One-time setup: ~60-100 minutes
- ⚠️ Needs production database access

**Best for:** Production export where performance matters

---

### Option 2: Use Current CTE Approach
**Pros:**
- ✅ No infrastructure changes
- ✅ No additional storage
- ✅ Works immediately

**Cons:**
- ❌ 4-8 days export time
- ❌ 5-15x slower queries
- ❌ Queries 1.94B row table on every batch
- ❌ Not reusable for future exports

**Best for:** One-time export where time doesn't matter (NOT recommended)

---

## Recommended Action Plan

### Step 1: Create Helper Table (SQL)
**File:** `Create_A2Iss1951MigratedEvents_Helper_Table.sql`
1. Run pre-flight checks (verify disk space)
2. Create helper table (~30-60 min)
3. Create indexes (~20-40 min)
4. Verify performance (EXPLAIN ANALYZE)

**Duration:** ~60-100 minutes
**Risk:** Low (CREATE TABLE AS SELECT, no schema changes)

---

### Step 2: Update Code (C#)
**File:** `DialogActivityExportService.cs`
1. Add `issueNumber == 1951` branch in `FetchStatusRecordsAsync`
2. Copy Issue #1716 query structure, change table name to `A2Iss1951MigratedEvents`
3. Update `GetTotalCountAsync` to query helper table
4. Add logging for helper table usage

**Duration:** ~30 minutes
**Risk:** Low (minimal changes, follows existing pattern)

---

### Step 3: Test Export
**Command:** `.\test-export.ps1 -Issue 1951 -MaxBatches 10`
1. Export first 50K rows (10 batches × 5000 rows)
2. Verify query timing (~100-200ms per batch)
3. Check EXPLAIN ANALYZE (should show Index-Only Scan)
4. Validate CSV output (correct columns, no duplicates)

**Duration:** ~1-2 hours
**Risk:** Low (test mode, limited scope)

---

### Step 4: Production Export
**Command:** `.\run-export.ps1 -Issue 1951 -BatchSize 2500`
1. Run full export (~33 hours)
2. Monitor progress (target: 100-150 rows/sec)
3. Checkpoint automatically saved every batch
4. Resume if interrupted

**Duration:** ~33 hours
**Risk:** Low (checkpoint resume supported)

---

## Success Criteria

- ✅ Helper table created with ~150M rows
- ✅ Indexes created (Index-Only Scans enabled)
- ✅ Query time < 200ms per batch
- ✅ Export throughput: 100-150 rows/sec
- ✅ Total export time: < 48 hours
- ✅ CSV validates correctly

---

## Key Takeaway

**Issue #1951 is just Issue #1716 at 15x scale.**

The optimization strategy is **identical**:
1. Create helper table with pre-filtered records
2. Add optimized indexes for cursor pagination
3. Update code to query helper table instead of full CorrespondenceStatuses
4. Export runs 3-6x faster

**Estimated effort:** ~2-3 hours (setup + testing)
**Estimated export time:** ~33 hours (vs 100-200 hours without optimization)
**Return on investment:** Save 67-167 hours of export time

---

## Next Steps

1. **Review** this optimization strategy with team
2. **Verify** 50 GB disk space available in production
3. **Schedule** helper table creation (or run CONCURRENTLY to avoid blocking)
4. **Execute** `Create_A2Iss1951MigratedEvents_Helper_Table.sql`
5. **Implement** code changes (mirror Issue #1716 approach)
6. **Test** with `--max-batches 10`
7. **Run** production export

---

## Related Documentation

- **Optimization Strategy:** `ISSUE_1951_OPTIMIZATION_STRATEGY.md`
- **Helper Table SQL:** `Create_A2Iss1951MigratedEvents_Helper_Table.sql`
- **Issue #1716 Reference:** `A2Iss1716A2Events_Helper_Table_Migration.md`
- **Network Analysis:** `NETWORK_BOTTLENECK_ANALYSIS.md`
- **Cursor Implementation:** `SEPARATE_CURSORS_IMPLEMENTATION.md`
