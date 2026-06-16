# A2Iss1951A2Events Analysis Results

## Table Size: 195,499,279 rows

This is larger than initially estimated (~150M). Adjusting recommendations accordingly.

---

## Next Steps

### 1. Check for Duplicates

Run the next queries from `Clean_A2Iss1951A2Events_Duplicates.sql`:

```sql
-- Check how many unique combinations exist (ignoring Source)
SELECT 
    'Unique combinations (ignoring Source)' AS description,
    COUNT(DISTINCT ("CorrespondenceId", "Timestamp", "PartyUuid", "Status")) AS count
FROM correspondence."A2Iss1951A2Events";
```

**Expected scenarios**:
- **If count ≈ 195.5M**: Very few duplicates, cleanup will be quick
- **If count < 195.5M**: Significant duplicates exist, more rows will be deleted

### 2. Analyze Duplicate Distribution

```sql
-- Find duplicate groups and their Source distribution
SELECT 
    COUNT(*) AS duplicate_groups,
    SUM(dup_count) AS total_duplicate_rows,
    SUM(CASE WHEN has_source_0 AND has_source_1 THEN dup_count ELSE 0 END) AS rows_with_both_sources,
    SUM(CASE WHEN has_source_0 AND NOT has_source_1 THEN dup_count ELSE 0 END) AS rows_with_only_source_0,
    SUM(CASE WHEN has_source_1 AND NOT has_source_0 THEN dup_count ELSE 0 END) AS rows_with_only_source_1
FROM (
    SELECT 
        "CorrespondenceId",
        "Timestamp",
        "PartyUuid",
        "Status",
        COUNT(*) AS dup_count,
        BOOL_OR("Source" = 0) AS has_source_0,
        BOOL_OR("Source" = 1) AS has_source_1
    FROM correspondence."A2Iss1951A2Events"
    GROUP BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
    HAVING COUNT(*) > 1
) duplicates;
```

**This tells you**:
- `duplicate_groups`: How many unique combinations have duplicates
- `total_duplicate_rows`: Total extra rows that will be deleted
- Distribution by Source value

---

## Adjusted Performance Expectations

### Index Creation Time (195.5M rows)

| Index | Estimated Time |
|-------|----------------|
| IX_A2Iss1951A2Events_CorrId_Status_Party | 6-10 minutes |
| IX_A2Iss1951A2Events_Status_Timestamp | 6-10 minutes |
| **Total** | **12-20 minutes** |

### Duplicate Cleanup Time

Depends on duplicate count:
- **Few duplicates (< 1M)**: 30 seconds - 2 minutes
- **Moderate (1-10M)**: 2-10 minutes  
- **Many (10-50M)**: 10-30 minutes
- **Heavy (> 50M)**: 30+ minutes

**Recommendation**: Use the transacted DELETE approach (Step 4) since you have a backup plan.

### Export Performance (After Optimization)

**Full Export Estimate** (195.5M rows):

| Batch Size | Batches | Time/Batch | Total Time | Notes |
|-----------|---------|------------|------------|-------|
| 5,000 | 39,100 | 30-50ms | 20-33 min | **Recommended** |
| 10,000 | 19,550 | 50-80ms | 16-26 min | May hit network throttle |

**Compared to old CTE approach**: 
- Old: ~50-100 hours
- New: ~20-30 minutes  
- **Improvement: 150-300x faster**

---

## Storage Requirements

### Current Table Size (Estimated)

Assuming ~1KB per row average:
- **Table data**: ~195 GB
- **Indexes**: ~40-60 GB (2 covering indexes)
- **Total**: ~240-260 GB

### After Duplicate Cleanup

If you have significant duplicates (e.g., 20% duplicate rate):
- **Rows after cleanup**: ~156M
- **Space saved**: ~40 GB
- **Final size**: ~200-220 GB

Run this to check actual current size:

