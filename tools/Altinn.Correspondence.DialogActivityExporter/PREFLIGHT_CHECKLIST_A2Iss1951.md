# Quick Pre-Flight Checklist: A2Iss1951MigratedEvents Helper Table

## ⏱️ Time Required: 5-10 minutes

Run these queries in order to determine if helper table creation is feasible:

---

## 🔍 Step 1: Quick Row Count (30 seconds)

```sql
SELECT 
    COUNT(*) AS total_rows,
    COUNT(*) FILTER (WHERE "Status" = 4) AS status_4_rows,
    COUNT(*) FILTER (WHERE "Status" = 6) AS status_6_rows
FROM correspondence."CorrespondenceStatuses"
WHERE "Status" IN (4, 6)
  AND "SyncedFromAltinn2" IS NULL
  AND "StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-12-31 23:59:59';
```

**Expected:** ~150M total rows
**If significantly different:** Verify filter criteria

---

## 📊 Step 2: Test Small Sample (10-30 seconds)

```sql
EXPLAIN (ANALYZE, BUFFERS, TIMING)
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
    AND stats."SyncedFromAltinn2" IS NULL
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
WHERE stats."Status" IN (4, 6)
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-12-31 23:59:59'
LIMIT 10000;
```

**Record:** 
- ✍️ Execution Time: _______ ms (look at bottom of output)
- ✍️ Rows returned: _______ (should be ~10,000)

---

## 🧮 Step 3: Calculate Estimate

```
Time for 10K rows: _______ ms (from Step 2)
Expected total rows: 150,000,000
Batches needed: 150,000,000 / 10,000 = 15,000

Estimated total time:
  = (time_for_10k × 15,000) / 60,000
  = (_______ × 15,000) / 60,000
  = _______ minutes
  = _______ hours
```

**Decision:**
- ✅ **< 60 minutes:** FAST - Proceed with confidence
- ⚠️ **60-120 minutes:** ACCEPTABLE - Proceed during off-peak hours
- ❌ **> 120 minutes:** TOO SLOW - Optimize source query first

---

## 💾 Step 4: Check Disk Space (5 seconds)

```sql
SELECT 
    pg_size_pretty(pg_database_size(current_database())) AS current_db_size;
```

**Verify:** At least **50 GB free space** available

---

## 🧪 Step 5: Test Table Creation (1-3 minutes)

```sql
-- Create test table with 100K rows
CREATE TABLE correspondence."A2Iss1951MigratedEvents_TEST" AS
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
    AND stats."SyncedFromAltinn2" IS NULL
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
WHERE stats."Status" IN (4, 6)
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-12-31 23:59:59'
LIMIT 100000;

-- Verify
SELECT COUNT(*) FROM correspondence."A2Iss1951MigratedEvents_TEST";

-- Clean up
DROP TABLE correspondence."A2Iss1951MigratedEvents_TEST";
```

**Expected:** Table created successfully with ~100K rows
**If fails:** Review error message, may need to optimize source query

---

## ✅ Decision Matrix

| Scenario | Estimated Time | Disk Space | Test Table | Decision |
|----------|---------------|------------|------------|----------|
| ✅ **PROCEED** | < 90 min | ≥ 50 GB | Success | Create helper table |
| ⚠️ **CAUTION** | 90-120 min | ≥ 50 GB | Success | Proceed off-peak |
| ❌ **OPTIMIZE** | > 120 min | Any | Any | Improve source query |
| ❌ **ABORT** | Any | < 50 GB | Any | Free up space first |

---

## 📋 Your Results

Fill this out as you run the queries:

```
✍️ Step 1: Total rows = _______________
✍️ Step 2: Execution time = _______ ms
✍️ Step 3: Estimated total time = _______ minutes (_______ hours)
✍️ Step 4: Available disk space = _______ GB
✍️ Step 5: Test table creation = SUCCESS / FAILED

✅ DECISION: PROCEED / CAUTION / OPTIMIZE / ABORT
```

---

## 🚀 If PROCEED or CAUTION:

1. Schedule creation during **off-peak hours** (if CAUTION)
2. Run: `Create_A2Iss1951MigratedEvents_Helper_Table.sql`
3. Monitor progress:
   ```sql
   SELECT COUNT(*) FROM correspondence."A2Iss1951MigratedEvents";
   ```
4. Expected completion: _______ hours (from Step 3)

---

## ⚠️ If OPTIMIZE:

1. Review `EXPLAIN ANALYZE` output from Step 2
2. Check for Sequential Scans on large tables
3. Consider adding indexes to CorrespondenceStatuses:
   ```sql
   CREATE INDEX CONCURRENTLY "IX_CorrespondenceStatuses_SyncedFromAltinn2_StatusChanged"
   ON correspondence."CorrespondenceStatuses" ("SyncedFromAltinn2", "StatusChanged", "Status")
   WHERE "SyncedFromAltinn2" IS NULL;
   ```
4. Run `VACUUM ANALYZE` on source tables
5. Re-run this checklist after optimizations

---

## 📚 Related Documentation

- **Detailed Analysis:** `Analyze_A2Iss1951_Helper_Table_Creation.sql`
- **Helper Table Creation:** `Create_A2Iss1951MigratedEvents_Helper_Table.sql`
- **Optimization Strategy:** `ISSUE_1951_OPTIMIZATION_STRATEGY.md`
- **Issue Comparison:** `ISSUE_1951_VS_1716_COMPARISON.md`
