# Issue #1951 Export Optimization Strategy

## Problem Statement

**Issue #1951** involves exporting ~**150 million Dialog Activity records** (15x larger than Issue #1716's 10M).

Current approach uses a **CTE + multiple JOINs** which will be extremely slow for this volume.

## Current Query Structure (Issue #1951)

```sql
WITH filtered AS (
    SELECT 
        stats."CorrespondenceId",
        stats."PartyUuid",
        stats."StatusChanged",
        stats."Status"
    FROM correspondence."CorrespondenceStatuses" stats
    WHERE stats."Status" = {statusValue}
      AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND @cutoffTimestamp
      AND stats."SyncedFromAltinn2" IS NULL
      AND stats."CorrespondenceId" > @lastId
    ORDER BY stats."CorrespondenceId"
    LIMIT @fetchLimit
)
SELECT ...
FROM filtered
INNER JOIN correspondence."Correspondences" corr 
    ON filtered."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
INNER JOIN correspondence."A2Parties" ap 
    ON filtered."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON filtered."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idc
    ON filtered."CorrespondenceId" = idc."CorrespondenceId" 
    AND idc."StatusAction" = '{statusActionValue}'
ORDER BY filtered."CorrespondenceId"
```

### Performance Bottlenecks

1. **5-table JOINs on every batch** (150M rows × batch count)
2. **Cursor pagination** on massive CorrespondenceStatuses table (1.94 billion rows)
3. **Complex filters** on non-indexed combinations
4. **No pre-filtering table** like Issue #1716's A2Iss1716A2Events helper table

## Optimization Options

### Option 1: Create Helper Table (RECOMMENDED)
**Similar to Issue #1716's A2Iss1716A2Events approach**

#### Step 1: Create Helper Table
```sql
-- Create pre-filtered table for Issue #1951 affected records
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
    AND stats."SyncedFromAltinn2" IS NULL
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
INNER JOIN correspondence."ExternalReferences" er
    ON stats."CorrespondenceId" = er."CorrespondenceId" 
    AND er."ReferenceType" = 3
WHERE stats."Status" IN (4, 6)
  AND stats."StatusChanged" BETWEEN '2019-03-23 00:00:00' AND '2026-12-31 23:59:59';
```

**Expected Row Count:** ~150 million (pre-filtered from 1.94 billion)

#### Step 2: Create Optimized Indexes
```sql
-- Primary index: CorrespondenceId + Status for cursor pagination
CREATE INDEX CONCURRENTLY "IX_A2Iss1951MigratedEvents_CorrespondenceId_Status"
ON correspondence."A2Iss1951MigratedEvents" ("CorrespondenceId", "Status");

-- Covering index: Includes PartyUuid for Index-Only Scans
CREATE INDEX CONCURRENTLY "IX_A2Iss1951MigratedEvents_CorrespondenceId_Status_Covering"
ON correspondence."A2Iss1951MigratedEvents" ("CorrespondenceId", "Status")
INCLUDE ("PartyUuid", "StatusChanged");

-- Update statistics
ANALYZE correspondence."A2Iss1951MigratedEvents";
```

#### Step 3: Simplified Export Query
```sql
SELECT DISTINCT
    er."ReferenceValue" AS DialogId,
    idc."Id" AS DialogActivityId,
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    @status AS Status,
    @activityType AS ActivityType
FROM correspondence."A2Iss1951MigratedEvents" migEvents
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON migEvents."CorrespondenceId" = stats."CorrespondenceId" 
    AND migEvents."Status" = stats."Status" 
    AND migEvents."PartyUuid" = stats."PartyUuid"
INNER JOIN correspondence."ExternalReferences" er
    ON migEvents."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idc
    ON migEvents."CorrespondenceId" = idc."CorrespondenceId"
    AND idc."StatusAction" = @statusAction
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
WHERE migEvents."Status" = @statusValue
  AND migEvents."CorrespondenceId" > @lastId
ORDER BY migEvents."CorrespondenceId"
LIMIT @fetchLimit;
```

