# Database Documentation
## Dialog Activity Export - Performance Optimization

This folder contains documentation for optimizing PostgreSQL queries and C# code used to export dialog activity data for Issues #1951 and #1716.

## Overview

**Challenge:** Export ~150-160 million dialog activity records from a 1.94 billion row table.  
**Solution:** Optimized indexes + separate query execution (no UNION ALL) + in-memory sorting.  
**Result:** 840x performance improvement (40+ min → ~3 seconds per batch).

**Scope:** Export operations only. Runtime API performance is already well-optimized.

---

## 📁 Core Documents

### Production Ready

1. **[Index_Creation_Scripts.sql](Index_Creation_Scripts.sql)** ⭐ **START HERE**
   - Production-ready index creation scripts
   - Includes monitoring queries and rollback plan
   - Estimated time: 8-12 hours for Index #2
   - Disk space: ~27 GB total

2. **[Test_Export_Query.sql](Test_Export_Query.sql)** 🧪
   - Optimized test queries (separate Status 4 and Status 6)
   - No UNION ALL - queries run separately
   - Use in DBeaver/pgAdmin for validation

3. **[Performance_Optimization_Summary.sql](Performance_Optimization_Summary.sql)** 📊
   - Complete optimization journey documentation
   - Before/after metrics (840x improvement)
   - Why certain approaches were rejected (UNION ALL, Created filter)

### Production Tracking

4. **[Index_Creation_Production_Summary.md](Index_Creation_Production_Summary.md)** ⏱️
   - Actual production timing (Index #1: 2h 15m, Index #2: 3h 9m)
   - Lessons learned and optimization insights
   - Combined results: 5h 24m total

### Testing & Verification

5. **[Testing_Guide.md](Testing_Guide.md)** 🧪 **Testing Made Easy**
   - Quick test with limited results (1000-10000 rows)
   - Verify output format before full export
   - Multiple testing methods (batch size, cutoff date, SQL queries)
   - Azure authentication options (CLI, Visual Studio, VS Code, Managed Identity)
   - Troubleshooting common issues

6. **[Quick_Test_Reference.md](Quick_Test_Reference.md)** ⚡ **Quick Reference Card**
   - One-page cheat sheet for testing
   - Common commands and expected output
   - Performance expectations and troubleshooting

7. **[Azure_Identity_Migration_Summary.md](Azure_Identity_Migration_Summary.md)** 🔐 **Azure.Identity SDK**
   - Migration from Azure CLI process to Azure.Identity SDK
   - Supported authentication methods (10 options)
   - Benefits and improved developer experience

### Configuration & Utilities

8. **[Configure_PostgreSQL_For_Index_Creation.sql](Configure_PostgreSQL_For_Index_Creation.sql)**
   - PostgreSQL optimization for index creation
   - Set maintenance_work_mem, parallel workers, etc.

9. **[Check_Disk_Space_And_Table_Stats.sql](Check_Disk_Space_And_Table_Stats.sql)**
   - Verify disk space and table health before index creation

### A2Parties Setup (Separate)

10. **[Fix_A2Parties_Recipient_Filter_Schema.sql](Fix_A2Parties_Recipient_Filter_Schema.sql)**
   - Schema changes (Part 1: transactional)

11. **[Fix_A2Parties_Recipient_Filter_Index.sql](Fix_A2Parties_Recipient_Filter_Index.sql)**
   - Index creation (Part 2: non-transactional)

---

## 🚀 Quick Start

### Step 1: Create Index #1 (✅ Completed)
```sql
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced"
ON correspondence."CorrespondenceStatuses" ("Status", "SyncedFromAltinn2")
INCLUDE ("CorrespondenceId", "PartyUuid")
WHERE "SyncedFromAltinn2" IS NOT NULL;
```
**Time:** 2h 15m (actual)  
**Size:** ~3 GB

### Step 2: Create Index #2 (✅ Completed)
```sql
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CorrespondenceStatuses_Status_StatusChanged_Migrated"
ON correspondence."CorrespondenceStatuses" ("Status", "StatusChanged")
INCLUDE ("CorrespondenceId", "PartyUuid")
WHERE "SyncedFromAltinn2" IS NULL;
```
**Time:** 3h 9m (actual - 2.5x faster than estimated!)  
**Size:** ~24 GB

**Total:** 5h 24m for both indexes, ~27 GB disk space

---

## 📊 Key Performance Results

| Optimization | Impact |
|--------------|--------|
| **Removed UNION ALL + ORDER BY** | Separate queries 840x faster (40+ min → 3s) |
| **Removed corr.Created filter** | Eliminated post-index filtering bottleneck (12+ min → 3s) |
| **In-memory sorting in C#** | Faster than database sort on millions of rows |
| **Separate Status 4 & 6 queries** | Allows parallel execution and early LIMIT termination |

### Code Changes (DialogActivityExportService.cs)
- ✅ Runs Status 4 and Status 6 as **separate queries**
- ✅ Merges results in **C# using LINQ**
- ✅ Sorts in-memory (faster than ORDER BY after UNION ALL)
- ✅ Uses cursor pagination: `(CorrespondenceId, Status) > (lastId, lastStatus)`

---

## ⚠️ Important Notes

1. **No corr.Created filter** - Causes 3s → 12+ min degradation
2. **No ORDER BY after UNION ALL** - Forces full scan before LIMIT
3. **Separate queries are faster** - Database UNION ALL adds overhead
4. **Index #2 is the priority** - 150M records vs 15M for Index #1
5. **Run during low traffic** - Index #2 takes 8-12 hours

---

## 🔍 Verification

After creating indexes, verify with:

```sql
-- Check index is being used
EXPLAIN (ANALYZE, BUFFERS) 
SELECT COUNT(*)
FROM correspondence."CorrespondenceStatuses" stats
WHERE stats."Status" = 4
  AND stats."SyncedFromAltinn2" IS NULL
  AND stats."StatusChanged" BETWEEN '2019-03-23' AND '2026-05-19';
```

**Expected:** `Index Scan using IX_CorrespondenceStatuses_Status_StatusChanged_Migrated`

---

## 📈 Metrics

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Query Time (Issue #1951) | 40+ minutes | ~3 seconds | **800x faster** |
| Index Scans | Sequential (1.94B rows) | Partial Index (~300M rows) | **99% reduction** |
| Disk I/O | 30.7M buffer reads | <100K buffer reads | **99% reduction** |
| Export Time (estimated) | 4-6 hours | 30-60 minutes | **5-10x faster** |

## Safety

- ✅ All indexes use `CONCURRENTLY` - zero downtime
- ✅ No table locks - production unaffected
- ✅ Fully reversible - can drop indexes if needed
- ✅ No impact on runtime API performance

## Issues Reference

- **Issue #1951:** ~150 million migrated events (`SyncedFromAltinn2 IS NULL`)
- **Issue #1716:** ~7-9 million synced events (`SyncedFromAltinn2 IS NOT NULL`)

## Related Files

```text
altinn-correspondence/
├── docs/database/
│   ├── README.md (this file)
│   ├── Quick_Reference.md
│   ├── Index_Creation_Scripts.sql
│   ├── Technical_Documentation.md
│   └── Query_Documentation.md
└── tools/Altinn.Correspondence.DialogActivityExporter/
    └── README.md (export tool usage)
```
