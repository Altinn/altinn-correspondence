# A2Iss1951A2Events Helper Table - Export Query Optimization

## Overview

After cleaning duplicates from the `A2Iss1951A2Events` helper table, this document provides recommendations for optimizing the export queries for Issue #1951.

---

## Table Structure (After Cleanup)

```sql
CREATE TABLE correspondence."A2Iss1951A2Events" (
    "CorrespondenceId" uuid NOT NULL,
    "Timestamp" timestamptz NOT NULL,
    "PartyUuid" uuid NOT NULL,
    "Status" int4 NOT NULL,
    "Source" int4 NOT NULL  -- Should now be unique per (CorrespondenceId, Timestamp, PartyUuid, Status)
);
```

**Key Constraint**: After deduplication, each combination of `(CorrespondenceId, Timestamp, PartyUuid, Status)` should appear only once.

---

## Recommended Indexes

### Primary Index: Cursor Pagination

```sql
CREATE INDEX CONCURRENTLY "IX_A2Iss1951A2Events_CorrId_Status_Party"
ON correspondence."A2Iss1951A2Events" (
    "CorrespondenceId",
    "Status"
)
INCLUDE ("PartyUuid", "Timestamp");
```

**Purpose**: 
- Supports cursor pagination: `WHERE (CorrespondenceId, Status) > (@lastId, @lastStatus)`
- Covering index (no heap lookups needed for these columns)
- Matches the export query's ORDER BY clause

### Secondary Index: Status Filtering

```sql
CREATE INDEX CONCURRENTLY "IX_A2Iss1951A2Events_Status_Timestamp"
ON correspondence."A2Iss1951A2Events" (
    "Status",
    "Timestamp"
)
INCLUDE ("CorrespondenceId", "PartyUuid");
```

**Purpose**:
- Fast filtering by Status (4 or 6)
- Supports timestamp range queries if needed
- Alternative index for optimizer to choose

**Note**: After creating indexes, run `ANALYZE correspondence."A2Iss1951A2Events";`

---

## Optimized Export Query

### Pattern (Similar to Issue #1716)

```sql
-- Status 4 (Read) query using helper table
SELECT DISTINCT
    er."ReferenceValue" AS DialogId,
    idcFetch."Id" AS DialogActivityId,
    stats."CorrespondenceId",
    stats."StatusChanged" AS Timestamp,
    ap."OutputActorId" AS ActorId,
    ap."Name" AS ActorName,
    4 AS Status,
    'CorrespondenceOpened' AS ActivityType
FROM correspondence."A2Iss1951A2Events" a2Events
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON a2Events."CorrespondenceId" = stats."CorrespondenceId" 
    AND a2Events."Status" = stats."Status" 
    AND a2Events."PartyUuid" = stats."PartyUuid"
    AND a2Events."Timestamp" = stats."StatusChanged"  -- Add timestamp matching for precision
INNER JOIN correspondence."Correspondences" corr
    ON a2Events."CorrespondenceId" = corr."Id"
    AND corr."Altinn2CorrespondenceId" IS NOT NULL
    AND corr."IsMigrating" = FALSE
INNER JOIN correspondence."ExternalReferences" er
    ON a2Events."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcFetch
    ON a2Events."CorrespondenceId" = idcFetch."CorrespondenceId"
    AND idcFetch."StatusAction" = '3'  -- Fetched action for Status 4
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"  -- Recipient filter
WHERE a2Events."Status" = 4
  -- Cursor pagination (for subsequent batches):
  AND (a2Events."CorrespondenceId", a2Events."Status") > (@lastId, @lastStatus)
ORDER BY stats."CorrespondenceId", Status
LIMIT @fetchLimit;
```

### Key Changes from Previous CTE Approach

1. **Direct Helper Table Join**: Use `A2Iss1951A2Events` directly instead of CTE filtering `CorrespondenceStatuses`
   - **Why**: Helper table is pre-filtered from Altinn2, much smaller than scanning 1.94B rows
   - **Performance**: Similar to Issue #1716's helper table approach

2. **Timestamp Matching**: Added `a2Events."Timestamp" = stats."StatusChanged"`
   - **Why**: After deduplication, timestamps should match exactly
   - **Benefit**: More precise join, reduces chance of incorrect matches

3. **Cursor on Helper Table**: `(a2Events."CorrespondenceId", a2Events."Status") > (@lastId, @lastStatus)`
   - **Why**: Uses index on `A2Iss1951A2Events` for efficient seek
   - **Critical**: Same lesson from Issue #1716 cursor fix

