-- ========================================
-- Monitoring Script for Notification Cleanup Progress
-- ========================================

-- 1. Check if the required index exists
SELECT 
	schemaname,
	tablename,
	indexname,
	indexdef
FROM pg_indexes
WHERE tablename = 'CorrespondenceNotifications'
	AND schemaname = 'correspondence'
	AND indexname = 'IX_CorrespondenceNotifications_Cleanup';

-- If the index doesn't exist or migration failed, create it manually (DURING OFF-PEAK HOURS):
-- Option A: CONCURRENTLY (recommended for production - doesn't block writes, but takes longer)
/*
CREATE INDEX CONCURRENTLY IF NOT EXISTS IX_CorrespondenceNotifications_Cleanup 
ON correspondence."CorrespondenceNotifications" ("NotificationSent" DESC)
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL;
*/

-- Option B: Regular (faster but blocks writes - only use during maintenance window)
/*
CREATE INDEX IF NOT EXISTS IX_CorrespondenceNotifications_Cleanup 
ON correspondence."CorrespondenceNotifications" ("NotificationSent" DESC)
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL;
*/

-- Check index size after creation
SELECT 
	schemaname,
	tablename,
	indexname,
	pg_size_pretty(pg_relation_size(schemaname||'.'||indexname::text)) as index_size
FROM pg_indexes
WHERE tablename = 'CorrespondenceNotifications'
	AND schemaname = 'correspondence'
	AND indexname = 'IX_CorrespondenceNotifications_Cleanup';

-- 2. Total count of notifications that need processing
SELECT COUNT(*) as total_to_process
FROM correspondence."CorrespondenceNotifications"
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL
  AND "NotificationSent" IS NOT NULL;

-- 3. Check progress by date range (shows distribution by month)
SELECT 
	DATE_TRUNC('month', "NotificationSent") AS month,
	COUNT(*) as notification_count
FROM correspondence."CorrespondenceNotifications"
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL
  AND "NotificationSent" IS NOT NULL
GROUP BY DATE_TRUNC('month', "NotificationSent")
ORDER BY month DESC
LIMIT 50;

-- 4. Count notifications by year (broader view)
SELECT 
	DATE_PART('year', "NotificationSent") AS year,
	COUNT(*) as notification_count
FROM correspondence."CorrespondenceNotifications"
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL
  AND "NotificationSent" IS NOT NULL
GROUP BY DATE_PART('year', "NotificationSent")
ORDER BY year DESC;

-- 5. Find the date range of notifications to process
SELECT 
	MIN("NotificationSent") as oldest_notification,
	MAX("NotificationSent") as newest_notification,
	COUNT(*) as total_count
FROM correspondence."CorrespondenceNotifications"
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL
  AND "NotificationSent" IS NOT NULL;

-- 6. Check remaining work after a specific timestamp (use this to track progress)
-- Replace @timestamp with the "Next batch will process notifications older than" value from logs
SELECT 
	COUNT(*) as remaining_notifications,
	MIN("NotificationSent") as oldest_remaining,
	MAX("NotificationSent") as newest_remaining
FROM correspondence."CorrespondenceNotifications"
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL
  AND "NotificationSent" < '2024-01-01T00:00:00Z'::timestamptz; -- Replace with your timestamp

-- 7. Estimate query performance (EXPLAIN ANALYZE - be careful in production)
-- This will actually run the query, so use a recent date to limit results
EXPLAIN ANALYZE
SELECT *
FROM correspondence."CorrespondenceNotifications"
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL
  AND "NotificationSent" < NOW()
ORDER BY "NotificationSent" DESC
LIMIT 1000;

-- 8. Check for notifications with NULL NotificationSent (these will be skipped)
SELECT COUNT(*) as notifications_with_null_sent
FROM correspondence."CorrespondenceNotifications"
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL
  AND "NotificationSent" IS NULL;

-- 9. Sample of oldest notifications that will be processed
SELECT 
	"Id",
	"NotificationSent",
	"RequestedSendTime",
	"Altinn2NotificationId",
	"SyncedFromAltinn2"
FROM correspondence."CorrespondenceNotifications"
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL
  AND "NotificationSent" IS NOT NULL
ORDER BY "NotificationSent" ASC
LIMIT 10;

-- 10. Sample of newest notifications that will be processed
SELECT 
	"Id",
	"NotificationSent",
	"RequestedSendTime",
	"Altinn2NotificationId",
	"SyncedFromAltinn2"
FROM correspondence."CorrespondenceNotifications"
WHERE "Altinn2NotificationId" IS NOT NULL 
  AND "SyncedFromAltinn2" IS NOT NULL
  AND "NotificationSent" IS NOT NULL
ORDER BY "NotificationSent" DESC
LIMIT 10;

-- ========================================
-- Performance Analysis for Counting Queries
-- ========================================

-- 11. Test if cleanup index helps your counting query
-- Optimized version using NotificationSent to leverage IX_CorrespondenceNotifications_Cleanup
EXPLAIN (ANALYZE, BUFFERS)
WITH RankedNotifications AS (
	SELECT 
		noti."CorrespondenceId",
		ROW_NUMBER() OVER (PARTITION BY noti."CorrespondenceId" ORDER BY noti."Id") as rn
	FROM correspondence."CorrespondenceNotifications" noti
	INNER JOIN correspondence."Correspondences" corr 
		ON noti."CorrespondenceId" = corr."Id"
	WHERE corr."IsMigrating" = false
	  AND corr."Altinn2CorrespondenceId" IS NOT NULL
	  AND noti."Altinn2NotificationId" IS NOT NULL
	  AND noti."SyncedFromAltinn2" IS NOT NULL
	  AND noti."NotificationSent" < '2026-04-25'  -- ✅ Changed to use NotificationSent (indexed!)
)
SELECT 
	COUNT(CASE WHEN rn = 1 THEN 1 END) as NumCorrespondencesWithSyncedNotifications,
	COUNT(*) as NumSyncedNotifications
FROM RankedNotifications;

-- Look for in the output:
-- ✅ GOOD: "Index Scan using IX_CorrespondenceNotifications_Cleanup"
--          This means the query is using the new filtered index efficiently
-- ⚠️  OK: "Index Scan using IX_CorrespondenceNotifications_Synced" (existing composite index)
-- ❌ BAD: "Seq Scan on CorrespondenceNotifications" (full table scan)
--
-- Benefits of using NotificationSent instead of SyncedFromAltinn2:
-- ✅ Leverages the DESC ordering in the index
-- ✅ Index filter already excludes rows with NULL Altinn2NotificationId/SyncedFromAltinn2
-- ✅ Scans only ~16M rows instead of 918M rows
-- ✅ No need for additional index creation
