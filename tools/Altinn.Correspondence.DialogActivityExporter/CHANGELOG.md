# DialogActivityExporter Changelog

## 2025-01-XX - Query Fixes & Parameter Cleanup

### Added: Pre-Calculated Counts Configuration

**Feature**: Support for pre-calculated record counts to avoid expensive COUNT(*) queries.

**Why**:
- COUNT(*) queries on 1.94B row table take 30-60 seconds each
- Counts are only used for progress reporting, not for data correctness
- By pre-calculating once and storing in config, we save ~90 seconds on every export

**Changes**:
1. **appsettings.json**: Added `PreCalculatedCounts` section with `Issue1716` and `Issue1951` fields
2. **Program.cs**: Reads pre-calculated counts and passes to export methods
3. **DialogActivityExportService.cs**:
   - `ExportToCSVAsync`: Added `preCalculatedCount` parameter (default: 0)
   - `ExportBothToCSVAsync`: Added `preCalculatedCount1716` and `preCalculatedCount1951` parameters
   - Uses pre-calculated counts if > 0, otherwise falls back to database query
4. **calculate-counts.sql**: Helper script with COUNT queries to populate appsettings.json

**Usage**:
```bash
# 1. Run count queries (takes ~90 seconds)
# Execute calculate-counts.sql in pgAdmin or Azure Data Studio

# 2. Update appsettings.json with results
{
  "PreCalculatedCounts": {
    "Issue1716": 8456789,
    "Issue1951": 152348912
  }
}

# 3. Run export (no COUNT delay)
dotnet run -- --issue all --output output.csv --cutoff "2026-05-19 11:35:59" --azure-ad -y
```

**Behavior**:
- If pre-calculated count > 0: Uses config value, logs "Using pre-calculated count"
- If pre-calculated count = 0 and not test mode: Queries database (30-60s delay)
- If test mode (--max-batches): Skips count entirely (fast startup)

### Fixed: Query Generation to Match Tested Queries

**Issue**: The generated queries in `FetchStatusRecordsAsync` did not match the tested/validated queries in `docs/database/Test_Export_Query.sql`.

**Changes Made**:

1. **StatusAction String Comparison** (REVERTED)
   - StatusAction is stored as TEXT in the database, not integer
   - Fixed: `StatusAction = '3'` and `StatusAction = '6'` (with quotes)
   - Previous incorrect attempt: Removed quotes thinking it was an integer

2. **Timestamp Filtering by Issue**
   - **Issue #1716**: Uses `SyncedFromAltinn2 < cutoff` (simple less-than)
   - **Issue #1951**: Uses `StatusChanged BETWEEN '2019-03-23' AND cutoff` (range for index selectivity)
   - Applied same logic to both `FetchStatusRecordsAsync` and `GetTotalCountAsync`

3. **SyncedFromAltinn2 Filter Placement**
   - Kept filter in JOIN condition: `AND stats."SyncedFromAltinn2" IS NULL/IS NOT NULL`
   - This matches the tested query structure exactly

4. **Removed createdFilter Parameter**
   - The `corr.Created` filter was removed for performance optimization
   - It caused 3s → 12+ min degradation by filtering AFTER index scan
   - Completely removed `oldestCorrespondenceDate` parameter from entire codebase

### Refactored: GetFiltersForIssue Method

**Improvement**: Centralized all issue-specific query logic in one place for better maintainability.

**Changes**:
- Changed return type from `(string SyncFilter, string TimestampColumn, string CreatedFilter)` to `(string SyncFilter, string TimestampFilter)`
- Now returns complete WHERE clause timestamp filter instead of just column names
- Removed conditional logic from `FetchStatusRecordsAsync` and `GetTotalCountAsync`
- Added comprehensive documentation for each issue's characteristics

**Benefits**:
- Single source of truth for issue-specific filtering
- Easier to maintain - change filters in one place
- More readable - less conditional logic in query methods
- Better documented - each issue's behavior is clearly described

### Removed: oldestCorrespondenceDate Parameter

**Reason**: The `corr.Created` filter was removed in performance optimization. Adding it caused massive query degradation (3s → 12+ minutes) because it filtered rows AFTER the index scan instead of during it.

**Files Changed**:
- `DialogActivityExportService.cs`:
  - Removed parameter from: `ExportToCSVAsync`, `ExportBothToCSVAsync`, `ExportIssueToWriter`, `ProcessBatchAsync`, `FetchStatusRecordsAsync`, `GetTotalCountAsync`, `GetFiltersForIssue`
  - Removed parameter logic from query construction
  - Removed from query logging logic

- `Program.cs`:
  - Removed `--oldest` command-line argument parsing
  - Removed `OldestCorrespondenceDate` property from `ExportOptions` class
  - Removed from method call sites
  - Updated help documentation to remove `--oldest` references
  - Added deprecation warning if `--oldest` is used (logs warning and ignores value)

- `test-export.ps1`:
  - Removed `OldestDate` parameter from script
  - Removed `--oldest` argument construction logic
  - Removed display of OldestDate in configuration output

### Query Performance Notes

From `Test_Export_Query.sql`:

**Why Separate Queries (No UNION ALL)**:
- Separate queries: Status 4 ~21ms + Status 6 ~3s = Total ~3s
- UNION ALL + ORDER BY: 12-40+ minutes (840x slower!)
- With UNION ALL, PostgreSQL must scan millions of rows before applying LIMIT
- Separate queries allow early termination at LIMIT, and in-memory sorting is faster

**Issue #1951 Performance**:
- Records: ~150 million
- Uses `StatusChanged BETWEEN` for index selectivity
- `corr.Created` filter NOT used (causes 3s → 12+ min degradation)

**Issue #1716 Performance**:
- Records: ~7-9 million  
- Uses `SyncedFromAltinn2 < cutoff`
- Much smaller dataset, no need for BETWEEN optimization

### Testing

Build Status: ✅ Successful

**Recommended Test**:
```powershell
cd tools\Altinn.Correspondence.DialogActivityExporter
.\test-export.ps1 -Issue 1951 -MaxBatches 1
```

This will:
- Export first batch (1000 rows) of Issue #1951
- Log the generated queries in test mode
- Verify query structure matches `Test_Export_Query.sql`
- Skip total count for faster startup

**Verify Query Output**:
Check logs for "TEST MODE - Executing query" to see the actual SQL generated and confirm it matches the tested queries.
