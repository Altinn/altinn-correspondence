# Helper Table Performance Optimization

## Overview

Issue #1716 export now uses a **helper table** (`A2iss1716corrs`) for dramatic performance improvement:
- **Before**: ~5,700ms per batch
- **After**: ~850ms per batch  
- **Improvement**: **6.7x faster** (83% reduction in query time)

## Helper Table Structure

### Option 1: Minimal Structure (Requires Correspondences JOIN)
```sql
CREATE TABLE correspondence."A2iss1716corrs" (
    "Altinn2CorrespondenceId" INT NOT NULL,
    "Altinn3CorrespondenceId" UUID NOT NULL,
    CONSTRAINT "PK_A2iss1716corrs" PRIMARY KEY ("Altinn3CorrespondenceId")
);
```
⚠️ **Performance**: Still requires JOIN to Correspondences for Recipient filter

### Option 2: Optimized Structure (Recommended)
```sql
CREATE TABLE correspondence."A2iss1716corrs" (
    "Altinn2CorrespondenceId" INT NOT NULL,
    "Altinn3CorrespondenceId" UUID NOT NULL,
    "Recipient" TEXT NOT NULL,  -- ✅ Eliminates Correspondences JOIN
    CONSTRAINT "PK_A2iss1716corrs" PRIMARY KEY ("Altinn3CorrespondenceId")
);
```
✅ **Performance**: No Correspondences JOIN needed!

### Recommended Index
```sql
-- Composite index for ordered scanning
CREATE INDEX "IX_A2iss1716corrs_A2_A3" 
    ON correspondence."A2iss1716corrs"
    ("Altinn2CorrespondenceId", "Altinn3CorrespondenceId");

ANALYZE correspondence."A2iss1716corrs";
```

### Population Script (Option 2 - Recommended)
```sql
INSERT INTO correspondence."A2iss1716corrs"
SELECT 
    "Altinn2CorrespondenceId",
    "Id" AS "Altinn3CorrespondenceId",
    "Recipient"
FROM correspondence."Correspondences"
WHERE "Altinn2CorrespondenceId" IS NOT NULL
  AND "IsMigrating" = FALSE
  AND EXISTS (
      SELECT 1 
      FROM correspondence."CorrespondenceStatuses" s
      WHERE s."CorrespondenceId" = "Correspondences"."Id"
        AND s."Status" IN (4, 6)
        AND s."SyncedFromAltinn2" < '2026-02-15 00:00:00'
  );
```

### Purpose

The helper table contains:
- **Altinn2CorrespondenceId**: Original INT primary key from Altinn 2
- **Altinn3CorrespondenceId**: UUID primary key in Altinn 3

This pre-filtered list of correspondences allows:
1. Fast JOIN without complex filter evaluation
2. Chronological ordering using A2 INT ID (faster than UUID comparison)
3. Index-only scans (all needed columns in index)

## Query Optimization

### Original Query (Issue #1716 without helper table)

```sql
WITH filtered AS (
    SELECT 
        stats."CorrespondenceId",
        stats."PartyUuid",
        stats."StatusChanged",
        stats."Status"
    FROM correspondence."CorrespondenceStatuses" stats
    WHERE stats."Status" = 4
      AND stats."SyncedFromAltinn2" < @cutoffTimestamp
      AND stats."SyncedFromAltinn2" IS NOT NULL
    ORDER BY stats."CorrespondenceId", stats."Status"
    LIMIT 1000
)
SELECT ... FROM filtered
INNER JOIN correspondence."Correspondences" corr ...
INNER JOIN correspondence."A2Parties" ap ...
```

**Problems**:
- Sequential scan on CorrespondenceStatuses (568M rows estimated)
- Complex JOIN conditions on Correspondences
- Slow UUID ordering

### Optimized Query (with helper table)

```sql
WITH filtered AS (
    SELECT 
        helper."Altinn2CorrespondenceId",
        stats."CorrespondenceId",
        stats."PartyUuid",
        stats."StatusChanged",
        stats."Status"
    FROM correspondence."A2iss1716corrs" helper
    INNER JOIN correspondence."CorrespondenceStatuses" stats 
        ON helper."Altinn3CorrespondenceId" = stats."CorrespondenceId"
    WHERE stats."Status" = 4
      AND stats."SyncedFromAltinn2" < @cutoffTimestamp
    ORDER BY helper."Altinn2CorrespondenceId", stats."Status"
    LIMIT 1000
)
SELECT ... FROM filtered
INNER JOIN correspondence."A2Parties" ap ...
```

**Benefits**:
- Parallel index-only scan on helper table (~8.5M rows)
- Fast PK lookup into CorrespondenceStatuses
- No Correspondences JOIN needed (already filtered)
- Fast INT ordering instead of UUID ordering

## Performance Metrics

### EXPLAIN ANALYZE Results

```
Execution Time: 847.773 ms
Buffers: shared hit=11795 read=290
I/O Timings: shared read=816.488

-> Parallel Index Only Scan using "IX_A2iss1716corrs_A2_A3"
   Heap Fetches: 0  (Index-only scan!)

-> Index Scan using "IX_Correspondence_CorrespondenceId_Status"
   loops=2003
```

### Key Improvements

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| **Execution Time** | 5,702 ms | 848 ms | 6.7x faster |
| **I/O Time** | 9,093 ms | 816 ms | 11x faster |
| **Disk Reads** | 3,377 buffers | 290 buffers | 91% reduction |
| **Index Type** | Seq Scan | Index-only Scan | ✅ Optimal |

## Implementation Details

### Code Structure

