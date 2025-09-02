# Detailed Correspondence Statistics Report

This implementation generates comprehensive statistics reports with detailed correspondence data per service owner in parquet format.

## Features Implemented

✅ **Detailed Statistics Report Handler** - Generates comprehensive correspondence data grouped by service owner  
✅ **Parquet File Generation** - Uses Parquet.Net to create structured data files  
✅ **API Endpoints** - Manual trigger endpoints for testing  
✅ **Direct ServiceOwnerId Usage** - Uses the new ServiceOwnerId field from database entities  
✅ **Local File Storage** - Stores reports in `./reports/` directory  
✅ **Comprehensive Data** - Includes all correspondence details, not just counts  

## How to Test

### 1. Generate a Statistics Report

Make a POST request to generate a new report:

```bash
POST /correspondence/api/v1/statistics/generate-report
# No authentication required
```

**Response:**
```json
{
  "filePath": "C:\\path\\to\\reports\\correspondence_detailed_report_20250127_143022_Development.parquet",
  "serviceOwnerCount": 5,
  "totalCorrespondenceCount": 150,
  "generatedAt": "2025-01-27T14:30:22.123Z",
  "environment": "Development",
  "fileSizeBytes": 8192
}
```

### 2. Generate Daily Summary Report

Generate a daily summary report with aggregated data per service owner per day. Each row represents one day's usage for one service owner:

```bash
POST /correspondence/api/v1/statistics/generate-daily-summary
# No authentication required
# No request body or parameters needed
```

**Response:**
```json
{
  "filePath": "C:\\path\\to\\reports\\daily_summary_report_20250127_143022_Development.parquet",
  "serviceOwnerCount": 5,
  "totalCorrespondenceCount": 150,
  "generatedAt": "2025-01-27T14:30:22.123Z",
  "environment": "Development",
  "fileSizeBytes": 4096
}
```

### 3. Generate Statistics Summary

Generate a summary with correspondence counts per service owner. This endpoint automatically generates a new detailed report and then creates an in-memory summary from it:

```bash
POST /correspondence/api/v1/statistics/generate-summary
# No authentication required
# No request body or parameters needed
```

**Response:**
```json
{
  "serviceOwnerSummaries": [
    {
      "serviceOwnerId": "123456789",
      "serviceOwnerName": "Test Organization",
      "correspondenceCount": 45,
      "percentageOfTotal": 30.0,
      "uniqueResourceCount": 3,
      "mostRecentCorrespondence": "2025-01-27T10:30:00Z"
    }
  ],
  "totalCorrespondences": 150,
  "totalServiceOwners": 5,
  "generatedAt": "2025-01-27T14:30:22.123Z",
  "environment": "Development",
  "dateRange": {
    "from": "2025-01-01T00:00:00Z",
    "to": "2025-01-27T14:30:22Z"
  }
}
```

### 4. List Available Reports

Get a list of all generated report files:

```bash
GET /correspondence/api/v1/statistics/reports
# No authentication required
```

**Response:**
```json
[
  {
    "fileName": "correspondence_detailed_report_20250127_143022_Development.parquet",
    "filePath": "C:\\path\\to\\reports\\correspondence_detailed_report_20250127_143022_Development.parquet",
    "size": 8192,
    "created": "2025-01-27T14:30:22.000Z",
    "lastModified": "2025-01-27T14:30:22.000Z"
  }
]
```

### 5. Download a Report File

Download a specific report file:

```bash
GET /correspondence/api/v1/statistics/download/{fileName}
# No authentication required
```

This will return the parquet file as a binary download.

## Data Structure

### Daily Summary Report Structure

The daily summary parquet files contain aggregated data with the following fields:

| Field | Type | Description |
|-------|------|-------------|
| `Date` | string | Date in YYYY-MM-DD format |
| `Year` | int | Year (YYYY) |
| `Month` | int | Month (MM) |
| `Day` | int | Day (DD) |
| `ServiceOwnerId` | string | Service Owner ID (organization number) |
| `ServiceOwnerName` | string | Service Owner Name (for readability) |
| `MessageSender` | string | Message sender |
| `ResourceId` | string | Resource ID |
| `MessageCount` | int | Number of messages/correspondences for this service owner on this date |
| `DatabaseStorageBytes` | long | Total database storage used (metadata) in bytes |
| `AttachmentStorageBytes` | long | Total attachment storage used in bytes |

