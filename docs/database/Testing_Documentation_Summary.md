# Testing Documentation - Summary

## Date: 2024-01-XX
## Purpose: Enable quick testing of DialogActivityExportService with limited results

---

## ✅ New Files Created

### 1. Testing_Guide.md
**Location:** `docs/database/Testing_Guide.md`

**Purpose:** Comprehensive guide for testing the DialogActivityExportService with limited datasets (1000-10000 rows)

**Contents:**
- 🚀 Quick start examples (PowerShell script + direct commands)
- 4 testing methods:
  1. Small batch size (1000-5000 rows) - Recommended
  2. Recent cutoff date (smaller dataset)
  3. Local/dev database (smallest dataset)
  4. Manual SQL queries (verify before export)
- Output format verification
- Expected performance metrics
- Troubleshooting guide
- Complete test workflow best practices

**Note:** Minimum batch size is 1000 rows (enforced by DialogActivityExporter)

**Quick Access:**
```powershell
cd C:\Repos\Altinn\altinn-correspondence
code docs\database\Testing_Guide.md
```

---

### 2. test-export.ps1
**Location:** `tools/Altinn.Correspondence.DialogActivityExporter/test-export.ps1`

**Purpose:** Convenient PowerShell script for quick testing with sensible defaults

**Features:**
- ✅ Auto-generates timestamped output filenames
- ✅ Creates output directory if needed
- ✅ Shows file size and row count after completion
- ✅ Offers to open CSV file in default viewer
- ✅ Full parameter help with examples
- ✅ Validates parameters (Issue: 1951/1716/all, BatchSize: 100-100000)

**Usage:**
```powershell
# Navigate to exporter tool
cd tools\Altinn.Correspondence.DialogActivityExporter

# Simple test (defaults to Issue #1951, 1000 rows)
.\test-export.ps1

# Test with different parameters
.\test-export.ps1 -Issue 1716 -BatchSize 5000

# Custom output path
.\test-export.ps1 -OutputPath C:\temp\my_test.csv

# View detailed help
Get-Help .\test-export.ps1 -Detailed
```

**Parameters:**
- `-Issue` (1951|1716|all) - Default: 1951
- `-BatchSize` (1000-100000) - Default: 1000
- `-OutputPath` - Default: Auto-generated timestamped file
- `-CutoffDate` - Default: "2026-05-19 11:35:59"
- `-OldestDate` - Default: "2019-03-23" (Issue #1951 only)
- `-UseAzureAd` - Default: true
- `-ConnectionString` - Optional, overrides Azure AD

**Note:** Minimum batch size is 1000 rows (enforced by DialogActivityExporter)

---

## 📝 Updated Files

### README.md
**Location:** `docs/database/README.md`

**Changes:**
- Added Testing_Guide.md to documentation index
- Updated section numbering (4→5, 5→6, etc.)
- Added "Testing & Verification" section with 🧪 emoji

**New Section:**
```markdown
### Testing & Verification

5. **[Testing_Guide.md](Testing_Guide.md)** 🧪 **Testing Made Easy**
   - Quick test with limited results (100-1000 rows)
   - Verify output format before full export
   - Multiple testing methods (batch size, cutoff date, SQL queries)
   - Troubleshooting common issues
```

---

## 🎯 Testing Workflow

### Quick Test (2 seconds)
```powershell
cd tools\Altinn.Correspondence.DialogActivityExporter
.\test-export.ps1
# Opens CSV file automatically - verify format looks correct
```

### Verify Output Format
Expected CSV structure:
```csv
DialogId,DialogActivityId,Timestamp,ActorId,ActorName,ActivityType
123,abc-def-456,2024-01-15 10:30:00,12345678,Test Organization,Read
123,ghi-jkl-789,2024-01-16 14:22:13,12345678,Test Organization,Confirmed
```

### Scale to Production
Once format is verified:
```powershell
# Full production export with 50,000 row batches
dotnet run -- `
  --issue all `
  --output C:\exports\full_export.csv `
  --cutoff "2026-05-19 11:35:59" `
  --oldest "2019-03-23" `
  --batch-size 50000 `
  --azure-ad `
  --yes
```

---

## 📊 Benefits

✅ **Fast verification** - Test in seconds instead of waiting for full export  
✅ **Safe experimentation** - Small batch sizes minimize database impact  
✅ **Format validation** - Catch CSV structure issues before multi-hour exports  
✅ **Flexible testing** - Multiple methods for different scenarios  
✅ **User-friendly** - PowerShell script handles common tasks automatically  
✅ **Well-documented** - Comprehensive guide with examples and troubleshooting  

---

## 🔗 Related Documentation

- **Testing_Guide.md** - Complete testing documentation
- **Index_Creation_Production_Summary.md** - Production index results
- **Performance_Optimization_Summary.sql** - Query optimization details (840x improvement)
- **Test_Export_Query.sql** - Manual SQL test queries for DBeaver

---

## 📂 Final Documentation Structure (9 files)

1. **README.md** - Central navigation hub
2. **Testing_Guide.md** - Testing documentation (NEW) 🧪
3. **Index_Creation_Scripts.sql** - Production index creation
4. **Index_Creation_Production_Summary.md** - Actual timings (5h 24m)
5. **Test_Export_Query.sql** - Manual SQL test queries
6. **Performance_Optimization_Summary.sql** - Optimization journey
7. **Query_Performance_Analysis.md** - EXPLAIN ANALYZE details
8. **Database_Migration_Analysis.md** - Migration approach
9. **Index_Analysis_Notes.md** - Technical index analysis

**Plus:** `test-export.ps1` PowerShell script in `tools/` directory

---

## ✅ Verification

Build status: ✅ **Successful**  
All files created: ✅ **2 new files**  
Documentation updated: ✅ **README.md**  
Ready for testing: ✅ **Yes**

---

## 🚀 Next Steps

1. **Try the test script:**
   ```powershell
   cd tools\Altinn.Correspondence.DialogActivityExporter
   .\test-export.ps1
   ```

2. **Verify output format** matches expectations

3. **Run production export** with confidence once format is validated

4. **Share Testing_Guide.md** with team members who need to run exports