**Benefits:**
- ✅ **Reduces JOINs:** 5 tables → 4 tables (removes Correspondences join)
- ✅ **Pre-filtered dataset:** 1.94B → 150M rows (92% reduction)
- ✅ **Optimized indexes:** Purpose-built for cursor pagination
- ✅ **Proven approach:** Same strategy as Issue #1716
- ✅ **Index-Only Scans:** Covering index includes all cursor fields

**Expected Performance:**
- **Query time:** 100-200ms per batch (similar to Issue #1716)
- **Network transfer:** Still bottleneck (~8ms/row)
- **Total export time:** ~33 hours @ 125 rows/sec (batch size 5000)

---

### Option 2: Optimize Existing Indexes (LESS EFFECTIVE)

**Without helper table, optimize CorrespondenceStatuses indexes:**

```sql
-- Add covering index for Issue #1951 query
CREATE INDEX CONCURRENTLY "IX_CorrespondenceStatuses_1951_Optimized"
ON correspondence."CorrespondenceStatuses" (
    "CorrespondenceId", 
    "Status"
)
INCLUDE ("PartyUuid", "StatusChanged")
WHERE "SyncedFromAltinn2" IS NULL
  AND "Status" IN (4, 6)
  AND "StatusChanged" >= '2019-03-23 00:00:00';
```

**Limitations:**
- ❌ Still queries 1.94B row table
- ❌ Still needs 5-table JOINs on every batch
- ❌ Partial index helps, but not as effective as pre-filtered table
- ⚠️ Index size: Potentially 10-20 GB (partial index on 150M rows)

**Expected Performance:**
- **Query time:** 500-1500ms per batch (5-15x slower than helper table)
- **Total export time:** ~100-200 hours (4-8 days)

---

### Option 3: Partition by Status (COMPLEX, OVERKILL)

Create separate partitions for Status 4 and Status 6 in CorrespondenceStatuses.

**Not recommended:** Too much infrastructure change for one-time export.

---

## Recommended Approach: Helper Table (Option 1)

### Implementation Plan

#### Phase 1: Helper Table Creation (ONE-TIME, ~30-60 minutes)
1. **Create helper table** with DISTINCT pre-filtered records
2. **Verify row counts** match expected ~150M
3. **Create optimized indexes** (CONCURRENTLY to avoid blocking)
4. **Update statistics** (ANALYZE)

#### Phase 2: Code Changes (30 minutes)
1. **Update `FetchStatusRecordsAsync`** to detect Issue #1951 and use helper table query
2. **Update `GetTotalCountAsync`** to query helper table for counts
3. **Add logging** to indicate helper table usage

#### Phase 3: Testing (1-2 hours)
1. **Test export** with `--max-batches 10` (50K rows)
2. **Verify query timing** (should be ~100-200ms per batch)
3. **Check EXPLAIN ANALYZE** to confirm index usage
4. **Validate CSV output** (correct columns, no duplicates)

#### Phase 4: Production Export (~33 hours)
1. **Run full export** with batch size 5000
2. **Monitor progress** (155 rows/sec target)
3. **Checkpoint resume** if interrupted

---

## Code Changes Required

### 1. Update `FetchStatusRecordsAsync` Method

Add Issue #1951 helper table query branch (similar to Issue #1716):

```csharp
if (issueNumber == 1951)
{
    // Use A2Iss1951MigratedEvents helper table for Issue #1951
    var cursorPredicate = lastCorrespondenceId.HasValue 
        ? "AND migEvents.\"CorrespondenceId\" > @lastId"
        : "";

    query = $@"
        SELECT DISTINCT
            er.""ReferenceValue"" AS DialogId,
            {idcJoinAlias}.""Id"" AS DialogActivityId,
            stats.""CorrespondenceId"",
            stats.""StatusChanged"" AS Timestamp,
            ap.""OutputActorId"" AS ActorId,
            ap.""Name"" AS ActorName,
            {statusValue} AS Status,
            '{activityType}' AS ActivityType
        FROM correspondence.""A2Iss1951MigratedEvents"" migEvents
        INNER JOIN correspondence.""CorrespondenceStatuses"" stats 
            ON migEvents.""CorrespondenceId"" = stats.""CorrespondenceId"" 
            AND migEvents.""Status"" = stats.""Status"" 
            AND migEvents.""PartyUuid"" = stats.""PartyUuid""
        INNER JOIN correspondence.""ExternalReferences"" er
            ON migEvents.""CorrespondenceId"" = er.""CorrespondenceId""
            AND er.""ReferenceType"" = 3
        INNER JOIN correspondence.""IdempotencyKeys"" {idcJoinAlias}
            ON migEvents.""CorrespondenceId"" = {idcJoinAlias}.""CorrespondenceId""
            AND {idcJoinAlias}.""StatusAction"" = '{statusActionValue}'
        INNER JOIN correspondence.""A2Parties"" ap 
            ON stats.""PartyUuid"" = ap.""PartyUuid""
        WHERE migEvents.""Status"" = {statusValue}
          {cursorPredicate}
        ORDER BY migEvents.""CorrespondenceId""
        LIMIT @fetchLimit";
}
```

### 2. Update `GetTotalCountAsync` Method

```csharp
if (issueNumber == 1951)
{
    countQuery = @"
        SELECT COUNT(*)
        FROM correspondence.""A2Iss1951MigratedEvents""
        WHERE ""Status"" IN (4, 6)";
}
```

---

## Expected Timeline

| Phase | Duration | Notes |
|-------|----------|-------|
| **Helper Table Creation** | 30-60 min | One-time setup in production |
| **Index Creation** | 20-40 min | CONCURRENTLY, no blocking |
| **Code Changes** | 30 min | Minimal changes to existing code |
| **Testing** | 1-2 hours | Test with --max-batches 10 |
| **Production Export** | ~33 hours | 150M rows @ 125 rows/sec |
| **TOTAL** | ~35-36 hours | Most time is export itself |

---

## Risk Mitigation

### Risk 1: Helper Table Creation Fails
- **Mitigation:** Test query on smaller subset first (LIMIT 1000000)
- **Fallback:** Keep existing CTE approach as backup

### Risk 2: Helper Table Doesn't Fit in Memory
- **Mitigation:** Covering index enables Index-Only Scans (no heap access)
- **Monitoring:** Check `pg_stat_user_indexes` for index usage

### Risk 3: Export Still Too Slow
- **Mitigation:** Network transfer is the bottleneck, not query time
- **Consideration:** Deploy exporter to Azure VM in same region (50-100x speedup)

---

## Success Criteria

- ✅ Helper table created with ~150M rows
- ✅ Query time < 200ms per batch (measured via EXPLAIN ANALYZE)
- ✅ Export throughput: 100-150 rows/sec sustained
- ✅ Total export time: < 48 hours
- ✅ CSV output validates correctly (no missing rows, no duplicates)

---

## Next Steps

1. **Review this strategy** with team
2. **Get approval** for helper table creation in production
3. **Schedule maintenance window** (if needed for index creation)
4. **Execute Phase 1** (helper table + indexes)
5. **Implement code changes** (Phase 2)
6. **Test on subset** (Phase 3)
7. **Run production export** (Phase 4)

---

## References

- **Issue #1716 Optimization:** `A2Iss1716A2Events_Helper_Table_Migration.md`
- **Query Performance Analysis:** `NETWORK_BOTTLENECK_ANALYSIS.md`
- **Cursor Implementation:** `SEPARATE_CURSORS_IMPLEMENTATION.md`
- **Index Documentation:** `Index_Creation_Scripts.sql`
