# Pre-Calculated Counts Feature Summary

## Problem Statement

COUNT(*) queries on the 1.94 billion row CorrespondenceStatuses table are expensive:
- Issue #1716 count: ~30 seconds
- Issue #1951 count: ~60 seconds
- **Total delay: ~90 seconds on every export run**

The counts are only used for progress reporting (percentage complete, ETA), not for data correctness. They don't need to be recalculated every time.

## Solution

Store pre-calculated counts in `appsettings.json` and read them at startup. Only query the database if counts are not provided (set to 0).

## Implementation

### 1. Configuration (appsettings.json)

```json
{
  "ConnectionString": "",
  "BatchSize": 50000,
  "PreCalculatedCounts": {
    "Issue1716": 0,
    "Issue1951": 0,
    "Comment": "Pre-calculated total counts from COUNT(*) queries. Set to 0 to force runtime calculation. These values are used for progress reporting only."
  }
}
```

### 2. Code Changes

**Program.cs:**
- Reads `PreCalculatedCounts:Issue1716` and `PreCalculatedCounts:Issue1951` from config
- Passes counts to export methods

**DialogActivityExportService.cs:**
- `ExportToCSVAsync`: Added `preCalculatedCount` parameter (default: 0)
- `ExportBothToCSVAsync`: Added `preCalculatedCount1716` and `preCalculatedCount1951` parameters
- Logic: If preCalcCount > 0, use it; else if !skipTotalCount, query database

### 3. Helper Script (calculate-counts.sql)

SQL script with COUNT queries for both issues. Users run these once, copy results to appsettings.json.

## Usage Workflow

### Initial Setup (One-Time, ~90 seconds):

```sql
-- 1. Run calculate-counts.sql queries (update timestamps first)
SELECT COUNT(*) AS Issue1716_Count FROM ... -- ~30 seconds
SELECT COUNT(*) AS Issue1951_Count FROM ... -- ~60 seconds
```

### 2. Update Configuration:

```json
{
  "PreCalculatedCounts": {
    "Issue1716": 8456789,      // Copy from query result
    "Issue1951": 152348912,    // Copy from query result
    "Comment": "Pre-calculated 2026-05-19"
  }
}
```

### 3. Run Export (Instant Count):

```powershell
dotnet run -- --issue all --output export.csv --cutoff "2026-05-19 11:35:59" --azure-ad -y
# Output: "Using pre-calculated counts: 160,805,701 total (1716: 8,456,789, 1951: 152,348,912)"
```

## Behavior Matrix

| Config Value | skipTotalCount | Test Mode | Behavior |
|--------------|----------------|-----------|----------|
| > 0 | false | No | ✅ Use pre-calculated (instant) |
| > 0 | true | Yes | ✅ Use pre-calculated (instant) |
| 0 | false | No | ⏱️ Query database (~30-60s) |
| 0 | true | Yes | 🚀 Skip entirely (0 delay) |

## Benefits

1. **Performance**: Save ~90 seconds on every production export
2. **User Experience**: No waiting for counts on repeated exports
3. **Flexibility**: Set to 0 to force recalculation if needed
4. **Test Mode**: Still works with `--max-batches` (skips count entirely)

## When to Update Counts

Update pre-calculated counts when:
- Running export with a NEW cutoff date (significant time change)
- Database has significant new data (e.g., after migration batch)
- Counts become noticeably inaccurate (doesn't affect correctness, just progress reporting)

**Note**: Small inaccuracies (±1-2%) are acceptable since counts are only for progress reporting.

## Files Modified

1. ✅ `appsettings.json` - Added PreCalculatedCounts section
2. ✅ `Program.cs` - Read config values, pass to methods
3. ✅ `DialogActivityExportService.cs` - Accept and use pre-calculated counts
4. ✅ `calculate-counts.sql` - Helper script with COUNT queries
5. ✅ `README.md` - Updated with usage instructions
6. ✅ `CHANGELOG.md` - Documented feature

## Testing

```powershell
# 1. Test with pre-calculated counts (should log "Using pre-calculated count")
# Update appsettings.json with counts > 0
dotnet run -- --issue all --output test.csv --cutoff "2026-05-19 11:35:59" --azure-ad -y

# 2. Test without counts (should log "Calculated total records")
# Set appsettings.json counts to 0
dotnet run -- --issue all --output test.csv --cutoff "2026-05-19 11:35:59" --azure-ad -y

# 3. Test mode (should log "Skipping total count calculation")
dotnet run -- --issue all --output test.csv --cutoff "2026-05-19 11:35:59" --azure-ad --max-batches 1 -y
```

## Production Recommendation

✅ **DO**: Pre-calculate counts once, store in appsettings.json  
✅ **DO**: Update counts when cutoff date changes significantly  
❌ **DON'T**: Rely on runtime COUNT queries (slow)  
❌ **DON'T**: Obsess over exact counts (they're for progress reporting only)

## Backward Compatibility

✅ Fully backward compatible:
- If PreCalculatedCounts not in config: Defaults to 0, queries database as before
- Existing behavior unchanged when counts are 0
- No breaking changes to API or command-line arguments
