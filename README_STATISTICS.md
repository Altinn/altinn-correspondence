# Daily Summary Statistics Report

This implementation generates daily summary reports with aggregated correspondence data per service owner per day in parquet format.

## Features Implemented

✅ **Daily Summary Report Handler** - Generates aggregated daily correspondence data grouped by service owner  
✅ **Parquet File Generation** - Uses Parquet.Net to create structured data files  
✅ **API Endpoints** - Manual trigger endpoints for testing  
✅ **Direct ServiceOwnerId Usage** - Uses the new ServiceOwnerId field from database entities  
✅ **Azure Blob Storage** - Stores reports in Azure Blob Storage "reports" container  
✅ **Aggregated Data** - Includes daily aggregated metrics and counts per service owner  
✅ **Maskinporten Authentication** - Secure endpoints with maintenance scope requirement

## Authentication Requirements

Both statistics endpoints use **API Key authentication** with **IP-based rate limiting**:

- **Authentication Type**: API Key
- **Required Header**: `X-API-Key: <your-api-key>`
- **Configuration**: Set `StatisticsApiKey` in appsettings
- **Development Key**: `dev-api-key-12345`
- **Production Key**: Set `StatisticsApiKey` in production configuration
- **Rate Limiting**: Enforced per IP address using Redis
- **Rate Limit Configuration**: Set `StatisticsRateLimit:RateLimitAttempts` and `StatisticsRateLimit:RateLimitWindowMinutes` in appsettings

**Response Codes**:
- `200 OK` - Success
- `401 Unauthorized` - Missing or invalid API key
- `403 Forbidden` - Invalid API key
- `429 Too Many Requests` - Rate limit exceeded (per IP address)
- `500 Internal Server Error` - Server error

**Rate Limit Headers** (included in all responses):
- `X-RateLimit-Limit` - Maximum requests allowed per window
- `X-RateLimit-Remaining` - Remaining requests in current window
- `X-RateLimit-Reset` - Unix timestamp when the rate limit resets
- `Retry-After` - Seconds to wait before retrying (when rate limited)

### Rate Limiting Behavior

- **Sliding Window**: Uses a sliding window approach (not fixed windows)
- **Per IP Address**: Each client IP gets its own rate limit quota
- **Redis-based**: Distributed rate limiting that works across multiple application instances
- **Graceful Degradation**: If Redis is unavailable, requests are allowed (with error logging)

**Example Rate Limit Response (429):**
```json
{
  "error": "Rate limit exceeded",
  "retryAfter": 1800,
  "resetTime": "2025-01-27T15:30:00.000Z"
}
```

**Example Rate Limit Headers:**
```
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1737991800
Retry-After: 1800
```

## How to Test

### 1. Generate Daily Summary Report

Generate a daily summary report with aggregated data per service owner per day. Each row represents one day's usage for one service owner. The report is uploaded to Azure Blob Storage in the "reports" container.

**Filename Format:**
- `{TIMESTAMP}_daily_summary_report_{VERSION}_{ENVIRONMENT}.parquet`
- **TIMESTAMP**: `yyyyMMdd_HHmmss` (UTC) - for easy sorting
- **VERSION**: `A3` (Altinn3 only) or `A2A3` (Altinn2 + Altinn3)
- **ENVIRONMENT**: Environment name (Development, Test, Production)

**Request Body (optional):**
```json
{
  "altinn2Included": true
}
```

**Parameters:**
- `altinn2Included` (boolean, optional): Whether to include Altinn2 correspondences in the report. Default is `true`. Set to `false` to generate reports with only Altinn3 correspondences.

```bash
POST /correspondence/api/v1/statistics/generate-daily-summary
# Requires API key authentication via X-API-Key header
# Optional request body to filter Altinn versions
```

**Response:**
```json
{
  "filePath": "https://yourstorageaccount.blob.core.windows.net/reports/20250127_143022_daily_summary_report_A2A3_Development.parquet",
  "serviceOwnerCount": 5,
  "totalCorrespondenceCount": 150,
  "generatedAt": "2025-01-27T14:30:22.123Z",
  "environment": "Development",
  "fileSizeBytes": 4096
}
```

### 2. Generate and Download Daily Summary Report

Generate a daily summary report with aggregated data per service owner per day and download it directly as a parquet file. This is the recommended endpoint for most use cases as it combines generation and download in a single request.

**Parameters:**
- `altinn2Included` (boolean, optional): Whether to include Altinn2 correspondences in the report. Default is `true`. Set to `false` to generate reports with only Altinn3 correspondences.

**Response:**
- **200 OK**: Returns the parquet file as `application/octet-stream` with filename in Content-Disposition header
- **500 Internal Server Error**: Server error during generation

**Filename Format:**
- `{TIMESTAMP}_daily_summary_report_{VERSION}_{ENVIRONMENT}.parquet`
- **TIMESTAMP**: `yyyyMMdd_HHmmss` (UTC) - for easy sorting
- **VERSION**: `A3` (Altinn3 only) or `A2A3` (Altinn2 + Altinn3)
- **ENVIRONMENT**: Environment name (Development, Test, Production)

```bash
POST /correspondence/api/v1/statistics/generate-and-download-daily-summary
# Requires API key authentication via X-API-Key header
# Optional request body: {"altinn2Included": true}
```

