# Quick Reference
## Dialog Activity Export Index Optimization

**Purpose:** Export optimization for Issues #1951 and #1716  
**Date:** May 26, 2026

---

## TL;DR

**What:** Create 2 critical indexes for 150M+ row export  
**Why:** Current queries take 1+ hours due to full table scans  
**How:** All indexes created with `CONCURRENTLY` (zero downtime)  
**Impact:** 100-500x faster queries, ~13.5 GB disk space  

---

## Deployment Plan

### Phase 1: Critical Indexes (90 minutes) ✅ REQUIRED
```sql
-- Issue #1716: Synced events (15 min, 1.5 GB)
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced"
ON correspondence."CorrespondenceStatuses" ("Status", "SyncedFromAltinn2")
INCLUDE ("CorrespondenceId", "PartyUuid")
WHERE "SyncedFromAltinn2" IS NOT NULL;

-- Issue #1951: Migrated events (60 min, 12 GB)
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CorrespondenceStatuses_Status_StatusChanged_Migrated"
ON correspondence."CorrespondenceStatuses" ("Status", "StatusChanged")
INCLUDE ("CorrespondenceId", "PartyUuid")
WHERE "SyncedFromAltinn2" IS NULL;
```

### Phase 2: Optional Index (30 minutes) ⚠️ CONDITIONAL
```sql
-- Only create if export testing shows benefit
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Correspondences_Id_Created_MigrationFilter"
ON correspondence."Correspondences" ("Id", "Created")
INCLUDE ("Recipient")
WHERE "Altinn2CorrespondenceId" IS NOT NULL AND "IsMigrating" = FALSE;
```

**Full SQL script:** See [Index_Creation_Scripts.sql](Index_Creation_Scripts.sql)

---

## Performance Metrics

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| **Query Time** | 33+ minutes | < 5 seconds | **400x faster** |
| **Export Time** | 4-6 hours | 30-60 minutes | **5-10x faster** |
| **Disk I/O** | 30.7M buffers | < 100K buffers | **99% reduction** |
| **Disk Space** | - | -13.5 GB | 2 critical indexes |

---

## Key Points

✅ **Export indexes:** Required for Issues #1951 & #1716  
✅ **Runtime API:** No changes needed (already optimized)  
✅ **Zero downtime:** All operations use CONCURRENTLY  
✅ **Fully reversible:** Can rollback with `DROP INDEX CONCURRENTLY`  
✅ **Write overhead:** 5-10% slower inserts on CorrespondenceStatuses (acceptable)

---

## Monitoring During Creation

```sql
-- Check progress:
SELECT 
    phase, 
    ROUND(100.0 * tuples_done / tuples_total, 2) AS pct_complete
FROM pg_stat_progress_create_index;

-- Check if index is being used:
SELECT 
    schemaname, tablename, indexname, idx_scan, idx_tup_read
FROM pg_stat_user_indexes
WHERE indexname LIKE ''IX_CorrespondenceStatuses%''
ORDER BY idx_scan DESC;
```

---

## FAQ

**Q: Will this impact production?**  
A: No. All indexes use `CONCURRENTLY` which doesn''t lock tables.

**Q: How long will index creation take?**  
A: Phase 1: ~90 minutes. Index #1 takes ~15 min, Index #2 takes ~60 min.

**Q: Can we rollback if there''s an issue?**  
A: Yes. Use `DROP INDEX CONCURRENTLY` to remove them safely.

**Q: Why won''t this improve runtime API performance?**  
A: Runtime queries start from `Correspondences` table (already has good indexes). Export queries start from `CorrespondenceStatuses` table (needs new indexes). Different starting points = different optimization needs.

**Q: What if index creation is interrupted?**  
A: Index will be marked INVALID. Drop it with `DROP INDEX` and recreate.

**Q: Why partial indexes?**  
A: Smaller size, faster scans, targeted for specific export queries. Index #1 covers only 7-9M rows (synced events), Index #2 covers 150M rows (migrated events).

---

## Rollback Commands

If you need to remove the indexes:

```sql
DROP INDEX CONCURRENTLY IF EXISTS correspondence."IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced";
DROP INDEX CONCURRENTLY IF EXISTS correspondence."IX_CorrespondenceStatuses_Status_StatusChanged_Migrated";
DROP INDEX CONCURRENTLY IF EXISTS correspondence."IX_Correspondences_Id_Created_MigrationFilter";
```

---

## Files Reference

| File | Purpose |
|------|---------|
| **Index_Creation_Scripts.sql** | Production-ready SQL with monitoring queries |
| **Technical_Documentation.md** | Business case, query analysis, detailed explanations |
| **Query_Documentation.md** | Query logic and filter explanations |

---

**Status:** ✅ Ready for Deployment  
**Risk Level:** 🟢 Low (zero downtime, fully reversible)  
**Approval:** Ready for DBA review
