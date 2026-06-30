# Dialog Activity Exporter

Console application for exporting dialog activities from the Correspondence database to CSV format.

## ⚠️ POST-EXPORT CLEANUP REQUIRED

**IMPORTANT**: After completing both exports, the following cleanup tasks must be performed manually to reclaim disk space and remove temporary infrastructure.

### TODO: Cleanup Checklist

#### 1. Drop Helper Tables (After Export Completion)
```sql
-- Drop Issue #1716 helper table (~10M rows, imported from Altinn 2)
DROP TABLE IF EXISTS correspondence."A2Iss1716A2Events";

-- Drop Issue #1951 helper table (~191M rows, imported from Altinn 2)
DROP TABLE IF EXISTS correspondence."A2Iss1951A2Events";

-- Drop A2Parties helper table (used for actor name lookups)
DROP TABLE IF EXISTS correspondence."A2Parties";
```

**Estimated space reclaimed**: ~50-60 GB

**Verification**:
```sql
-- Verify tables are dropped
SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
FROM pg_tables 
WHERE schemaname = 'correspondence' 
  AND tablename IN ('A2Iss1716A2Events', 'A2Iss1951A2Events', 'A2Parties');
```

#### 2. Drop Unused Indexes on CorrespondenceStatuses (Not Used in Final Export)
```sql
-- These indexes were created during development but are NOT used by the final export
-- The final export uses helper tables (A2Iss1716A2Events, A2Iss1951A2Events) instead

-- Drop Issue #1716 index (created in development, not used in final)
DROP INDEX CONCURRENTLY IF EXISTS correspondence."IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced";

-- Drop Issue #1951 index (created in development, not used in final)
DROP INDEX CONCURRENTLY IF EXISTS correspondence."IX_CorrespondenceStatuses_Status_StatusChanged_Migrated";
```

