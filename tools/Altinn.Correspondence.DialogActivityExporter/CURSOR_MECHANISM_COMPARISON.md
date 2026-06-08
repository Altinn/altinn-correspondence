# Cursor Mechanism Comparison

## Before: Single Composite Cursor

```
┌─────────────────────────────────────────────────────────────────┐
│                         BATCH 1                                 │
├─────────────────────────────────────────────────────────────────┤
│ Query Status 4: Fetch 10,000 records                            │
│   Results: (A0001, 4), (A0002, 4), ... (A9999, 4), (A10000, 4) │
│                                                                  │
│ Query Status 6: Fetch 10,000 records                            │
│   Results: (B0001, 6), (B0002, 6), ... (Z9999, 6), (Z10000, 6) │
│                                                                  │
│ Merge & Sort: Take first 10,000 by (CorrespondenceId, Status)  │
│   ✓ (A0001, 4), (A0002, 4), ... (A10000, 4)                    │
│                                                                  │
│ Single Cursor Saved: (A10000, 4) ◄─── Problem starts here      │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                         BATCH 2                                 │
├─────────────────────────────────────────────────────────────────┤
│ Query Status 4:                                                 │
│   WHERE (CorrespondenceId, Status) > ('A10000', 4)             │
│   ✓ Returns: (A10001, 4), (A10002, 4), ... ✓ CORRECT          │
│                                                                  │
│ Query Status 6:                                                 │
│   WHERE (CorrespondenceId, Status) > ('A10000', 4)             │
│   ✗ Returns: (Z0001, 6), (Z0002, 6), ...                       │
│   ✗ LOST: All Status 6 where ID < 'A10000'!                    │
│           (A0001, 6) ← LOST                                     │
│           (A5000, 6) ← LOST                                     │
│           (A9999, 6) ← LOST                                     │
└─────────────────────────────────────────────────────────────────┘

                    ⚠️ DATA LOSS OCCURRED ⚠️
```

## After: Separate Independent Cursors

```
┌─────────────────────────────────────────────────────────────────┐
│                         BATCH 1                                 │
├─────────────────────────────────────────────────────────────────┤
│ Query Status 4: Fetch 10,000 records                            │
│   Results: (A0001, 4), (A0002, 4), ... (A9999, 4), (A10000, 4) │
│   Last Status 4 ID: A10000 ◄─── Independent cursor for Status 4│
│                                                                  │
│ Query Status 6: Fetch 10,000 records                            │
│   Results: (B0001, 6), (B0002, 6), ... (Z9999, 6), (Z10000, 6) │
│   Last Status 6 ID: Z10000 ◄─── Independent cursor for Status 6│
│                                                                  │
│ Merge & Sort: Take first 10,000 by (CorrespondenceId, Status)  │
│   ✓ (A0001, 4), (A0002, 4), ... (A10000, 4)                    │
│                                                                  │
│ Two Cursors Saved:                                              │
│   Status 4 Cursor: A10000                                       │
│   Status 6 Cursor: Z10000                                       │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                         BATCH 2                                 │
├─────────────────────────────────────────────────────────────────┤
│ Query Status 4:                                                 │
│   WHERE CorrespondenceId > 'A10000'                            │
│   ✓ Returns: (A10001, 4), (A10002, 4), ... ✓ CORRECT          │
│                                                                  │
│ Query Status 6:                                                 │
│   WHERE CorrespondenceId > 'Z10000'                            │
│   ✓ Returns: (Z10001, 6), (Z10002, 6), ... ✓ CORRECT          │
│   ✓ NO LOSS: Status 6 progresses independently                 │
│                                                                  │
│ Both status types progress at their own natural rate            │
└─────────────────────────────────────────────────────────────────┘

                    ✅ NO DATA LOSS ✅
```

## Key Differences

| Aspect                    | Before (Single Cursor)              | After (Separate Cursors)           |
|---------------------------|-------------------------------------|-------------------------------------|
| **Cursor Structure**      | `(Guid id, int status)`            | Two `Guid?` cursors                |
| **Status 4 Query**        | `WHERE (id, status) > (@id, @st)`  | `WHERE id > @id4`                  |
| **Status 6 Query**        | `WHERE (id, status) > (@id, @st)`  | `WHERE id > @id6`                  |
| **Progress Tracking**     | Coupled (shared cursor)             | Independent per status             |
| **Data Loss Risk**        | HIGH (volume imbalance)             | NONE (independent progress)        |
| **Query Complexity**      | Composite tuple comparison          | Simple single-field comparison     |
| **Index Usage**           | Composite index required            | Single-column index sufficient     |
| **Checkpoint Fields**     | `LastCorrespondenceId`, `LastStatus`| `LastStatus4Id`, `LastStatus6Id`  |

## Real-World Impact

### Scenario: Production Data with Volume Imbalance
```
Total Records:
  Status 4 (Read/Opened):    1,000,000 records
  Status 6 (Confirmed):         10,000 records

Batch Size: 10,000

Old Implementation:
  ✗ After processing all Status 4 records, Status 6 would skip
    any records with CorrespondenceId < last Status 4 ID
  ✗ Potential data loss: Up to 10,000 Status 6 records

New Implementation:
  ✓ Status 4 and Status 6 progress independently
  ✓ All 1,010,000 records exported correctly
  ✓ Zero data loss
```

## Checkpoint Recovery Example

### Old Checkpoint (❌ Not Compatible)
```json
{
  "IssueNumber": 1716,
  "CutoffTimestamp": "2024-01-01T00:00:00Z",
  "TotalProcessed": 50000,
  "BatchNumber": 5,
  "LastCorrespondenceId": "a1b2c3d4-...",
  "LastStatus": 4,
  "CheckpointTime": "2024-01-15T10:30:00Z"
}
```

### New Checkpoint (✅ Independent Progress)
```json
{
  "IssueNumber": 1716,
  "CutoffTimestamp": "2024-01-01T00:00:00Z",
  "TotalProcessed": 50000,
  "BatchNumber": 5,
  "LastStatus4CorrespondenceId": "a1b2c3d4-...",
  "LastStatus6CorrespondenceId": "z9y8x7w6-...",
  "CheckpointTime": "2024-01-15T10:30:00Z"
}
```

Notice how the new checkpoint tracks the progress of each status independently!
