# Statistics Report MVP

This MVP implements a basic statistics reporting system that generates correspondence counts per service owner in parquet format.

## Features Implemented

✅ **Statistics Report Handler** - Generates correspondence counts grouped by service owner  
✅ **Parquet File Generation** - Uses Parquet.Net to create parquet files  
✅ **API Endpoints** - Manual trigger endpoints for testing  
✅ **Service Owner ID Extraction** - Extracts organization number from Sender field  
✅ **Local File Storage** - Stores reports in `./reports/` directory  

## How to Test

### 1. Generate a Statistics Report

Make a POST request to generate a new report:

```bash
POST /correspondence/api/v1/statistics/generate-report
Authorization: Bearer {token with maintenance permissions}
```

**Response:**
```json
{
  "filePath": "C:\\path\\to\\reports\\service_owner_statistics_20250127_143022_Development.parquet",
  "serviceOwnerCount": 5,
  "totalCorrespondenceCount": 150,
  "generatedAt": "2025-01-27T14:30:22.123Z",
  "environment": "Development",
  "fileSizeBytes": 2048
}
```

### 2. List Available Reports

Get a list of all generated report files:

```bash
GET /correspondence/api/v1/statistics/reports
Authorization: Bearer {token with maintenance permissions}
```

**Response:**
```json
[
  {
    "fileName": "service_owner_statistics_20250127_143022_Development.parquet",
    "filePath": "C:\\path\\to\\reports\\service_owner_statistics_20250127_143022_Development.parquet",
    "size": 2048,
    "created": "2025-01-27T14:30:22.000Z",
    "lastModified": "2025-01-27T14:30:22.000Z"
  }
]
```

### 3. Download a Report File

Download a specific report file:

```bash
GET /correspondence/api/v1/statistics/download/{fileName}
Authorization: Bearer {token with maintenance permissions}
```

This will return the parquet file as a binary download.

## Data Structure

The generated parquet files contain the following fields:

| Field | Type | Description |
|-------|------|-------------|
| `ServiceOwnerId` | string | Organization number extracted from Sender field |
| `ServiceOwnerName` | string | Service owner name (if available in database) |
| `CorrespondenceCount` | int | Number of correspondences for this service owner |
| `ReportDate` | DateTimeOffset | When the report was generated |
| `Environment` | string | Environment name (Development, Test, Production) |

## Service Owner ID Extraction

The system extracts service owner IDs from the `Sender` field using this logic:

- **Format**: `"0192:123456789"` or `"urn:altinn:organizationnumber:123456789"`
- **Extraction**: Takes the part after the last colon (`:`)
- **Example**: `"0192:987654321"` → `"987654321"`

## File Storage

- **Location**: `./reports/` directory (relative to application root)
- **Naming**: `service_owner_statistics_{timestamp}_{environment}.parquet`
- **Format**: Apache Parquet for efficient data storage and analysis

## Security

- All endpoints require `AuthorizationConstants.Maintenance` permissions
- Files are stored locally (will be moved to blob storage in future iterations)
- Download endpoint validates filenames to prevent directory traversal attacks

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
df = pd.read_parquet('service_owner_statistics_20250127_143022_Development.parquet')

# Display the data
print(df.head())
print(f"Total service owners: {len(df)}")
print(f"Total correspondences: {df['CorrespondenceCount'].sum()}")
```
