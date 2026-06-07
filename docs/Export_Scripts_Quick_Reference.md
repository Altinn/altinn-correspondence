# Export Scripts Quick Reference

## Test Script (test-export-2.ps1)

**Purpose**: Quick validation with limited data (default: 2 batches × 5,000 rows = 5,000 rows)

### Basic Usage
```powershell
# Quick test with defaults (5,000 rows)
.\test-export-2.ps1

# Test with different batch size
.\test-export-2.ps1 -BatchSize 5000

# Test more batches
.\test-export-2.ps1 -MaxBatches 5

# Test with specific output location
.\test-export-2.ps1 -OutputPath "D:\test\my_test.csv"
```

### Key Features
- ✅ **Fast startup**: No COUNT queries (test mode)
- ✅ **Limited data**: Only exports specified batches
- ✅ **Progress shows**: Processed rows, rate, elapsed time
- ✅ **Default**: 2 batches × 5,000 rows = 5,000 rows total
- ✅ **Duration**: < 10 seconds

### When to Use
- Verify query correctness
- Check CSV output format
- Test performance on small dataset
- Validate cursor pagination
- Debug issues before full export

### Expected Output
```
Batch 1: Fetch=150ms, Merge=4ms, Write=4ms, Total=160ms, Rows=5000
Batch 2: Fetch=300ms, Merge=2ms, Write=2ms, Total=305ms, Rows=5000
Export completed. Total: 5,000 rows in 00:00:00.5
```

---

## Production Script (export-production-1716.ps1)

**Purpose**: Full export of ~9.97M rows for Issue #1716

### Basic Usage
```powershell
# Full production export with defaults
.\export-production-1716.ps1

# Export to specific location
.\export-production-1716.ps1 -OutputPath "D:\exports\issue_1716_final.csv"

# Use specific cutoff date
.\export-production-1716.ps1 -CutoffDate "2026-02-15 14:30:00"

# Use larger batch size (faster, if network is fast)
.\export-production-1716.ps1 -BatchSize 10000

# Start fresh (ignore checkpoint)
.\export-production-1716.ps1 -FreshStart
```

### Key Features
- ✅ **Full export**: All ~9.97M rows
- ✅ **Progress tracking**: Shows percentage, ETA, throughput
- ✅ **Resume support**: Checkpoint file for interruption recovery
- ✅ **Pre-flight checks**: Validates auth, disk space, permissions
- ✅ **Batch size**: 5,000 rows (optimized for network performance)
- ✅ **Estimated time**: 35-50 minutes

### Pre-Flight Checks
Script automatically validates:
1. ✅ Azure AD authentication (or connection string)
2. ✅ Output directory writable
3. ✅ Sufficient disk space (~500 MB needed)

### When to Use
- Full production export of Issue #1716
- After successful test export validation
- When prerequisites are met (indexes, ANALYZE)

### Expected Output
```
[1/3] Azure AD authentication... ✓
      Logged in as: user@domain.com
[2/3] Output directory writable... ✓
[3/3] Disk space... ✓
      Available: 125.5 GB

Processed: 5,234,567 / 9,970,000 (52.5%) | 4,123 rows/sec | ETA: 00:19:23
...
Export completed. Total: 9,970,000 rows in 00:40:15
```

### Resume Support
If export is interrupted:
1. Checkpoint file saved: `{output}.checkpoint.json`
2. Re-run same command → automatically resumes
3. Use `-FreshStart` to start over (ignores checkpoint)

---

## Parameters Comparison

| Parameter | test-export-2.ps1 | export-production-1716.ps1 | Notes |
|-----------|------------------|---------------------------|-------|
| **Issue** | 1716 (default) | 1716 (fixed) | Test can change, prod is fixed |
| **BatchSize** | 5000 (default) | 5000 (default) | Both optimized for performance |
| **MaxBatches** | 2 (default) | N/A (full export) | Test only - limits data |
| **OutputPath** | C:\temp\test_export_... | C:\temp\dialog_activity_export_... | Different naming |
| **CutoffDate** | 2026-02-15 | Current date/time | Test uses fixed, prod uses now |
| **UseAzureAd** | true | true | Both support Azure AD |
| **ConnectionString** | Optional | Optional | Override Azure AD |
| **FreshStart** | N/A | Switch | Production only - ignore checkpoint |

---

## Typical Workflow

### 1. Test Export (First Time)
```powershell
# Quick test to verify everything works
.\test-export-2.ps1

# Expected: 5,000 rows in < 10 seconds
# Verify: CSV format, no errors, timing looks good
```

### 2. Test Export (Larger Sample)
```powershell
# Test with more data to validate throughput
.\test-export-2.ps1 -MaxBatches 10

# Expected: 100,000 rows in < 30 seconds
# Verify: Consistent batch timing (200-500ms each)
```

