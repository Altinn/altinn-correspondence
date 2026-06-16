# A2Iss1951A2Events Duplicate Analysis Results

## Sample Analysis Results (1% sample)

| Metric | Value | Interpretation |
|--------|-------|----------------|
| **Sample size** | 1,922,683 rows | 1% of 195.5M |
| **Unique in sample** | 1,922,251 | Very few duplicates |
| **% Unique** | 99.98% | Excellent data quality! |
| **Estimated duplicates** | ~43,926 rows | Only 0.02% of total |
| **Estimated after cleanup** | ~195,455,353 rows | Minimal impact |

---

## 🎯 Recommendation: PROCEED with Simple Cleanup

### Why This is Good News

✅ **Very few duplicates** (0.02% of data)  
✅ **Cleanup will be fast** (<5 minutes)  
✅ **Minimal storage impact** (saves ~40MB)  
✅ **Low risk** operation  

### Decision: Skip Temp Index

Since you only have ~44k duplicates:
- Creating temp index would take 5-10 minutes
- Cleanup without index will take <5 minutes  
- **Not worth the index creation time**

---

## 📋 Recommended Action Plan (Total: 8-12 minutes)

### Step 1: Create Backup (3-5 minutes)

```sql
-- Create backup table
DROP TABLE IF EXISTS correspondence."A2Iss1951A2Events_backup";

CREATE TABLE correspondence."A2Iss1951A2Events_backup" AS
SELECT * FROM correspondence."A2Iss1951A2Events";

-- Verify backup
SELECT 
    'Backup created' AS status,
    COUNT(*) AS rows_backed_up
FROM correspondence."A2Iss1951A2Events_backup";
```

**Expected**: 195,499,279 rows backed up

### Step 2: Remove Duplicates (2-5 minutes)

```sql
BEGIN;

-- Delete duplicates (keep Source = 1 over Source = 0)
WITH ranked_records AS (
    SELECT 
        ctid,
        ROW_NUMBER() OVER (
            PARTITION BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
            ORDER BY "Source" DESC  -- Prefer Source = 1
        ) AS rn
    FROM correspondence."A2Iss1951A2Events"
)
DELETE FROM correspondence."A2Iss1951A2Events"
WHERE ctid IN (
    SELECT ctid 
    FROM ranked_records 
    WHERE rn > 1
);

-- Check how many rows were deleted
SELECT 
    'Rows deleted' AS action,
    195499279 - COUNT(*) AS deleted_count,
    COUNT(*) AS remaining_rows
FROM correspondence."A2Iss1951A2Events";

-- Verify no duplicates remain (should return 0)
SELECT 
    'Remaining duplicates (should be 0)' AS check_name,
    COUNT(*) AS duplicate_count
FROM (
    SELECT 
        "CorrespondenceId",
        "Timestamp",
        "PartyUuid",
        "Status"
    FROM correspondence."A2Iss1951A2Events"
    GROUP BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
    HAVING COUNT(*) > 1
) remaining_dupes;

-- If results look good (deleted ~44k rows, 0 duplicates remain):
COMMIT;

-- If something looks wrong:
-- ROLLBACK;
```

**Expected Results**:
- Deleted count: ~44,000 rows
- Remaining rows: ~195,455,000
- Remaining duplicates: 0

### Step 3: Create Production Indexes (10-15 minutes)

```sql
-- Index 1: For cursor pagination
CREATE INDEX CONCURRENTLY "IX_A2Iss1951A2Events_CorrId_Status_Party"
ON correspondence."A2Iss1951A2Events" (
    "CorrespondenceId",
    "Status"
)
INCLUDE ("PartyUuid", "Timestamp");

-- Index 2: For Status filtering
CREATE INDEX CONCURRENTLY "IX_A2Iss1951A2Events_Status_Timestamp"
ON correspondence."A2Iss1951A2Events" (
    "Status",
    "Timestamp"
)
INCLUDE ("CorrespondenceId", "PartyUuid");

-- Update statistics
ANALYZE correspondence."A2Iss1951A2Events";

-- Verify indexes created
SELECT 
    schemaname,
    relname AS tablename,
    indexrelname AS indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size
FROM pg_stat_user_indexes
WHERE schemaname = 'correspondence'
  AND relname = 'A2Iss1951A2Events'
ORDER BY indexrelname;
```

**Expected**: 
- Index 1 size: ~10-15 GB
- Index 2 size: ~10-15 GB
- Total index size: ~20-30 GB

### Step 4: Test Query Performance (30 seconds)

```sql
-- Test Status 4 query
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    4 AS Status
FROM correspondence."A2Iss1951A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
    AND a2Events."Timestamp" = stats."StatusChanged"
INNER JOIN correspondence."Correspondences" corr
    ON a2Events."CorrespondenceId" = corr."Id"
    AND corr."Altinn2CorrespondenceId" IS NOT NULL
    AND corr."IsMigrating" = FALSE
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
WHERE a2Events."Status" = 4
LIMIT 5000;
```

**Expected**:
- Execution time: < 100ms
- Uses: Index Scan on IX_A2Iss1951A2Events_CorrId_Status_Party
- No sequential scans

---

## 📊 Summary

| Phase | Time | Status |
|-------|------|--------|
| Analysis (sample) | 30 sec | ✅ DONE |
| Backup creation | 3-5 min | ⏭️ NEXT |
| Duplicate removal | 2-5 min | ⏭️ NEXT |
| Index creation | 10-15 min | ⏭️ NEXT |
| Test query | 30 sec | ⏭️ NEXT |
| **TOTAL** | **16-26 min** | **Ready to proceed** |

---

## ✅ Green Light to Proceed

With only 0.02% duplicates:
- ✅ Low risk operation
- ✅ Fast cleanup (<5 min)
- ✅ Minimal impact on storage
- ✅ Simple transacted delete approach
- ✅ Backup as safety net

**Start with Step 1 (Backup) now!**

---

## 🔍 After Cleanup - Export Performance

Once indexes are created, you can expect:

| Issue | Rows | Batch Size | Time/Batch | Total Export Time |
|-------|------|-----------|------------|-------------------|
| **#1951** | ~195M | 5,000 | 30-50ms | 25-40 minutes |
| **#1716** | ~10M | 5,000 | 30-50ms | 2-3 minutes |
| **Combined** | ~205M | 5,000 | 30-50ms | 27-43 minutes |

**Compared to old CTE approach**: 200-300x faster! 🚀

---

## 🧹 Optional: Cleanup Backup After Verification

Once you've verified the export works correctly for a few days:

```sql
-- Drop backup table (frees ~200 GB)
DROP TABLE correspondence."A2Iss1951A2Events_backup";
```

**Wait at least 1 week** before dropping the backup to ensure everything works correctly.