**Example Daily Summary Data:**
```parquet
Date       | Year | Month | Day | ServiceOwnerId | ServiceOwnerName | MessageSender | ResourceId | MessageCount | DatabaseStorageBytes | AttachmentStorageBytes
2025-01-15 | 2025 | 1     | 15  | 987654321     | Test Org         | sender1      | resource1  | 45          | 46080               | 0
2025-01-15 | 2025 | 1     | 15  | 123456789     | Another Org      | sender2      | resource2  | 23          | 23552               | 0
2025-01-16 | 2025 | 1     | 16  | 987654321     | Test Org         | sender1      | resource1  | 52          | 53248               | 0
```

### Detailed Correspondence Report Structure

The detailed correspondence parquet files contain information for each individual correspondence with the following fields:

| Field | Type | Description |
|-------|------|-------------|
| `CorrespondenceId` | string | Unique identifier for the correspondence |
| `ServiceOwnerId` | string | Service Owner ID from the database ServiceOwnerId field |
| `ServiceOwnerName` | string | Service owner name (looked up from ServiceOwner table) |
| `ResourceId` | string | Resource ID for the correspondence |
| `Sender` | string | Correspondence sender (URN format) |
| `Recipient` | string | Correspondence recipient |
| `SendersReference` | string | Sender's reference for the correspondence |
| `Created` | DateTimeOffset | When the correspondence was created |
| `RequestedPublishTime` | DateTimeOffset | When the correspondence was requested to be published |
| `ReportDate` | DateTimeOffset | When this report was generated |
| `Environment` | string | Environment name (Development, Test, Production) |
| `ServiceOwnerMigrationStatus` | int | Migration status (0: pending, 1: completed with owner, 2: completed without) |

## Service Owner ID Usage

The system now uses the direct `ServiceOwnerId` field from the database entities:

- **Source**: Direct field from `CorrespondenceEntity.ServiceOwnerId` 
- **Format**: Organization number without prefix (e.g., `"987654321"`)
- **Lookup**: Service owner names are retrieved from the `ServiceOwner` table using the ID
- **Migration Status**: Includes migration status to track data consistency during the transition period

## File Storage

- **Location**: `./reports/` directory (relative to application root)
- **Naming**: `correspondence_detailed_report_{timestamp}_{environment}.parquet`
- **Format**: Apache Parquet for efficient data storage and analysis

## Security

- **No authentication required** - endpoints are open for internal reporting use
- Files are stored locally (will be moved to blob storage in future iterations)
- Download endpoint validates filenames to prevent directory traversal attacks
- **Note**: These endpoints should not be exposed in production without proper security

## Next Steps for Full Implementation

1. **Automated Scheduling** - Implement monthly scheduled background jobs using Hangfire
2. **Blob Storage** - Move from local storage to Azure Blob Storage
3. **Additional Metrics** - Add attachment storage, database storage, and resource-level statistics
4. **Environment Configuration** - Separate test and production data processing
5. **Historical Tracking** - Implement accumulated yearly overviews with monthly updates
6. **Notification System** - Alert when reports are generated or fail

## Testing in Development

1. Ensure you have some test correspondence data in your database
2. Run the application locally
3. Use the API endpoints above with appropriate authentication
4. Check the `./reports/` directory for generated files
5. Use a parquet file viewer to inspect the data (e.g., Python pandas, Apache Arrow, etc.)

## Example Using Python to Read Generated File

```python
import pandas as pd

# Read the parquet file
df = pd.read_parquet('correspondence_detailed_report_20250127_143022_Development.parquet')

# Display the data
print("Sample of correspondence data:")
print(df.head())

# Basic statistics
print(f"\nTotal correspondences in report: {len(df)}")
print(f"Unique service owners: {df['ServiceOwnerId'].nunique()}")
print(f"Date range: {df['Created'].min()} to {df['Created'].max()}")

# Group by service owner
service_owner_summary = df.groupby(['ServiceOwnerId', 'ServiceOwnerName']).agg({
    'CorrespondenceId': 'count',
    'ResourceId': 'nunique'
}).rename(columns={'CorrespondenceId': 'TotalCorrespondences', 'ResourceId': 'UniqueResources'})

print(f"\nCorrespondences per service owner:")
print(service_owner_summary.sort_values('TotalCorrespondences', ascending=False))
```