**Estimated space reclaimed**: ~27 GB (Index #1: ~3 GB, Index #2: ~24 GB)

**Verification**:
```sql
-- Verify indexes are dropped
SELECT 
    schemaname,
    tablename,
    indexname,
    pg_size_pretty(pg_relation_size(schemaname||'.'||indexname)) as size
FROM pg_indexes
WHERE schemaname = 'correspondence'
  AND indexname IN (
      'IX_CorrespondenceStatuses_Status_SyncedTimestamp_Synced',
      'IX_CorrespondenceStatuses_Status_StatusChanged_Migrated'
  );
```

#### 3. Drop Indexes on Helper Tables (After Dropping Tables)
```sql
-- Indexes on A2Iss1716A2Events (automatically dropped with table, but listed for reference)
-- - IX_A2Iss1716A2Events_Status_CorrId
-- - IX_A2Iss1716A2Events_CorrId_Status_Party

-- Indexes on A2Iss1951A2Events (automatically dropped with table, but listed for reference)
-- - IX_A2Iss1951A2Events_Status_CorrId
-- - IX_A2Iss1951A2Events_CorrId_Status_Party

-- Note: These indexes are automatically dropped when their parent tables are dropped
```

#### 4. Vacuum and Analyze After Cleanup

**⚠️ CRITICAL WARNING**: `VACUUM FULL` takes an **exclusive lock** on the table and will **block all queries** (reads and writes) for the duration of the operation. On a 1.94 billion row table like CorrespondenceStatuses, this can take **several hours** and will cause **production outage**.

**Recommended Approach** (choose one):

**Option A: Non-Blocking VACUUM (Recommended for Production)**
```sql
-- Non-blocking, safe for production (reclaims space gradually)
VACUUM correspondence."CorrespondenceStatuses";
ANALYZE correspondence."CorrespondenceStatuses";
```
- ✅ No locks, production traffic continues
- ✅ Safe to run anytime
- ⚠️ May not reclaim all space immediately

**Option B: VACUUM FULL During Maintenance Window (Maximum Space Reclaim)**
```sql
-- BLOCKS ALL ACCESS - Only run during scheduled maintenance window!
VACUUM FULL correspondence."CorrespondenceStatuses";
ANALYZE correspondence."CorrespondenceStatuses";
```
- ⚠️ **Requires maintenance window** (several hours)
- ⚠️ **Blocks all queries** during operation
- ✅ Reclaims maximum disk space
- ✅ Rebuilds table and indexes optimally

**Total estimated space reclaimed**: ~80-90 GB

---

## Purpose

Export dialog activity data for two separate data quality issues:
- **Issue #1951**: Migrated events (NOT synced from Altinn2) - ~190M records
- **Issue #1716**: Synced events from Altinn2 - ~10M records

**Important**: The data is exported from helper tables (`A2Iss1951A2Events` and `A2Iss1716A2Events`) that were **imported from Altinn 2**. These tables are pre-filtered and contain only the relevant events for each issue.

## Prerequisites

- .NET 10 SDK
- Access to Correspondence production database
- Required PostgreSQL indexes on helper tables
- **For Azure AD auth**: Azure credentials (Azure CLI, Visual Studio, VS Code, managed identity, or environment variables)

## Authentication

### Option 1: Azure AD (Recommended for Production)

The app uses Azure.Identity's DefaultAzureCredential which automatically tries multiple authentication methods:

**Supported Credential Sources (tried in order):**
- Environment variables (for service principals/automation)
- Managed Identity (when running in Azure)
- Visual Studio (Tools → Options → Azure Service Authentication)
- VS Code (Azure Account extension)
- Azure CLI (`az login`)

**Example - Azure CLI:**
```powershell
# Login to Azure CLI
az login

# Run production script with Azure AD (no connection string needed!)
.\export-1951-production.ps1
```

**How it works:**
- Detects your Windows username (e.g., `rfatl`)
- Generates access token using Azure CLI: `az account get-access-token --resource-type oss-rdbms`
- Builds connection string automatically:
  - Host: `altinn-corr-prod-dbserver.postgres.database.azure.com`
  - Database: `correspondence`
  - Username: `{your-username}@ai-dev.no`
  - Password: Azure AD access token

### Option 2: Manual Connection String

You can also provide a connection string manually:

```powershell
.\export-1951-production.ps1 -ConnectionString "Host=server;Database=db;Username=user;Password=pass"
```

## Building

```powershell
cd tools/Altinn.Correspondence.DialogActivityExporter
dotnet build -c Release
```

## Production Export Scripts

For production use, we provide dedicated PowerShell scripts:

### Issue #1951 (Migrated Events)
```powershell
# Default: Exports to C:\temp\dialog_activity_export_1951_{timestamp}.csv
.\export-1951-production.ps1

# Custom output path
.\export-1951-production.ps1 -OutputPath D:\exports\issue1951.csv

# Larger batch size for faster export
.\export-1951-production.ps1 -BatchSize 10000

# Adjust throttle delay (default 1000ms) to avoid Azure throttling
.\export-1951-production.ps1 -ThrottleDelayMs 5000

# Resume from checkpoint (use same output path as original export)
.\export-1951-production.ps1 -OutputPath C:\temp\dialog_activity_export_1951_20260618_093638.csv
```

**Features**:
- Resumable: Can be stopped/restarted (checkpoint file tracks progress)
- Progress tracking: Shows processed rows, rate, elapsed time
- Optimized queries: Uses index scans for fast batch processing
- Throttle mitigation: Configurable delay between batches to avoid Azure rate limiting

### Issue #1716 (Synced Events)
```powershell
# Default: Exports to C:\temp\dialog_activity_export_1716_{timestamp}.csv
.\export-1716-production.ps1

# Custom output path
.\export-1716-production.ps1 -OutputPath D:\exports\issue1716.csv

# Adjust throttle delay for Azure database throttling
.\export-1716-production.ps1 -ThrottleDelayMs 5000

# Resume from checkpoint
.\export-1716-production.ps1 -OutputPath C:\temp\dialog_activity_export_1716_20260618_093638.csv
```

## Test Scripts

For quick testing with limited data:

```powershell
# Test Issue #1951 (2 batches, ~5000 rows)
.\test-export-Issue1951.ps1

# Test Issue #1716 (2 batches, ~5000 rows)
.\test-export-Issue1716.ps1

# More batches for verification
.\test-export-Issue1951.ps1 -MaxBatches 5
```

## Direct Command-Line Usage

You can also run the application directly with `dotnet run`:

```powershell
# Issue #1951
dotnet run -- --issue 1951 --output C:\temp\export.csv --azure-ad

# Issue #1716
dotnet run -- --issue 1716 --output C:\temp\export.csv --azure-ad

# With custom batch size
dotnet run -- --issue 1951 --output C:\temp\export.csv --azure-ad --batch-size 10000

# Limit batches for testing
dotnet run -- --issue 1951 --output C:\temp\export.csv --azure-ad --max-batches 2
```

### Command-Line Arguments

**Required**:
- `--issue` - Issue number (1951 or 1716)
- `--output` - Output CSV file path

**Connection (choose one)**:
- `--azure-ad` - Use Azure AD authentication (automatic, recommended)
- `--connection` - PostgreSQL connection string (manual)

**Optional**:
- `--batch-size` - Batch size (default: 5000)
- `--throttle-delay` - Delay in ms between fast batches (default: 1000, 0=disabled)
- `--max-batches` - Limit export to N batches (for testing)
- `-f, --fresh` - Force fresh start, ignore existing checkpoint
- `-y, --yes` - Skip confirmation prompt
- `-h, --help` - Show help

## Configuration File

You can also use `appsettings.json` for configuration:

```json
{
  "ConnectionString": "Host=prod-db;Database=correspondence;...",
  "BatchSize": 5000
}
```

Then run with fewer arguments:

```powershell
dotnet run -- --issue 1951 --output C:\temp\issue1951.csv --azure-ad
```

## Performance

With proper database indexes on helper tables:
- **Issue #1716**: ~7-15 minutes for ~10M records (~25,000 rows/sec without throttling)
  - With default 1s throttle delay: ~2-3 hours (avoids Azure rate limiting)
- **Issue #1951**: ~2-3 hours for ~190M records (~25,000 rows/sec without throttling)
  - With default 1s throttle delay: ~3-5 hours (avoids Azure rate limiting)

**Note**: Azure PostgreSQL Flexible Server may throttle sustained high-speed exports. The `--throttle-delay` parameter adds a configurable delay between batches to avoid hitting Azure IOPS/network limits. Default is 1000ms (1 second). Increase to 5000ms (5 seconds) if throttling still occurs.

Without indexes, queries will be very slow and may timeout.

## Output Format

CSV with columns:
- DialogId
- DialogActivityId (may be empty)
- Timestamp (ISO 8601)
- ActorId (URN format)
- ActorName
- ActivityType ("CorrespondenceOpened" or "CorrespondenceConfirmed")

## Issue Documentation

### Issue #1716: Synced Events from Altinn2

**Data Source**: Events that were synced from Altinn 2 to Correspondence database.

#### Summary

| Metric | Value |
|--------|-------|
| **Total Rows** | ~9,970,000 |
| **Status 4 (Opened)** | ~9.5M rows |
| **Status 6 (Confirmed)** | ~500K rows |
| **Helper Table** | `A2Iss1716A2Events` (imported from Altinn 2) |
| **Export Time (1s throttle)** | ~2-3 hours (recommended) |
| **Export Time (no throttle)** | ~9-12 minutes (may hit Azure throttling) |
| **Output Size** | ~2.0 GB |
| **Batch Size** | 5,000 rows (optimal) |
| **Total Batches** | ~1,994 |

#### Helper Table Structure
```sql
CREATE TABLE correspondence."A2Iss1716A2Events" (
    "CorrespondenceId" uuid NOT NULL,
    "PartyUuid" uuid NOT NULL,
    "Status" integer NOT NULL,
    "Timestamp" timestamp with time zone NOT NULL,
    "Source" integer -- 0=ServiceEngine, 1=Archive (for troubleshooting only)
);
```

#### Indexes
```sql
-- Primary lookup by Status and CorrespondenceId
CREATE INDEX "IX_A2Iss1716A2Events_Status_CorrId"
ON "A2Iss1716A2Events" ("Status", "CorrespondenceId")
INCLUDE ("PartyUuid", "Timestamp");

-- Composite covering index for joins
CREATE INDEX "IX_A2Iss1716A2Events_CorrId_Status_Party"
ON "A2Iss1716A2Events" ("CorrespondenceId", "Status", "PartyUuid")
INCLUDE ("Timestamp");
```

#### Production Script
```powershell
# Default export (recommended)
.\export-1716-production.ps1

# Custom output path
.\export-1716-production.ps1 -OutputPath D:\exports\issue1716.csv

# Adjust throttle delay if Azure throttling occurs
.\export-1716-production.ps1 -ThrottleDelayMs 5000

# Resume from checkpoint
.\export-1716-production.ps1 -OutputPath C:\temp\dialog_activity_export_1716_20260618_093638.csv
```

#### Test Script
```powershell
# Test with 2 batches (~10,000 rows)
.\test-export-Issue1716.ps1

# Test with more batches
.\test-export-Issue1716.ps1 -MaxBatches 5
```

---

### Issue #1951: Migrated Events (NOT Synced from Altinn2)

**Data Source**: Events that were migrated to Correspondence database but NOT synced from Altinn 2.

#### Summary

| Metric | Value |
|--------|-------|
| **Total Rows** | ~190,846,000 |
| **Status 4 (Opened)** | ~190M rows |
| **Status 6 (Confirmed)** | ~846K rows |
| **Helper Table** | `A2Iss1951A2Events` (imported from Altinn 2) |
| **Export Time (1s throttle)** | ~3-5 hours (recommended) |
| **Export Time (no throttle)** | ~2-3 hours (may hit Azure throttling) |
| **Output Size** | ~40-50 GB |
| **Batch Size** | 5,000 rows (optimal) |
| **Total Batches** | ~38,170 |

#### Helper Table Structure
```sql
CREATE TABLE correspondence."A2Iss1951A2Events" (
    "CorrespondenceId" uuid NOT NULL,
    "PartyUuid" uuid NOT NULL,
    "Status" integer NOT NULL,
    "Timestamp" timestamp with time zone NOT NULL,
    "Source" integer -- 0=ServiceEngine, 1=Archive (for troubleshooting only)
);
```

#### Indexes
```sql
-- Primary lookup by Status and CorrespondenceId
CREATE INDEX "IX_A2Iss1951A2Events_Status_CorrId"
ON "A2Iss1951A2Events" ("Status", "CorrespondenceId")
INCLUDE ("PartyUuid", "Timestamp");

-- Composite covering index for joins
CREATE INDEX "IX_A2Iss1951A2Events_CorrId_Status_Party"
ON "A2Iss1951A2Events" ("CorrespondenceId", "Status", "PartyUuid")
INCLUDE ("Timestamp");
```

#### Production Script
```powershell
# Default export (recommended)
.\export-1951-production.ps1

# Custom output path
.\export-1951-production.ps1 -OutputPath D:\exports\issue1951.csv

# Larger batch size (if network permits)
.\export-1951-production.ps1 -BatchSize 10000

# Adjust throttle delay
.\export-1951-production.ps1 -ThrottleDelayMs 5000

# Resume from checkpoint
.\export-1951-production.ps1 -OutputPath C:\exports\dialog_activity_1951.csv
```

#### Test Script
```powershell
# Test with 2 batches (~10,000 rows)
.\test-export-Issue1951.ps1

# Test with more batches
.\test-export-Issue1951.ps1 -MaxBatches 5
```

---

### A2Parties Helper Table

**Purpose**: Provides actor names for party UUIDs in exported data.

#### Structure
```sql
CREATE TABLE correspondence."A2Parties" (
    "PartyUuid" uuid PRIMARY KEY,
    "OutputActorId" text NOT NULL,
    "Name" text NOT NULL
);
```

**Note**: This table is used by both Issue #1716 and #1951 exports for actor name lookups. It should be dropped after both exports are complete (see cleanup checklist above).

## Checkpoint/Resume Feature

Both production scripts support resumable exports:

- **Checkpoint file**: `{output_path}.checkpoint.json`
- Saved after every batch
- Automatically resumes if interrupted
- Use `-FreshStart` flag to ignore checkpoint and restart

Example checkpoint:
```json
{
  "TotalProcessed": 50000000,
  "LastStatus4CorrespondenceId": "12345678-1234-1234-1234-123456789abc",
  "LastStatus6CorrespondenceId": "87654321-4321-4321-4321-cba987654321",
  "CheckpointTime": "2026-06-16T14:30:45"
}
```

## Troubleshooting

### Connection Issues
- **Azure AD**: Make sure you're logged in (`az login`)
- **Azure AD**: Ensure Azure CLI is in your PATH
- Verify connection string is correct
- Check network access to database
- Ensure user has SELECT permissions on helper tables

### Token Expiration
- Azure AD tokens expire after 1 hour
- For long exports (>1 hour), token may expire mid-export
- Solution: Script will prompt for re-authentication or use service principal

### Performance Issues
- Verify required indexes exist on helper tables
- Check batch size (default 5000 is optimal for most cases)
- Monitor database load during export
- **Azure throttling**: If batches suddenly slow down after first 10-20 batches, increase `--throttle-delay` to 5000ms or higher

### Azure Database Throttling
- Azure PostgreSQL Flexible Server has IOPS and network throughput limits
- Sustained high-speed exports may trigger throttling after ~80K-100K rows
- Symptoms: First batches fast (150-200ms), then suddenly slow (30-60 seconds)
- Solution: Increase `-ThrottleDelayMs` parameter (try 5000ms)
- To disable throttling mitigation: `-ThrottleDelayMs 0` (not recommended for large exports)

### Memory Issues
- Reduce batch size (try 2500 or 1000)
- Ensure sufficient disk space for output file (~5-10 GB for Issue #1951)

## Monitoring

The tool provides real-time progress:
- Processed row count
- Processing rate (rows/second)
- Elapsed time

Example output:
```
Processed: 50,000,000 | 25,000 rows/sec | Elapsed: 00:33:20
```

## Related Documentation

This README contains comprehensive documentation for both Issue #1716 and #1951, consolidating all information in one place.

## Diagnostic Tools

SQL files for troubleshooting and analysis (in this folder):
- **`calculate-counts.sql`** - Calculate total row counts for validation and progress estimates
- **`diagnose-query-performance.sql`** - Diagnose slow query performance, check indexes, get EXPLAIN plans
- **`find-duplicate-source.sql`** - Find duplicate records in helper tables or related tables

**Database reference files** (in `docs/database/`):
- **`Optimize_A2Iss1716A2Events_Indexes.sql`** - Index definitions for Issue #1716 helper table (ACTIVE - used in production)
- **`Check_Disk_Space_And_Table_Stats.sql`** - Check disk space, table statistics, and index health
- **`Performance_Optimization_Summary.sql`** - Historical documentation of optimization decisions (reference only)
- **`Index_Creation_Scripts.sql`** - **HISTORICAL ONLY** - Unused indexes on CorrespondenceStatuses (see cleanup TODO)
- **`Test_Export_Query.sql`** - Test query examples for validation

**Note**: Helper table creation scripts removed - helper tables (`A2Iss1716A2Events`, `A2Iss1951A2Events`) were imported from Altinn 2, not created from Correspondence database.
