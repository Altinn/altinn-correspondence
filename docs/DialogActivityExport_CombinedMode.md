# Combined Export Feature
## Export Both Issues to Single CSV File

## Overview

The DialogActivityExporter now supports exporting **both Issue #1716 and #1951** to a single CSV file using `--issue all`.

## Why Combine?

✅ **Single unified dataset** - All dialog activities in one file  
✅ **Simpler processing** - No need to merge CSVs manually  
✅ **Better progress tracking** - See combined progress across both issues  
✅ **Consistent ordering** - Results ordered by CorrespondenceId across both issues  

## Usage

### Export Both Issues

```powershell
dotnet run --project tools/Altinn.Correspondence.DialogActivityExporter -- `
  --issue all `
  --output "C:\temp\all_dialog_activities.csv" `
  --connection "Host=prod-db;Database=correspondence;..." `
  --cutoff "2026-05-19 11:35:59" `
  --oldest "2019-03-23"
```

### How It Works

1. **Phase 1**: Export Issue #1716 (Synced events)
   - Processes ~7-9M records first (faster)
   - Uses `SyncedFromAltinn2 IS NOT NULL` filter

2. **Phase 2**: Export Issue #1951 (Migrated events)
   - Processes ~150M records second
   - Uses `SyncedFromAltinn2 IS NULL` filter

3. **Result**: Single CSV file with all records combined

## Output

The CSV will contain records from both issues:

```csv
DialogId,DialogActivityId,Timestamp,ActorId,ActorName,ActivityType
"uuid1",...  -- From Issue #1716
"uuid2",...  -- From Issue #1716
...
"uuid3",...  -- From Issue #1951
"uuid4",...  -- From Issue #1951
...
```

## Progress Reporting

The progress bar shows combined progress:

```
[████████████░░░░░░░░] 47.23% | 70,845,000/157,000,000 | 12,450 rows/sec | ETA: 01:45:32
```

- **TotalCount**: Combined count from both issues
- **TotalProcessed**: Running total across both issues
- **ETA**: Estimated time for complete export

## Performance

| Mode | Records | Estimated Time |
|------|---------|----------------|
| `--issue 1716` | 7-9M | 15-30 minutes |
| `--issue 1951` | 150M | 30-60 minutes |
| `--issue all` | 157-159M | **45-90 minutes** |

**Note:** With proper indexes. Without indexes, export will take hours.

## Alternative: Separate Files

If you prefer separate files, export individually:

```powershell
# Export Issue #1716
dotnet run -- --issue 1716 --output "C:\temp\issue1716.csv" ...

# Export Issue #1951
dotnet run -- --issue 1951 --output "C:\temp\issue1951.csv" ...

# Later combine manually if needed
Get-Content C:\temp\issue1716.csv, C:\temp\issue1951.csv | 
  Select-Object -Skip 1 | 
  Out-File C:\temp\combined.csv
```

## Implementation Details

### Key Changes

1. **Program.cs**
   - Added `--issue all` option
   - Added `ExportBoth` flag to `ExportOptions`
   - Updated help text with new example

2. **DialogActivityExportService.cs**
   - Added `ExportBothToCSVAsync()` method
   - Added `ExportIssueToWriter()` helper method
   - Progress tracking spans both issues

### Benefits of Implementation

✅ **Sequential processing** - One issue completes before next starts  
✅ **Single file handle** - No need to merge files  
✅ **Consistent error handling** - Single try/catch for entire operation  
✅ **Combined progress** - Real-time tracking across both exports  

## When to Use Each Mode

| Use Case | Recommended Mode |
|----------|------------------|
| **Production bulk export** | `--issue all` |
| **Testing/verification** | Individual issues |
| **Partial re-export** | Individual issues |
| **Different timestamps per issue** | Individual issues |
| **Maximum simplicity** | `--issue all` |

## Example Workflows

### Workflow 1: Single Combined Export
```powershell
# One command, one file, done
dotnet run -- `
  --issue all `
  --output "C:\exports\dialog_activities_$(Get-Date -Format 'yyyyMMdd').csv" `
  --connection $connStr `
  --cutoff "2026-05-19 11:35:59" `
  --oldest "2019-03-23" `
  -y
```

### Workflow 2: Separate with Different Cutoffs
```powershell
# Issue #1716 with earlier cutoff
dotnet run -- --issue 1716 --output "C:\temp\1716.csv" --cutoff "2026-02-15"

# Issue #1951 with later cutoff
dotnet run -- --issue 1951 --output "C:\temp\1951.csv" --cutoff "2026-05-19" --oldest "2019-03-23"

# Manually combine if needed
```

## FAQ

**Q: Will the combined file be properly sorted?**  
A: Each issue is sorted by CorrespondenceId internally. The file will have #1716 records first, then #1951 records, each section sorted.

**Q: Can I filter by issue after export?**  
A: The CSV only contains ActivityType field. Issue #1716 and #1951 both produce the same ActivityType values ("CorrespondenceOpened" or "CorrespondenceConfirmed"). To distinguish between issues, you would need to track the row numbers (Issue #1716 is exported first).

**Q: What if one issue fails?**  
A: The export will fail completely. Use individual issue modes if you need fault tolerance.

**Q: Does this use more memory?**  
A: No, it uses the same streaming approach. Memory usage is constant regardless of total row count.

**Q: Is it slower than separate exports?**  
A: Slightly faster actually, since it only opens one database connection and one file handle.

## Troubleshooting

### Issue: Progress seems stuck
- First issue (#1716) processes first - may appear slow initially
- After ~15-30 min, progress will accelerate for #1951

### Issue: Want to resume interrupted export
- Not currently supported
- Use separate issue exports if you need resume capability

### Issue: Need different parameters per issue
- Use individual exports with `--issue 1716` and `--issue 1951`
- The `--oldest` parameter only applies to Issue #1951 anyway