### 3. Production Export
```powershell
# Full production export
.\export-production-1716.ps1

# Expected: ~9.97M rows in 35-50 minutes
# Monitor: Progress updates, ETA, consistent timing
```

### 4. Production Export (If Interrupted)
```powershell
# Just re-run - will resume automatically
.\export-production-1716.ps1

# Or start fresh
.\export-production-1716.ps1 -FreshStart
```

---

## Performance Expectations

### Test Export (5,000 rows)
| Metric | Expected |
|--------|----------|
| Time per batch | 150-500ms |
| Total time | < 10 seconds |
| Throughput | 2,000-6,000 rows/sec |
| Batches | 2 |

### Production Export (9.97M rows)
| Metric | Expected |
|--------|----------|
| Time per batch | 200-500ms |
| Total time | 35-50 minutes |
| Throughput | 3,000-5,000 rows/sec |
| Batches | ~997 |
| Output size | ~400 MB |

---

## Troubleshooting

### Test Export Slow (>1s per batch)
**Check**:
1. Run EXPLAIN ANALYZE on test queries
2. Verify indexes are being used
3. Check Status 6 index selection

**Fix**:
- Run `ANALYZE correspondence."A2Iss1716A2Events";`
- Consider dropping `IX_A2Iss1716A2Events_Status_CorrId` if Status 6 slow

### Production Export Failed
**Symptoms**:
- Connection timeout
- Azure AD auth failure
- Out of disk space

**Fix**:
1. Check Azure login: `az account show`
2. Re-login: `az login`
3. Free disk space: Need 500 MB minimum
4. Resume: Just re-run the script (uses checkpoint)

### Export Stuck at Same Percentage
**Symptoms**:
- Progress not updating for 5+ minutes
- Same ETA for extended period

**Fix**:
1. Check database server load
2. Verify network connection
3. Look at logs for specific batch timing
4. May need to restart and resume

### Checkpoint File Issues
**Symptoms**:
- Cannot resume after interruption
- Checkpoint file corrupted

**Fix**:
```powershell
# Delete checkpoint and start fresh
Remove-Item "C:\temp\dialog_activity_export_*.checkpoint.json"
.\export-production-1716.ps1 -FreshStart
```

---

## Advanced Usage

### Custom Connection String
```powershell
# Test script
.\test-export-2.ps1 -ConnectionString "Host=mydb.postgres.azure.com;Database=correspondence;Username=myuser;Password=***"

# Production script
.\export-production-1716.ps1 -ConnectionString "Host=mydb.postgres.azure.com;Database=correspondence;Username=myuser;Password=***"
```

### Larger Batch Size (Faster)
```powershell
# Test with 20K batch
.\test-export-2.ps1 -BatchSize 10000

# Production with 20K batch (expect 20-30 minutes)
.\export-production-1716.ps1 -BatchSize 10000
```

### Specific Date Range
```powershell
# Production export up to specific date
.\export-production-1716.ps1 -CutoffDate "2026-01-31 23:59:59"
```

---

## File Locations

### Scripts
- **Test**: `tools/Altinn.Correspondence.DialogActivityExporter/test-export-2.ps1`
- **Production**: `tools/Altinn.Correspondence.DialogActivityExporter/export-production-1716.ps1`

### Default Output
- **Test**: `C:\temp\test_export_1716_{timestamp}.csv`
- **Production**: `C:\temp\dialog_activity_export_1716_{timestamp}.csv`

### Checkpoint Files
- **Format**: `{output_path}.checkpoint.json`
- **Example**: `C:\temp\dialog_activity_export_1716_20260605_110000.csv.checkpoint.json`
- **Auto-deleted**: On successful completion
- **Preserved**: On failure (for resume)

---

## Quick Decision Tree

```
Need to validate query/format?
├─ YES → Use test-export-2.ps1 (default: 2 batches)
└─ NO → Continue

Need to test throughput/performance?
├─ YES → Use test-export-2.ps1 with -MaxBatches 10
└─ NO → Continue

Ready for full production export?
├─ YES → Use export-production-1716.ps1
└─ NO → Go back to testing

Export interrupted?
├─ YES → Just re-run export-production-1716.ps1 (auto-resume)
└─ NO → Normal completion

Want to start over?
├─ YES → Use export-production-1716.ps1 -FreshStart
└─ NO → Use checkpoint resume
```

---

## Summary

| Script | Purpose | Rows | Time | Use When |
|--------|---------|------|------|----------|
| **test-export-2.ps1** | Validation | 20K | <10s | Testing queries, format, performance |
| **export-production-1716.ps1** | Production | 9.97M | 35-50m | Full export with monitoring |

Both scripts use **batch size 5,000** for optimal network performance with Azure PostgreSQL.
