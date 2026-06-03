# DialogActivityExporter - Testing Guide

## ⚡ First-Time Setup (Choose One Method)

Before you can run tests, you need to configure database access:

### Method 1: Edit appsettings.json (Recommended)
```powershell
cd tools\Altinn.Correspondence.DialogActivityExporter
notepad appsettings.json
```

Update with your connection string:
```json
{
  "ConnectionString": "Host=your-server;Database=correspondence;Username=user;Password=pass",
  "BatchSize": 50000
}
```

**Tip:** See `appsettings.local.example.json` for a complete example.

### Method 2: Use Azure Authentication

The `--azure-ad` flag uses Azure.Identity SDK, which supports multiple authentication methods:

**Supported Methods (automatically tried in order):**
- Environment variables (service principals)
- Managed Identity (Azure VMs/App Service)
- Visual Studio (Tools → Options → Azure Service Authentication)
- VS Code (Azure Account extension)
- Azure CLI (`az login`)
- Azure PowerShell (`Connect-AzAccount`)

**Setup using Azure CLI:**
```powershell
# Install (if not already installed)
winget install Microsoft.AzureCLI

# Login
az login
```

**Setup using Visual Studio:**
1. Tools → Options → Azure Service Authentication
2. Sign in with your Azure account

**Setup using VS Code:**
1. Install Azure Account extension
2. Ctrl+Shift+P → "Azure: Sign In"

### Method 3: Pass Connection String as Parameter
```powershell
.\test-export.ps1 -ConnectionString "Host=server;Database=correspondence;..."
```

---

## 🚀 Quick Start: Test with Limited Data

### Option A: Use the PowerShell Test Script (Easiest!)
```powershell
cd tools\Altinn.Correspondence.DialogActivityExporter

# Default test (Issue #1951, 2 batches = 2000 rows)
.\test-export.ps1

# Test with just 1 batch (1000 rows)
.\test-export.ps1 -MaxBatches 1

# Test Issue #1716 with 5 batches (25,000 rows)
.\test-export.ps1 -Issue 1716 -BatchSize 5000 -MaxBatches 5

# Test both issues (2 batches per issue)
.\test-export.ps1 -Issue all -MaxBatches 2

# Custom output path
.\test-export.ps1 -OutputPath C:\temp\my_test.csv
```

The script will:
- ✅ Limit export to specified number of batches (default: 2)
- ✅ Generate timestamped output filename automatically
- ✅ Create output directory if needed
- ✅ Show file size and row count after completion

### Option B: Direct Command Line
```powershell
cd tools\Altinn.Correspondence.DialogActivityExporter

# Test Issue #1951 with 2 batches (2000 rows)
dotnet run -- --issue 1951 --output C:\temp\test.csv --cutoff "2026-05-19 11:35:59" --batch-size 1000 --max-batches 2 --azure-ad --yes

# After ~4 seconds, open C:\temp\test.csv to verify format
```

**Expected Output:**
```csv
DialogId,DialogActivityId,Timestamp,ActorId,ActorName,ActivityType
123,abc-def-456,2024-01-15 10:30:00,12345678,Test Org,Read
```

---

## Quick Test with Limited Results (1-10 Batches)

This guide explains how to run a quick test of the DialogActivityExportService with a limited number of batches to verify functionality and output format.

**Note:** Use the `--max-batches` parameter to limit export to N batches for testing (default in test script: 2 batches).

---

## Method 1: Use Max Batches Parameter (Recommended for Quick Tests)

The `--max-batches` parameter limits the export to a specific number of batches, allowing you to test format and function without processing the entire dataset.

### Using the PowerShell Test Script (Recommended)

A convenient test script is provided: `test-export.ps1`

```powershell
# Navigate to exporter directory
cd tools\Altinn.Correspondence.DialogActivityExporter

# Run with defaults (Issue #1951, 2 batches = 2000 rows)
.\test-export.ps1

# Customize parameters
.\test-export.ps1 -Issue 1716 -BatchSize 5000 -MaxBatches 3
```

