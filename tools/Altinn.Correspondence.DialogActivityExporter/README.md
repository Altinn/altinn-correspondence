# Dialog Activity Exporter

Console application for exporting dialog activities from the Correspondence database to CSV format.

## Purpose

Export dialog activity data for two separate data quality issues:
- **Issue #1951**: Migrated events (NOT synced from Altinn2) - ~150M records
- **Issue #1716**: Synced events from Altinn2 - ~7-9M records

## Prerequisites

- .NET 10 SDK
- Access to Correspondence production database
- Required PostgreSQL indexes (see `docs/database/` folder)
- **For Azure AD auth**: Azure CLI installed and logged in (`az login`)

## Authentication

### Option 1: Azure AD (Recommended for Production)

The app can automatically authenticate using your Azure AD credentials:

```powershell
# Make sure you're logged in to Azure CLI
az login

# Run with --azure-ad flag (no connection string needed!)
dotnet run --project . -- `
  --issue all `
  --output "C:\temp\export.csv" `
  --azure-ad `
  --cutoff "2026-05-19 11:35:59" `
  --oldest "2019-03-23"
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
dotnet run -- `
  --issue all `
  --output "C:\temp\export.csv" `
  --connection "Host=server;Database=db;Username=user;Password=pass" `
  --cutoff "2026-05-19 11:35:59"
```

## Building

```powershell
cd tools/Altinn.Correspondence.DialogActivityExporter
dotnet build -c Release
```

## Usage

### Export Both Issues to Single CSV (Recommended)

**With Azure AD (simplest):**
```powershell
dotnet run --project . -- `
  --issue all `
  --output "C:\temp\all_dialog_activities.csv" `
  --azure-ad `
  --cutoff "2026-05-19 11:35:59" `
  --oldest "2019-03-23"
```

**With connection string:**
```powershell
dotnet run --project . -- `
  --issue all `
  --output "C:\temp\all_dialog_activities.csv" `
  --connection "Host=prod-db;Database=correspondence;Username=user;Password=pass" `
  --cutoff "2026-05-19 11:35:59" `
  --oldest "2019-03-23" `
  --batch-size 50000
```

**Note:** Issue #1716 will be exported first (faster), followed by Issue #1951.

### Issue #1951 (Migrated Events Only)

```powershell
dotnet run --project . -- `
  --issue 1951 `
  --output "C:\temp\issue1951_migrated_events.csv" `
  --connection "Host=prod-db;Database=correspondence;Username=user;Password=pass" `
  --cutoff "2026-05-19 11:35:59" `
  --oldest "2019-03-23" `
  --batch-size 50000
```

### Issue #1716 (Synced Events Only)

```powershell
dotnet run --project . -- `
  --issue 1716 `
  --output "C:\temp\issue1716_synced_events.csv" `
  --connection "Host=prod-db;Database=correspondence;Username=user;Password=pass" `
  --cutoff "2026-02-15 00:00:00" `
  --batch-size 50000
```

## Command-Line Arguments

### Required
- `--issue` - Issue number (1951, 1716, or **'all'** to export both)
- `--output` - Output CSV file path
- `--cutoff` - Cutoff timestamp (yyyy-MM-dd HH:mm:ss)

### Connection (choose one)
- `--connection` - PostgreSQL connection string (manual)
- `--azure-ad` - Use Azure AD authentication (automatic, recommended)

### Optional
- `--oldest` - Oldest correspondence date (yyyy-MM-dd HH:mm:ss) - Issue 1951 only
- `--batch-size` - Batch size (default: 50000)
- `-y, --yes` - Skip confirmation prompt
- `-h, --help` - Show help

## Configuration File

You can also use `appsettings.json` for configuration:

```json
{
  "ConnectionString": "Host=prod-db;Database=correspondence;...",
  "BatchSize": 50000
}
```

Then run with fewer arguments:

```powershell
dotnet run -- --issue 1951 --output "C:\temp\issue1951.csv" --cutoff "2026-05-19 11:35:59" --oldest "2019-03-23"
```

## Performance

With proper database indexes:
- **Issue #1716 only**: ~15-30 minutes for 7-9M records
- **Issue #1951 only**: ~30-60 minutes for 150M records
- **Both issues (--issue all)**: ~45-90 minutes for 150-160M records total

Without indexes, queries will take hours and may timeout.

## Output Format

CSV with columns:
- DialogId
- DialogActivityId (may be empty)
- Timestamp (ISO 8601)
- ActorId (URN format)
- ActorName
- ActivityType ("CorrespondenceOpened" or "CorrespondenceConfirmed")

## Database Requirements

Before running, ensure the required indexes are created. See:
- `docs/database/DBA_Index_Creation_Scripts.sql`
- `docs/database/DBA_Index_Request_Executive_Summary.md`

Contact your DBA to create the necessary indexes for optimal performance.

## Troubleshooting

### Connection Issues
- **Azure AD**: Make sure you're logged in (`az login`)
- **Azure AD**: Ensure Azure CLI is in your PATH
- Verify connection string is correct
- Check network access to database
- Ensure user has SELECT permissions

### Token Expiration
- Azure AD tokens expire after 1 hour
- For long exports (>1 hour), token may expire mid-export
- Recommended: Use batching or export separate issues if this occurs

### Performance Issues
- Verify required indexes exist (see database documentation)
- Check batch size (try lower values if memory constrained)
- Monitor database load during export

### Memory Issues
- Reduce batch size (try 25000 or 10000)
- Ensure sufficient disk space for output file

## Monitoring

The tool provides real-time progress with:
- Progress bar
- Current/total row counts
- Processing rate (rows/second)
- Estimated time remaining

Example output:
```
[████████████░░░░░░░░] 47.23% | 70,845,000/150,000,000 | 12,450 rows/sec | ETA: 01:45:32
```
