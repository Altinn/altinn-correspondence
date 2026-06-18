# Dialog Activity Exporter

Console application for exporting dialog activities from the Correspondence database to CSV format.

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
```

**Features**:
- Resumable: Can be stopped/restarted (checkpoint file tracks progress)
- Progress tracking: Shows processed rows, rate, elapsed time
- Optimized queries: Uses index scans for fast batch processing

### Issue #1716 (Synced Events)
```powershell
# Default: Exports to C:\temp\dialog_activity_export_1716_{timestamp}.csv
.\export-1716-production.ps1

# Custom output path
.\export-1716-production.ps1 -OutputPath D:\exports\issue1716.csv
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
- **Issue #1716**: ~7-15 minutes for ~10M records (~25,000 rows/sec)
- **Issue #1951**: ~2-3 hours for ~190M records (~25,000 rows/sec)

Without indexes, queries will be very slow and may timeout.

## Output Format

CSV with columns:
- DialogId
- DialogActivityId (may be empty)
- Timestamp (ISO 8601)
- ActorId (URN format)
- ActorName
- ActivityType ("CorrespondenceOpened" or "CorrespondenceConfirmed")

## Helper Tables

The export reads from pre-filtered helper tables that were **imported from Altinn 2**:

- **A2Iss1951A2Events**: Contains migrated events (NOT synced from Altinn2) - ~190M rows
  - Status 4 (Read/Opened): ~190M rows
  - Status 6 (Confirmed): ~846K rows

- **A2Iss1716A2Events**: Contains synced events from Altinn2 - ~10M rows
  - Status 4 (Read/Opened): ~9.5M rows
  - Status 6 (Confirmed): ~500K rows

These tables have the required indexes for optimal export performance.

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

- **Production Guide**: `ISSUE-1951-PRODUCTION-EXPORT.md`
- **Quick Reference**: `../docs/Export_Scripts_Quick_Reference.md`
- **Issue #1716 Specs**: `../docs/Issue_1716_Export_Final_Specifications.md`