4. **ORDER BY on Stats Columns**: `ORDER BY stats."CorrespondenceId", Status`
   - **Why**: DISTINCT requires ORDER BY columns to be in SELECT list
   - **Works**: Join equivalence allows optimizer to use helper table index

---

## Query Performance Expectations

### Before (CTE approach with CorrespondenceStatuses)
- **Query Time**: 3-5 seconds per batch (Status 4), 15-20 seconds (Status 6)
- **Problem**: Scanning 1.94B rows even with partial index

### After (Helper table approach)
- **Query Time**: 17-50ms per batch (both Status 4 and 6)
- **Improvement**: 100-1000x faster
- **Why**: Scanning ~150M pre-filtered rows instead of 1.94B rows

### Expected Full Export Time

Assuming ~150 million rows for Issue #1951:

| Batch Size | Batches Needed | Time per Batch | Total Time |
|-----------|----------------|----------------|------------|
| 5,000 | 30,000 | 30ms avg | 15 minutes |
| 10,000 | 15,000 | 50ms avg | 12.5 minutes |

**Recommendation**: Start with batch size 5,000 (proven stable for Issue #1716).

---

## Implementation in DialogActivityExportService.cs

### Add Helper Table Detection

```csharp
private async Task<(int batchCount, (Guid correspondenceId, int status)? newCursor)> ProcessBatchAsync(
    NpgsqlConnection connection,
    StreamWriter writer,
    int issueNumber,
    DateTime cutoffTimestamp,
    (Guid correspondenceId, int status)? lastCursor,
    CancellationToken cancellationToken)
{
    // ... existing code ...

    string query;

    if (issueNumber == 1716)
    {
        // Existing helper table query for Issue #1716
        // ... (already implemented)
    }
    else if (issueNumber == 1951)
    {
        // NEW: Helper table query for Issue #1951
        var a2EventsCursorPredicate = lastCursor.HasValue 
            ? "AND (a2Events.\"CorrespondenceId\", a2Events.\"Status\") > (@lastId, @lastStatus)"
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
            FROM correspondence.""A2Iss1951A2Events"" a2Events
            INNER JOIN correspondence.""CorrespondenceStatuses"" stats 
                ON a2Events.""CorrespondenceId"" = stats.""CorrespondenceId"" 
                AND a2Events.""Status"" = stats.""Status"" 
                AND a2Events.""PartyUuid"" = stats.""PartyUuid""
                AND a2Events.""Timestamp"" = stats.""StatusChanged""
            INNER JOIN correspondence.""Correspondences"" corr
                ON a2Events.""CorrespondenceId"" = corr.""Id""
                AND corr.""Altinn2CorrespondenceId"" IS NOT NULL
                AND corr.""IsMigrating"" = FALSE
            INNER JOIN correspondence.""ExternalReferences"" er
                ON a2Events.""CorrespondenceId"" = er.""CorrespondenceId""
                AND er.""ReferenceType"" = 3
            INNER JOIN correspondence.""IdempotencyKeys"" {idcJoinAlias}
                ON a2Events.""CorrespondenceId"" = {idcJoinAlias}.""CorrespondenceId""
                AND {idcJoinAlias}.""StatusAction"" = '{statusActionValue}'
            INNER JOIN correspondence.""A2Parties"" ap 
                ON stats.""PartyUuid"" = ap.""PartyUuid""
                AND corr.""Recipient"" <> ap.""RecipientUrn""
            WHERE a2Events.""Status"" = {statusValue}
              {a2EventsCursorPredicate}
            ORDER BY stats.""CorrespondenceId"", Status
            LIMIT @fetchLimit";
    }
    else
    {
        // Fallback: original CTE approach (if helper table doesn't exist)
        // ... (keep existing implementation as fallback)
    }

    // ... rest of method ...
}
```

---

## Migration Steps

### 1. Clean Duplicates
Run `docs/database/Clean_A2Iss1951A2Events_Duplicates.sql`:
- Analyze duplicate situation
- Create backup
- Remove duplicates (keeping Source = 1 preferred)
- Verify results

### 2. Create Indexes
```sql
-- Run from Clean_A2Iss1951A2Events_Duplicates.sql Step 6
CREATE INDEX CONCURRENTLY "IX_A2Iss1951A2Events_CorrId_Status_Party" ...
CREATE INDEX CONCURRENTLY "IX_A2Iss1951A2Events_Status_Timestamp" ...
ANALYZE correspondence."A2Iss1951A2Events";
```

**Time**: 5-10 minutes for index creation (150M rows)

### 3. Test Query Performance
```sql
-- Test Status 4 query
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT ...
FROM correspondence."A2Iss1951A2Events" a2Events
...
WHERE a2Events."Status" = 4
LIMIT 5000;
```

**Expected**: 
- Index scan on `IX_A2Iss1951A2Events_CorrId_Status_Party`
- Execution time < 100ms
- No sequential scans

### 4. Update DialogActivityExportService.cs
- Add helper table query branch for Issue #1951
- Test with small batch (`--max-batches 2`)
- Verify timing logs show 30-50ms per batch

### 5. Update calculate-counts.sql
Add helper table query for Issue #1951:

```sql
-- Issue #1951 Status 4 - Using Helper Table
SELECT COUNT(DISTINCT stats."CorrespondenceId")
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
INNER JOIN correspondence."ExternalReferences" er
    ON a2Events."CorrespondenceId" = er."CorrespondenceId"
    AND er."ReferenceType" = 3
INNER JOIN correspondence."IdempotencyKeys" idcFetch
    ON a2Events."CorrespondenceId" = idcFetch."CorrespondenceId"
    AND idcFetch."StatusAction" = '3'
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
WHERE a2Events."Status" = 4;
```

---

## Verification Checklist

### Pre-Deployment
- [ ] Duplicates removed from `A2Iss1951A2Events`
- [ ] Backup table created (`A2Iss1951A2Events_backup`)
- [ ] Indexes created and `ANALYZE` run
- [ ] Test queries show < 100ms execution time
- [ ] Index scans confirmed in EXPLAIN plans (no seq scans)

### Post-Deployment
- [ ] Test export with `--max-batches 2` shows 30-50ms timing
- [ ] Full export completes in 15-20 minutes
- [ ] Row counts match expected (~150M rows)
- [ ] Spot check data accuracy (compare samples with Altinn2 export)

---

## Troubleshooting

### Query Still Slow (>1 second per batch)

**Check**:
```sql
EXPLAIN (ANALYZE, BUFFERS)
SELECT DISTINCT ...
FROM correspondence."A2Iss1951A2Events" a2Events
...
WHERE a2Events."Status" = 4
LIMIT 5000;
```

**If showing sequential scan**:
1. Run `ANALYZE correspondence."A2Iss1951A2Events";`
2. Check if indexes exist: `\d correspondence."A2Iss1951A2Events"`
3. Check index validity: See verification query in cleanup script

**If using wrong index**:
- May need to drop competing index (similar to Issue #1716 Status 6 issue)
- Run `ANALYZE` again after dropping index

### Duplicate Rows in Export

**Cause**: DISTINCT may not be working as expected

**Check**:
```sql
-- Verify no duplicates in helper table after cleanup
SELECT 
    "CorrespondenceId", "Timestamp", "PartyUuid", "Status",
    COUNT(*)
FROM correspondence."A2Iss1951A2Events"
GROUP BY "CorrespondenceId", "Timestamp", "PartyUuid", "Status"
HAVING COUNT(*) > 1
LIMIT 10;
```

**Fix**: If duplicates remain, re-run duplicate cleanup script

---

## Performance Comparison

| Approach | Query Time | Full Export Time | Complexity |
|----------|-----------|------------------|------------|
| **Original CTE** | 3-20s | 12-50 hours | Medium |
| **Helper Table** | 17-50ms | 15-20 minutes | Low |
| **Improvement** | **100-1000x** | **40-200x** | **Simpler** |

---

## Summary

✅ **Benefits of Helper Table Approach**:
- 100-1000x faster queries (17-50ms vs 3-20s)
- 40-200x faster full export (15-20 min vs 12-50 hours)
- Simpler query structure (no complex CTE)
- Consistent with Issue #1716 proven pattern
- Scales well with cursor pagination

⚠️ **Prerequisites**:
- Clean helper table (no duplicates)
- Proper indexes created
- `ANALYZE` run after index creation
- Test queries verified < 100ms

📋 **Next Steps**:
1. Run duplicate cleanup script
2. Create indexes
3. Test query performance
4. Update DialogActivityExportService.cs
5. Test with small batch export
6. Deploy to production
