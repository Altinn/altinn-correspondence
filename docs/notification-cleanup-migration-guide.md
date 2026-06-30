# Notification Cleanup Migration Guide

## Overview
This migration adds a database index to optimize the cleanup job for synced Altinn2 notifications that are missing Dialogporten activities.

## Migration Details
- **Migration Name**: `AddNotificationCleanupIndex`
- **File**: `src/Altinn.Correspondence.Persistence/Migrations/20260623113729_AddNotificationCleanupIndex.cs`
- **Index Name**: `IX_CorrespondenceNotifications_Cleanup`
- **Table**: `correspondence.CorrespondenceNotifications`

## What Does This Index Do?
The index optimizes the query used by `MigrateNotificationEventsBatchHandler` which processes notifications in descending date order (newest to oldest). The filtered index only includes rows that need processing:
- Have `Altinn2NotificationId` (synced from Altinn2)
- Have `SyncedFromAltinn2` timestamp
- Ordered by `NotificationSent` DESC

## Production Considerations

### Expected Impact
- **Estimated Rows to Index**: ~16 million notifications
- **Base Table Size**: ~918 million rows
- **Index Build Time**: Varies based on hardware (typically 5-30 minutes for this size)
- **Index Size**: Estimated 100-500 MB (much smaller than full table due to filter)

### Migration Approaches

#### Option 1: Run via EF Core Migration (Recommended for smaller databases)
```bash
dotnet ef database update --project src/Altinn.Correspondence.Persistence --startup-project src/Altinn.Correspondence.API
```

**Pros:**
- Automatic, integrated with deployment
- Transactional (rolls back on failure)

**Cons:**
- Blocks table writes during creation
- Migration may timeout on very large tables

#### Option 2: Manual CONCURRENTLY Creation (Recommended for production)
If the migration fails or you need zero-downtime deployment:

```sql
-- Run during off-peak hours if possible
-- CONCURRENTLY allows writes to continue but takes longer
CREATE INDEX CONCURRENTLY IF NOT EXISTS IX_CorrespondenceNotifications_Cleanup 
ON correspondence."CorrespondenceNotifications" ("NotificationSent" DESC)
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL;
```

**Pros:**
- No write blocking
- Can run during business hours

**Cons:**
- Cannot run in a transaction
- Slightly longer build time
- Must be run separately from migration

### Monitoring Index Creation

#### Check if index exists:
```sql
SELECT 
	schemaname,
	tablename,
	indexname,
	indexdef,
	pg_size_pretty(pg_relation_size(schemaname||'.'||indexname::text)) as size
FROM pg_indexes
WHERE tablename = 'CorrespondenceNotifications'
	AND schemaname = 'correspondence'
	AND indexname = 'IX_CorrespondenceNotifications_Cleanup';
```

#### Monitor CONCURRENTLY progress (if applicable):
```sql
-- Check for index creation activity
SELECT 
	pid,
	now() - pg_stat_activity.query_start AS duration,
	query,
	state
FROM pg_stat_activity
WHERE query LIKE '%IX_CorrespondenceNotifications_Cleanup%'
	AND state != 'idle';
```

#### Verify index is being used:
```sql
EXPLAIN ANALYZE
SELECT *
FROM correspondence."CorrespondenceNotifications"
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL
  AND "NotificationSent" < NOW()
ORDER BY "NotificationSent" DESC
LIMIT 1000;
```

Look for:
- `Index Scan using IX_CorrespondenceNotifications_Cleanup`
- Low execution time (should be <100ms for 1000 rows)

## Performance Impact

### Before Index
- Query scans large portions of the 918M row table
- Slow query times (potentially seconds or timeouts)
- High I/O and memory usage

### After Index
- Query uses efficient index scan
- Fast query times (<100ms)
- Minimal resource usage
- Batch processing can handle 16M notifications efficiently

## Rollback

If you need to remove the index:

```sql
-- Via migration
dotnet ef database update AddNotificationCleanupIndex --project src/Altinn.Correspondence.Persistence --startup-project src/Altinn.Correspondence.API

-- Or manually
DROP INDEX IF EXISTS correspondence.IX_CorrespondenceNotifications_Cleanup;
```

## Testing

After creating the index, verify performance using the monitoring script:
```bash
# Run queries in docs/notification-cleanup-monitoring.sql
```

## Related Files
- Migration: `src/Altinn.Correspondence.Persistence/Migrations/20260623113729_AddNotificationCleanupIndex.cs`
- Handler: `src/Altinn.Correspondence.Application/MigrateNotificationEventsBatch/MigrateNotificationEventsBatchHandler.cs`
- Repository: `src/Altinn.Correspondence.Persistence/Repositories/CorrespondenceNotificationRepository.cs`
- Monitoring Script: `docs/notification-cleanup-monitoring.sql`
- Endpoint: `MaintenanceController.CleanupMissingSyncedNotificationEvents`

## Questions or Issues?

If index creation fails or performance is not improved:
1. Check that migration ran successfully
2. Verify index exists using the queries above
3. Run `ANALYZE correspondence."CorrespondenceNotifications";` to update statistics
4. Check query execution plan includes index usage
5. Review PostgreSQL logs for any errors during index creation