**Script Features:**
- ✅ Limits export to specified batches (default: 2)
- ✅ Auto-generates timestamped filenames
- ✅ Creates output directories automatically
- ✅ Shows file size and approximate row count
- ✅ Offers to open CSV file after completion
- ✅ Full parameter help: `Get-Help .\test-export.ps1 -Detailed`

### Manual Command Line Examples

#### Test with 1 batch (1000 rows) for Issue #1951:
```powershell
cd tools\Altinn.Correspondence.DialogActivityExporter

dotnet run -- `
  --issue 1951 `
  --output C:\temp\test_export_1951.csv `
  --cutoff "2026-05-19 11:35:59" `
  --oldest "2019-03-23" `
  --batch-size 1000 `
  --max-batches 1 `
  --azure-ad `
  --yes
```

#### Test with 5 batches (25,000 rows) for Issue #1716:
```powershell
dotnet run -- `
  --issue 1716 `
  --output C:\temp\test_export_1716.csv `
  --cutoff "2026-02-15 00:00:00" `
  --batch-size 5000 `
  --max-batches 5 `
  --azure-ad `
  --yes
```

### What This Does:
- **Max Batches**: Service fetches exactly N batches then stops
- **Skip COUNT(*)**: Automatically skips slow total count query (faster startup on billion-row tables)
- **Query Logging**: Logs actual SQL queries with substituted parameters (test mode only)
- **Immediate Results**: Data written to file after each batch (no memory buffering)
- **File Output**: CSV file contains partial results you can immediately verify
- **Test Mode Indicator**: Console shows "TEST MODE: Limited to N batch(es)"

**Benefits:**
- ✅ No need to press Ctrl+C - automatically stops after N batches
- ✅ Instant startup - no waiting for COUNT(*) on 1.94B row table
- ✅ Query verification - see exact SQL being executed with parameters
- ✅ Predictable output size: batch_size × max_batches rows
- ✅ Fast verification: 1-2 batches completes in seconds
- ✅ Memory safe: Data streamed to disk, not buffered in memory

---

## Method 2: Use Recent Cutoff Date (Alternative Approach)

Use a very recent cutoff date to limit the total number of records that match the query (useful if you don't want to use --max-batches).

### Example: Last 24 Hours Only
```powershell
$yesterday = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss")

dotnet run -- `
  --issue 1951 `
  --output C:\temp\test_export_recent.csv `
  --cutoff $yesterday `
  --oldest "2019-03-23" `
  --batch-size 50000 `
  --azure-ad `
  --yes
```

This will only export records with `StatusChanged < yesterday`, which might be a small dataset depending on your production activity.

---

## Method 3: Test Against Local/Dev Database (Smallest Dataset)

If you have a local or development database with a smaller dataset, use that for testing:

```powershell
dotnet run -- `
  --issue 1951 `
  --output C:\temp\test_export_dev.csv `
  --connection "Host=localhost;Database=correspondence_dev;Username=devuser;Password=devpass" `
  --cutoff "2026-12-31 23:59:59" `
  --batch-size 1000 `
  --yes
```

---

## Method 4: Manual SQL Test Query (Verify Data Before Export)

Before running the full export, you can test the query manually in DBeaver/pgAdmin to see exactly how many rows will be returned.