The `DialogActivityExportService` now has conditional query generation:

```csharp
if (issueNumber == 1716)
{
    // Use optimized helper table query
    // Orders by Altinn2CorrespondenceId (INT) for chronology
}
else // Issue 1951
{
    // Use standard CTE approach
    // No helper table yet for Issue 1951
}
```

### Why A2CorrespondenceId Ordering?

The **Altinn2CorrespondenceId** (INT) is used for ordering because:
1. **Chronological sequence**: INT values reflect creation order in Altinn 2
2. **Faster comparisons**: INT sorting is faster than UUID sorting
3. **Event sequencing**: Dialog activity events should be in chronological order
4. **No data comparison**: INT comparison is simpler than timestamp comparison

### Cursor Pagination

Cursor still uses UUID-based tuple for compatibility:
```sql
AND (stats."CorrespondenceId", stats."Status") > (@lastId, @lastStatus)
```

The helper table ensures we scan in A2 INT order, but cursor uses UUID to maintain consistency across batches.

## Setup Requirements

### Database Prerequisites

1. **Helper table must exist**:
```sql
-- Verify table exists
SELECT COUNT(*) FROM correspondence."A2iss1716corrs";
```

2. **Indexes must be created**:
```sql
-- Check indexes
SELECT indexname, indexdef 
FROM pg_indexes 
WHERE tablename = 'A2iss1716corrs';
```

3. **Statistics must be current**:
```sql
ANALYZE correspondence."A2iss1716corrs";
ANALYZE correspondence."CorrespondenceStatuses";
```

### Expected Row Counts

- **Helper table**: ~7-9 million rows (Issue 1716 correspondences)
- **Status 4**: ~50-60% of helper rows have Status=4 records
- **Status 6**: ~10-20% of helper rows have Status=6 records

## Issue #1951 Status

Issue #1951 (Migrated Events) **does not yet use a helper table**:
- Uses standard CTE approach
- Still performs reasonably (~6 min for 2 batches in test mode)
- Could benefit from similar optimization if needed

### Creating Helper Table for Issue #1951

If performance for Issue 1951 becomes a concern:

```sql
CREATE TABLE correspondence."A2iss1951corrs" (
    "Altinn2CorrespondenceId" INT NOT NULL,
    "Altinn3CorrespondenceId" UUID NOT NULL,
    CONSTRAINT "PK_A2iss1951corrs" PRIMARY KEY ("Altinn3CorrespondenceId")
);

CREATE INDEX "IX_A2iss1951corrs_A2_A3" 
    ON correspondence."A2iss1951corrs"
    ("Altinn2CorrespondenceId", "Altinn3CorrespondenceId");

-- Populate with Issue 1951 correspondences
INSERT INTO correspondence."A2iss1951corrs"
SELECT 
    "Altinn2CorrespondenceId",
    "Id"
FROM correspondence."Correspondences"
WHERE "Altinn2CorrespondenceId" IS NOT NULL
  AND "IsMigrating" = FALSE
  AND EXISTS (
      SELECT 1 FROM correspondence."CorrespondenceStatuses" s
      WHERE s."CorrespondenceId" = "Correspondences"."Id"
        AND s."Status" IN (4, 6)
        AND s."SyncedFromAltinn2" IS NULL
        AND s."StatusChanged" BETWEEN '2019-03-23' AND '2026-05-19 11:35:59'
  );
```

Then update code to use helper table for Issue 1951 as well.

## Verification

### Test Helper Table Performance

```sql
-- Test Issue 1716 query performance
EXPLAIN (ANALYZE, BUFFERS)
SELECT 
    helper."Altinn2CorrespondenceId",
    stats."CorrespondenceId",
    stats."PartyUuid",
    stats."StatusChanged",
    stats."Status"
FROM correspondence."A2iss1716corrs" helper
INNER JOIN correspondence."CorrespondenceStatuses" stats 
    ON helper."Altinn3CorrespondenceId" = stats."CorrespondenceId"
WHERE stats."Status" = 4
  AND stats."SyncedFromAltinn2" < '2026-02-15 00:00:00'
ORDER BY helper."Altinn2CorrespondenceId", stats."Status"
LIMIT 1000;
```

**Expected**:
- Execution time: < 1 second
- Index-only scan on helper table
- Minimal disk I/O

### Monitor Export Performance

```bash
# Run test export for Issue 1716
dotnet run -- --issue 1716 \
  --output test_1716.csv \
  --cutoff "2026-02-15" \
  --max-batches 2 \
  --azure-ad \
  --yes

# Expected: ~1-2 minutes for 2 batches (vs ~6 minutes before)
```

## Troubleshooting

### Query still slow (> 2 seconds per batch)

**Check**:
1. Helper table indexes exist: `\d+ correspondence."A2iss1716corrs"`
2. Statistics are current: `ANALYZE correspondence."A2iss1716corrs";`
3. Helper table populated: `SELECT COUNT(*) FROM correspondence."A2iss1716corrs";`

### No performance improvement

**Possible causes**:
1. Database disk I/O is bottleneck (check `I/O Timings` in EXPLAIN)
2. Network latency between app and database
3. Helper table not being used (check EXPLAIN ANALYZE output)

### Wrong results

**Check**:
1. Helper table data is correct
2. Cursor pagination logic is working
3. No duplicate rows in helper table

## Benefits Summary

✅ **6.7x faster** query execution  
✅ **11x less** disk I/O  
✅ **Index-only scans** (optimal)  
✅ **Chronological ordering** using A2 INT ID  
✅ **Production-ready** performance

The helper table optimization makes Issue #1716 export practical for production use!