**Example Response Headers:**
```
Content-Type: application/octet-stream
Content-Disposition: attachment; filename="20250127_143022_daily_summary_report_A2A3_Development.parquet"
X-File-Hash: base64-encoded-md5-hash
X-File-Size: 4096
X-Service-Owner-Count: 5
X-Total-Correspondence-Count: 150
X-Generated-At: 2025-01-27T14:30:22.123Z
X-Environment: Development
X-Altinn2-Included: true
```



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
| `ResourceTitle` | string | Service owner name in Norwegian (from Resource Registry) |
| `RecipientType` | string | Recipient type (Organization, Person, or Unknown) |
| `AltinnVersion` | string | Altinn version (Altinn2 or Altinn3) |
| `MessageCount` | int | Number of messages/correspondences for this service owner on this date |
| `DatabaseStorageBytes` | long | Total database storage used (metadata) in bytes |
| `AttachmentStorageBytes` | long | Total attachment storage used in bytes |

**Example Daily Summary Data:**
```parquet
Date       | Year | Month | Day | ServiceOwnerId | ServiceOwnerName | MessageSender | ResourceId | ResourceTitle | RecipientType | AltinnVersion | MessageCount | DatabaseStorageBytes | AttachmentStorageBytes
2025-01-15 | 2025 | 1     | 15  | 987654321     | Test Org         | sender1      | resource1  | Digitaliseringsdirektoratet | Organization  | Altinn3       | 45          | 46080               | 0
2025-01-15 | 2025 | 1     | 15  | 123456789     | Another Org      | sender2      | resource2  | NAV | Person        | Altinn2       | 23          | 23552               | 0
2025-01-16 | 2025 | 1     | 16  | 987654321     | Test Org         | sender1      | resource1  | Digitaliseringsdirektoratet | Unknown       | Altinn3       | 8           | 8192                | 0
```


## Service Owner ID Usage

The system now uses the direct `ServiceOwnerId` field from the database entities:

- **Source**: Direct field from `CorrespondenceEntity.ServiceOwnerId` 
- **Format**: Organization number without prefix (e.g., `"987654321"`)
- **Lookup**: Service owner names are retrieved from the `ServiceOwner` table using the ID
- **Migration Status**: Includes migration status to track data consistency during the transition period

## File Storage

- **Location**: Azure Blob Storage in the "reports" container
- **Naming**: `{TIMESTAMP}_daily_summary_report_{VERSION}_{ENVIRONMENT}.parquet`
- **Format**: Apache Parquet for efficient data storage and analysis
- **Access**: Files are accessible via the generated blob URLs in the API responses

## Security

- **API Key Authentication**: Both endpoints require API key authentication via `X-API-Key` header
- **IP-based Rate Limiting**: Rate limiting enforced per client IP address using Redis distributed cache
- **Rate Limit Configuration**:
  - **Development**: 10 requests per hour per IP
  - **Production**: 100 requests per hour per IP
  - **Configurable**: Set via `StatisticsRateLimit:RateLimitAttempts` and `StatisticsRateLimit:RateLimitWindowMinutes`
- **Response Codes**:
  - `200 OK` - Success
  - `401 Unauthorized` - Missing or invalid API key
  - `403 Forbidden` - Invalid API key
  - `429 Too Many Requests` - Rate limit exceeded (per IP address)
  - `500 Internal Server Error` - Server error
- Files are stored in Azure Blob Storage in the "reports" container
- Download endpoint validates filenames to prevent directory traversal attacks
- **Production Ready**: Secure for production use with proper API key authentication and rate limiting

## Next Steps for Full Implementation

1. **Automated Scheduling** - Implement daily scheduled background jobs using Hangfire
2. ✅ **Blob Storage** - Azure Blob Storage integration completed
3. ✅ **Additional Metrics** - Attachment storage, database storage, and resource-level statistics implemented
4. ✅ **Environment Configuration** - Environment-specific processing implemented
5. **Historical Tracking** - Implement accumulated yearly overviews with monthly updates
6. **Notification System** - Alert when reports are generated or fail

## Testing in Development

1. Ensure you have some test correspondence data in your database
2. Run the application locally
3. Use the API key from configuration (`dev-api-key-12345` in development)
4. Use the API endpoints above with API key authentication:
   - Header: `X-API-Key: dev-api-key-12345`
5. Check the Azure Blob Storage "reports" container for generated files
6. Use a parquet file viewer to inspect the data (e.g., Python pandas, Apache Arrow, etc.)

## Example Using Python to Read Generated File

```python
import pandas as pd

# Read the parquet file
df = pd.read_parquet('20250127_143022_daily_summary_report_A2A3_Development.parquet')

# Display the data
print("Sample of daily summary data:")
print(df.head())

# Basic statistics
print(f"\nTotal daily summary records in report: {len(df)}")
print(f"Unique service owners: {df['ServiceOwnerId'].nunique()}")
print(f"Date range: {df['Date'].min()} to {df['Date'].max()}")
print(f"Total messages across all service owners: {df['MessageCount'].sum()}")

# Group by service owner
service_owner_summary = df.groupby(['ServiceOwnerId', 'ServiceOwnerName']).agg({
    'MessageCount': 'sum',
    'ResourceId': 'nunique',
    'DatabaseStorageBytes': 'sum',
    'AttachmentStorageBytes': 'sum'
}).rename(columns={
    'MessageCount': 'TotalMessages', 
    'ResourceId': 'UniqueResources',
    'DatabaseStorageBytes': 'TotalDatabaseStorage',
    'AttachmentStorageBytes': 'TotalAttachmentStorage'
})

print(f"\nDaily summary per service owner:")
print(service_owner_summary.sort_values('TotalMessages', ascending=False))
```