### Issue #1951 Test Query (Status 4 + Status 6):
```sql
-- Test Query: Count records for Issue #1951
SELECT COUNT(*) 
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
    AND corr."SyncedFromAltinn2" IS NULL
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
WHERE stats."Status" IN (4, 6)
  AND stats."StatusChanged" < '2026-05-19 11:35:59'::timestamp
  -- Optional: Add oldest date filter for Issue #1951
  -- AND corr."Created" >= '2019-03-23'::timestamp
;

-- Sample first 100 rows (Status 4):
SELECT 
    corr."Altinn2CorrespondenceId" AS "DialogId",
    stats."Id" AS "DialogActivityId",
    stats."StatusChanged" AS "Timestamp",
    COALESCE(ap."ActorId", 0) AS "ActorId",
    COALESCE(ap."ActorName", '') AS "ActorName",
    CASE stats."Status"
        WHEN 4 THEN 'Read'
        WHEN 6 THEN 'Confirmed'
    END AS "ActivityType"
FROM correspondence."CorrespondenceStatuses" stats
INNER JOIN correspondence."Correspondences" corr 
    ON stats."CorrespondenceId" = corr."Id" 
    AND corr."Altinn2CorrespondenceId" IS NOT NULL 
    AND corr."IsMigrating" = FALSE
    AND corr."SyncedFromAltinn2" IS NULL
INNER JOIN correspondence."A2Parties" ap 
    ON stats."PartyUuid" = ap."PartyUuid"
    AND corr."Recipient" <> ap."RecipientUrn"
WHERE stats."Status" = 4
  AND stats."StatusChanged" < '2026-05-19 11:35:59'::timestamp
ORDER BY stats."CorrespondenceId", stats."Status"
LIMIT 100;
```

This allows you to:
1. **Verify the COUNT**: See how many total rows match before exporting
2. **Inspect sample data**: Review the actual data that will be exported
3. **Adjust cutoff date**: If too many rows, use a more recent cutoff

---

## Output Format Verification

### Expected CSV Format:
```csv
DialogId,DialogActivityId,Timestamp,ActorId,ActorName,ActivityType
123,abc-def-456,2024-01-15 10:30:00,12345678,Test Organization,Read
123,ghi-jkl-789,2024-01-16 14:22:13,12345678,Test Organization,Confirmed
```

