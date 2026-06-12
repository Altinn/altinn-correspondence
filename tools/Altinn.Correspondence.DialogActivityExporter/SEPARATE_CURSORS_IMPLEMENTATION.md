# Separate Cursors Implementation for DialogActivityExportService

## Overview
This document describes the implementation of separate cursors for Status 4 (Read) and Status 6 (Confirmed) in the DialogActivityExportService to prevent data loss during export operations.

## Problem Statement
The original implementation used a single composite cursor `(Guid correspondenceId, int status)` to track progress across both status types. This approach had a critical flaw:

**Data Loss Scenario:**
When Status 4 and Status 6 have significantly different volumes (e.g., 1M Status 4 records vs 10K Status 6 records), the cursor from the more prevalent status could cause the other status to skip large ranges of data.

**Example:**
```
Batch 1:
- Status 4: Returns 10,000 records (A001-A010000, status: 4)
- Status 6: Returns 10,000 records (Z001-Z010000, status: 6)
- Merged: Takes first 10,000 sorted by (CorrespondenceId, Status)
- Last cursor: (A010000, 4)

Batch 2 using cursor (A010000, 4):
- Status 4: ✅ Correctly fetches records after A010000
- Status 6: ❌ SKIPS all Status 6 records where CorrespondenceId <= A010000
  - Lost: (A000100, 6), (A005000, 6), etc.
```

## Solution: Independent Cursors

### Changes Made

#### 1. Cursor Variables
**Before:**
```csharp
(Guid correspondenceId, int status)? lastCursor = null;
```

**After:**
```csharp
Guid? lastStatus4CorrespondenceId = null;
Guid? lastStatus6CorrespondenceId = null;
```

#### 2. ProcessBatchAsync Method Signature
**Before:**
```csharp
private async Task<(int Count, (Guid correspondenceId, int status)? LastCursor)> ProcessBatchAsync(
    // ...
    (Guid correspondenceId, int status)? lastCursor,
    // ...
)
```

**After:**
```csharp
private async Task<(int Count, Guid? LastStatus4CorrespondenceId, Guid? LastStatus6CorrespondenceId)> ProcessBatchAsync(
    // ...
    Guid? lastStatus4CorrespondenceId,
    Guid? lastStatus6CorrespondenceId,
    // ...
)
```

#### 3. Cursor Tracking in ProcessBatchAsync
**Before:**
```csharp
var lastProcessedCursor = allResults.Count > 0 
    ? (allResults[^1].CorrespondenceId, allResults[^1].Status) 
    : ((Guid correspondenceId, int status)?)null;

return (allResults.Count, lastProcessedCursor);
```

**After:**
```csharp
// Track the last CorrespondenceId for each status independently
Guid? newStatus4Cursor = lastStatus4CorrespondenceId;
Guid? newStatus6Cursor = lastStatus6CorrespondenceId;

if (status4Results.Count > 0)
{
    newStatus4Cursor = status4Results[^1].CorrespondenceId;
}
if (status6Results.Count > 0)
{
    newStatus6Cursor = status6Results[^1].CorrespondenceId;
}

return (allResults.Count, newStatus4Cursor, newStatus6Cursor);
```

#### 4. FetchStatusRecordsAsync Method Signature
**Before:**
```csharp
private async Task<List<DialogActivityRecord>> FetchStatusRecordsAsync(
    // ...
    (Guid correspondenceId, int status)? lastCursor,
    // ...
)
```

**After:**
```csharp
private async Task<List<DialogActivityRecord>> FetchStatusRecordsAsync(
    // ...
    Guid? lastCorrespondenceId,
    // ...
)
```

#### 5. Query Cursor Predicates
**Before (Issue #1716):**
```sql
AND (a2Events."CorrespondenceId", a2Events."Status") > (@lastId, @lastStatus)
```

**After (Issue #1716):**
```sql
AND a2Events."CorrespondenceId" > @lastId
```

**Before (Issue #1951):**
```sql
AND (stats."CorrespondenceId", stats."Status") > (@lastId, @lastStatus)
ORDER BY stats."CorrespondenceId", stats."Status"
```

**After (Issue #1951):**
```sql
AND stats."CorrespondenceId" > @lastId
ORDER BY stats."CorrespondenceId"
```

#### 6. ExportCheckpoint Record
**Before:**
```csharp
internal record ExportCheckpoint
{
    public int IssueNumber { get; init; }
    public DateTime CutoffTimestamp { get; init; }
    public long TotalProcessed { get; init; }
    public int BatchNumber { get; init; }
    public Guid? LastCorrespondenceId { get; init; }
    public int? LastStatus { get; init; }
    public DateTime CheckpointTime { get; init; }
}
```

**After:**
```csharp
internal record ExportCheckpoint
{
    public int IssueNumber { get; init; }
    public DateTime CutoffTimestamp { get; init; }
    public long TotalProcessed { get; init; }
    public int BatchNumber { get; init; }
    public Guid? LastStatus4CorrespondenceId { get; init; }
    public Guid? LastStatus6CorrespondenceId { get; init; }
    public DateTime CheckpointTime { get; init; }
}
```

## Benefits

1. **Data Integrity**: Ensures no records are skipped regardless of volume differences between status types
2. **Independent Progress**: Each status type progresses at its natural rate
3. **Correct Resumption**: Checkpoint recovery accurately restores progress for both status types
4. **Simpler Queries**: Cursor predicates are simpler (single field comparison instead of composite)
5. **Better Index Usage**: Database can more efficiently use indexes on CorrespondenceId alone

## Testing Recommendations

1. **Volume Imbalance Test**: Test with highly skewed data (e.g., 1M Status 4, 10K Status 6)
2. **Checkpoint Recovery Test**: Verify that resuming from checkpoint correctly processes both status types
3. **Data Completeness Test**: Verify final export contains all expected records for both statuses
4. **Performance Test**: Measure query performance with simplified cursor predicates

## Backward Compatibility

⚠️ **Important**: This change is **NOT backward compatible** with existing checkpoint files.

**Mitigation:**
- The checkpoint validation in `ExportToCSVAsync` checks `IssueNumber` and `CutoffTimestamp`
- Old checkpoint files will fail to deserialize correctly due to missing fields
- Users should either:
  - Use `freshStart: true` flag to delete old checkpoints
  - Manually delete checkpoint files before resuming
  - Let the export complete without interruption

## Implementation Date
2026 (as part of chore/cleanupMigration2026 branch)

## Related Files
- `DialogActivityExportService.cs` - Main implementation
- `ExportCheckpoint` - Checkpoint data structure
- SQL queries in `FetchStatusRecordsAsync` - Database query modifications