```sql
SELECT 
    schemaname,
    relname AS tablename,
    pg_size_pretty(pg_total_relation_size(relid)) AS total_size,
    pg_size_pretty(pg_relation_size(relid)) AS table_size,
    pg_size_pretty(pg_indexes_size(relid)) AS indexes_size
FROM pg_stat_user_tables
WHERE schemaname = 'correspondence'
  AND relname = 'A2Iss1951A2Events';
```

---

## Recommendation: Proceed with Caution

Given the large size (195M rows), I recommend:

### Phase 1: Analysis (Safe - No Changes)
✅ Run all analysis queries (Steps 1-2)
- Understand duplicate situation
- Preview what will be kept/deleted
- Check Source distribution

### Phase 2: Backup (Essential)
✅ Create backup table
```sql
CREATE TABLE correspondence."A2Iss1951A2Events_backup" AS
SELECT * FROM correspondence."A2Iss1951A2Events";
```

**Warning**: This will take 10-20 minutes and temporarily double storage usage.

### Phase 3: Cleanup (Transacted)
✅ Run DELETE in transaction
```sql
BEGIN;
-- DELETE duplicates...
-- Verify results
-- COMMIT or ROLLBACK
```

**If comfortable with results**: `COMMIT;`  
**If uncertain**: `ROLLBACK;` and investigate further

### Phase 4: Indexes
✅ Create indexes (12-20 minutes)
✅ Run ANALYZE

### Phase 5: Test Query
✅ Test helper table query performance
- Should be 17-50ms per batch
- Compare with old CTE approach

---

## Quick Decision Matrix

**Scenario A: Few Duplicates (< 1% of rows)**
- ✅ Cleanup will be fast (< 5 min)
- ✅ Proceed with full cleanup
- ✅ Create indexes
- ✅ Deploy to production

**Scenario B: Moderate Duplicates (1-10% of rows)**
- ⚠️ Cleanup may take 10-30 min
- ✅ Review preview carefully
- ✅ Use transaction with COMMIT after verification
- ✅ Monitor during cleanup

**Scenario C: Heavy Duplicates (> 10% of rows)**
- ⚠️ Cleanup may take 30+ min
- ⚠️ Consider using "create clean table and swap" approach (Step 5)
- ⚠️ Schedule during maintenance window
- ✅ Keep backup for at least 1 week

---

## What to Run Next

Copy and paste this into your PostgreSQL client:

```sql
-- Step 1: Check unique combinations
SELECT 
    'Unique combinations (ignoring Source)' AS description,
    COUNT(DISTINCT ("CorrespondenceId", "Timestamp", "PartyUuid", "Status")) AS count
FROM correspondence."A2Iss1951A2Events";

-- Step 2: Analyze duplicate distribution (this may take 2-5 minutes)
SELECT 
    COUNT(*) AS duplicate_groups,
    SUM(dup_count) AS total_duplicate_rows,
    SUM(CASE WHEN has_source_0 AND has_source_1 THEN dup_count ELSE 0 END) AS rows_with_both_sources,
    SUM(CASE WHEN has_source_0 AND NOT has_source_1 THEN dup_count ELSE 0 END) AS rows_with_only_source_0,
    SUM(CASE WHEN has_source_1 AND NOT has_source_0 THEN dup_count ELSE 0 END) AS rows_with_only_source_1
FROM (
    SELECT 
        "CorrespondenceId",
        "Timestamp",
        "PartyUuid",
        "Status",
        COUNT(*) AS dup_count,
        BOOL_OR("Source" = 0) AS has_source_0,
        BOOL_OR("Source" = 1) AS has_source_1
    FROM correspondence."A2Iss1951A2Events"
    GROUP BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
    HAVING COUNT(*) > 1
) duplicates;

-- Step 3: Check Source distribution
SELECT 
    "Source",
    COUNT(*) AS count,
    ROUND(100.0 * COUNT(*) / SUM(COUNT(*)) OVER (), 2) AS percentage
FROM correspondence."A2Iss1951A2Events"
GROUP BY "Source"
ORDER BY "Source";
```

**Reply with the results** and I'll provide specific recommendations for your situation!