### Columns:
- **DialogId**: `Altinn2CorrespondenceId` (integer from Altinn 2)
- **DialogActivityId**: UUID of the CorrespondenceStatus record
- **Timestamp**: `StatusChanged` (Issue #1951) or `SyncedFromAltinn2` (Issue #1716)
- **ActorId**: From `A2Parties.ActorId` (0 if null)
- **ActorName**: From `A2Parties.ActorName` (empty string if null)
- **ActivityType**: "Read" (Status 4) or "Confirmed" (Status 6)

---

## Performance Expectations (with Indexes)

With the optimized indexes in place:

| Dataset | Batch Size | Expected Time | Index Used |
|---------|-----------|---------------|------------|
| **1,000 rows** | 1000 | ~2 seconds | IX_CorrespondenceStatuses_Status_StatusChanged_Migrated |
| **5,000 rows** | 5000 | ~3-5 seconds | IX_CorrespondenceStatuses_Status_StatusChanged_Migrated |
| **10,000 rows** | 10000 | ~5-8 seconds | IX_CorrespondenceStatuses_Status_StatusChanged_Migrated |
| **100,000 rows** | 50000 | ~15-30 seconds | IX_CorrespondenceStatuses_Status_StatusChanged_Migrated |

---

## Troubleshooting

### Issue: "No connection string provided" / "Azure authentication failed"
**Error Message:** `No connection string provided and automatic Azure authentication failed`

**This happens when:** No Azure credentials are configured (Azure CLI, Visual Studio, VS Code, etc.)

**Solutions (choose one):**

#### Option A: Use Connection String Parameter (Quickest)
```powershell
.\test-export.ps1 -ConnectionString "Host=your-server;Database=correspondence;Username=user;Password=pass"
```

#### Option B: Configure appsettings.json (Best for Repeated Tests)
```powershell
# Edit appsettings.json in the exporter directory
cd tools\Altinn.Correspondence.DialogActivityExporter
notepad appsettings.json
```

Add your connection string:
```json
{
  "ConnectionString": "Host=your-server;Database=correspondence;Username=user;Password=pass",
  "BatchSize": 50000
}
```

Then run without parameters:
```powershell
.\test-export.ps1
```

#### Option C: Use Azure Credentials (For Production/Azure Environments)

The `--azure-ad` flag uses the Azure.Identity SDK which automatically tries multiple authentication methods:

**Supported Authentication Methods (in order):**
1. **Environment Variables** (service principals)
2. **Managed Identity** (Azure VMs, App Service, Container Apps)
3. **Visual Studio** (Tools → Options → Azure Service Authentication)
4. **Visual Studio Code** (Azure Account extension)
5. **Azure CLI** (`az login`)
6. **Azure PowerShell** (`Connect-AzAccount`)

**Option C1: Azure CLI (Most Common)**
```powershell
# Install Azure CLI (if needed)
winget install Microsoft.AzureCLI

# Restart terminal, then login
az login

# Run test
.\test-export.ps1
```

**Option C2: Visual Studio Authentication**
```text
1. In Visual Studio: Tools → Options → Azure Service Authentication
2. Sign in with your Azure account
3. Run test: .\test-export.ps1
```

**Option C3: VS Code with Azure Account Extension**
```text
1. Install Azure Account extension in VS Code
2. Sign in to Azure (Ctrl+Shift+P → "Azure: Sign In")
3. Run test: .\test-export.ps1
```

**Benefits:**
- ✅ No need to manage passwords or connection strings
- ✅ Works with multiple authentication methods
- ✅ Automatically refreshes tokens
- ✅ Secure (no credentials in files)

### Issue: Export runs too long
**Solution**: Press **Ctrl+C** to cancel, then check the output file - partial results are already written

### Issue: File is empty or only has header
**Solution**: Check the cutoff date - you might be filtering out all records. Try a future date like `--cutoff "2026-12-31 23:59:59"`

### Issue: "Invalid issue number"
**Solution**: Only 1951, 1716, or "all" are valid. Make sure to use `--issue 1951` not `--issue #1951`

### Issue: "Invalid batch size. Must be >= 1000"
**Solution**: Minimum batch size is 1000 rows. Use `.\test-export.ps1 -BatchSize 1000` or higher

---

## Best Practice: Test Workflow

1. **Run COUNT query** in DBeaver to verify expected row count
2. **Start with batch-size 1000** to quickly verify CSV format
3. **Open CSV file** after first batch completes (~2 seconds)
4. **Verify columns and data** look correct
5. **Press Ctrl+C** if satisfied, or let it finish
6. **Scale up** to full production export with larger batch-size (50000)

---

## Example: Complete Test Session

```powershell
# Navigate to exporter tool
cd C:\Repos\Altinn\altinn-correspondence\tools\Altinn.Correspondence.DialogActivityExporter

# Run quick test with 1000 rows (Issue #1951)
dotnet run -- `
  --issue 1951 `
  --output C:\temp\test_export.csv `
  --cutoff "2026-05-19 11:35:59" `
  --oldest "2019-03-23" `
  --batch-size 1000 `
  --azure-ad `
  --yes

# After ~2 seconds, output file appears: C:\temp\test_export.csv
# Open in Excel/Notepad to verify format

# If format looks good, press Ctrl+C to cancel
# Or let it finish (will process all matching records in batches of 1000)
```

---

## Notes

- **Azure AD Authentication**: Requires Azure CLI (`az login`) to be authenticated
- **Batch Size**: Minimum is 1000 (enforced by the exporter)
- **Progress Bar**: Shows real-time progress with rows/sec and ETA
- **Cursor Pagination**: Ensures consistent results even if database changes during export
- **File Safety**: Output file is written incrementally, so Ctrl+C won't lose data

---

## Related Documentation

- **Index_Creation_Scripts.sql**: Production index creation commands
- **Test_Export_Query.sql**: Manual SQL test queries for DBeaver/pgAdmin
- **Performance_Optimization_Summary.sql**: Query optimization details (840x improvement)
- **README.md**: Overview of database optimization project
