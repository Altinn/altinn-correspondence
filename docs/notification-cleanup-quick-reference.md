# Quick Reference: Notification Cleanup Job

## Check Progress in Production Logs

Look for log entries showing **actual notification dates being processed**:

```
[Info] Processing batch of 1000 notification events. 
	   Date range: 2023-06-15 14:23:10 to 2023-06-15 18:45:30. 
	   Next batch will process notifications older than 2023-06-15 14:23:10
```

**What this tells you:**
- ✅ Processing 1000 notifications from June 15, 2023
- ✅ Next batch will continue from June 15, 2023 (moving backward in time)
- ✅ Progress is visible - watch the dates move backward toward oldest data

## Check Remaining Work (PostgreSQL)

```sql
-- Quick check: How many notifications left to process?
SELECT COUNT(*) as remaining_to_process
FROM correspondence."CorrespondenceNotifications"
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL
  AND "NotificationSent" < 'TIMESTAMP_FROM_LOG'::timestamptz;
```

Replace `TIMESTAMP_FROM_LOG` with the "Next batch will process..." date from your latest log entry.

## Check if Index Exists

```sql
-- Verify index is created and check size
SELECT 
	indexname,
	pg_size_pretty(pg_relation_size('correspondence.IX_CorrespondenceNotifications_Cleanup')) as size
FROM pg_indexes
WHERE tablename = 'CorrespondenceNotifications'
	AND indexname = 'IX_CorrespondenceNotifications_Cleanup';
```

Expected result: Index exists with size (100-500 MB typical)

## Verify Index is Being Used

```sql
-- Check query plan (should show Index Scan)
EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM correspondence."CorrespondenceNotifications"
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL
  AND "NotificationSent" < NOW()
ORDER BY "NotificationSent" DESC
LIMIT 1000;
```

Look for: `Index Scan using IX_CorrespondenceNotifications_Cleanup`

## Trigger Cleanup Job Manually

```bash
# Start with default batch (100 notifications)
POST /correspondence/api/v1/maintenance/cleanup-missing-synced-notification-events

# Or specify batch size (e.g., 1000)
POST /correspondence/api/v1/maintenance/cleanup-missing-synced-notification-events?batchCount=1000

# Requires MaintenanceScope authorization
```

## Watch for Queue Throttling

If processing too fast for Dialogporten API:

```
[Warning] Migration queue has 550 jobs (threshold: 500). 
		  Delaying next batch by 1 minute to prevent queue overflow.
```

This is normal - the job automatically throttles to prevent overwhelming downstream services.

## Troubleshooting

### "Not seeing progress in logs"
- Ensure Hangfire workers are running
- Check Migration queue has active workers
- Look for errors in Hangfire dashboard

### "Queries are slow"
- Verify index exists (query above)
- Run `ANALYZE correspondence."CorrespondenceNotifications";`
- Check index is being used (EXPLAIN query above)

### "Index creation failed during migration"
- Create manually: See `docs/notification-cleanup-migration-guide.md`
- Use CONCURRENTLY option for zero downtime

### "How long will this take?"
- Depends on: batch size, Dialogporten API speed, Hangfire worker count
- Monitor first few batches to establish baseline
- 16M notifications ÷ (batches per hour × notifications per batch) = hours

## Complete Documentation

- **Full Guide**: `docs/notification-cleanup-migration-guide.md`
- **Monitoring Queries**: `docs/notification-cleanup-monitoring.sql`
- **Changes Summary**: `docs/notification-cleanup-changes-summary.md`
